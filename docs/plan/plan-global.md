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
  versions des documents et quarantaine (voir une version bloquée, la publier ou la rejeter). Accessible seulement
  par tunnel SSH (aucune page de connexion exposée) ; réglages (seuil, quota, modèle) en lecture seule : ils changent
  par Git, donc tracés et, dès la S7, évalués en CI

**Terminé quand** : une source corrompue est bloquée sans impact sur la démo, et l'alerte arrive.
**ADR** : 008 stratégie de découpage (avec les observations de S1 et S5), 009 batch incrémental plutôt que streaming
**Blocs RNCP** : BC03, BC01 (qualité et traçabilité)

### S6 · Terraform : apprentissage et infrastructure Azure

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

**Terminé quand** : `terraform destroy` puis `terraform apply` recrée un environnement fonctionnel sans intervention manuelle.
**ADR** : 010 Container Apps plutôt qu'AKS, 011 hébergement d'Airflow, 012 LLM par API ou auto-hébergé sur GPU (calcul de coût comparatif)
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
- **Versionnement** de ce qui influence la qualité : prompt, modèle, modèle d'embedding, jeu d'évaluation
- Page publique des résultats d'évaluation, par version

- **Questions réelles des visiteurs** (formulaire de retours, avec leur accord) : source de questions écrites par
  d'autres que l'auteur pour le jeu d'évaluation ; un retour « réponse fausse » devient un cas de test

- **Back office, v2** : transformer un retour de visiteur en cas de test ; résultats d'évaluation par version

**Terminé quand** : une modification du prompt qui dégrade les scores est bloquée automatiquement.
**ADR** : 013 métriques et seuils d'évaluation
**Blocs RNCP** : BC04

### S8 · Observabilité, coûts, sécurité

- **OpenTelemetry** : une trace par requête (recherche, génération, garde-fous), tableau de bord de latence
- **FinOps / GreenOps** : coût par requête et par ingestion, alertes de budget Azure,
  estimation de la consommation énergétique, effet de la mise à l'échelle jusqu'à zéro
- **Garde-fous v2** : détection d'injection de prompt, filtrage des sorties, journalisation des refus
- **Sécurité** : revue des accès, rotation des secrets, analyse des images de conteneurs
- **Surveillance de la dérive** : suivi dans le temps du taux de « je ne sais pas » et des scores de recherche

- **Données des visiteurs** : revoir la politique des retours (contenu, durée, information), à intégrer au registre
  des traitements de la S9

- **Suivi en direct dans l'interface** (demandé le 2026-10-07) : chaque étape affichée au fur et à mesure
  (vectorisation, recherche, seuil, génération, contrôles), réponse affichée en continu (streaming) ; mêmes mesures
  que les traces OpenTelemetry. Les citations ne se vérifient qu'une fois la réponse complète : l'afficher
  (« vérification des citations… »). Panneau « sous le capot » expliqué pour un non-spécialiste (retour de testeur :
  « je n'ai pas tout compris »)
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

---

## Correspondance avec la RNCP41993

| Bloc | Preuves produites | Semaines |
|---|---|---|
| BC01 Gouvernance | Registre des sources et licences, règles de citation, dossier de gouvernance, registre des risques, classification AI Act | S1, S2, S5, S9 |
| BC02 Infrastructure | Terraform, deux environnements, réseau, Key Vault, arbitrages d'hébergement et de GPU, FinOps | S3, S6, S8 |
| BC03 Pipelines | Airflow, contrôles qualité, quarantaine, versionnement, arbitrage batch/streaming | S1, S4, S5 |
| BC04 Industrialisation | Évaluation en CI, déploiement conditionné, observabilité, coûts, garde-fous, dérive | S2, S7, S8 |

## Registre des ADR prévus

| N° | Sujet | Semaine |
|---|---|---|
| 001 | PostgreSQL + pgvector plutôt que Qdrant | S1 ✔ |
| 002 | Embeddings locaux bge-m3 | S1 ✔ |
| 003 | Ingestion console .NET puis Airflow | S1 ✔ |
| 004 | Choix du LLM | S2 ✔ (Mistral, UE) |
| 005 | Recherche hybride | S2 ✔ (écartée, mesures) |
| 006 | Hébergement de la démo | S3 |
| 007 | Airflow et tâches .NET conteneurisées | S4 |
| 008 | Stratégie de découpage | S5 |
| 009 | Batch incrémental plutôt que streaming | S5 |
| 010 | Container Apps plutôt qu'AKS | S6 |
| 011 | Hébergement d'Airflow | S6 |
| 012 | LLM par API ou auto-hébergé sur GPU | S6 |
| 013 | Métriques et seuils d'évaluation | S7 |

## Risques du planning

- **Le temps disponible** : la mission client passe en premier. En cas de retard, sacrifier dans cet ordre :
  la recherche hybride (S2), l'élargissement du corpus (S5), les garde-fous v2 (S8), puis reporter en S10 le
  suivi en direct et les tableaux de bord du back office (S8). Ne jamais sacrifier
  l'évaluation (S7) : c'est ce qui distingue le projet.
- **Airflow et Terraform** sont nouveaux : S4 et S6 sont les semaines les plus susceptibles de déborder. La S10 est là pour ça.
- **Le coût Azure** : fixer une alerte de budget dès la S3 et détruire la préproduction quand elle ne sert pas.
