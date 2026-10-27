---
paths:
  - ".github/**"
---

# Chaîne CI/CD (S7, ADR-015)

- **Le YAML n'orchestre que des commandes et des scripts lançables à la main** (`dotnet`, `terraform`,
  `deploy/*.sh`) : il déclenche, autorise, enchaîne et affiche. Tolérés dans le YAML : installer un outil
  (`setup-dotnet`, `setup-terraform`) et le câblage propre à la chaîne (clé SSH, `GITHUB_STEP_SUMMARY`). Toute
  logique qui décide ou transforme (version, construction, déploiement) va dans un script du dépôt.
- Actions tierces épinglées par empreinte de commit, version en commentaire. `permissions: contents: read` par
  défaut ; écriture accordée job par job (`packages: write`, `id-token: write`).
- Toute écriture en production (`apply` OVH, déploiement du VPS) passe par l'environnement `production` (relecteur
  requis). Azure : `fmt`/`validate` en PR, pas d'`apply` après fusion (option B) ; recréation par `azure.yml`, à la
  main seulement, environnement `azure` (limité à `main`, relecteur requis), jamais planifiée (ADR-016).
- `az` dans un job : `.github/connexion-azure.sh` ; le jeton OIDC de GitHub expire en quelques minutes, se reconnecter
  avant une étape qui suit une longue attente. Terraform (`ARM_USE_OIDC=true`) renouvelle seul ses jetons.
- Identités : Azure par OIDC (`etat/creer-identites-ci.sh`, une identité par usage : on n'élargit pas les
  existantes) ; OVH par comptes de service (`limpide-ci-lecture` en PR, `limpide-ci-ecriture` dans `production`).
- Outils tiers, surtout de sécurité (Grype) : lancés par un script depuis leur image Docker **épinglée par empreinte**,
  dans une version publiée depuis au moins deux semaines, dans un job sans secret quand c'est possible ; pas d'action
  du catalogue (compromission de `trivy-action`, mars 2026).
- **Images tierces du serveur** (S8) : `deploy/analyser-images.sh --tierces` le lundi, rapport sans échec (correctifs
  chez l'éditeur) ; le signal d'action, ce sont les PR de Dependabot (`.github/dependabot.yml`, docker-compose,
  délai de 14 jours, majeures ignorées) ; **lire le diff de chaque PR** : la CI ne contrôle pas l'architecture
  (PR #50 : `-386`, image 32 bits, proposée comme version plus récente). Étiquettes exactes dans les fichiers
  Compose, suffixe de distribution explicite pour les bases (`-bookworm`) : jamais de changement de glibc par
  surprise.
- **Évaluation (ADR-016)** : `evaluation.yml` → `deploy/evaluer.sh` sur le runner (PostgreSQL jetable, corpus du
  socle, jeu complet, `gate`) ; le job « Évaluation » est un contrôle requis de `main`. Ce qui déclenche :
  `deploy/evaluation-requise.sh`, liste large ; un nouveau fichier qui influe sur les réponses ou leur mesure doit y
  entrer. Corpus évalué = sauvegarde (`deploy/exporter-corpus.sh`), à rafraîchir après chaque publication.
- Version des images : `deploy/version-images.sh`, règle unique. Ajouter un fichier copié dans une image = l'ajouter
  à cette règle **et** aux filtres de chemins de `deploiement.yml`.
- Dépôt public : rien de secret dans les journaux ni dans `GITHUB_STEP_SUMMARY` (un `terraform show` y est affiché).
- Toute modification passe par une PR : `main` protégée, Alexandre fusionne et valide les environnements.
