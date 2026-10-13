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
- PostgreSQL 17 + pgvector (image `pgvector/pgvector:pg17`), schéma par migrations SQL numérotées
  (`src/Limpide.Infrastructure/Migrations/`, DbUp, commande `migrate`).
- Embeddings : bge-m3 (1024 dimensions), derrière `IEmbeddingGenerator` (Microsoft.Extensions.AI) ; fournisseur par
  configuration (`Embedding:Provider` = `Ollama` sur le poste, `OpenAI` = API compatible : **bge-m3 chez OVHcloud AI
  Endpoints en production**, VPS et Azure, ADR-012 ; mêmes vecteurs, cosinus 0,99998 ; clé `Embedding:ApiKey`).
- Paquets : Npgsql, Pgvector, AngleSharp (HTML, dans Core), PdfPig (PDF), OllamaSharp, Microsoft.Extensions.Hosting.
- Génération (S2) : Mistral via son API, inférence UE, derrière `IChatClient` (Microsoft.Extensions.AI) ;
  **Medium 3.5 (`mistral-medium-2604`) par défaut** ; Small 4 pour des essais (`--Generation:Model=mistral-small-2603`).
  Pas de LLM local. Voir ADR-004.
- Phase 2 : Airflow (S4), migrations SQL (S5), Terraform sur Azure (S6 : Container Apps, PostgreSQL managé,
  Blob Storage, Key Vault, identités managées), CI/CD avec évaluation (S7), OpenTelemetry et coûts (S8).

## Décisions déjà prises (voir `docs/adr/`)

Numérotation : un numéro d'ADR s'attribue **à l'écriture**, dans l'ordre ; une décision à venir se désigne par son
sujet, jamais par un numéro réservé (convention du 2026-10-10). Index : `docs/adr/README.md`.

