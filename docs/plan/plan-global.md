# Limpide — plan global (9 semaines + 1 de marge)

**Rythme visé** : 8 à 10 heures par semaine, en parallèle d'une mission client.
**Principe** : une démo en ligne dès la semaine 3, puis l'industrialisation. Chaque composant ajouté
en phase 2 répond à une limite constatée en phase 1, et chaque arbitrage est tracé dans un ADR.

| Phase | Semaines | Résultat |
|---|---|---|
| 1 — POC de bout en bout | S1 à S3 | Démo publique en ligne |
| 2 — Industrialisation | S4 à S9 | Pipeline Airflow, infrastructure Terraform, évaluation en CI, observabilité, gouvernance |
| Marge | S10 | Rattrapage, finitions, préparation de la soutenance |

---

## Phase 1 — POC de bout en bout

### S1 · Environnement, ingestion simple, pgvector

Détail : [`semaine-01.md`](semaine-01.md)

- Docker Compose (PostgreSQL + pgvector, Ollama), solution .NET
- Commandes idempotentes `fetch`, `extract`, `chunk`, `embed`, `search`
- Découpage structurel (article par article pour l'AI Act)
- 10 questions de test et premier score de recherche

**Terminé quand** : le score de référence (bon passage dans le top 5) est noté dans le README.
**Blocs RNCP** : BC03 (amorce), BC01 (inventaire des sources et licences)

### S2 · RAG et interface « sous le capot »

- **Génération** : appel à un LLM hébergé derrière `IChatClient` (Microsoft.Extensions.AI),
  prompt imposant de citer les passages par identifiant
- **Séparation texte cité / réponse générée** : extraits reproduits à l'identique avec source et licence,
  réponse présentée comme la formulation de l'assistant (contrainte CC-BY-ND de la CNIL)
- **Garde-fous v1** :
  - « je ne sais pas » si aucun passage ne dépasse un seuil de score
  - refus des questions hors périmètre
  - avertissement : l'assistant ne qualifie pas une situation juridique
- **Recherche hybride** : vectorielle + plein texte (`tsvector`), fusion des classements.
  À mesurer contre le score de S1 : on ne la garde que si elle améliore le résultat
- **Interface** (Blazor) avec le panneau « sous le capot » : passages et scores, stratégie de recherche,
  latence par étape, tokens, coût estimé, garde-fous déclenchés
- **Transparence AI Act** : mention claire que l'utilisateur échange avec une IA

**Terminé quand** : une question produit une réponse citée, et le panneau affiche toutes les métriques.
**ADR** : 004 choix du LLM (API hébergée, modèle, coût par requête), 005 recherche hybride (avec mesures)
**Blocs RNCP** : BC04

### S3 · Mise en ligne : démo accessible

- Dockerfile de l'API, image publiée sur un registre
- Déploiement **manuel** : petit serveur avec Docker Compose, ou Azure Container Apps via le portail.
  Le manuel est volontaire, il servira de point de comparaison avec Terraform en S6
- HTTPS, nom de domaine
- **Protection de la démo publique** : limitation du débit par IP, plafond de dépenses mensuel côté fournisseur
  du LLM, protection anti-bot, longueur de question limitée
- README avec lien vers la démo et captures

- **Formulaire de retours** en bas de page (ajouté en cours de S3) : trois questions facultatives, dernière question
  jointe seulement avec l'accord du visiteur, aucune adresse IP, conservation 12 mois, anti-spam ; commande `feedback`

**Terminé quand** : une personne extérieure peut utiliser la démo, et un abus ne peut pas dépasser le budget fixé.
**ADR** : 006 hébergement de la démo (coût, simplicité, souveraineté)
**Blocs RNCP** : BC02 (amorce)

> **Point de décision fin S3** : faire le bilan des limites de la V1 (ingestion manuelle, déploiement
> à la main, pas d'évaluation automatique). C'est la liste de travail de la phase 2.
> Contacter Jedha pour savoir si un projet personnel peut servir de support d'évaluation.

---

## Phase 2 — Industrialisation

### S4 · Airflow : apprentissage et DAG d'ingestion

- **Concepts à maîtriser** : DAG, tâches, opérateurs, planificateur, relances, idempotence,
  pourquoi on ne fait pas transiter de données entre tâches
- Airflow en local avec le Docker Compose officiel, un DAG trivial pour prendre la main.
  Vérifier la version : Airflow 3 diffère de la plupart des tutoriels écrits pour la version 2
- DAG `ingestion` qui lance les commandes .NET existantes **dans des conteneurs** : fetch → extract → chunk → embed
- Planification hebdomadaire, relances automatiques, notification en cas d'échec

**Terminé quand** : le DAG tourne de bout en bout, et le relancer ne crée aucun doublon.
**ADR** : 007 Airflow et tâches .NET conteneurisées (plutôt que du Python)
**Blocs RNCP** : BC03

### S5 · Qualité des données, quarantaine, versionnement

- **Contrôles qualité** entre extraction et embeddings : document vide, extraction ratée,
  doublons, langue inattendue, passages trop courts ou trop longs, nombre de passages anormal par rapport à la version précédente
- **Quarantaine** : une version qui échoue n'est pas publiée, la version courante reste en service, alerte envoyée.
  Démontrer avec une source volontairement corrompue
- **Traçabilité** : chaque réponse cite la version et la date de collecte du document
- Élargissement du corpus (autres fiches CNIL, lignes directrices européennes) maintenant que le pipeline est fiable

- **Back office, v1** (demandé le 2026-10-07) : retours des visiteurs (lecture, statut « traité », export) ;
  versions des documents et quarantaine (voir une version bloquée, la publier, la recontrôler ou la rejeter).
  **Exposé sur `admin.limpide-ia.fr`** (décision du 2026-10-12, au lieu d'un tunnel SSH), connexion **OpenID Connect
  générique** avec Microsoft comme premier fournisseur : changer de fournisseur = configuration, pas de code ; accès
  réservé aux identifiants listés en configuration ; journal des décisions (qui, quoi, quand, motif) ; clés de
  chiffrement ASP.NET conservées dans un volume. Réglages (seuil, quota, modèle) en lecture seule : ils changent
  par Git, donc tracés et, dès la S7, évalués en CI

**Terminé quand** : une source corrompue est bloquée sans impact sur la démo, et l'alerte arrive.
**ADR** : 008 cycle de vie des versions et migrations (ajouté, session 1), 009 batch incrémental plutôt que streaming, 010 découpage structurel plutôt qu'à taille fixe (mesuré)
**Blocs RNCP** : BC03, BC01 (qualité et traçabilité)

### Étape intermédiaire (avant la S6) · Rendre Limpide compréhensible par tous

Demandée le 2026-10-12 : la démo doit parler à un visiteur sans connaissance technique (premier retour : « il donne
un point de vue légal », « je n'ai pas tout compris [du panneau] mais c'est utile »).

- **Objectif explicite** dès l'arrivée : ce que Limpide fait (retrouver et citer les passages de l'AI Act et des fiches
  de la CNIL qui répondent à une question), ce qu'il ne fait pas (aucun avis sur votre situation), et pourquoi il
  montre tout (vérifier soi-même) ; quelques exemples de questions à cliquer.
- **« Sous le capot » expliqué en langage courant** : le déroulé en étapes (votre question → recherche des passages
  les plus proches par le sens → seuil « je ne sais pas » → rédaction par Mistral → contrôles des citations), chaque
  mesure traduite (« score de proximité », « tokens = morceaux de texte facturés », « coût : ce que cette réponse a
  coûté »), un garde-fou déclenché expliqué par ce qu'il protège ; détails techniques repliés pour qui les veut.
- **Validé par des personnes non techniques** (2 ou 3, retours notés dans le bilan) : comprennent-elles ce que fait
  Limpide et ce que montre le panneau ?

**Terminé quand** : une personne non technique explique, sans aide, ce que fait Limpide et ce que montre le panneau.

### S6 · Terraform : apprentissage et infrastructure Azure

**Décision du 2026-10-12 (option B)** : la démo publique reste sur le VPS ; Azure est une **vitrine d'architecture**
recréée à la demande (`apply` / `destroy` par session, alerte de budget dès le premier jour), pas un hébergement
permanent. Apprentissage de Terraform d'abord sans frais : fournisseurs `docker` (pile locale) et `ovh` (zone DNS).

- **Concepts à maîtriser** : provider, ressource, variable, **état (state)**, `plan` / `apply`, modules
- Prise en main : un groupe de ressources créé puis détruit
- **État distant** dans un compte de stockage Azure, avec verrouillage
- Infrastructure complète :
  - Azure Container Apps (API, tâches d'ingestion), mise à l'échelle jusqu'à zéro
  - PostgreSQL managé avec pgvector
  - Blob Storage pour les documents bruts
  - Key Vault et identités managées : aucun secret dans le code ni dans les variables d'environnement
  - réseau : la base n'est joignable que depuis l'application
- Modules et deux environnements : préproduction et production
- Hébergement d'Airflow : trancher et documenter
- **Alternatives moins chères qu'Azure** (demandé le 2026-10-12 : baisser les coûts au maximum) : pour la même
  architecture (conteneurs sans serveur, PostgreSQL + pgvector, stockage S3, secrets, tâches planifiées), chiffrer
  Azure face à Scaleway, OVHcloud, Hetzner, Clever Cloud et à l'auto-hébergement sur le VPS (Docker Compose ou un
  Kubernetes léger) ; deux profils : **fonctionnement permanent** (démo publique) et **à la demande** (vitrine
  recréée par session). Prix relevés et datés, sources citées ; tableau dans l'ADR d'infrastructure, avec ce que
  chaque option fait perdre (identités managées, préproduction, haute disponibilité). Le moins cher qui tient les
  exigences de la démo devient la cible de l'étude de migration (S10).
- **Portabilité vers un cloud souverain** (demandé le 2026-10-12 ; migration envisagée, pas décidée) :
  - stockage des fichiers bruts **derrière une interface** (comme `IEmbeddingGenerator`), deux implémentations
    possibles : Azure Blob et S3 (Scaleway, OVHcloud, Outscale) ; Azure Blob n'est pas compatible S3, le SDK Azure
    utilisé directement enfermerait le code ;
  - Terraform organisé **par rôle** (calcul, base, stockage, secrets, identité) : les ressources changent d'un
    fournisseur à l'autre, l'organisation reste ;
  - dans l'ADR d'infrastructure, un tableau d'équivalences Azure / Scaleway / OVHcloud, coûts compris : Container Apps
    ↔ Serverless Containers (Scaleway) ; PostgreSQL managé + pgvector (confirmé chez Scaleway, à vérifier chez OVH) ;
    Blob ↔ stockage S3 ; Key Vault ↔ gestionnaires de secrets (à vérifier) ; **identités managées : pas
    d'équivalent** (clés d'API à stocker) ; Entra ID ↔ fournisseur OpenID Connect libre auto-hébergé (Pocket ID,
    Authelia), déjà possible sans changer le code (connexion du back office en OpenID Connect générique)

**Terminé quand** : `terraform destroy` puis `terraform apply` recrée un environnement fonctionnel sans intervention manuelle.
**ADR** : Container Apps plutôt qu'AKS, hébergement d'Airflow, hébergement des modèles : LLM par API ou auto-hébergé sur GPU (calcul de coût comparatif)
**Blocs RNCP** : BC02

### S7 · Évaluation et CI/CD

- **Jeu d'évaluation** d'environ 50 questions, à partir des 10 de S1 :
  - questions avec passage attendu
  - questions hors périmètre (refus attendu)
  - questions sans réponse dans le corpus (« je ne sais pas » attendu)
  - tentatives d'injection de prompt
- **Métriques** : rappel de la recherche (bon passage dans le top k), fidélité de la réponse aux sources,
  taux de refus corrects, latence, coût par question
- **CI (GitHub Actions)** : build, tests, évaluation. Le déploiement est bloqué si les scores baissent
- **CD** : déploiement automatique en préproduction, puis production après validation.
  Authentification à Azure par OIDC, sans secret stocké dans GitHub
- **Terraform dans la chaîne** (ajouté le 2026-10-13) : `fmt`, `validate` et `plan` à chaque pull request (plan
  affiché dans la PR, relu avant la fusion) ; `apply` après la fusion, validation manuelle pour la production.
  Prérequis : état distant et verrouillé (S6), OIDC. Zone DNS OVH hors chaîne (pas d'OIDC chez OVH : `apply` manuel)
- **Option B et préproduction** : la préproduction Azure n'existe pas en permanence. Option à valider en S6, après
  mesure de la durée de création : **préproduction éphémère** créée par la chaîne (`apply`, déploiement, données
  restaurées depuis une sauvegarde du stockage Azure, évaluation, `destroy`). La production reste la démo du VPS :
  déploiement par la chaîne (SSH) après une évaluation réussie, à la place du déploiement manuel
- **Versionnement** de ce qui influence la qualité : prompt, modèle, modèle d'embedding, jeu d'évaluation
- Page publique des résultats d'évaluation, par version

- **Questions réelles des visiteurs** (formulaire de retours, avec leur accord) : source de questions écrites par
  d'autres que l'auteur pour le jeu d'évaluation ; un retour « réponse fausse » devient un cas de test

- **Back office, v2** : transformer un retour de visiteur en cas de test ; résultats d'évaluation par version

**Terminé quand** : une modification du prompt qui dégrade les scores est bloquée automatiquement.
**ADR** : métriques et seuils d'évaluation
**Blocs RNCP** : BC04

### S8 · Observabilité, coûts, sécurité

- **OpenTelemetry** : une trace par requête (recherche, génération, garde-fous), tableau de bord de latence
- **FinOps / GreenOps** : coût par requête et par ingestion, alertes de budget Azure,
  estimation de la consommation énergétique, effet de la mise à l'échelle jusqu'à zéro
- **Garde-fous v2** : détection d'injection de prompt, filtrage des sorties, journalisation des refus
- **Sécurité** : revue des accès, rotation des secrets, analyse des images de conteneurs
- **Surveillance de la dérive** : suivi dans le temps du taux de « je ne sais pas » et des scores de recherche
- **Ressources du serveur** : alerter sur la mémoire **disponible** (`MemAvailable`), le swap utilisé et le disque,
  pas sur la mémoire « utilisée » : le graphique d'OVH affichait 100 % alors que 1,6 Go restaient disponibles
  (cache disque, 2026-10-09, voir `deploy/README.md`)

- **Données des visiteurs** : revoir la politique des retours (contenu, durée, information), à intégrer au registre
  des traitements de la S9

- **Suivi en direct dans l'interface** (demandé le 2026-10-07) : chaque étape affichée au fur et à mesure
  (vectorisation, recherche, seuil, génération, contrôles), réponse affichée en continu (streaming) ; mêmes mesures
  que les traces OpenTelemetry. Les citations ne se vérifient qu'une fois la réponse complète : l'afficher
  (« vérification des citations… »). L'explication du panneau pour un non-spécialiste est avancée à l'étape
  intermédiaire d'avant la S6 ; ici, l'affichage en direct reprend ces mêmes explications
- **Back office, v3** : tableaux de bord — coût par jour et par modèle, quota consommé, garde-fous déclenchés,
  taux de « je ne sais pas » (dérive), latences ; authentification Entra ID une fois sur Azure

**Terminé quand** : pour n'importe quelle requête de la démo, on retrouve sa trace, son coût et ses garde-fous.
**Blocs RNCP** : BC04, BC02 (sécurité, FinOps)

### S9 · Gouvernance, documentation, démonstration

- **Dossier de gouvernance** :
  - classification des données et RACI
  - registre des sources et des licences
  - registre des risques IA (hallucination, dénaturation d'un texte, injection, dépendance au fournisseur)
  - **classification de Limpide lui-même au regard de l'AI Act** (obligations de transparence)
  - fiche descriptive du système : modèles, données, limites connues, métriques
  - procédure d'incident
- Relecture et mise à jour de tous les ADR
- README final : démo, architecture, résultats d'évaluation, choix et limites
- **Vidéo de 2 à 3 minutes** : une question, le panneau « sous le capot », une source corrompue bloquée,
  un déploiement bloqué par l'évaluation
- Mise à jour du profil (Malt, LinkedIn) avec le lien

**Terminé quand** : quelqu'un qui ne connaît pas le projet comprend en 5 minutes ce qu'il fait, comment, et pourquoi ces choix.
**Blocs RNCP** : BC01, tous les blocs pour la soutenance

### S10 · Marge

Rattrapage du retard accumulé, finitions, préparation d'un support de soutenance si la certification se confirme.

**Piste, si le temps le permet** (demandée le 2026-10-12 ; elle prépare l'étape « Migration cloud », après le
projet) : **étude de migration vers un cloud souverain**, en
partant du comparatif de coûts de la S6 (alternative la moins chère qui tient les exigences de la démo).
Cibles : Scaleway (le plus proche de l'architecture Azure prévue), OVHcloud (déjà l'hébergeur de la démo), Outscale
(le plus souvent cité comme qualifié SecNumCloud) ; intermédiaires : Bleu (technologie Microsoft), S3NS (Google).
Livrable : statut SecNumCloud vérifié dans la **liste officielle de l'ANSSI** (les comparatifs se contredisent),
effort par composant, coût comparé, et démonstration du changement de fournisseur d'identité par configuration
(Microsoft → Pocket ID ou Authelia). Point de départ : les équivalences notées en S6.

**Étape bonus, seulement s'il reste du temps** (demandée le 2026-10-08) : **chaque retour devient une question de test**.
En S7, la conversion d'un retour en cas de test est manuelle (back office v2). Le bonus l'automatise jusqu'à la
proposition : à chaque retour accompagné de sa question (avec l'accord du visiteur), un cas de test candidat est préparé
(question, comportement attendu : répondre, refuser ou ne pas qualifier, passage attendu si on le connaît) et attend
une validation humaine dans le back office ; validé, il rejoint `eval/questions.json` par Git, donc passe par la CI.
Jamais d'ajout sans relecture : un cas de test faux fausserait toutes les mesures suivantes.
Les retours n'alimentent **jamais** le corpus : seulement l'évaluation.
Lien : le post LinkedIn du jour 10 (`.vscode/linkedin-limpide.md`) annonce « Chaque retour devient une question de
test » ; vrai en intention dès maintenant, en pratique avec ce bonus.


## Après le projet · Migration cloud (demandée le 2026-10-13)

Après les 10 semaines, avant la mise à l'échelle (qui en dépend : la cible doit tenir un corpus plus gros).
Objectif : **quitter Azure pour un hébergeur moins cher et plus souverain**, choisi sur mesures, puis y migrer
réellement l'environnement industrialisé (préproduction, production, CI/CD), et pas seulement l'étudier.

**Entrées** : le comparatif de coûts de la S6 (Azure / Scaleway / OVHcloud / Hetzner / Clever Cloud / auto-hébergement)
et l'étude de la S10 (statut SecNumCloud vérifié sur la liste de l'ANSSI, effort par composant, équivalences).

**Critères du choix**, écrits avant de comparer :

- coût mensuel réel, en fonctionnement permanent et à la demande, relevé et daté ;
- souveraineté : société et juridiction européennes (hors lois extraterritoriales), données en France, qualification
  SecNumCloud si le surcoût reste raisonnable ;
- services équivalents : conteneurs, PostgreSQL managé avec pgvector, stockage S3, secrets, tâches planifiées,
  authentification de la CI sans secret stocké si le fournisseur le permet ;
- effort de migration et réversibilité (Terraform par rôle, stockage derrière une interface).

**Ce qui se perd, à dire franchement** : les identités managées et la connexion à PostgreSQL sans mot de passe
(option choisie sur Azure en S6) n'ont pas d'équivalent direct chez les hébergeurs européens étudiés : clés d'API ou
mots de passe à stocker dans un gestionnaire de secrets, à durée de vie courte et à faire tourner. C'est le prix de la
souveraineté ; le chiffrer et le compenser (rotation, droits minimaux, journal des accès) plutôt que l'ignorer.

**Migration** : Terraform du fournisseur cible (mêmes rôles, nouvelles ressources), implémentation S3 du stockage,
données copiées puis vérifiées (nombre de passages, évaluation identique avant et après), CI/CD réauthentifiée,
bascule puis destruction de l'environnement Azure ; la démo publique peut rejoindre la cible si elle y coûte moins
que le VPS (ADR-006 révisé). Changement du fournisseur d'identité du back office (Microsoft → Pocket ID ou Authelia)
par configuration, comme prévu.

**Livrables** : ADR « hébergeur cible » (tableau des critères, coûts datés, ce qui se perd), environnement cible
recréé par `apply`, évaluation identique avant et après, Azure détruit, coût mensuel comparé.

## Après le projet · Mise à l'échelle (demandée le 2026-10-13)

Hors calendrier de certification : une fois les 10 semaines bouclées. Objectif : passer d'une démo sur un échantillon
(AI Act + fiches IA de la CNIL) à un outil utile sur **l'ensemble des textes qui encadrent l'AI Act**, nettement plus
pertinent, et qui serve **autant à un juriste qu'à une entreprise**.

**Corpus** : inventaire en début d'étape, source par source, avec licence et langue vérifiées (`docs/sources.md`) :
actes délégués et d'exécution, lignes directrices de la Commission, code de bonnes pratiques des modèles d'usage
général, questions-réponses du Bureau de l'IA, modifications du règlement (versions consolidées et dates
d'application), autres publications des autorités (CNIL, CEPD). Contraintes déjà rencontrées : certains textes
n'existent qu'en anglais (S5 : lignes directrices écartées faute de version française, décision à prendre sur le
multilingue) ; les normes harmonisées sont payantes, donc probablement citées sans être reproduites.

**Volume et architecture** : probablement quelques milliers à quelques dizaines de milliers de passages, encore loin
des « millions » de l'ADR-001. La mesure décidera : relever la latence de recherche, puis seulement réviser l'ADR-001
(réglages de pgvector d'abord, base dédiée type Qdrant ensuite). Vectorisation : 816 ms par passage sur CPU (ADR-002),
soit ≈ 4 h 30 pour 20 000 passages ; à rapprocher de l'ADR sur l'hébergement des modèles (S6).

**Pertinence, mesurée** : objectifs chiffrés fixés au départ, sur le jeu de questions de la S7 élargi.
Pistes à mesurer une par une, jamais ajoutées sans mesure : reclassement des résultats (reranking), reformulation de
la question, distinction articles (qui font foi) et considérants (aide à l'interprétation), renvois entre textes
(article 6 → annexe III, règlement → lignes directrices), filtres par source et par date.

**Deux publics** :

- **Juriste** : citation précise (article, paragraphe, point), texte en vigueur à une date donnée, renvois entre
  textes, distinction de ce qui oblige et de ce qui éclaire.
- **Entreprise** : formulation simple, entrée par rôle (fournisseur, déployeur, importateur, distributeur) et par
  type de système, obligations rattachées aux textes.
- **Règle inchangée** : Limpide ne qualifie pas la situation de l'utilisateur (garde-fou `qualification-juridique`).
  Pour une entreprise, il montre les obligations attachées à chaque rôle selon les textes ; c'est elle qui se situe.
  Cette frontière est la difficulté principale de l'étape : à trancher dans un ADR, avant le code.

**Évaluation** : questions par public, comportements attendus (répondre, refuser, ne pas qualifier) ; relecture
**par un juriste**, pas seulement par un modèle (limite assumée jusqu'ici, ADR-004).

**Livrables** : corpus élargi et documenté, ADR (périmètre du corpus et langues, deux publics, révision de l'ADR-001 si
la mesure l'exige), scores avant/après publiés, démo mise à jour.

---

## Correspondance avec la RNCP41993

| Bloc | Preuves produites | Semaines |
|---|---|---|
| BC01 Gouvernance | Registre des sources et licences, règles de citation, dossier de gouvernance, registre des risques, classification AI Act | S1, S2, S5, S9 |
| BC02 Infrastructure | Terraform, deux environnements, réseau, Key Vault, arbitrages d'hébergement et de GPU, FinOps | S3, S6, S8 |
| BC03 Pipelines | Airflow, contrôles qualité, quarantaine, versionnement, arbitrage batch/streaming | S1, S4, S5 |
| BC04 Industrialisation | Évaluation en CI, déploiement conditionné, observabilité, coûts, garde-fous, dérive | S2, S7, S8 |

## Registre des ADR

Un numéro est attribué **au moment où l'ADR est écrit** (convention adoptée le 2026-10-10) : les décisions à venir se
désignent par leur sujet. Index des ADR écrits : `docs/adr/README.md`.

| N° | Sujet | Semaine |
|---|---|---|
| 001 | PostgreSQL + pgvector plutôt que Qdrant | S1 ✔ |
| 002 | Embeddings locaux bge-m3 | S1 ✔ |
| 003 | Ingestion console .NET puis Airflow | S1 ✔ |
| 004 | Choix du LLM | S2 ✔ (Mistral, UE) |
| 005 | Recherche hybride | S2 ✔ (écartée, mesures) |
| 006 | Hébergement de la démo | S3 ✔ (VPS OVHcloud) |
| 007 | Airflow et tâches .NET conteneurisées | S4 ✔ |
| 008 | Cycle de vie des versions et migrations du schéma (non prévu au départ) | S5 ✔ |
| 009 | Batch incrémental plutôt que streaming | S5 ✔ |
| 010 | Stratégie de découpage : structurel plutôt qu'à taille fixe (mesuré) | S5 ✔ |

À écrire :

| Sujet | Semaine |
|---|---|
| Container Apps plutôt qu'AKS | S6 |
| Hébergement d'Airflow | S6 |
| Hébergement des modèles : LLM par API ou auto-hébergé sur GPU, embeddings | S6 |
| Métriques et seuils d'évaluation | S7 |

## Risques du planning

- **Le temps disponible** : la mission client passe en premier. En cas de retard, sacrifier dans cet ordre :
  la recherche hybride (S2), l'élargissement du corpus (S5), les garde-fous v2 (S8), puis reporter en S10 le
  suivi en direct et les tableaux de bord du back office (S8). Ne jamais sacrifier
  l'évaluation (S7) : c'est ce qui distingue le projet.
- **Airflow et Terraform** sont nouveaux : S4 et S6 sont les semaines les plus susceptibles de déborder. La S10 est là pour ça.
- **Le coût Azure** : fixer une alerte de budget dès la S3 et détruire la préproduction quand elle ne sert pas.
