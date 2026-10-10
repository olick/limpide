# Airflow (local) — orchestration de l'ingestion (S4)

Docker Compose **officiel** d'Apache Airflow 3.3.1 (exécuteur Celery), deux modifications seulement :
ports limités à `127.0.0.1`, DAG d'exemple désactivés. Les ajouts propres à Limpide sont dans
`docker-compose.override.yaml` (fusionné automatiquement). Pour le développement : l'hébergement d'Airflow
en production est décidé en S6 (ADR sur l'hébergement d'Airflow).

**Airflow orchestre, il ne traite pas les données** : chaque tâche lance une commande de la console d'ingestion
(`fetch`, `extract`, `chunk`, `embed`), déjà idempotente ; c'est ce qui rend les relances sans risque.

## Démarrage

```bash
cd pipelines/airflow
# .env (non versionné, chmod 600) :
#   AIRFLOW_UID=$(id -u)
#   FERNET_KEY=...            chiffrement des secrets stockés par Airflow :
#     python3 -c "import base64,os; print(base64.urlsafe_b64encode(os.urandom(32)).decode())"
#   DOCKER_GID=...            getent group docker | cut -d: -f3 (le worker lance des conteneurs)
#   LIMPIDE_DATA_DIR=...      chemin absolu de data/ à la racine du dépôt
#   LIMPIDE_DB_PASSWORD=...   POSTGRES_PASSWORD du .env de la racine
docker compose up airflow-init        # première fois : base d'Airflow, compte administrateur
docker compose up -d --wait

# Pour le DAG ingestion, depuis la racine du dépôt : la pile (postgres, ollama) et l'image d'ingestion.
docker compose up -d --wait
docker build --target ingestion -t limpide-ingestion .   # à refaire après toute modification du code .NET
```

Interface : http://localhost:8080 (identifiant et mot de passe par défaut : `airflow` / `airflow`, acceptables
parce que l'interface n'est joignable que depuis le poste).

| Service | Rôle |
|---|---|
| `airflow-apiserver` | interface web et API |
| `airflow-scheduler` | décide quoi lancer et quand |
| `airflow-dag-processor` | lit les fichiers de `dags/` (≈ toutes les 30 s) |
| `airflow-worker` | exécute les tâches (Celery) |
| `airflow-triggerer` | tâches différées (attentes asynchrones) |
| `postgres`, `redis` | base d'Airflow (historique, états), file de messages des workers |

## Vérifier les DAG dans l'éditeur (Pylance)

Airflow n'est installé que dans les conteneurs : sans environnement local, Pylance ne connaît pas `airflow` et
signale tout. Environnement de développement, mêmes versions que les conteneurs (non versionné) :

```bash
cd pipelines/airflow
python3 -m venv .venv
.venv/bin/pip install "apache-airflow==3.3.1" "apache-airflow-providers-docker==4.5.9" pyright \
  --constraint "https://raw.githubusercontent.com/apache/airflow/constraints-3.3.1/constraints-3.12.txt"
.venv/bin/pyright      # depuis la racine du dépôt : mêmes règles que Pylance (pyrightconfig.json, mode strict)
```

## Commandes utiles

```bash
docker compose exec airflow-scheduler airflow dags list
docker compose exec airflow-scheduler airflow dags list-import-errors   # erreur dans un fichier de DAG ?
docker compose logs -f airflow-dag-processor                            # lecture des fichiers de DAG
docker compose down                                                     # arrêt (l'historique reste)
```

## Piège rencontré

Après avoir désactivé les DAG d'exemple, la base d'Airflow gardait leur trace et `airflow dags list` échouait
(`DeserializationError: ... 'example_custom_weight'`). Base encore vide : réinitialisée avec
`docker compose down -v` (supprime **seulement** le volume du projet `airflow`), puis `airflow-init`.

## DAG

| DAG | Rôle |
|---|---|
| `ingestion` | `fetch → extract → chunk → embed → publish`, le lundi à 4 h UTC ; chaque tâche lance l'image `limpide-ingestion` |
| `decouverte` | apprentissage : deux tâches qui réussissent, une qui échoue volontairement (relances, logs, « Clear ») |

### `ingestion`

- **Une tâche = un conteneur** (`DockerOperator`) lancé sur le réseau `limpide_default` de la pile du dépôt, avec
  `data/` monté : l'ingestion lancée par Airflow et celle lancée à la main partagent les mêmes fichiers et la même base.
- **Réglages** : pas de rattrapage, une exécution à la fois, 2 relances à 5 min d'écart, délai maximal par tâche
  (15 min ; 1 h pour `embed`, ≈ 10 min pour le corpus complet sur CPU).
- **Résumé de chaque tâche** : la console écrit en dernière ligne un JSON
  (`{"command":"fetch","exitCode":0,"unchanged":2,"created":1}`), gardé par Airflow comme valeur de retour
  (onglet **XCom** de la tâche). Clés : `unchanged`, `created`, `restored`, `extracted`, `chunked`, `passages`,
  `pending`, `embedded`, `failed`.
- **Secret** : le mot de passe de la base est une variable Airflow lue dans l'environnement du worker
  (`AIRFLOW_VAR_LIMPIDE_DB_PASSWORD`). Vérifié le 2026-10-08 : affiché `***` dans les champs rendus, absent en clair
  des logs et de la base d'Airflow.
