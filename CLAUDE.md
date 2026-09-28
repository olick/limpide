# Limpide — contexte pour Claude Code

## Le projet

**Limpide** : assistant RAG « qui joue cartes sur table » sur les textes publics encadrant l'IA
(AI Act, fiches pratiques IA de la CNIL). À chaque réponse, l'interface montre ce qui se passe sous le capot :
passages cités avec leur score, stratégie de recherche, latence, tokens, coût, garde-fous déclenchés.
Une page publique affiche les résultats d'évaluation.

Objectifs, par ordre de priorité :
1. Une démo publique en ligne, montrable en rendez-vous client et en entretien (positionnement architecte IA / FDE).
2. Un passage documenté du POC à la production : c'est le cœur du portfolio.
3. Des preuves pour les 4 blocs de la certification RNCP41993 « Architecte en intelligence artificielle » (Jedha) :
   BC01 gouvernance, BC02 infrastructure, BC03 pipelines de données, BC04 industrialisation.

Différenciation : ce n'est pas « un RAG de plus en .NET » (voir lucidRAG, projet voisin riche en fonctionnalités).
L'angle est la **maîtrise** : transparence, évaluation mesurée, coûts, garde-fous, gouvernance, industrialisation.
Ne pas ajouter de fonctionnalités (multimodal, GraphRAG…) qui éloignent de cet angle.

## Auteur

Alexandre, architecte / tech lead .NET, 16 ans d'expérience, indépendant.
- Maîtrise : .NET, architecture, PostgreSQL, Docker, Kubernetes.
- **N'a jamais utilisé Terraform ni Airflow** : les introduire en expliquant les concepts, pas en boîte noire.
- Poste : Debian 13 (trixie).
- Style attendu : direct, honnête, itératif. Signaler les problèmes plutôt que rassurer.
  Proposer des choix argumentés, pas des listes exhaustives.

## Stack

- .NET 10, solution `Limpide` : `src/Limpide.Core` (domaine, découpage, interfaces, sans dépendance d'infra),
  `src/Limpide.Ingestion` (console : collecte, extraction, embeddings, stockage), `tests/Limpide.Core.Tests` (xUnit).
- PostgreSQL 17 + pgvector (image `pgvector/pgvector:pg17`), schéma dans `db/init/001_schema.sql`.
- Embeddings : bge-m3 (1024 dimensions) via Ollama en local, derrière `IEmbeddingGenerator`
  (Microsoft.Extensions.AI) pour pouvoir changer de fournisseur par configuration.
- Paquets : Npgsql, Pgvector, AngleSharp (HTML), PdfPig (PDF), OllamaSharp, Microsoft.Extensions.Hosting.
- Phase 2 (plus tard) : Airflow, Terraform sur Azure (Container Apps, PostgreSQL managé, Key Vault,
  identités managées), CI/CD avec évaluation automatique, OpenTelemetry, suivi des coûts.

## Décisions déjà prises (voir `docs/adr/`)

- ADR-001 : PostgreSQL + pgvector plutôt que Qdrant.
- ADR-002 : embeddings locaux bge-m3 (à confirmer par les mesures de fin de semaine 1).
- ADR-003 : ingestion en console .NET en phase 1, reprise par Airflow en phase 2 sans réécriture.
- Licence du dépôt : Apache 2.0.
- Batch incrémental, pas de streaming : les sources changent rarement. Pas de Kafka.

## Règles à respecter dans le code

- **Chaque commande d'ingestion est séparée et idempotente** (`fetch`, `extract`, `embed`, `search`).
  Les étapes échangent via le stockage (fichiers bruts, base), jamais en mémoire.
- **Versionnement** : empreinte SHA-256 du contenu brut. Même empreinte = rien à faire.
  Passer l'ancienne version à `is_current = false` **avant** d'insérer la nouvelle (index unique partiel).
- Chaque passage (`chunks`) est rattaché à une version précise et enregistre `embedding_model`.
- **Licences du corpus** (voir `docs/sources.md`) :
  - CNIL : CC-BY-ND 4.0 FR → attribution, **aucune modification**. Images exclues (CC-BY-NC-ND).
  - EUR-Lex : décision 2011/833/UE → mention de la source, ne pas dénaturer le sens.
  - Conséquence de conception : les extraits affichés sont reproduits **à l'identique**, avec source et licence ;
    la réponse générée est présentée comme une formulation de l'assistant, distincte des textes cités.
- L'assistant aide à naviguer dans les textes ; il **ne qualifie pas** la situation juridique de l'utilisateur.
- Collecte polie : `User-Agent` explicite (`Limpide/0.1 (+url du dépôt)`), pause d'1 à 2 s entre requêtes.
- Découpage **structurel** : un passage par article pour l'AI Act (redécoupé par paragraphe si long),
  par titre de section pour la CNIL, 500 à 1 500 caractères, titre de rattachement dans `heading`.

## Environnement local

```bash
docker compose up -d --wait        # postgres + ollama, ports limités à 127.0.0.1
docker compose exec postgres psql -U rag -d rag
dotnet build
```

Pièges déjà rencontrés :
- Pas de `$` dans les valeurs de `.env` : Docker Compose les interprète comme des variables.
- Ports Docker toujours liés à `127.0.0.1` (Docker contourne le pare-feu, Ollama n'a pas d'authentification).
- Ne jamais lancer les scripts avec `sudo` (ils l'appellent eux-mêmes si besoin).
- `.env` et `data/` ne doivent jamais être commités.

## Où on en est

Planning : phase 1 (S1–S3) POC de bout en bout et démo en ligne ; phase 2 (S4–S9) industrialisation.
Plan détaillé de la semaine en cours : `docs/plan/semaine-01.md`.

- [x] Session 1 : Docker, pgvector, Ollama, solution .NET créée
- [ ] Session 2 : corpus (`corpus.json`) et commande `fetch` — **prochaine tâche**
- [ ] Session 3 : extraction et découpage structurel, avec tests
- [ ] Session 4 : commande `embed`, mesures de durée
- [ ] Session 5 : commande `search`, 10 questions de test, score de référence dans le README

Critère de fin de la session 2 : lancer `fetch` deux fois de suite ne crée qu'une seule version par document.
