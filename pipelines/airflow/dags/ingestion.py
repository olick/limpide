"""Ingestion du corpus de Limpide : fetch → extract → chunk → embed, chaque semaine.

Airflow orchestre, il ne traite pas les données : chaque tâche lance l'image `limpide-ingestion` (console .NET) avec
une commande. Les commandes sont idempotentes, c'est ce qui rend les relances sans risque ; elles échangent par le
stockage (data/, base), jamais par Airflow. Chacune écrit un résumé JSON en dernière ligne, gardé comme XCom.

Prérequis (développement) : la pile du dépôt démarrée (`docker compose up -d` à la racine : postgres, ollama) et
l'image construite (`docker build --target ingestion -t limpide-ingestion .`). Voir pipelines/airflow/README.md.
"""

import datetime
import os

import pendulum
from airflow.providers.docker.operators.docker import DockerOperator
from airflow.sdk import DAG
from docker.types import Mount

# Réseau Docker de la pile locale du dépôt (projet Compose « limpide ») : les conteneurs y joignent postgres et ollama.
NETWORK = "limpide_default"
IMAGE = "limpide-ingestion"

ENVIRONMENT = {
    # Mot de passe : variable Airflow lue dans l'environnement du worker, masquée (***) dans l'interface et les logs.
    "ConnectionStrings__Rag": "Host=postgres;Port=5432;Database=rag;Username=rag;"
    "Password={{ var.value.limpide_db_password }}",
    "Embedding__Endpoint": "http://ollama:11434",
    # Une ligne par message dans les logs d'Airflow.
    "Logging__Console__FormatterName": "simple",
    "Logging__Console__FormatterOptions__SingleLine": "true",
}


def ingestion_task(command: str, timeout: datetime.timedelta) -> DockerOperator:
    return DockerOperator(
        task_id=command,
        image=IMAGE,
        command=[command],
        network_mode=NETWORK,
        environment=ENVIRONMENT,
        # data/ du dépôt (fichiers bruts et extraits), partagé avec l'ingestion lancée à la main.
        mounts=[Mount(source="{{ var.value.limpide_data_dir }}", target="/limpide/data", type="bind")],
        # Même UID que le worker (AIRFLOW_UID, celui du poste) : les fichiers écrits dans data/ restent les tiens.
        user=str(os.getuid()),
        # Le worker est lui-même un conteneur : son répertoire temporaire n'existe pas sur le poste, ne pas le monter.
        mount_tmp_dir=False,
        # Les logs sont déjà recopiés dans Airflow : le conteneur est supprimé, qu'il ait réussi ou non.
        auto_remove="force",
        execution_timeout=timeout,
    )


with DAG(
    dag_id="ingestion",
    description="Collecte, extraction, découpage et vectorisation du corpus (commandes .NET conteneurisées)",
    schedule="0 4 * * 1",  # le lundi à 4 h (UTC) : les sources changent rarement
    start_date=pendulum.datetime(2026, 10, 1, tz="UTC"),
    catchup=False,  # pas de rattrapage des semaines passées depuis start_date
    max_active_runs=1,  # deux ingestions simultanées se disputeraient les mêmes versions
    default_args={"retries": 2, "retry_delay": datetime.timedelta(minutes=5)},
    tags=["limpide"],
    doc_md=__doc__,
) as dag:
    fetch = ingestion_task("fetch", datetime.timedelta(minutes=15))
    extract = ingestion_task("extract", datetime.timedelta(minutes=15))
    chunk = ingestion_task("chunk", datetime.timedelta(minutes=15))
    # Corpus complet sur CPU : ≈ 10 min ; une semaine ordinaire : quelques passages.
    embed = ingestion_task("embed", datetime.timedelta(hours=1))

    fetch >> extract >> chunk >> embed