- **Vérifié le 2026-10-08** : une exécution du DAG sur une nouvelle version CNIL (`fetch` : 1 créée ; `extract`,
  `chunk` : 26 passages ; `embed` : 26) produit exactement l'état d'une ingestion manuelle sur la version précédente,
  au même texte : 26 passages identiques (texte, titre, ancre, versions), vecteurs identiques au bit près ; fichiers
  écrits dans `data/` avec l'UID du poste.

## Pannes testées (session 4)

Échecs : `extract`, `chunk` et `embed` tournent même si l'étape précédente a échoué (`trigger_rule="all_done"`) ;
la tâche `bilan` rassemble les résumés, envoie **une** alerte Discord si une tâche a échoué, et échoue à son tour
(sinon Airflow marquerait l'exécution réussie, puisque `embed` réussit). Après réparation d'une tâche (« Clear »),
cocher **Downstream** pour relancer aussi `bilan`.

La **cause** figure dans le log et dans l'alerte. Airflow recopie la sortie du conteneur en INFO, ligne par ligne,
sans en connaître la gravité ; `IngestionCommand` (un `DockerOperator` à peine étendu) fait donc des lignes d'erreur
de la console (`fail:`, `Erreur (...)`) le message de l'échec : la ligne ERROR du log dit pourquoi, au lieu de
`Docker container failed: {'StatusCode': 1}`. Après la dernière tentative, `keep_failure_cause` (*callback* d'échec)
garde ce message comme XCom, et `bilan` le reprend :

```
**Limpide : ingestion en échec** (panne-ollama)
❌ `embed` : Erreur (HttpRequestException) : Name or service not known (ollama:11434)
✅ `fetch` : {"command":"fetch","exitCode":0,"unchanged":1,"created":2}
...
Logs : http://localhost:8080/dags/ingestion/runs/panne-ollama
```

Délai avant l'alerte : ≈ 10 min par tâche en échec (3 tentatives à 5 min d'écart), voulu : une coupure brève se
résout souvent d'elle-même. Relancer une exécution sans date logique (déclenchée à la main) : par l'interface ou
par l'API (`POST /api/v2/dags/ingestion/clearTaskInstances` avec `dag_run_id`) ; `airflow tasks clear` filtre par
dates et ne la trouve pas.

| Scénario | Comment | Résultat |
|---|---|---|
| Source indisponible | `airflow dags trigger ingestion -c '{"corpus": "data/pannes/source-indisponible.json"}'` (un document CNIL en 404) | 2026-10-08 : `fetch` 3 tentatives en échec (`HTTP 404`), les étapes suivantes réussissent, `bilan` en échec, alerte Discord reçue ; base inchangée (aucune trace du document de test) |
| Ollama arrêté pendant `embed` | `docker compose stop ollama` (racine), puis déclenchement | 2026-10-08 : `fetch` crée 2 versions CNIL, `extract` et `chunk` réussissent, `embed` 3 tentatives en échec (`Name or service not known (ollama:11434)`), alerte avec la cause ; **fiches CNIL absentes de la recherche ≈ 17 min**. Réparation : `docker compose start ollama`, « Clear » d'`embed` avec la suite : 57/57 passages vectorisés, exécution réussie, aucun doublon (691 passages courants vectorisés) |
| Base arrêtée | `docker compose stop postgres` (racine), puis déclenchement | 2026-10-08 : les 4 tâches en échec après 3 tentatives (`Erreur (NpgsqlException) : Name or service not known`), **une** alerte avec les 4 causes, ≈ 31 min après le début. Réparation : `docker compose start postgres`, « Réinitialiser » l'exécution (tâches en échec) : succès, aucun doublon (ni passage, ni seconde version courante) |

`data/pannes/` (non versionné) : `source-indisponible.json` = le format de `corpus.json` avec un seul document,
`"url": "https://www.cnil.fr/fr/limpide-test-page-inexistante"`.

## Constats (matière de l'ADR-007 et de la S5)

- **`catchup=False` lance quand même la dernière échéance manquée** : sortir le DAG de pause un jeudi a lancé
  aussitôt l'exécution du lundi précédent. Sans conséquence ici (commandes idempotentes), mais à savoir : désactiver
  la pause, c'est potentiellement lancer une ingestion.

- **Une source qui échouait arrêtait la chaîne** (`extract`, `chunk`, `embed` en `upstream_failed`) : corrigé en
  session 4 (`all_done` + tâche `bilan`).
- **Un document disparaissait de la recherche entre `fetch` et la fin d'`embed`** (`fetch` rendait la nouvelle
  version courante tout de suite). **Corrigé en S5** (ADR-008) : candidate, puis bascule atomique par `publish`.
- **Fausses nouvelles versions CNIL** (déjà connues) : le 2026-10-08, `fetch` a créé 2 versions CNIL dont le texte
  extrait est identique à l'ancien (mêmes 26 et 31 passages), et ce **trois fois dans la journée** (8 h, 16 h 30,
  19 h) : le HTML change plusieurs fois par jour. ≈ 1 min de vectorisation inutile à chaque exécution.
  **Corrigé en S5** (ADR-008) : candidate au texte identique écartée par `extract` (vérifié le 2026-10-10 : 2 fausses
  versions écartées, aucune vectorisation).
- **Interface en français** : « Clear » s'appelle « Réinitialiser » (raccourci Maj + C).