- ADR-001 : PostgreSQL + pgvector plutôt que Qdrant.
- ADR-002 : embeddings locaux bge-m3 (accepté après les mesures de la semaine 1).
- ADR-003 : ingestion en console .NET en phase 1, reprise par Airflow en phase 2 sans réécriture.
- ADR-005 : recherche vectorielle seule — l'hybride (plein texte + RRF) mesurée moins bonne (top 5 : 6/10 contre 8/10),
  gardée en option `Search:Strategy=Hybrid` ; seuil de pertinence `Generation:MinScore = 0,50` (pas d'appel au LLM en dessous).
- ADR-004 : génération par Mistral (API, inférence UE), **Medium 3.5** retenu après mesure et relecture : Small
  déforme des textes sans que rien ne le détecte ; le défaut de Medium (trancher la situation) a été corrigé par les
  consignes `answer/3` et est surveillé par le garde-fou `qualification-juridique`.
- ADR-007 : Airflow orchestre les commandes .NET conteneurisées (`DockerOperator` en local, socket Docker = choix de
  développement ; service cloud en S6), aucun code métier en Python ; étapes en `all_done`, tâche `bilan` pour l'alerte.
- ADR-008 : cycle de vie des versions — `fetch` crée une **candidate**, la version publiée reste en service jusqu'à
  `publish` (bascule atomique) ; candidate au texte identique écartée par `extract` (`TextFingerprint`) ; schéma par
  migrations DbUp (`migrate`, jamais au démarrage du web).
- ADR-009 : batch incrémental hebdomadaire, pas de flux continu (13 jours mesurés : aucun changement réel de texte,
  jusqu'à 3 fausses versions CNIL par jour ; 11 à 19 s par passage sans changement).
- ADR-010 : découpage structurel, **mesuré** contre la taille fixe (1 000 / 200) sur le corpus élargi : top 5 7/10
  contre 5/10, MRR 0,52 contre 0,37 ; `FixedSizeChunker` gardé, activable (`--Ingestion:ChunkingStrategy=FixedSize`).
- ADR-011 : Azure Container Apps (plan Consommation) plutôt qu'AKS (tarifs relevés le 2026-10-13 : ≈ 0,08 $/h en
  séance, ≈ 55 $/mois permanent, contre ≈ 65 €/mois pour AKS avec un nœud B2ms) ; AKS = voie de montée en charge,
  et de sortie si l'hébergeur cible n'a pas d'équivalent.
- ADR-012 : génération par API Mistral (GPU rentable seulement vers 3 800 questions/jour face à Medium, et avec un
  modèle moins bon) ; vectorisation par **bge-m3 chez OVHcloud AI Endpoints** (0,01 €/million de tokens, France, mêmes
  vecteurs et même évaluation qu'Ollama ; mistral-embed mesuré et écarté : top 5 5/10, MRR 0,31) ; Ollama sur le poste
  et en secours (profil `ollama` du VPS, `deploy/ollama`). **En production le 2026-10-13** : clé `OVH_AI_ENDPOINTS_KEY` dans le
  `.env.prod` du serveur, `web` redéployé, conteneurs Ollama supprimés (volume gardé), mémoire disponible 1 508 → 3 007 Mo ;
  clé `ovh-ai-endpoints-key` dans le coffre permanent Azure.
  Mesures du 2026-10-13 (`docs/notes/observations-modeles-ovh.md`) : rédaction par modèles ouverts chez OVHcloud
  (`Generation:Endpoint`, `Generation:ApiKey` générique) : gpt-oss-120b 11/15, 0,04 centime mais 14 s ; Llama 3.3 tranche
  la situation ; Qwen3.5-397B plus cher que Medium (réflexion). `ModelText.Clean` retire les caractères invisibles
  (U+200B de gpt-oss dans « [P5] »). Vectorisation Qwen3-Embedding-8B (`RequestDimensions`, `QueryInstruction`) :
  top 5 10/10, MRR 0,80 ; latence remesurée 218 ms (3,9 s = ralentissement passager) : candidat à confirmer sur 50
  questions (S7). Relecture de gpt-oss : 4 erreurs + 1 qualification invisibles au contrôle automatique → **Medium
  gardé**. **Tâche notée en S7** : passer la vectorisation à Qwen3-Embedding-8B si le jeu de 50 questions confirme
  (seuil, réglages, bascule sans interruption, ADR-012 révisé) et écrire l'épisode LinkedIn à ce moment (note P). Garde-fou à renforcer en S8 : « Oui » en tête sur la situation de l'utilisateur non détecté. Bases d'essai
  locales `rag_mistral`, `rag_qwen`.
- ADR-013 (proposé) : Airflow **allégé** (LocalExecutor, `pipelines/airflow/docker-compose.leger.yaml`) sur le VPS de la
  démo, interface par tunnel SSH ; mesuré : 0,70 Go au repos, 0,88 Go au pic (officiel Celery : 1,77 Go) ; VPS : 2,97 Go
  disponibles ; Airflow géré ≈ 300–350 $/mois écarté ; repli : minuteur système. **En production le 2026-10-13** : fichiers du corpus
  copiés dans le volume, `.env` d'Airflow sur le serveur (secrets tirés au hasard ou copiés de `.env.prod`, rien
  affiché), DAG paramétré par `LIMPIDE_*` (`docker-compose.production.yaml`) ; première ingestion réelle réussie :
  17 fausses versions CNIL écartées, rien publié, mémoire disponible ≥ 1,9 Go. Interface : tunnel SSH vers 8080.
- Licence du dépôt : Apache 2.0.

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
docker compose up -d --wait        # postgres + ollama + back office (http://localhost:5182), ports sur 127.0.0.1
docker compose up -d --build admin # back office après une modification de son code ; `stop admin` avant de le déboguer
dotnet run --project src/Limpide.Ingestion -- migrate   # schéma (base neuve ou après git pull)
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

**Journal LinkedIn** (`.vscode/linkedin-limpide.md`, demandé le 2026-10-09, réorganisé le 2026-10-12) : **une seule
série, dans l'ordre réel du projet**. Épisodes 1 à 25 écrits (S1 à S6 en cours, jusqu'au 2026-10-13 ; épisode 2 « Pourquoi ne pas simplement poser la question à une IA ? » inséré le 2026-10-12, suivants renumérotés) ; lancement publié le
2026-10-08, épisode 1 le 2026-10-09, épisode 2 le 2026-10-12, épisode 3 le 2026-10-13 ; post hors série « À vous de le casser ». À chaque étape faite du plan,
**proposer à Alexandre l'épisode suivant** (26 : fin de la S6) et l'ajouter à la suite. Règles :

- un épisode ne raconte qu'une tâche faite, à sa place dans la chronologie ; récit : problème, enjeu, piste essayée,
  résultat, analyse, décision, leçon, question au lecteur ; 1 500 à 2 200 caractères ; pied « ◀ Épisode précédent /
  📚 Tous les épisodes » ;
