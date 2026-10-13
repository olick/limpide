# Décisions d'architecture (ADR)

Une décision par fichier : le contexte, les options envisagées, la décision, ses conséquences et sa condition de
révision. Modèle : [`000-modele.md`](000-modele.md).

**Numérotation** : un numéro est attribué au moment où l'ADR est écrit, dans l'ordre. Les décisions à venir se
désignent par leur sujet (liste dans `docs/plan/plan-global.md`, « Registre des ADR »).

| N° | Décision | Date |
|---|---|---|
| [001](001-pgvector.md) | PostgreSQL + pgvector plutôt qu'une base vectorielle dédiée | 2026-09-28 |
| [002](002-embeddings-locaux.md) | Embeddings calculés localement avec bge-m3 (Ollama) | 2026-09-28 |
| [003](003-ingestion-phase1.md) | Ingestion par une console .NET en phase 1, Airflow en phase 2 | 2026-09-28 |
| [004](004-choix-llm.md) | Génération des réponses par Mistral, hébergé en UE (Medium 3.5) | 2026-10-05 |
| [005](005-recherche-hybride.md) | Recherche vectorielle seule (l'hybride, mesurée, ne l'améliore pas) et seuil de pertinence | 2026-10-05 |
| [006](006-hebergement-demo.md) | Démo hébergée sur un VPS français, sous Docker Compose | 2026-10-05 |
| [007](007-airflow-taches-conteneurisees.md) | Airflow orchestre des commandes .NET conteneurisées, sans code métier en Python | 2026-10-08 |
| [008](008-cycle-de-vie-des-versions.md) | Cycle de vie des versions (candidate → publiée) et migrations du schéma | 2026-10-10 |
| [009](009-batch-incremental.md) | Mise à jour du corpus en batch incrémental hebdomadaire, pas en flux continu | 2026-10-11 |
| [010](010-decoupage-structurel.md) | Découpage structurel (article, considérant, section) plutôt qu'à taille fixe, mesuré | 2026-10-11 |
| [011](011-container-apps-plutot-qu-aks.md) | Azure Container Apps (plan Consommation) plutôt qu'AKS pour exécuter Limpide sur Azure | 2026-10-13 |
| [012](012-hebergement-des-modeles.md) | Génération par API (pas de GPU) ; vectorisation par bge-m3 chez OVHcloud AI Endpoints, Ollama en secours | 2026-10-13 |
