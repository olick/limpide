"""Ingestion du corpus de Limpide : fetch → extract → chunk → embed → publish, chaque semaine.

Airflow orchestre, il ne traite pas les données : chaque tâche lance l'image `limpide-ingestion` (console .NET) avec
une commande. Les commandes sont idempotentes, c'est ce qui rend les relances sans risque ; elles échangent par le
stockage (data/, base), jamais par Airflow. Chacune écrit un résumé JSON en dernière ligne, gardé comme XCom.

Une version collectée est une candidate : la version publiée reste en service jusqu'à `publish`, qui bascule la recherche
en une transaction. Une candidate au texte identique à la version publiée est écartée dès `extract`.

Échecs : une étape en échec n'arrête pas les suivantes (un document indisponible ne bloque pas les autres) ; la tâche
`bilan` envoie alors une seule alerte Discord et marque l'exécution en échec.

Prérequis (développement) : la pile du dépôt démarrée (`docker compose up -d` à la racine : postgres, ollama) et
l'image construite (`docker build --target ingestion -t limpide-ingestion .`). Voir pipelines/airflow/README.md.
"""

import datetime
import json
import os
import urllib.parse
import urllib.request
from typing import Any

import pendulum
from airflow.providers.docker.exceptions import DockerContainerFailedException
from airflow.providers.docker.operators.docker import DockerOperator
from airflow.sdk import DAG, Context, Param, Variable, chain, get_current_context, task
from airflow.sdk.exceptions import AirflowException
from docker.types import Mount

# Réseau Docker de la pile locale du dépôt (projet Compose « limpide ») : les conteneurs y joignent postgres et ollama.
NETWORK = "limpide_default"
IMAGE = "limpide-ingestion"
COMMANDS = ["fetch", "extract", "chunk", "embed", "publish"]

ENVIRONMENT: dict[str, str] = {
    # Mot de passe : variable Airflow lue dans l'environnement du worker, masquée (***) dans l'interface et les logs.
    "ConnectionStrings__Rag": "Host=postgres;Port=5432;Database=rag;Username=rag;"
    "Password={{ var.value.limpide_db_password }}",
    "Embedding__Endpoint": "http://ollama:11434",
    # Une ligne par message dans les logs d'Airflow.
    "Logging__Console__FormatterName": "simple",
    "Logging__Console__FormatterOptions__SingleLine": "true",
}


class IngestionCommand(DockerOperator):
    """DockerOperator dont l'échec dit pourquoi : les lignes d'erreur de la console .NET deviennent le message.

    Airflow recopie la sortie du conteneur en INFO, ligne par ligne, sans en connaître la gravité ; sans cela, la seule
    ligne ERROR du log serait « Docker container failed: {'StatusCode': 1} ».
    """

    def execute(self, context: Context) -> list[str] | str | None:
        try:
            return super().execute(context)
        except DockerContainerFailedException as failure:
            output = [line.decode() if isinstance(line, bytes) else line for line in failure.logs or []]
            # « fail: » / « crit: » : journaux de la console ; « Erreur (...) » : exception non gérée (base arrêtée...).
            errors = [line.strip() for line in output if line.lstrip().startswith(("fail:", "crit:", "Erreur ("))]
            causes = [error.split("] ", 1)[-1] for error in errors[-5:]]  # sans le préfixe « fail: Catégorie[0] »
            raise AirflowException("\n".join(causes) or str(failure)) from failure


def ingestion_task(
    command: str, timeout: datetime.timedelta, environment: dict[str, str] | None = None, **kwargs: Any
) -> IngestionCommand:
    return IngestionCommand(
        task_id=command,
        image=IMAGE,
        command=[command],
        network_mode=NETWORK,
        environment=ENVIRONMENT | (environment or {}),
        # data/ du dépôt (fichiers bruts et extraits), partagé avec l'ingestion lancée à la main.
        mounts=[Mount(source="{{ var.value.limpide_data_dir }}", target="/limpide/data", type="bind")],
        # Même UID que le worker (AIRFLOW_UID, celui du poste) : les fichiers écrits dans data/ restent les tiens.
        user=str(os.getuid()),
        # Le worker est lui-même un conteneur : son répertoire temporaire n'existe pas sur le poste, ne pas le monter.
        mount_tmp_dir=False,
        # Les logs sont déjà recopiés dans Airflow : le conteneur est supprimé, qu'il ait réussi ou non.
        auto_remove="force",
        execution_timeout=timeout,
        on_failure_callback=keep_failure_cause,
        **kwargs,
    )


