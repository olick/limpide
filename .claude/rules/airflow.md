---
paths:
  - "pipelines/airflow/**"
---

# Airflow (S4, ADR-007, ADR-013)

Alexandre ne connaissait pas Airflow avant la S4 : concepts d'abord. Détail et constats :
`pipelines/airflow/README.md`, `docs/plan/semaine-04.md` (journal de réalisation).

- Airflow 3.3.1. DAG `ingestion` : une tâche `DockerOperator` par commande .NET (`fetch`, `extract`, `chunk`,
  `validate`, `embed`, `publish`), **aucun code métier en Python** ; étapes en `all_done`, tâche `bilan` (une alerte
  Discord par exécution, quarantaine comprise, sans faire échouer l'exécution).
- Chaque commande écrit un résumé JSON en dernière ligne (`CommandSummary`), repris en XCom.
- Local : compose officiel + `docker-compose.override.yaml` (socket Docker pour le worker seul : choix de
  développement ; `AIRFLOW_VAR_*`, webhook Discord dans `pipelines/airflow/.env`). Réseau `limpide_default`.
- Production (ADR-013) : `docker-compose.leger.yaml` + `docker-compose.production.yaml` (LocalExecutor, DAG
  paramétré par `LIMPIDE_*`), interface par tunnel SSH vers 8080.
- Démonstration de quarantaine : `demo/quarantaine.sh`.
- Pylance strict : `pipelines/airflow/.venv` + `pyrightconfig.json` (`.venv/bin/pyright` depuis la racine).

Pièges :

- `catchup=False` lance quand même la dernière échéance manquée quand on sort le DAG de pause.
- XCom pour des métadonnées seulement, jamais des documents.
