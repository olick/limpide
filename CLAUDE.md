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

- Maîtrise : .NET, architecture, Docker, Kubernetes. PostgreSQL : familier (usage courant), **pas expert** —
  ne pas écrire qu'il le maîtrise (précisé le 2026-10-13).
- **Découvre Terraform et Airflow avec ce projet** (S4, S6) : expliquer les concepts, pas de boîte noire.
- Poste : Debian 13 (trixie).
- Style attendu : direct, honnête, itératif. Signaler les problèmes plutôt que rassurer.
  Proposer des choix argumentés, pas des listes exhaustives.

## Stack

- .NET 10, solution `Limpide` :
  - `src/Limpide.Core` : domaine, extraction, découpage, qualité, évaluation, génération (`AnswerService`, prompt,
    contrôle des citations), interfaces (`IPassageSearch`) ;
    sans dépendance d'infra ni E/S (seuls paquets : AngleSharp, Microsoft.Extensions.AI.Abstractions).
  - `src/Limpide.Infrastructure` : PostgreSQL/pgvector (stores, `PgvectorPassageSearch`), migrations, embeddings,
    génération (`IChatClient` via Microsoft.Extensions.AI.OpenAI) ;
    tout s'enregistre par `services.AddLimpideInfrastructure(configuration)`.
  - `src/Limpide.Ingestion` : console (commandes, mode interactif) ; aucun accès direct à Npgsql ni Ollama.
  - `src/Limpide.Web` : Blazor Web App, rendu interactif côté serveur ; une page (`Components/Pages/Home.razor`)
    sur `AnswerService`. Mêmes user-secrets que la console (même `UserSecretsId`).
  - `src/Limpide.Admin` : back office Blazor (port local 5182), connexion OpenID Connect.
  - `src/appsettings.shared.json` : connexion, Embedding, Search, Generation, communs aux applications
    (chargés en premier par `AddLimpideSharedSettings`, donc surchargeables). Ne pas les dupliquer ailleurs.
  - `tests/Limpide.Core.Tests` (xUnit).
- PostgreSQL 17 + pgvector (image `pgvector/pgvector:pg17`), migrations SQL numérotées DbUp
  (`src/Limpide.Infrastructure/Migrations/`, commande `migrate`, jamais au démarrage du web).
- Embeddings : bge-m3 (1024 dimensions), derrière `IEmbeddingGenerator` ; `Embedding:Provider` = `Ollama` sur le
  poste, `OpenAI` (API compatible) en production : bge-m3 chez OVHcloud AI Endpoints (clé `Embedding:ApiKey`).
- Génération : Mistral (API, inférence UE, `https://api.eu.mistral.ai/v1`), **Medium 3.5 (`mistral-medium-2604`)**
  par défaut ; Small 4 pour des essais (`--Generation:Model=mistral-small-2603`). Pas de LLM local.
- Paquets : Npgsql, Pgvector, AngleSharp, PdfPig, OllamaSharp, Markdig, DbUp, Azure.Identity.
- Infrastructure : Docker Compose (poste et VPS), Airflow, Terraform (Azure, zone DNS OVH).

## Décisions (`docs/adr/`, index `docs/adr/README.md`)

Un numéro d'ADR s'attribue **à l'écriture**, dans l'ordre ; une décision à venir se désigne par son sujet.

- 001 PostgreSQL + pgvector plutôt que Qdrant. 002 embeddings bge-m3. 003 ingestion en console .NET, reprise
  par Airflow sans réécriture.
- 004 génération Mistral **Medium 3.5** (Small déforme des textes sans que rien ne le détecte) ; consignes
  `answer/3` + garde-fou `qualification-juridique`.
- 005 recherche vectorielle seule (hybride en option `Search:Strategy=Hybrid`) ; seuil `Generation:MinScore = 0,50`
  (pas d'appel au LLM en dessous).
- 006 démo sur un VPS OVHcloud + Docker Compose. 007 Airflow orchestre les commandes .NET conteneurisées, aucun code
  métier en Python.
- 008 cycle de vie des versions : `fetch` crée une **candidate**, la version publiée reste servie jusqu'à `publish`
  (bascule atomique) ; texte identique écarté par `extract` (`TextFingerprint`).
- 009 batch incrémental hebdomadaire. 010 découpage structurel (`FixedSizeChunker` activable,
  `--Ingestion:ChunkingStrategy=FixedSize`). 011 Azure Container Apps plutôt qu'AKS.
- 012 génération par API ; vectorisation bge-m3 chez OVHcloud AI Endpoints, Ollama sur le poste et en secours.
  **Révision en cours (S7)** : Qwen3-Embedding-8B confirmé sur 50 questions, bascule prévue par la CI.
- 013 (proposé, en production) Airflow allégé (LocalExecutor) sur le VPS.
- Licence du dépôt : Apache 2.0.

## Règles à respecter dans le code

- **Chaque commande d'ingestion est séparée et idempotente** (`fetch`, `extract`, `chunk`, `validate`, `embed`,
  `publish`). Les étapes échangent via le stockage (fichiers bruts, base), jamais en mémoire.
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

