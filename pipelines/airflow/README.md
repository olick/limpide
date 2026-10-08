# Airflow (local) — orchestration de l'ingestion (S4)

Docker Compose **officiel** d'Apache Airflow 3.3.1 (exécuteur Celery), deux modifications seulement :
ports limités à `127.0.0.1`, DAG d'exemple désactivés. Pour le développement : l'hébergement d'Airflow
en production est décidé en S6 (ADR-011).

**Airflow orchestre, il ne traite pas les données** : chaque tâche lance une commande de la console d'ingestion
(`fetch`, `extract`, `chunk`, `embed`), déjà idempotente ; c'est ce qui rend les relances sans risque.

## Démarrage

```bash
cd pipelines/airflow
# .env (non versionné) : AIRFLOW_UID=$(id -u) et FERNET_KEY (chiffrement des secrets stockés par Airflow) :
#   python3 -c "import base64,os; print('FERNET_KEY=' + base64.urlsafe_b64encode(os.urandom(32)).decode())" >> .env
docker compose up airflow-init        # première fois : base d'Airflow, compte administrateur
docker compose up -d --wait
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
| `decouverte` | apprentissage : deux tâches qui réussissent, une qui échoue volontairement (relances, logs, « Clear ») |
