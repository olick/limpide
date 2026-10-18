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

L'angle est la **maîtrise** (transparence, évaluation mesurée, coûts, garde-fous, gouvernance, industrialisation),
pas « un RAG de plus en .NET » (voir lucidRAG). Ne pas ajouter de fonctionnalités (multimodal, GraphRAG…) qui
éloignent de cet angle.

Auteur : Alexandre, architecte / tech lead .NET, indépendant. PostgreSQL : familier, **pas expert** (ne pas écrire
qu'il le maîtrise). Découvre Terraform et Airflow avec ce projet : expliquer les concepts, pas de boîte noire.

## Règles et interdits

- L'assistant aide à naviguer dans les textes ; il **ne qualifie jamais** la situation juridique de l'utilisateur.
- **Licences du corpus** (`docs/sources.md`) : CNIL en CC-BY-ND 4.0 FR (attribution, **aucune modification**, images
  exclues) ; EUR-Lex (décision 2011/833/UE : source mentionnée, sens non dénaturé). Les extraits affichés sont
  reproduits **à l'identique**, avec source et licence ; la réponse générée est présentée comme une formulation de
  l'assistant, distincte des textes cités.
- **Chaque commande d'ingestion est séparée et idempotente** (`fetch`, `extract`, `chunk`, `validate`, `embed`,
  `publish`) ; les étapes échangent via le stockage (fichiers bruts, base), jamais en mémoire.
- **Versionnement** : empreinte SHA-256 du contenu brut, même empreinte = rien à faire. Passer l'ancienne version à
  `is_current = false` **avant** d'insérer la nouvelle (index unique partiel). Chaque passage est rattaché à une
  version ; chaque vecteur, à son modèle (`chunk_embeddings`, un par modèle, ADR-017). `fetch` crée une
  **candidate** ; la version publiée reste servie jusqu'à `publish` (ADR-008).
- Schéma : uniquement par une nouvelle migration SQL numérotée (`src/Limpide.Infrastructure/Migrations/`, DbUp,
  commande `migrate`), **jamais** au démarrage du web.
- Pas de LLM local (génération par API Mistral, ADR-004/012). Pas de code métier en Python : Airflow ne fait
  qu'orchestrer les commandes .NET (ADR-007).
- Ne pas changer de base vectorielle, de stratégie de recherche ou de découpage sans mesure : pgvector (ADR-001),
  recherche vectorielle seule (ADR-005), découpage structurel (ADR-010).
- **Changer de modèle d'embedding impose de recalibrer `Generation:MinScore`** (0,60, propre à
  Qwen3-Embedding-8B+titre) et de relancer l'évaluation ; procédure de bascule : ADR-017.
- **Vitrine des technologies** (`src/Limpide.Web/Components/TechStack.razor`) : à jour à chaque adoption.
- Jamais commités : `.env`, `.env.*`, `data/`, état Terraform.
- **Toute modification passe par une PR** (`main` protégée, « Build et tests » requis) : Alexandre fusionne ;
  le jeton `gh` du poste n'a ni écriture du contenu, ni administration.
- **Agents de `.claude/agents/`, lancés sans attendre la demande** : `relecteur-regles` avant chaque commit,
  `relecteur-infra` avant une PR qui touche `.github/`, `deploy/` ou `infrastructure/terraform/`,
  `redacteur-linkedin` quand une étape du plan est faite.

Décisions : `docs/adr/README.md` ; un numéro d'ADR s'attribue **à l'écriture**. Licence : Apache 2.0.

## Architecture

- `src/Limpide.Core` : domaine, extraction, découpage, qualité, génération (`AnswerService`), évaluation ;
  **sans dépendance d'infra ni E/S** (seuls paquets : AngleSharp, Microsoft.Extensions.AI.Abstractions).
- `src/Limpide.Infrastructure` : PostgreSQL/pgvector, migrations, embeddings, génération (`IChatClient`) ;
  tout s'enregistre par `services.AddLimpideInfrastructure(configuration)`.
- `src/Limpide.Ingestion` : console (commandes, mode interactif) ; aucun accès direct à Npgsql ni Ollama.
- `src/Limpide.Web` (démo, Blazor serveur, page `Components/Pages/Home.razor`) et `src/Limpide.Admin` (back office,
  OpenID Connect) ; mêmes user-secrets que la console.
- `src/appsettings.shared.json` : connexion, Embedding, Search, Generation, communs aux applications ; ne pas les
  dupliquer ailleurs.
- Modèles : Qwen3-Embedding-8B réduit à 1 024 dimensions, par OVHcloud AI Endpoints partout, poste compris (clé
  `Embedding:ApiKey`) ; bge-m3 par Ollama seulement hors ligne (surcharges dans `appsettings.shared.json`) ;
  Mistral **Medium 3.5** (`mistral-medium-2604`) par défaut, Small 4 pour des essais
  (`--Generation:Model=mistral-small-2603`).

