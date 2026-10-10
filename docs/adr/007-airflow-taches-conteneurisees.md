# ADR-007 : Airflow orchestre des commandes .NET conteneurisées, sans code métier en Python

- **Statut** : accepté
- **Date** : 2026-10-08

## Contexte

En phase 1, l'ingestion (`fetch → extract → chunk → embed`) se lance à la main depuis la console .NET (ADR-003).
Risque : un corpus qui vieillit sans que personne ne le voie, et des échecs silencieux. Il faut une exécution
planifiée, des relances, un historique et une alerte, sans réécrire un code déjà testé et mesuré (extracteurs,
découpeurs versionnés, recherche à 8/10).

Airflow 3.3.1 est installé en local avec le Docker Compose officiel (`pipelines/airflow/`).
Schéma du pipeline : `docs/pipeline-ingestion.md`.

## Options envisagées

1. **Réécrire les étapes en Python dans les tâches Airflow** (`PythonOperator`, `@task`) : le cas des tutoriels.
   Deux langages pour la même logique ; extracteurs et découpeurs à retester ; le code devient inutilisable hors
   d'Airflow.
2. **Lancer la console .NET dans le worker** (`BashOperator` + `dotnet` dans l'image d'Airflow) : image Airflow
   personnalisée avec le SDK .NET, dépendances mêlées, versions couplées.
3. **Une tâche = un conteneur de la console .NET** (`DockerOperator` en local) : l'image `limpide-ingestion`,
   déjà construite pour la production en S3, reçoit la commande en argument.

## Décision

Option 3. Le DAG `ingestion` ne contient que l'ordre des tâches, leurs réglages d'exécution et l'alerte :

- **Une tâche par commande**, chacune dans un conteneur ; les commandes restent idempotentes et échangent par le
  stockage (`data/`, base), jamais par Airflow. Le code de sortie dit à Airflow si la tâche a réussi.
- **Résumé de chaque tâche** : la console écrit en dernière ligne un JSON (`{"command":"fetch","unchanged":2,
  "created":1}`), gardé par Airflow comme XCom. XCom ne transporte que ces compteurs, jamais de documents.
- **Réglages** : le lundi à 4 h UTC, pas de rattrapage, une exécution à la fois, 2 relances à 5 min d'écart,
  délai maximal par tâche.
- **Échecs** : une étape en échec n'arrête pas les suivantes (`trigger_rule="all_done"`) : un document
  indisponible ne doit pas empêcher l'indexation des autres, et chaque commande ne traite que les versions
  cohérentes. Une tâche finale `bilan` envoie **une** alerte Discord, avec la cause de chaque échec (lignes
  d'erreur de la console, devenues le message de l'exception), et marque l'exécution en échec.
- **Secrets** : variables Airflow lues dans l'environnement du worker (`AIRFLOW_VAR_*`), dont le nom contient
  `password` ou `secret` : masquées (`***`) dans l'interface et les logs, jamais stockées dans la base d'Airflow
  (vérifié). En production : un gestionnaire de secrets (Key Vault, S6).

## Conséquences

- **Réutilisation complète** : la même image tourne à la main (`docker run`), en production (profil `outils`) et
  dans Airflow. Vérifié : une ingestion par le DAG produit les mêmes passages et les mêmes vecteurs, au bit près,
  qu'une ingestion manuelle.
- **Testable hors d'Airflow** : chaque commande se lance et se débogue seule ; Airflow n'ajoute que
  l'orchestration.
- **Coût** : un conteneur par tâche, ≈ 1 s de démarrage chacun ; négligeable pour un traitement hebdomadaire.
- **Socket Docker monté dans le worker : choix de DÉVELOPPEMENT seulement.** Il donne à Airflow le contrôle complet
  de Docker, donc de la machine. En production, les tâches seront lancées par un service fait pour cela
  (Container Apps Jobs ou équivalent, décision en S6, ADR sur l'hébergement d'Airflow), avec une identité aux droits limités.
- **Code de sortie grossier** : une commande en échec partiel (un document sur trois) et une commande en échec
  total (base arrêtée) rendent le même code 1. Airflow relance donc toute la commande dans les deux cas ;
  acceptable parce qu'elle est idempotente.

## Pannes testées (2026-10-08)

| Panne | Comportement observé |
|---|---|
| Source indisponible (404) | `fetch` : 3 tentatives en échec ; les étapes suivantes tournent ; alerte Discord ; base inchangée |
| Ollama arrêté pendant `embed` | `embed` : 3 tentatives en échec, alerte avec la cause ; documents absents de la recherche ≈ 17 min ; après redémarrage et relance : travail terminé, aucun doublon |
| Base arrêtée | les 4 tâches en échec après relances, **une** alerte avec les 4 causes (≈ 31 min) ; après redémarrage et relance : travail terminé, aucun doublon, une seule version courante par document |

## Ce que les tests ont révélé (à traiter en S5)

- **Un document disparaît de la recherche entre `fetch` et la fin d'`embed`** : `fetch` rend la nouvelle version
  courante avant que ses passages soient vectorisés. Si `embed` échoue, le document reste absent jusqu'à la
  réparation. Réponse : publier une version seulement quand ses passages sont prêts (cycle de vie, S5).
- **Fausses nouvelles versions CNIL** : identifiants aléatoires dans le HTML, qui change plusieurs fois par jour
  (3 fausses versions le 2026-10-08), texte identique ; ≈ 1 min de vectorisation inutile à chaque exécution.
  Réponse : publier seulement si le texte extrait change (S5).
- **`catchup=False` lance quand même la dernière échéance manquée** à la sortie de pause.