## Conventions durables

- **Console** (`src/Limpide.Ingestion/Cli/`) : sans argument, mode interactif ; avec une commande, exécution unique
  et code de sortie (mode d'Airflow, à préserver). Toute commande se déclare dans `CommandCatalog`. Configuration
  reconstruite à chaque commande (`--Section:Cle=valeur` ne vaut que pour elle). Résumé JSON en dernière ligne
  (`CommandSummary`). Quarantaine = code 0, signalée par le résumé.
- **Extracteurs et découpeurs versionnés** (`cnil-html/2`, `ai-act/1`…) : changer la version relance l'étape.
  Modifier `Packer` impose d'incrémenter la version des deux découpeurs. Les extracteurs ne produisent que du texte
  présent dans la source (seuls les espaces sont normalisés).
- **Vectorisation** : on vectorise « titre + texte » (`embedding_model = bge-m3+titre`) ; la recherche ne compare
  que des vecteurs de même libellé ; `embed` recalcule ce qui manque ou vient d'un autre modèle. Le seuil
  `MinScore` est propre au modèle : **le recalibrer à tout changement d'embedding**.
- **Consignes** versionnées (`AnswerPrompt.Version`, enregistrée avec chaque réponse) : toute modification du
  prompt l'incrémente. Modèles épinglés par version datée ; tarifs dans `Generation:Prices`, tarif absent = coût
  « inconnu », jamais deviné. Température 0, 5 passages.
- **Garde-fous** déterministes (`citation-inventee`, `sans-citation`, `reponse-tronquee`, `reponse-vide`,
  `qualification-juridique`, `consigne-divulguee`…) : ils signalent, ils ne réécrivent pas. Avertissement juridique
  fixe, jamais généré.
- **Évaluation** : relancer `evaluate` avant/après toute modification de la recherche ; `evaluate-answers` écrit
  `eval/results/AAAA-MM-JJ-HHmm-<modèle>.md` + `.json`, versionnés, jamais écrasés. Jeu : `eval/questions.json`
  (champ `behavior`, version `jeu/2`).
- **Vitrine des technologies** (`src/Limpide.Web/Components/TechStack.razor`) : à mettre à jour à chaque technologie
  adoptée (GitHub Actions en S7, OpenTelemetry en S8…).

## Environnement local

```bash
docker compose up -d --wait        # postgres + ollama + back office (http://localhost:5182), ports sur 127.0.0.1
docker compose up -d --build admin # back office après une modification de son code ; `stop admin` avant de le déboguer
dotnet run --project src/Limpide.Ingestion -- migrate   # schéma (base neuve ou après git pull)
docker compose exec postgres psql -U rag -d rag          # autorisé sans confirmation sur la base locale
dotnet build
```

Web : profil VS Code « Web » ou `dotnet run --project src/Limpide.Web --launch-profile http` (http://localhost:5181).
Secrets : `dotnet user-secrets` (`Mistral:ApiKey`, mot de passe PostgreSQL), `DOTNET_ENVIRONMENT=Development`.

Pièges déjà rencontrés :

- Pas de `$` dans les valeurs de `.env` : Docker Compose les interprète comme des variables.
- Ports Docker toujours liés à `127.0.0.1` (Docker contourne le pare-feu `ufw`, Ollama n'a pas d'authentification).
- Ne jamais lancer les scripts avec `sudo` (ils l'appellent eux-mêmes si besoin).
- `.env`, `.env.*`, `data/` et l'état Terraform ne doivent jamais être commités.
- Recréer la base : `docker compose rm -sf postgres && docker volume rm limpide_pgdata`, puis `migrate` ;
  **jamais** `docker compose down -v` (efface aussi le volume Ollama et bge-m3).
- Après un `pg_restore --clean` : **redémarrer `web`** (« cache lookup failed for type », pgvector recréé).
- EUR-Lex bloque les scripts : l'AI Act vient de l'API CELLAR (XHTML Formex, pas la page EUR-Lex).
- Pages CNIL : identifiants aléatoires dans le HTML → fausses nouvelles versions, écartées par `TextFingerprint`.

## Production

- Démo : https://www.limpide-ia.fr (domaine nu redirigé), back office https://admin.limpide-ia.fr.
  VPS OVHcloud VPS-1 (57.129.175.88, Debian 13, utilisateur `debian`, SSH par clé), dépôt dans `~/limpide`,
  projet Compose `limpide-prod` (`docker-compose.prod.yml`, Caddy, secrets dans `.env.prod`).
  Procédure : `deploy/README.md`. Sauvegarde quotidienne de la base (3 h 15 UTC, `~/backups`, 7 jours).
- Chaque déploiement de `web` coupe le site quelques secondes. Mémoire : lire `free -m` (available), pas le
  graphique d'OVH.
- Quota : 10 questions/h par IP, 60/jour ; plafond Mistral 10 €/mois.
- Airflow en production par tunnel SSH vers 8080 (`.claude/rules/airflow.md`). Azure : vitrine recréée à la demande,
  jamais la démo (`.claude/rules/terraform.md`).

## Où on en est

Plan : `docs/plan/plan-global.md`. Un fichier par semaine, `docs/plan/semaine-NN.md` (plan, puis journal de
réalisation) : **toujours lire celui de la semaine en cours**.

| Semaine | État | Résultat |
|---|---|---|
| S1–S3 | ✅ | POC, démo en ligne le 2026-10-07 (bilan : `docs/plan/bilan-phase-1.md`) |
| S4 | ✅ | Airflow, DAG `ingestion`, alertes Discord |
| S5 | ✅ 2026-10-12 | Cycle de vie des versions, contrôles qualité, quarantaine, traçabilité, back office |
| Étape « compréhensible par tous » | ✅ code | Page d'accueil et « Sous le capot » en langage clair |
| S6 | ✅ 2026-10-13 | Terraform, Azure à la demande, Airflow et vectorisation OVHcloud en production |
| **S7** | **en cours** | Évaluation et CI/CD (`docs/plan/semaine-07.md`) |
| S8–S10 | à venir | Observabilité et coûts, gouvernance, marge |

**S7** : session 1 faite (jeu de 50 questions `jeu/2`, Qwen3-Embedding confirmé) ; session 2 faite sauf le juge de
fidélité (en cours : `judge-calibrate`). Ensuite : session 3 (prompt hors du code, seuils, ADR métriques),
session 4 (GitHub Actions, OIDC, Terraform dans la chaîne), session 5 (blocage démontré, page publique), puis
bascule de la vectorisation sur Qwen3-Embedding-8B **par la CI**. Bases locales d'essai : `rag_mistral`, `rag_qwen`.

Points ouverts pour la S8 : garde-fou « Oui » en tête sur la situation de l'utilisateur non détecté ; alerte de
budget Mistral à construire (la console n'en a pas) ; anti-bot ; test du message à la 11e question.

**En suspens (Alexandre), reporté à la fin du plan initial — ne pas relancer d'ici là** : validation de la page
d'accueil par 2 ou 3 personnes non techniques, message à Jedha, testeurs extérieurs, relecture du jeu `jeu/2`,
exercices Airflow dans l'interface, enregistrement de la démonstration de quarantaine.

En cas de retard, sacrifier dans cet ordre : garde-fous v2 (S8), puis reporter en S10 le suivi en direct et les
tableaux de bord du back office (S8). **Ne jamais sacrifier l'évaluation (S7).**

Après le plan : migration cloud vers un hébergeur moins cher et plus souverain, puis mise à l'échelle
(`plan-global.md`, sections « Après le projet »).

**Journal LinkedIn** : `.vscode/linkedin-limpide.md` (règles dans sa section « Format »). Épisodes 1 à 26 écrits ;
à chaque étape faite du plan, **proposer l'épisode suivant** (27 : S7 ; celui sur Qwen au moment de la bascule).

## Tenue de ce fichier

Ce fichier est chargé à chaque conversation : n'y garder que ce qui sert à chaque séance.
En fin de session : le détail (mesures, constats, déploiement) va dans le journal de réalisation de
`docs/plan/semaine-NN.md` ; ici, seulement l'état de la semaine, et une convention ou un piège s'il vaut pour
toutes les séances. Ce qui ne concerne qu'un dossier va dans `.claude/rules/` (chargé par chemin).
