# Limpide

Assistant RAG « transparent » sur les textes publics encadrant l'IA (AI Act, recommandations CNIL).
Chaque réponse expose ce qui se passe sous le capot : sources citées, scores, stratégie de recherche,
latence, tokens, coût, garde-fous déclenchés. Une page publique affiche les résultats d'évaluation.


## Objectifs du projet

1. Une démo publique, en ligne, montrable en rendez-vous et en entretien.
2. Un passage documenté du POC à la production (Airflow, Terraform, CI/CD, observabilité).
3. Des preuves pour les 4 blocs de la RNCP41993 (gouvernance, infrastructure, pipelines, industrialisation).

## Phases

| Phase | Semaines | Résultat |
|---|---|---|
| 1 — POC de bout en bout | S1–S3 | Démo en ligne (ingestion simple .NET, pgvector, RAG, interface) |
| 2 — Industrialisation | S4–S9 | Airflow, qualité des données, Terraform/Azure, évaluation en CI, observabilité, FinOps |

Plan de la semaine en cours : [`docs/plan/semaine-01.md`](docs/plan/semaine-01.md)

## Démarrage local

```bash
cp .env.example .env
docker compose up -d
docker compose exec ollama ollama pull bge-m3
docker compose exec postgres psql -U rag -d rag -c "\dx"   # doit lister l'extension vector
```

## Structure

```
.
├── db/init/            Scripts SQL exécutés au premier démarrage de PostgreSQL
├── docs/
│   ├── adr/            Décisions d'architecture (une par fichier)
│   ├── plan/           Plans hebdomadaires
│   └── sources.md      Inventaire du corpus et conditions de réutilisation
├── scripts/            Scripts utilitaires (création de la solution .NET)
├── src/                Code .NET (créé par scripts/bootstrap.sh)
├── data/               Documents bruts téléchargés (ignoré par git)
└── docker-compose.yml  PostgreSQL + pgvector, Ollama
```
