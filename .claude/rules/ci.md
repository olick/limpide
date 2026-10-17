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
  requis). Azure : `fmt`/`validate` seulement, pas d'`apply` après fusion (option B).
- Identités : Azure par OIDC (`etat/creer-identites-ci.sh`, une identité par usage : on n'élargit pas les
  existantes) ; OVH par comptes de service (`limpide-ci-lecture` en PR, `limpide-ci-ecriture` dans `production`).
- Outils tiers, surtout de sécurité (Grype) : lancés par un script depuis leur image Docker **épinglée par empreinte**,
  dans une version publiée depuis au moins deux semaines, dans un job sans secret quand c'est possible ; pas d'action
  du catalogue (compromission de `trivy-action`, mars 2026).
- Version des images : `deploy/version-images.sh`, règle unique. Ajouter un fichier copié dans une image = l'ajouter
  à cette règle **et** aux filtres de chemins de `deploiement.yml`.
- Dépôt public : rien de secret dans les journaux ni dans `GITHUB_STEP_SUMMARY` (un `terraform show` y est affiché).
- Toute modification passe par une PR : `main` protégée, Alexandre fusionne et valide les environnements.
