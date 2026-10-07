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
  - `src/Limpide.Web` : Blazor Web App, rendu interactif côté serveur ; une page (`Components/Pages/Home.razor`)
    sur `AnswerService`. Mêmes user-secrets que la console (même `UserSecretsId`).
  - `src/appsettings.shared.json` : connexion, Embedding, Search, Generation, communs aux deux applications
    (chargés en premier par `AddLimpideSharedSettings`, donc surchargeables). Ne pas les dupliquer ailleurs.
  - `tests/Limpide.Core.Tests` (xUnit).
- PostgreSQL 17 + pgvector (image `pgvector/pgvector:pg17`), schéma dans `db/init/001_schema.sql`.
- Embeddings : bge-m3 (1024 dimensions) via Ollama en local, derrière `IEmbeddingGenerator`
  (Microsoft.Extensions.AI) pour pouvoir changer de fournisseur par configuration.
- Paquets : Npgsql, Pgvector, AngleSharp (HTML, dans Core), PdfPig (PDF), OllamaSharp, Microsoft.Extensions.Hosting.
- Génération (S2) : Mistral via son API, inférence UE, derrière `IChatClient` (Microsoft.Extensions.AI) ;
  **Medium 3.5 (`mistral-medium-2604`) par défaut** ; Small 4 pour des essais (`--Generation:Model=mistral-small-2603`).
  Pas de LLM local. Voir ADR-004.
- Phase 2 : Airflow (S4), migrations SQL (S5), Terraform sur Azure (S6 : Container Apps, PostgreSQL managé,
  Blob Storage, Key Vault, identités managées), CI/CD avec évaluation (S7), OpenTelemetry et coûts (S8).

## Décisions déjà prises (voir `docs/adr/`)

- ADR-001 : PostgreSQL + pgvector plutôt que Qdrant.
- ADR-002 : embeddings locaux bge-m3 (accepté après les mesures de la semaine 1).
- ADR-003 : ingestion en console .NET en phase 1, reprise par Airflow en phase 2 sans réécriture.
- ADR-005 : recherche vectorielle seule — l'hybride (plein texte + RRF) mesurée moins bonne (top 5 : 6/10 contre 8/10),
  gardée en option `Search:Strategy=Hybrid` ; seuil de pertinence `Generation:MinScore = 0,50` (pas d'appel au LLM en dessous).
- ADR-004 : génération par Mistral (API, inférence UE), **Medium 3.5** retenu après mesure et relecture : Small
  déforme des textes sans que rien ne le détecte ; le défaut de Medium (trancher la situation) a été corrigé par les
  consignes `answer/3` et est surveillé par le garde-fou `qualification-juridique`.
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

**Semaine en cours : S3** (`docs/plan/semaine-03.md`) — mise en ligne de la démo

- [x] Session 1 : ADR-006 accepté — **OVHcloud VPS-1** (2 vCores, 4 Go, ≈ 3,81 € HT/mois, France) + Docker Compose,
  Ollama compris (≈ 2,3 Go utilisés sur 4) ; VPS-2 en rupture. Azure ≈ 10 fois plus cher pour un service toujours
  allumé. Pas de nom de domaine pour l'instant (démo d'abord sur l'IP). **Reste** : commande du VPS par Alexandre.
- [x] Session 2 : `Dockerfile` (cibles `web` et `ingestion`, non root), `docker-compose.prod.yml` (projet `limpide-prod` :
  Caddy, web, postgres, ollama + `ollama-model`, ingestion en profil `outils`), `deploy/Caddyfile`, `.env.prod` (ignoré).
  Testée en local : page via Caddy, préchauffage bge-m3 (2,3 s), base restaurée (691/691), `evaluate` 8/10, `ask` OK,
  puis dans le navigateur par Alexandre, qui a trouvé `blazor.web.js` en 404 : la restauration faite sur les seuls .csproj
  n'inclut pas le paquet des fichiers Blazor (le SDK ne l'ajoute qu'en voyant les .razor). Corrigé (publication sans
  `--no-restore`) et verrouillé : l'image ne se construit pas si `blazor.web.js` manque. Leçon : tester dans un navigateur.
  Procédure : `deploy/README.md`.
- [x] Session 3 : **démo en ligne sur https://www.limpide-ia.fr** (VPS 57.129.175.88 ; testée par Alexandre le 2026-10-06). Fait : mises à jour
  + `unattended-upgrades`, SSH par clé seule (`/etc/ssh/sshd_config.d/00-limpide.conf`, pas de root), pare-feu `ufw`
  (22, 80, 443), Docker, dépôt cloné dans `~/limpide`, `.env.prod`, base restaurée (691/691), recherche 8/10 sur le
  serveur, sauvegarde quotidienne (`deploy/backup-db.sh`, minuteur systemd 3 h 15 UTC, `~/backups`, 7 jours ;
  disque sauvegardé par OVH en plus), HTTPS Let's Encrypt par Caddy (`SITE_ADDRESS=www.limpide-ia.fr`, 2026-10-07).
  Domaine nu `limpide-ia.fr` : encore la redirection web d'OVH (pas de HTTPS) ; à passer en A vers le VPS.

- [ ] Session 4 : protéger la démo publique (limite par IP, plafond de dépenses) — **prochaine tâche**
- [ ] Session 5 : README et bilan de la phase 1

