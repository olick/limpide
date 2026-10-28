# Décisions d'architecture (ADR)

Une décision par fichier : le contexte, les options envisagées, la décision, ses conséquences et sa condition de
révision. Modèle : [`000-modele.md`](000-modele.md).

**Numérotation** : un numéro est attribué au moment où l'ADR est écrit, dans l'ordre. Les décisions à venir se
désignent par leur sujet (liste dans `docs/plan/plan-global.md`, « Registre des ADR »).

| N° | Décision | Date |
|---|---|---|
| [001](001-pgvector.md) | PostgreSQL + pgvector plutôt qu'une base vectorielle dédiée | 2026-09-28 |
| [002](002-embeddings-locaux.md) | Embeddings calculés localement avec bge-m3 (Ollama) — remplacé par l'ADR-012 | 2026-09-28 |
| [003](003-ingestion-phase1.md) | Ingestion par une console .NET en phase 1, Airflow en phase 2 | 2026-09-28 |
| [004](004-choix-llm.md) | Génération des réponses par Mistral, hébergé en UE (Medium 3.5) | 2026-10-05 |
| [005](005-recherche-hybride.md) | Recherche vectorielle seule (l'hybride, mesurée, ne l'améliore pas) et seuil de pertinence | 2026-10-05 |
| [006](006-hebergement-demo.md) | Démo hébergée sur un VPS français, sous Docker Compose | 2026-10-05 |
| [007](007-airflow-taches-conteneurisees.md) | Airflow orchestre des commandes .NET conteneurisées, sans code métier en Python | 2026-10-08 |
| [008](008-cycle-de-vie-des-versions.md) | Cycle de vie des versions (candidate → publiée) et migrations du schéma | 2026-10-10 |
| [009](009-batch-incremental.md) | Mise à jour du corpus en batch incrémental hebdomadaire, pas en flux continu | 2026-10-11 |
| [010](010-decoupage-structurel.md) | Découpage structurel (article, considérant, section) plutôt qu'à taille fixe, mesuré | 2026-10-11 |
| [011](011-container-apps-plutot-qu-aks.md) | Azure Container Apps (plan Consommation) plutôt qu'AKS pour exécuter Limpide sur Azure | 2026-10-13 |
| [012](012-hebergement-des-modeles.md) | Génération par API (pas de GPU) ; vectorisation chez OVHcloud AI Endpoints : bge-m3, puis Qwen3-Embedding-8B (révision du 2026-10-18), sans secours | 2026-10-13 |
| [013](013-hebergement-d-airflow.md) | Airflow allégé (LocalExecutor) sur le serveur de la démo, interface par tunnel SSH (proposé) | 2026-10-13 |
| [014](014-metriques-et-seuils-d-evaluation.md) | Métriques d'évaluation et seuils de blocage (barrière `gate`, juge en métrique agrégée) | 2026-10-15 |
| [015](015-chaine-ci-cd.md) | Chaîne CI/CD par GitHub Actions, qui n'orchestre que des scripts lançables en local | 2026-10-17 |
| [016](016-evaluation-dans-un-environnement-jetable.md) | Évaluation avant la production dans un environnement jetable sur le runner ; recréation d'Azure à part | 2026-10-17 |
| [017](017-vecteurs-par-modele.md) | Une table de vecteurs par modèle (`chunk_embeddings`) : changer de modèle d'embedding sans interruption, recherche exacte | 2026-10-18 |
| [018](018-supervision.md) | Supervision : OpenTelemetry, collecteur, Tempo, Loki, Prometheus et Grafana sur le VPS, journal des réponses en base | 2026-10-19 |
| [019](019-questions-des-visiteurs.md) | Questions des visiteurs : texte conservé 30 jours dans une table à part, masqué, sauf opposition ; mention d'information | 2026-10-21 |
| [020](020-pas-de-defi-anti-robot-sans-mesure.md) | Pas de défi anti-robot tant qu'aucun abus n'est mesuré ; alerte quand le quota du jour est atteint | 2026-10-27 |
| [021](021-controle-de-la-question.md) | Contrôle de la question avant la recherche, par règles, qui bloque les injections probables | 2026-10-28 |
| [022](022-fil-de-questions-sans-memoire.md) | Interface en fil de questions, sans mémoire de conversation | 2026-10-28 |