- uniquement des faits du projet (ADR, `docs/notes/`, `eval/`, mesures), sources indiquées sous le titre ; limites
  dites (ex. relecture faite par un modèle) ; rien d'inventé ; public non spécialiste : expliquer chaque terme ;
- en fin de fichier, des **notes de travail** (une par tâche à venir : idée, enjeu, ce qu'il faudra relever pendant la
  tâche) : relever ces faits pendant la tâche ; tenir à jour le journal des décisions et le sommaire ; un épisode écrit
  mais pas encore publié que le projet dépasse reçoit un bref rappel ; un épisode publié, un commentaire de suivi.

| Phase | Semaines | Résultat |
|---|---|---|
| 1 — POC de bout en bout | S1 à S3 | Démo publique en ligne (S2 : RAG + interface « sous le capot », S3 : mise en ligne) |
| 2 — Industrialisation | S4 à S9 | Airflow, qualité et quarantaine, Terraform, évaluation en CI, observabilité, gouvernance |
| Marge | S10 | Rattrapage, finitions, soutenance |

En cas de retard, sacrifier dans cet ordre : recherche hybride (S2), élargissement du corpus (S5),
garde-fous v2 (S8), puis reporter en S10 le suivi en direct et les tableaux de bord du back office (S8).
Ne jamais sacrifier l'évaluation (S7).

Ajouts au plan (demandés le 2026-10-07, détail dans `docs/plan/plan-global.md`) : **back office** en trois temps
(S5 : retours et quarantaine, exposé sur `admin.limpide-ia.fr` avec connexion OpenID Connect générique — Microsoft
d'abord, fournisseur changeable par configuration —, réglages en lecture seule car modifiés par Git ;
S7 : retour → cas de test ; S8 : tableaux de bord), **suivi en direct** des étapes et réponse en streaming (S8).

**Phase 1 terminée (2026-10-07)** : démo publique sur https://www.limpide-ia.fr. Bilan : `docs/plan/bilan-phase-1.md`.

**S4 terminée** (`docs/plan/semaine-04.md`) — Airflow ; Alexandre ne le connaît pas : concepts d'abord.
Détail et constats : `pipelines/airflow/README.md`.

- [x] Session 1 : Airflow 3.3.1 en local (compose officiel dans `pipelines/airflow/`, ports sur 127.0.0.1, exemples
  désactivés, `FERNET_KEY`), DAG d'apprentissage `decouverte`. **Reste** : exercices dans l'interface par Alexandre.