Conventions propres à un dossier : `.claude/rules/` (ingestion, génération, évaluation, interfaces web, Airflow,
Terraform, production, CI), chargées quand Claude lit un fichier du dossier.

## Environnement local

```bash
# première installation : README, section « Démarrage local »
docker compose up -d --wait        # postgres + ollama + back office (http://localhost:5182), ports sur 127.0.0.1
docker compose up -d --build admin # back office après une modification de son code ; `stop admin` avant de le déboguer
dotnet run --project src/Limpide.Ingestion -- migrate   # schéma (base neuve ou après git pull)
dotnet run --project src/Limpide.Web --launch-profile http   # démo, http://localhost:5181 (ou profil VS Code « Web »)
docker compose exec postgres psql -U rag -d rag          # autorisé sans confirmation sur la base locale
dotnet build && dotnet test
```

Pièges déjà rencontrés :

- Pas de `$` dans les valeurs de `.env` : Docker Compose les interprète comme des variables.
- Ports Docker toujours liés à `127.0.0.1` (Docker contourne le pare-feu `ufw`, Ollama n'a pas d'authentification).
- Recréer la base : `docker compose rm -sf postgres && docker volume rm limpide_pgdata`, puis `migrate` ;
  **jamais** `docker compose down -v` (efface aussi le volume Ollama et bge-m3).
- Après un `pg_restore --clean` : **redémarrer `web`** (« cache lookup failed for type », pgvector recréé).
- Secrets en `dotnet user-secrets` (`DOTNET_ENVIRONMENT=Development`), jamais dans `appsettings.json`.

Production : https://www.limpide-ia.fr et https://admin.limpide-ia.fr, sur un VPS OVHcloud (`deploy/README.md`,
`.claude/rules/production.md`). Azure n'est qu'une vitrine recréée à la demande, jamais la démo.

## Où on en est

Plan : `docs/plan/plan-global.md`. Un fichier par semaine, `docs/plan/semaine-NN.md` (plan, puis journal de
réalisation) : **toujours lire celui de la semaine en cours**.

S1–S6 faites le 2026-10-13 (POC, démo, Airflow, versions, back office, Terraform) ; S8–S10 à venir (observabilité
et coûts, gouvernance, marge). **S7 faite**, évaluation et CI/CD : sessions 1 à 4 faites (jeu `jeu/2`, juge
calibré, `gate`, ADR-014 ; chaîne GitHub Actions jusqu'au VPS, Grype, ADR-015 ; le 2026-10-17, évaluation sur le
runner en contrôle requis « Évaluation », ≈ 2 min et 0,28 $, et workflow « Azure » manuel et validé, ADR-016).
Sessions 5 et 6 faites le 2026-10-18 (blocage démontré, page `/evaluation` ; bascule sur Qwen3-Embedding-8B par la
CI, ADR-017). Après son déploiement : `/sante` (`missingEmbeddings` 0), puis réactiver le DAG `ingestion` (suspendu).

Points ouverts pour la S8 : garde-fou « Oui » en tête sur la situation de l'utilisateur non détecté ; alerte de
budget Mistral à construire ; anti-bot ; test du message à la 11e question.

**En suspens (Alexandre), reporté à la fin du plan initial — ne pas relancer** : validation de la page d'accueil
par des non-techniciens, message à Jedha, testeurs extérieurs, relecture de `jeu/2`, exercices Airflow,
**tous les enregistrements** de démonstration (quarantaine, blocage par l'évaluation, vidéo de la S9).

En retard : sacrifier les garde-fous v2 (S8), puis reporter en S10 le suivi en direct et les tableaux de bord du
back office ; **jamais l'évaluation (S7)**. Après le plan : migration cloud, puis mise à l'échelle.

**Journal LinkedIn** : `.linkedin/linkedin-limpide.md` (règles dans sa section « Format »). À chaque étape faite du
plan, **proposer l'épisode suivant** dans la série courte (`linkedin-limpide-serie-courte.md`), dont celui sur
Qwen au moment de la bascule.

## Tenue de ce fichier

Chargé à chaque conversation : n'y garder que ce qui sert à chaque séance (cible : moins de 130 lignes ;
« Où on en est » : 25 lignes au plus). En fin de session, le détail (mesures, constats, déploiement) va dans le
journal de réalisation de `docs/plan/semaine-NN.md` ; ici, seulement l'état de la semaine. Une convention propre
à un dossier va dans `.claude/rules/` ; une règle qui vaut partout, dans « Règles et interdits ».