def keep_failure_cause(context: Context) -> None:
    """Après la dernière tentative en échec : garde la cause (message de l'exception) pour l'alerte du bilan."""
    context["ti"].xcom_push(key="cause", value=str(context["exception"])[:1000].splitlines())


def send_discord_alert(message: str) -> None:
    """Webhook Discord : un simple POST JSON. L'adresse contient un jeton, d'où « secret » dans le nom (masquée)."""
    url = Variable.get("discord_webhook_secret", default=None)
    if not url:
        print("Pas d'alerte : variable discord_webhook_secret absente (DISCORD_WEBHOOK_URL dans .env).")
        return
    request = urllib.request.Request(
        url,
        data=json.dumps({"content": message[:2000]}).encode(),  # 2 000 caractères au plus par message
        # Discord refuse l'agent par défaut de Python (erreur 403 de Cloudflare).
        headers={"Content-Type": "application/json", "User-Agent": "Limpide-Airflow (+https://github.com/olick/limpide)"},
    )
    with urllib.request.urlopen(request, timeout=10):
        pass


with DAG(
    dag_id="ingestion",
    description="Collecte, extraction, découpage et vectorisation du corpus (commandes .NET conteneurisées)",
    schedule="0 4 * * 1",  # le lundi à 4 h (UTC) : les sources changent rarement
    start_date=pendulum.datetime(2026, 10, 1, tz="UTC"),
    catchup=False,  # pas de rattrapage des semaines passées (mais la dernière échéance manquée est lancée)
    max_active_runs=1,  # deux ingestions simultanées se disputeraient les mêmes versions
    default_args={"retries": 2, "retry_delay": datetime.timedelta(minutes=5)},
    # Un dict, converti par le DAG en ParamsDict ; les déclarations de types d'Airflow n'annoncent que ParamsDict.
    params={  # pyright: ignore[reportArgumentType]
        # Pour les tests de panne : un corpus de test placé dans data/ (ex. data/pannes/source-indisponible.json).
        "corpus": Param("corpus.json", type="string", description="Fichier du corpus, relatif à /limpide"),
    },
    tags=["limpide"],
    doc_md=__doc__,
) as dag:
    fetch = ingestion_task("fetch", datetime.timedelta(minutes=15),
                           environment={"Ingestion__CorpusPath": "{{ params.corpus }}"})
    # all_done : l'étape tourne même si la précédente a échoué. Chaque commande ne traite que les versions courantes
    # cohérentes ; un document en échec plus haut est signalé par elle (texte extrait absent...), les autres avancent.
    extract = ingestion_task("extract", datetime.timedelta(minutes=15), trigger_rule="all_done")
    chunk = ingestion_task("chunk", datetime.timedelta(minutes=15), trigger_rule="all_done")
    # Corpus complet sur CPU : ≈ 10 min ; une semaine ordinaire : quelques passages.
    embed = ingestion_task("embed", datetime.timedelta(hours=1), trigger_rule="all_done")
    # Ne publie que les candidates entièrement vectorisées : une étape en échec plus haut retient seulement sa version,
    # la version publiée reste en service (cycle de vie, migration 0003).
    publish = ingestion_task("publish", datetime.timedelta(minutes=5), trigger_rule="all_done")

    @task(trigger_rule="all_done", retries=0)
    def bilan() -> dict[str, Any]:
        """Rassemble les résumés ; une tâche sans résumé a échoué (le résumé n'est gardé qu'en cas de succès)."""
        context = get_current_context()
        ti = context["ti"]
        pulled: dict[str, str | None] = {command: ti.xcom_pull(task_ids=command) for command in COMMANDS}
        summaries = {command: summary for command, summary in pulled.items() if summary is not None}
        failed = [command for command in COMMANDS if command not in summaries]
        if not failed:
            return {command: json.loads(summary) for command, summary in summaries.items()}

        run_id = context["run_id"]
        lines = [f"**Limpide : ingestion en échec** ({run_id})"]
        for command in failed:
            cause: list[str] = ti.xcom_pull(task_ids=command, key="cause") or ["cause non relevée (voir les logs)"]
            lines.append(f"❌ `{command}` : " + "\n    ".join(cause))
        lines += [f"✅ `{command}` : {summary}" for command, summary in summaries.items()]
        lines.append(f"Logs : http://localhost:8080/dags/ingestion/runs/{urllib.parse.quote(run_id)}")
        message = "\n".join(lines)
        print(message)  # le même texte dans le log de bilan
        send_discord_alert(message)
        # Sans cette exception, l'exécution serait marquée réussie dès que la dernière tâche (embed) réussit.
        raise AirflowException(f"tâches en échec : {', '.join(failed)}")

    chain(fetch, extract, chunk, embed, publish, bilan())
