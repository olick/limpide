---
paths:
  - "infrastructure/terraform/**"
---

# Terraform et Azure (S6)

Alexandre ne connaissait pas Terraform avant la S6 : expliquer les concepts, pas de boîte noire.
Détail : `infrastructure/terraform/*/README.md`, `docs/plan/semaine-06.md` (journal de réalisation).

- **Option B** : la démo publique reste sur le VPS ; Azure = vitrine recréée à la demande
  (`azure/demo.sh --env <env>`, puis `azure/detruire.sh <env>`), budget « Limpide » 10 €/mois (alerte seulement).
- Ressources Azure **permanentes** (créées hors Terraform par `etat/creer-socle.sh`) : coffre de l'état
  `rg-limpide-tfstate` / `stlimpidetf024b1e9f` / conteneur `tfstate` (accès Entra ID, clés désactivées) et coffre à
  secrets `kvlimpide024b1e9f` (clés `mistral-api-key`, `ovh-ai-endpoints-key`), identités de la CI
  (`etat/creer-identites-ci.sh` : OIDC, `id-limpide-ci-lecture` pour les PR, `id-limpide-ci-production`). Tout le
  reste se détruit.
- États distants : `dns.tfstate`, `azure-preprod.tfstate`, `azure-prod.tfstate`. Les exercices d'`apprentissage/`
  gardent un état local. `.terraform/` et `*.tfstate` ignorés, `.terraform.lock.hcl` versionné.
- `ARM_SUBSCRIPTION_ID` par l'environnement. azurerm 5 n'active plus aucun fournisseur de ressources :
  `resource_providers_to_register`. `logs_destination` obligatoire pour Container Apps en azurerm 5.
- PostgreSQL **sans mot de passe** (Entra ID seul) ; rôle de l'identité managée créé par
  `pgaadauth_create_principal` (`initialiser-base.sh <env>`). Code : `Database:Authentication=EntraId`.

Pièges :

- **Toujours détruire par `detruire.sh`** : il retire l'administrateur Entra de l'état d'abord (sinon `DROP ROLE`
  refusé et `destroy` en échec).
- `principal_name` tronqué à 63 caractères par Azure → `ignore_changes`, sinon remplacement, donc `DROP ROLE`.
- Lire chaque `plan` : un `-/+` sur une base de données = données perdues.
- Durées mesurées : `demo.sh` sur environnement vide ≈ 11 min, `detruire.sh` ≈ 13 min.
- Zone DNS OVH (`dns/`, jeton limité à la zone dans `~/.ovh.conf`) : entrées posées par OVH (NS, MX, SPF, ftp, TXT)
  non gérées ; `apply` manuel jusqu'à la S7 (session 4a), où elle entre dans la CI avec le VPS importé (comptes de
  service OVH : lecture pour le `plan`, écriture protégée pour l'`apply`).
- Secrets jamais en variables Terraform (ils finissent en clair dans l'état) : Key Vault + identités managées.
