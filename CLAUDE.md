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

- .NET 10, solution `Limpide` :
  - `src/Limpide.Core` : domaine, extraction, découpage, évaluation, génération (`AnswerService`, prompt,
    contrôle des citations), interfaces (`IPassageSearch`) ;
    sans dépendance d'infra ni E/S (seuls paquets : AngleSharp, Microsoft.Extensions.AI.Abstractions).
  - `src/Limpide.Infrastructure` : PostgreSQL/pgvector (stores, `PgvectorPassageSearch`), Ollama, Mistral
    (`IChatClient` via Microsoft.Extensions.AI.OpenAI sur `https://api.eu.mistral.ai/v1`) ;
    tout s'enregistre par `services.AddLimpideInfrastructure(configuration)`.
  - `src/Limpide.Ingestion` : console (commandes, mode interactif) ; aucun accès direct à Npgsql ni Ollama.
  - `tests/Limpide.Core.Tests` (xUnit). Prévu en S2 : `src/Limpide.Web` (Blazor), qui réutilise l'infrastructure.
- PostgreSQL 17 + pgvector (image `pgvector/pgvector:pg17`), schéma dans `db/init/001_schema.sql`.
- Embeddings : bge-m3 (1024 dimensions) via Ollama en local, derrière `IEmbeddingGenerator`
  (Microsoft.Extensions.AI) pour pouvoir changer de fournisseur par configuration.
- Paquets : Npgsql, Pgvector, AngleSharp (HTML, dans Core), PdfPig (PDF), OllamaSharp, Microsoft.Extensions.Hosting.
- Génération (S2) : Mistral via son API, inférence UE, derrière `IChatClient` (Microsoft.Extensions.AI) ;
  Medium 3.5 pour la démo, Small 4 en développement (pas de LLM local). Voir ADR-004.
- Phase 2 : Airflow (S4), migrations SQL (S5), Terraform sur Azure (S6 : Container Apps, PostgreSQL managé,
  Blob Storage, Key Vault, identités managées), CI/CD avec évaluation (S7), OpenTelemetry et coûts (S8).

## Décisions déjà prises (voir `docs/adr/`)

- ADR-001 : PostgreSQL + pgvector plutôt que Qdrant.
- ADR-002 : embeddings locaux bge-m3 (accepté après les mesures de la semaine 1).
- ADR-003 : ingestion en console .NET en phase 1, reprise par Airflow en phase 2 sans réécriture.
- ADR-004 : génération par Mistral (API, inférence UE). Démo sur Medium 3.5 par prudence ; passer sur Small 4
  si la mesure de fin de S2 montre qu'il tient la qualité (citations, « je ne sais pas », fidélité au texte).
- Licence du dépôt : Apache 2.0.
- Batch incrémental, pas de streaming : les sources changent rarement. Pas de Kafka.

## Règles à respecter dans le code

- **Chaque commande d'ingestion est séparée et idempotente** (`fetch`, `extract`, `chunk`, `embed`, `search`).
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

Plan global (10 semaines, ADR prévus, correspondance RNCP) : `docs/plan/plan-global.md`.
Un fichier par semaine : `docs/plan/semaine-NN.md`. Toujours lire celui de la semaine en cours.

| Phase | Semaines | Résultat |
|---|---|---|
| 1 — POC de bout en bout | S1 à S3 | Démo publique en ligne (S2 : RAG + interface « sous le capot », S3 : mise en ligne) |
| 2 — Industrialisation | S4 à S9 | Airflow, qualité et quarantaine, Terraform, évaluation en CI, observabilité, gouvernance |
| Marge | S10 | Rattrapage, finitions, soutenance |

En cas de retard, sacrifier dans cet ordre : recherche hybride (S2), élargissement du corpus (S5),
garde-fous v2 (S8). Ne jamais sacrifier l'évaluation (S7).

**Semaine en cours : S2** (`docs/plan/semaine-02.md`) — RAG et interface « sous le capot »