**S2 terminée** (`docs/plan/semaine-02.md`) — RAG et interface « sous le capot »

- [x] Session 1 : `Limpide.Infrastructure` (code Npgsql/Ollama déplacé, `IPassageSearch` dans Core,
  `evaluate` identique), ADR-004 (Mistral, UE)
- [x] Session 2 : `ask` affiche l'`AnswerResult` complet (réponse, passages cités à l'identique avec source et
  licence, stratégie, durées, tokens, coût, garde-fous). Observations : `docs/notes/observations-generation.md`.
- [x] Session 3 : recherche hybride mesurée et écartée, seuil de pertinence calibré, avertissement fixe (ADR-005)
- [x] Session 4 : interface Blazor, testée de bout en bout par Alexandre le 2026-10-05
  (profil VS Code « Web », ou `dotnet run --project src/Limpide.Web --launch-profile http`, http://localhost:5181)
- [x] Session 5 : 15 questions (10 + 5 pièges) passées par `evaluate-answers`, Small et Medium comparés et relus,
  garde-fou `qualification-juridique`, consignes `answer/3`, démo basculée sur Medium (ADR-004).
  Captures d'écran dans `docs/images/`, intégrées au README.



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

Choix de la S3 :

- VPS OVHcloud VPS-1 (2 vCores, 3,7 Go, Debian 13), IP 57.129.175.88, utilisateur `debian`, connexion par clé ;
  mot de passe changé par Alexandre (jamais transmis). Domaine **`limpide-ia.fr`** (« ia », pas « ai »), démo sur
  `www` ; `limpide.fr/.eu/.io/.app` déjà pris par un même titulaire.
- Données transférées par `pg_dump`/`pg_restore` (3,7 Mo) plutôt que recalculées sur le VPS (30 à 40 min).
  **Après une restauration, redémarrer `web`** : `--clean` recrée pgvector, le type `vector` change d'identifiant, et
  Npgsql garde l'ancien en cache (« cache lookup failed for type 16386 », vu en production le 2026-10-06).
- `ASPNETCORE_FORWARDEDHEADERS_ENABLED=true` dans l'image web : l'application voit l'IP du visiteur et le schéma
  HTTPS derrière Caddy (nécessaire à la limite par IP de la session 4).
- `EmbeddingWarmup` (web) : vectorise une phrase au démarrage pour charger bge-m3 ; `OLLAMA_KEEP_ALIVE=-1` le garde.

Choix de la S2, session 5 :

- `eval/questions.json` : champ `behavior` (`answer` par défaut, `decline`, `noLegalQualification`) ; pièges t01–t05
  distincts des questions de calibrage du seuil. `evaluate` (recherche) ne compte que les questions `answer`.
- `evaluate-answers` : chaîne complète, contrôle automatique (`AnswerChecks` : OK / ÉCHEC / À RELIRE), rapport Markdown
  daté par modèle dans `eval/results/` (versionné : preuve et base de comparaison).
- Citations : renvois précis acceptés (`[P1a]`, `[P5.1.a]`, `[P4.c]`, `[P1, point a]`) ; refus = phrase seule,
  sinon garde-fou `reponse-ambigue`.
- Relecture complète faite par Claude (Alexandre l'a déléguée ; relecture par modèle, pas de juriste) : Small déforme
  2 textes sans que rien ne le détecte (q03, q10) ; Medium tranchait la situation (q03, t05), corrigé par `answer/3`.
- Garde-fou `qualification-juridique` (`LegalQualification`) : phrase en « vous/votre » + verdict, hors conditionnel et
  renvois ; heuristique qui signale sans bloquer ; affichée dans la réponse dans l'interface web.
- Rapports avec le texte des passages cités sous chaque réponse (relecture sans quitter le fichier), nommés
  `AAAA-MM-JJ-HHmm-<modèle>.md` (les réponses varient d'une passe à l'autre : ne pas écraser).

Choix de la S2, session 4 :

- Réponse du modèle = texte non fiable : rendu Markdown (Markdig) avec HTML brut, liens, images et liens
  automatiques **désactivés** (un `[x](javascript:...)` passait sinon, constaté au test). Seuls liens : les
  citations `[P1]`/`[P1a]`, ajoutées après coup vers l'extrait cité (`AnswerMarkdown`).
- Réponse (encadré bleu, sans-serif) et textes officiels (fond papier, filet doré, serif) visuellement distincts.
- Panneau « sous le capot » ouvert d'office quand un garde-fou s'est déclenché.
- Question limitée à 500 caractères ; délai maximal de 60 s par réponse.
- Pas d'outil d'automatisation de navigateur sur le poste : interaction testée à la main.

Choix de la S2, session 3 :

- Garde-fous v1 : seuil de pertinence (`hors-perimetre`, réponse fixe `AnswerPrompt.OutOfScope`, LLM non appelé) ;
  avertissement juridique fixe (`AnswerPrompt.Disclaimer`), jamais généré, sur toute réponse.
- Deux lignes de défense : seuil pour le hors sujet ; « je ne sais pas » du LLM pour les questions proches du sujet
  sans réponse dans le corpus (aucun seuil ne les sépare : « règles sur l'IA aux États-Unis » score 0,655).
- Le seuil (0,50) est propre à bge-m3+titre : le recalibrer à tout changement d'embedding.

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
