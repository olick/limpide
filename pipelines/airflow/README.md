# Airflow (local) — orchestration de l'ingestion (S4)

Docker Compose **officiel** d'Apache Airflow 3.3.1 (exécuteur Celery), deux modifications seulement :
ports limités à `127.0.0.1`, DAG d'exemple désactivés. Les ajouts propres à Limpide sont dans
`docker-compose.override.yaml` (fusionné automatiquement). Pour le développement : l'hébergement d'Airflow
en production est décidé en S6 (ADR-011).

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
| `ingestion` | `fetch → extract → chunk → embed`, le lundi à 4 h UTC ; chaque tâche lance l'image `limpide-ingestion` |
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

## Constats (matière de l'ADR-007 et de la S5)

- **`catchup=False` lance quand même la dernière échéance manquée** : sortir le DAG de pause un jeudi a lancé
  aussitôt l'exécution du lundi précédent. Sans conséquence ici (commandes idempotentes), mais à savoir : désactiver
  la pause, c'est potentiellement lancer une ingestion.

- **Une source qui échoue arrête la chaîne** : `fetch` rend un code non nul si un seul document échoue ; `extract`,
  `chunk` et `embed` ne tournent pas, même pour les documents collectés sans erreur. À trancher en session 4.
- **Un document disparaît de la recherche entre `fetch` et la fin d'`embed`** : `fetch` rend la nouvelle version
  courante tout de suite, et la recherche ne lit que les passages vectorisés des versions courantes. Si `embed`
  échoue (Ollama arrêté), le document reste absent jusqu'à la réparation. Réponse prévue en S5 : publier une version
  seulement quand ses passages sont prêts.
- **Fausses nouvelles versions CNIL** (déjà connues) : le 2026-10-08, `fetch` a créé 2 versions CNIL dont le texte
  extrait est identique à l'ancien (mêmes 26 et 31 passages) ; ≈ 1 min de vectorisation chaque semaine. S5.