- [x] Session 1 : `Limpide.Infrastructure` (code Npgsql/Ollama déplacé, `IPassageSearch` dans Core,
  `evaluate` identique), ADR-004 (Mistral, UE)
- [x] Session 2 : `ask` affiche l'`AnswerResult` complet (réponse, passages cités à l'identique avec source et
  licence, stratégie, durées, tokens, coût, garde-fous). Observations : `docs/notes/observations-generation.md`.
- [ ] Session 3 : garde-fous v1 et recherche hybride (ADR-005) — **prochaine tâche**
- [ ] Session 4 : interface Blazor
- [ ] Session 5 : tests et bilan, dont la comparaison Mistral Small 4 / Medium 3.5 (ADR-004)

**S1 terminée** (`docs/plan/semaine-01.md`) :

- [x] Session 1 : Docker, pgvector, Ollama, solution .NET créée
- [x] Session 2 : corpus (`corpus.json`) et commande `fetch`
- [x] Session 3 : commandes `extract` et `chunk`, extracteurs et découpeurs AI Act / CNIL testés
- [x] Session 4 : commande `embed`, mesures dans l'ADR-002 (691 passages, 9 min 31 s sur CPU, 816 ms par passage)
- [x] Session 5 : `search`, `evaluate`, 10 questions (`eval/questions.json`), score dans le README :
  top 5 = 5/10 (texte seul) puis **8/10 (titre + texte, retenu)**


Constats de la semaine 1 à reprendre en S2 (détail : `docs/notes/observations-decoupage.md`) :
les considérants évincent les articles en tête des résultats (recherche hybride, ADR-005) ; le vocabulaire
des utilisateurs diffère de celui du règlement (« grands modèles de langage » contre « modèles d'IA à usage général »).
`evaluate` donne le score à chaque modification de la recherche : le relancer avant/après, noter dans le README.
Observations de découpage à compléter au fil de l'eau : `docs/notes/observations-decoupage.md` (matière de l'ADR-008).

Choix de la session 3 (voir la discussion du 2026-09-29) :

- Deux commandes `extract` puis `chunk`, qui échangent via `data/extracted/<source>/<sha256>.json` :
  le texte extrait se relit à l'œil, et on peut comparer des découpages sans réextraire (futur ADR).
- Chaque extracteur et découpeur a une version (`cnil-html/1`...) enregistrée avec son résultat :
  la changer relance l'étape, sinon la commande ne refait rien.
- Extracteurs dans Core (AngleSharp, sans E/S), choisis selon la source. Ils ne produisent que du texte présent
  dans la source ; seuls les espaces sont normalisés. Blocs : titre (niveau), paragraphe, élément de liste
  (profondeur), avec l'ancre ELI pour l'AI Act (`art_5`, `005.001`, `rct_1`, `anx_III`).
- AI Act : titre, en-tête et pied du JO, notes, formule finale et signatures écartés ; considérants gardés.
- Découpeurs (`ai-act/1`, `cnil-sections/1`) : unités article / considérant / annexe / préambule pour l'AI Act,
  section pour la CNIL ; `Packer` commun (500 à 1 500 caractères, coupe entre groupes, puis blocs, puis phrases,
  jamais dans une phrase). Modifier `Packer` impose d'incrémenter la version des deux découpeurs.
- Table `chunks` : `anchor` (`art_5`, `rct_12`, `anx_III`, clé des questions de test), `extractor_version`
  et `chunker_version` ; `chunk` remplace tous les passages d'une version dans une transaction (COPY binaire).
- Pour recréer la base sans perdre bge-m3 : `docker compose rm -sf postgres && docker volume rm limpide_pgdata`,
  jamais `docker compose down -v` (efface aussi le volume Ollama).