- [x] Session 2 : image `ingestion` (déjà faite en S3) testée par `docker run` sur la pile locale : 4 commandes OK,
  `fetch` deux fois de suite = inchangé. Ajouts : résumé JSON en dernière ligne de chaque commande (`CommandSummary`,
  écrit après fermeture de l'hôte pour passer après les journaux) ; chiffrement GSS désactivé dans Npgsql
  (« libgssapi_krb5.so.2 » écrit sur stderr par les images .NET).
- [x] Session 3 : DAG `ingestion` (`DockerOperator`, réseau `limpide_default`, `data/` monté, UID du worker), lundi
  4 h UTC, sans rattrapage, 1 exécution à la fois, 2 relances à 5 min ; ajouts Limpide dans
  `docker-compose.override.yaml` (socket Docker pour le worker seul, `AIRFLOW_VAR_*` : mot de passe masqué `***`,
  vérifié). Vérifié le 2026-10-08 sur une vraie nouvelle version CNIL : passages et vecteurs identiques au bit près à
  ceux d'une ingestion manuelle. Sortir le DAG de pause a lancé aussitôt la dernière échéance manquée, malgré
  `catchup=False`. Règle locale : `psql` sur la base locale autorisé sans confirmation (`.claude/settings.local.json`).
- [x] Session 4 : étapes en `all_done` + tâche `bilan` (une alerte Discord par exécution, avec la cause de chaque
  échec ; webhook dans `pipelines/airflow/.env`, variable `discord_webhook_secret`) ; `IngestionCommand` fait des
  lignes d'erreur de la console le message d'échec. Trois pannes testées le 2026-10-08 (source en 404, Ollama arrêté,
  base arrêtée) : relances, alerte, réparation sans doublon. Constat clé pour la S5 : pendant la panne d'Ollama, les
  fiches CNIL ont été **absentes de la recherche ≈ 17 min** (version courante dès `fetch`). Fausses versions CNIL :
  3 dans la journée. Pylance strict : `pipelines/airflow/.venv` (Airflow 3.3.1) + `pyrightconfig.json`, 0 erreur.
- [x] Session 5 : ADR-007 (`docs/adr/007-airflow-taches-conteneurisees.md`), schéma `docs/pipeline-ingestion.md`.
  **Reste (Alexandre)** : exercices dans l'interface (onglet XCom, « Réinitialiser » = Clear).

**S5 terminée (2026-10-12)** (`docs/plan/semaine-05.md`) — qualité, quarantaine, migrations, back office.
**En cours : étape intermédiaire « rendre Limpide compréhensible par tous »** (plan global, avant la S6 ; demandée le
2026-10-12). Code fait (2026-10-12) : en-tête « ce qu'il fait / ne fait pas / pourquoi il montre tout » (encart
« Vous échangez avec une IA » gardé : AI Act, article 50) ; « Sous le capot » = récit en 5 étapes construit sur les
valeurs réelles (durées, meilleur score, seuil `MinScore`, passages cités, garde-fous traduits par
`PlainLanguage.cs`), coût en clair, détails techniques repliés. Testé dans le navigateur par Alexandre et déployé le
2026-10-12 ; premier déploiement de `web` sans erreur « antiforgery » (clés DataProtection dans le volume `web_keys`).
**Reste** : validation par 2 ou 3 personnes non techniques (critère : expliquer sans aide ce que fait Limpide et ce
que montre le panneau), retours à noter dans `docs/plan/bilan-phase-1.md`. **S6 terminée (2026-10-13)** (`docs/plan/semaine-06.md`) — Terraform et Azure ; schéma `docs/architecture-physique.md` ;
ADR-013 mis en œuvre le 2026-10-13 (Airflow et première ingestion en production). Rappel de la S6 : Alexandre ne connaît pas Terraform : concepts
d'abord. **Option B décidée le 2026-10-12** : la démo publique reste sur le VPS ; Azure = vitrine d'architecture
recréée à la demande (`apply` / `destroy` par session, alerte de budget), apprentissage d'abord sans frais
(fournisseurs `docker`, puis `ovh` pour la zone DNS). Terraform ne demande pas d'abonnement ; seul le fournisseur
`azurerm` en demande un. **Outil retenu : Terraform** (décision du 2026-10-12 : celui des clients et des offres ;
licence BSL depuis 2023) ; OpenTofu (fork compatible, MPL) noté dans l'ADR comme alternative sans réécriture. À faire en S6 :
**comparatif de coûts** Azure / Scaleway / OVHcloud / Hetzner / Clever Cloud / VPS auto-hébergé (permanent et à la
demande), hébergement d'Airflow (le corpus de production n'est mis à jour par aucun pipeline), portabilité souveraine.
Session 1 en cours : Terraform 1.16.5 installé (dépôt HashiCorp) ; exercice Docker
(`infrastructure/terraform/apprentissage/docker/`, base `tf-apprentissage-*` sur 5442, fournisseur `kreuzwerker/docker`
4.6.0) fait par Alexandre le 2026-10-12 : cycle, dérive, `-/+`, mot de passe en clair dans l'état, `destroy`. État et
`.terraform/` ignorés par Git, `.terraform.lock.hcl` versionné. Zone DNS OVH (`infrastructure/terraform/dns/`,
fournisseur `ovh/ovh` 2.22.0, jeton d'API limité à la zone dans `~/.ovh.conf`) : inventaire par sources de données
(12 entrées), import de `@`, `www`, `admin` (plan sans changement), entrée erronée `limpide-ia.fr.limpide-ia.fr`
supprimée par le code (2026-10-12) ; entrées posées par OVH (NS, MX, SPF, ftp, TXT) non gérées. Terraform ajouté à la
vitrine des technologies. État local (reconstructible par les blocs `import`). Azure (2026-10-13) : abonnement
« Azure subscription 1 », budget « Limpide » 10 €/mois (alerte seulement), Azure CLI 2.91 (dépôt `azure-cli` bookworm, clé
`/usr/share/keyrings/microsoft.gpg`) ; exercice `infrastructure/terraform/apprentissage/azure/` (azurerm 5.9.0, random
3.9.1 ; `ARM_SUBSCRIPTION_ID` par l'environnement ; `resource_providers_to_register` car azurerm 5 n'active plus rien :
`Microsoft.Storage` activé) fait par Alexandre : création, dérive d'étiquette (`~`), remplacement en cascade lu,
clé d'accès en clair dans l'état, `destroy` (rien ne reste). Session 2 faite (2026-10-13) : coffre de l'état
`rg-limpide-tfstate` / `stlimpidetf024b1e9f` / conteneur `tfstate`, créé par `infrastructure/terraform/etat/creer-socle.sh`
(hors Terraform ; clés d'accès désactivées, accès par Entra ID, « Storage Blob Data Contributor » sur le conteneur
seul, versions et suppression récupérable 30 jours) ; **seule ressource Azure permanente** (exception à l'option B).
État DNS migré (`dns/backend.tf`, `dns.tfstate`), plan sans changement, verrou vérifié par Alexandre. Les exercices
d'apprentissage gardent un état local. Session 3 faite (2026-10-13) : `infrastructure/terraform/azure/` (14 ressources,
fichiers par rôle, état `azure-preprod.tfstate`) ; PostgreSQL 17 B1ms **sans mot de passe** (Entra ID seul,
Alexandre administrateur, identité managée `id-limpide-preprod` = rôle créé par `pgaadauth_create_principal`),
`initialiser-base.sh` (rôle, migrations, droits, `--avec-corpus`) ; Key Vault RBAC (clé `mistral-api-key` déposée à
la main), stockage `brut` sans clés. Vérifié : 1 377 passages (1 130 servis), clé absente de l'état, aucun mot de
passe. **`apply` ≈ 10 min**, `destroy` ≥ 4 min (données pour la préproduction éphémère de la S7). Premier `destroy`
en échec (administrateur Entra supprimé avant le serveur = `DROP ROLE` refusé) : `detruire.sh` le retire de l'état
d'abord ; toujours détruire par ce script. Session 4 préparée (2026-10-13) : socle renommé `etat/creer-socle.sh`
(+ coffre à secrets **permanent** `kvlimpide024b1e9f` : clé Mistral déposée une fois ; le coffre par environnement de
la session 3 est retiré), `conteneurs.tf` (Log Analytics, Container Apps : `ca-ollama` interne, `ca-web` public),
pare-feu « services Azure » (accès public filtré, Entra ID = vraie barrière), images sur GitHub Container Registry
(`deploy/publier-images.sh`, version = empreinte du commit), `deploy/ollama/Dockerfile` (bge-m3 inclus, sans
bibliothèques GPU : 2,4 Go au lieu de 11,5 ; vecteurs identiques au poste). Code : `DatabaseOptions`
(`Database:Authentication` = `Password` par défaut | `EntraId`, jeton Entra ID par `UsePeriodicPasswordProvider`,
Azure.Identity 1.21.0). Ingestion sur Azure (jobs) reportée : stockage derrière une interface d'abord. **Session 4 faite (2026-10-13)** : Limpide tourne sur
Azure, testé par Alexandre (réponses correctes). Premier `apply` 22 min 16 s ; deux corrections (`logs_destination`
obligatoire en azurerm 5 ; `principal_name` tronqué à 63 caractères par Azure → `ignore_changes`, sinon remplacement
donc DROP ROLE). `azure/demo.sh` monte tout (vérifications, plan confirmé, apply, base, test HTTP, durées) ;
`detruire.sh` utilisable depuis un nouveau terminal. Session 5, code fait (2026-10-13) : `azure/modules/` (identite, donnees,
stockage, secrets, app, budget) et `azure/environnements/{preprod,prod}` (états `azure-preprod.tfstate` /
`azure-prod.tfstate`, tailles et budgets différents : B1ms/10 € contre B2s/20 €), `moved.tf` en préproduction (adresses
passées dans les modules ; plan : 18 ressources par environnement) ; `demo.sh --env`, `detruire.sh <env>`,
`initialiser-base.sh <env>`. **Test final fait (2026-10-13)** : `demo.sh` sur préproduction vide = infrastructure 9 min 10 s, base 28 s,
total 11 min 12 s, sans intervention (critère de la S6 atteint) ; `detruire.sh` complet : 13 min 5 s. Reste :
session 6 : ADR-011 (Container Apps) et comparatif de coûts (`docs/notes/comparatif-couts-hebergement.md`,
2026-10-13 : services gérés 75 à 108 €/mois contre 4 à 6 € pour un VPS ; Ollama ≈ 80 % du coût des conteneurs ; Azure
moins cher que Scaleway pour un service toujours allumé grâce à son tarif au repos ; Scaleway = cible de migration la
plus cohérente, une instance + Compose recréée par Terraform la moins chère) faits ; restent ADR Airflow, ADR modèles,
schéma. Épisode LinkedIn 23 écrit (2026-10-13 : Terraform, option B, deux échecs du cycle réel).

- [x] Session 1 (2026-10-10) : ADR-008. DbUp 7.0.1 (`dbup-postgresql`), migrations dans
  `src/Limpide.Infrastructure/Migrations/` (0001 schéma, 0002 retours : anciens `db/init`, rejouables ; 0003 cycle de
  vie : `status`, `status_reason`, `text_hash`, `published_at`, `quality_report`), commande `migrate`. Statuts
  (`VersionStatus`) : collected → extracted → chunked → (validated) → embedded → published → archived ; discarded,
  quarantined. Nouvelle commande et tâche du DAG `publish`. `VersionStatus.ReadyToEmbed` = `chunked` en attendant
  `validate` (session 2 : passer à `validated`). Vérifié : migration sur copie et base vide (même schéma), cycle
  complet sans fenêtre d'absence, DAG local (2 fausses versions CNIL écartées). Base locale migrée.
  **Déployé en production le 2026-10-10** : sauvegarde, `git pull`, image ingestion, `migrate` (3 versions → published,
  691 passages, 1 retour conservés), `web` reconstruit (plus d'erreur Kerberos dans ses journaux) ; `postgres` recréé
  au passage (montage `db/init` retiré), données intactes. Testé dans le navigateur par Alexandre le 2026-10-10. Avertissement préexistant : clés DataProtection de `web`
  non persistées (perdues à chaque recréation du conteneur) — à traiter avec la sécurité (S8).
- [x] Session 2 (2026-10-10) : commande et tâche du DAG `validate` (entre `chunk` et `embed`), 8 contrôles déterministes
  dans `Limpide.Core/Quality` (`QualityValidator`, 20 tests) : texte minimal, langue (part de mots outils français),
  nombre de passages (± 30 % de la version publiée), longueur, doublons, structure (AI Act ≥ 100 articles, CNIL ≥ 4
  intertitres), résidus HTML, texte d'interface. Seuils en configuration (`Quality`, appsettings de la console),
  calibrés avec marge : `docs/notes/observations-qualite.md`. Candidate → `validated` ou `quarantined` (raison dans
  `status_reason`, rapport JSON dans `quality_report`) ; versions publiées : rapport seul. `ReadyToEmbed` = `validated`.
  Même contenu qu'une version en quarantaine à la collecte : elle y reste (`stillQuarantined`). Quarantaine = code 0
  (compté dans le résumé) : l'alerte sera faite par `bilan` (session 3). Vérifié sur copie : quarantaine bloque
  embed/publish, version publiée servie, levée simulée → validée, vectorisée, publiée.
- [x] Session 3 (2026-10-10) : `bilan` alerte sur une quarantaine (raison + marche à suivre) sans faire échouer
  l'exécution ; résumé JSON avec détails (`quarantinedVersions`, `stillQuarantinedVersions`). Commandes `versions`,
  `release <empreinte> <motif>` (→ validated, publiée au prochain passage), `reject` (définitif : même contenu ignoré
  à la collecte). Démonstration `pipelines/airflow/demo/quarantaine.sh` : fiche CNIL servie tronquée par nginx local
  (`source-demo`, réseau `limpide_default`, `fetchUrl` du corpus de démo) → quarantaine (passages −62 %, 3
  intertitres), alerte reçue, 691 passages servis. Base locale : 2 versions de démonstration en quarantaine
  (c3094b5d, 4e4b03a2), gardées comme exemples. **Reste (Alexandre)** : enregistrer la démonstration pour la vidéo.
  **Sessions 2 et 3 déployées le 2026-10-11** (sauvegarde, image ingestion, `migrate` : rien à appliquer, `web`
  reconstruit, 691 passages servis). `validate` en production : « texte extrait absent » pour les 3 versions — le volume
  `data/` du serveur est vide, le corpus de production vient d'une sauvegarde de la base (S3) et **aucune ingestion
  n'y a jamais tourné** : le corpus de production n'est mis à jour par aucun pipeline (sujet de la S6, hébergement
  d'Airflow).
- [x] Session 4 : traçabilité faite (2026-10-11) — `RetrievedPassage.Origin` (`PassageOrigin` : id du passage,
  empreinte de version, extracteur, découpeur) ; interface : « Version collectée le … · 8ee3433abfca » sous chaque
  extrait cité (SHA-256 complète au survol), colonne « Version » dans « Passages fournis au modèle » ; retours :
  passages cités avec leur version ; commande `trace <question>` (lignage : passage → version → fichiers brut et
  extrait → extracteur, découpeur, embedding → contrôles qualité). Élargissement (2026-10-11) : les 15 autres pages
  de la série CNIL « Les fiches pratiques IA » (licence revérifiée, `docs/sources.md`) ; lignes directrices de la
  Commission non retenues (pas de version française trouvée, PDF : à trancher par Alexandre). Premier vrai test des
  contrôles : 2 pages en quarantaine, fausses alertes du contrôle de longueur (recalibré : ≤ 10 % de passages < 100 ou
  > 2 000 caractères) et un vrai défaut d'extraction (« Pour approfondir », documents joints : extracteur `cnil-html/2`) ;
  décision `retry` ajoutée (recontrôler après correction du code), utilisée pour les 2 pages. 18 documents publiés,
  1 130 passages. Évaluation : recherche top 5 7/10 (8/10 avant), MRR 0,52 ; réponses 11/15, 0 échec, relecture :
  fidèles (`docs/notes/observations-decoupage.md`). **En production le 2026-10-11** : base locale transférée
  (`pg_dump --exclude-table=feedback` pour garder les retours de production), `pg_restore --clean`, `web` reconstruit
  et redémarré ; 18 documents, 1 130 passages, 1 retour conservé. Après chaque recréation de `web` : erreurs
  « antiforgery token could not be decrypted » dans ses journaux (clés DataProtection non persistées, sans effet
  pour les visiteurs).
- [x] Session 5 (2026-10-11) : ADR-009 (batch incrémental, mesuré sur 13 jours) et ADR-010 (découpage structurel,
  **mesuré** contre la taille fixe sur une copie de la base : `FixedSizeChunker`, 1 362 passages, top 5 5/10 contre
  7/10, MRR 0,37 contre 0,52). Index `docs/adr/README.md`, README.
- [x] Back office v1 (2026-10-12, en production sur https://admin.limpide-ia.fr) : `src/Limpide.Admin` (Blazor, port local 5182),
  exposé sur `admin.limpide-ia.fr` (conteneur `admin`, bloc Caddy `ADMIN_ADDRESS`, base seulement : ni clé Mistral ni
  Ollama). Connexion OpenID Connect générique (`AdminAuthentication.cs`, seul fichier qui connaît le protocole ;
  Microsoft comptes personnels, `consumers/v2.0`) ; accès par liste d'identifiants « émetteur#sujet » (`Actor`, Core,
  testé) ; page `/refuse` qui affiche l'identifiant à autoriser. Pages : tableau de bord, versions et détail (rapport
  de contrôles, passages, décision publier / recontrôler / rejeter avec motif), retours (traité, export CSV protégé
  contre l'injection de formules), journal, réglages en lecture seule. Migration 0004 : `feedback.handled_at/by`,
  table `admin_actions` (journal écrit dans la transaction de la décision ; la console écrit « console »).
  Clés DataProtection persistées (`web_keys`, `admin_keys`). Connexion testée en local par Alexandre (inscription
  Entra « comptes personnels », même application pour le poste et la production, donc même identifiant). Piège vu :
  l'identifiant autorisé est « émetteur#sujet » complet (≈ 119 caractères), pas un GUID du portail (l'ID d'annuaire
  `9188040d…` est seulement la partie émetteur). Déployé le 2026-10-12 : migration 0004, `admin` (256 Mo), Caddy
  (`ADMIN_ADDRESS`), `web` recréé avec ses clés ; vérifié : redirect_uri en https, PKCE S256, portées
  `openid profile`, pages protégées → Microsoft. Première connexion en production par Alexandre le 2026-10-12 : tout
  fonctionne. Épisode 22 écrit.

**Après le projet : migration cloud** (ajoutée le 2026-10-13) : quitter Azure pour un hébergeur moins cher et plus
souverain, choisi sur critères écrits d'avance (comparatif S6, étude S10), et y migrer réellement ; perte assumée
des identités managées (à compenser). Puis **mise à l'échelle** (dont la question d'Elasticsearch/OpenSearch comme base vectorielle et plein texte, à
mesurer contre pgvector) (ajoutée au plan global le 2026-10-13) : toute la législation qui encadre
l'AI Act, pertinence mesurée nettement meilleure, deux publics (juriste et entreprise) sans jamais qualifier la
situation de l'utilisateur ; détail dans `docs/plan/plan-global.md`.

En suspens (Alexandre), **reporté à la fin du plan initial** (décision du 2026-10-13 : une fois les tâches du plan
faites, avant les étapes ajoutées « migration cloud » et « mise à l'échelle ») : validation de la page d'accueil par 2
ou 3 personnes non techniques, message à Jedha, 2 ou 3 testeurs extérieurs (retours via le formulaire, commande
`feedback`). Ne plus les relancer d'ici là.

**S3 terminée** (`docs/plan/semaine-03.md`) — mise en ligne de la démo

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
  Domaine nu `limpide-ia.fr` : entrée A vers le VPS, redirigé (301) vers `www` par Caddy (`REDIRECT_FROM` dans
  `.env.prod`, certificat Let's Encrypt propre), 2026-10-10.

- [x] Session 4 : quota en production (2026-10-07) — `QuestionQuota` (Core) : 10 questions/h par IP, 60/jour au total
  (≈ 9 €/mois max avec Medium, sous le plafond Mistral de 10 €), appliqué dans `Home.razor` avant l'appel au modèle ;
  IP relevée dans `App.razor` (seule requête HTTP de la visite), transmise en paramètre du composant racine, non
  journalisée. Vérifié : Caddy écarte un `X-Forwarded-For` forgé, Docker transmet l'IP publique réelle, et Docker
  contourne `ufw` pour les ports publiés. **Reste** : test du message à la 11e question,
  anti-bot (proposé : reporter en S8). La console Mistral n'a pas d'alerte de seuil documentée (vérifié le
  2026-10-10) : seulement le plafond mensuel (10 €, posé) et les tableaux de consommation ; l'alerte de budget est à
  construire en S8 (le coût par question est déjà calculé).
  Chaque déploiement de `web` coupe le site quelques secondes (502 pendant le redémarrage).
- [x] Session 5 : README réorganisé pour un lecteur extérieur (architecture en schéma, choix et ADR, mesures, garde-fous,
  limites) ; bilan `docs/plan/bilan-phase-1.md` (limites de la V1 = programme de la phase 2). **Reste (Alexandre)** :
  contacter Jedha, faire tester la démo par 2 ou 3 personnes et noter leurs retours dans le bilan.
  Formulaire de retours ajouté (`FeedbackForm.razor`, table `feedback` (migration 0002), `FeedbackStore`,
  commande `feedback`) : 3 questions facultatives, question jointe seulement si le visiteur coche la case, aucune IP,
  conservation 12 mois (purge à chaque envoi), 5 envois/h par visiteur (quota à clé `"feedback"`).

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
Observations de découpage à compléter au fil de l'eau : `docs/notes/observations-decoupage.md` (matière de l'ADR-010).

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
  jamais `docker compose down -v` (efface aussi le volume Ollama) ; puis `migrate` (le schéma n'est plus créé au
  démarrage du conteneur depuis la S5).
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
- Swap de 2 Go sur le VPS (`/swapfile`, `vm.swappiness=10`, 2026-10-09) : filet de sécurité (construction d'images
  sur le serveur pendant que tout tourne). Le graphique RAM d'OVH compte le cache disque : lire `free -m` → available.

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

Vitrine des technologies (demandée le 2026-10-12) : section « Comment Limpide est construit » de la démo
(`src/Limpide.Web/Components/TechStack.razor`), technologies groupées par rôle, icônes Simple Icons (CC0) servies par
l'application (`wwwroot/tech/`, aucune requête vers un tiers ; Microsoft absent de Simple Icons → icône OpenID).
**À tenir à jour** à chaque technologie adoptée (Terraform ou OpenTofu en S6, GitHub Actions en S7, OpenTelemetry en S8…).

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
  où tourne le modèle qui vectorise les questions en ligne, et combien il coûte au repos (ADR-006, puis l'ADR sur l'hébergement des modèles, S6).

Notes de la session 2 :

- EUR-Lex bloque les scripts (défi AWS WAF, `202` vide) : l'AI Act est téléchargé via l'API CELLAR
  (`fetchUrl` dans `corpus.json`). Le fichier brut est du XHTML Formex/CONVEX, pas la page EUR-Lex :
  en tenir compte pour l'extraction.
- `fetch` exige un `200` non vide et le bon type de contenu : un blocage ne crée jamais de version.
- Mot de passe PostgreSQL local dans `dotnet user-secrets` (projet Ingestion), jamais dans `appsettings.json`.