- La fiche CNIL « Annoter les données » contient un paragraphe en double dans la page elle-même : laissé tel quel.
- **Fausses nouvelles versions CNIL** (constaté le 2026-09-29) : le HTML brut contient des identifiants aléatoires
  (menu, jeton `form_build_id`) qui changent d'un jour à l'autre ; chaque `fetch` crée une version dont le texte
  extrait est identique. Sans gravité tant que la collecte est manuelle ; à traiter en S5 avec le cycle de vie
  des versions : ne publier une version que si son texte extrait diffère de la version publiée.

Console (`src/Limpide.Ingestion/Cli/`) : sans argument, mode interactif (`limpide>`, `help`, `exit`) ;
avec une commande, exécution unique et code de sortie (mode d'Airflow en S4, à préserver). La liste des
commandes (`CommandCatalog`) alimente l'aide et la validation : toute nouvelle commande s'y déclare.
Configuration et services reconstruits à chaque commande, pour que `--Section:Cle=valeur` ne vaille que pour elle.
VS Code : profils `Debug` / `Release` (`.vscode/launch.json`), `DOTNET_ENVIRONMENT=Development` pour user-secrets.

Choix de la S2, session 2 :

- Modèles épinglés par version datée : `mistral-small-2603` (développement), `mistral-medium-2604` (Medium 3.5,
  démo). Changer de modèle : `--Generation:Model=mistral-medium-2604` ; le coût suit (tarifs par modèle dans
  `Generation:Prices`, majoration UE comprise ; tarif absent = coût « inconnu », jamais deviné).
- Clé `Mistral:ApiKey` (user-secrets), exigée seulement par `ask` : l'ingestion fonctionne sans.
- Consignes versionnées (`AnswerPrompt.Version`, `answer/2`) et enregistrées avec chaque réponse :
  toute modification du prompt incrémente la version.
- Garde-fous déterministes : `citation-inventee`, `sans-citation` (sauf « je ne sais pas »), `reponse-tronquee`,
  `reponse-vide`. Les modèles citent `[P1a]` malgré la consigne : le contrôle l'accepte comme P1.
- Température 0 pour des mesures comparables ; 5 passages ; ≈ 1 800 tokens en entrée par question.

Choix de la session 5 :

- `evaluate` : rang du premier bon passage sur 10 résultats ; top 1, top 5, MRR. Passage attendu par `anchor`
  (AI Act) ou morceau du titre de rattachement (CNIL, sans ancre). Questions formulées sans reprendre les mots du titre.
- `Embedding:IncludeHeading = true` : on vectorise « titre + texte » ; `embedding_model = bge-m3+titre`.
  La recherche ne compare que des vecteurs de même libellé.

Choix de la session 4 :

- `embed` traite les passages des versions courantes sans embedding **ou** calculés avec un autre modèle que
  `Embedding:Model` : changer de modèle en configuration relance le calcul. Lots enregistrés un par un
  (reprise après interruption). Configuration : section `Embedding` d'`appsettings.json`
  (`--Embedding:BatchSize=32` en ligne de commande pour essayer une autre taille de lot).
- Ollama tourne sur CPU : le GPU du poste n'est pas exposé au conteneur (pas de NVIDIA Container Toolkit).
  Volontaire : c'est la configuration la plus proche de l'hébergement probable. Question ouverte dans l'ADR-002 :
  où tourne le modèle qui vectorise les questions en ligne, et combien il coûte au repos (ADR-006, 012).

Notes de la session 2 :

- EUR-Lex bloque les scripts (défi AWS WAF, `202` vide) : l'AI Act est téléchargé via l'API CELLAR
  (`fetchUrl` dans `corpus.json`). Le fichier brut est du XHTML Formex/CONVEX, pas la page EUR-Lex :
  en tenir compte pour l'extraction.
- `fetch` exige un `200` non vide et le bon type de contenu : un blocage ne crée jamais de version.
- Mot de passe PostgreSQL local dans `dotnet user-secrets` (projet Ingestion), jamais dans `appsettings.json`.
