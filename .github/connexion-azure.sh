#!/usr/bin/env bash
# Connexion d'Azure CLI par OIDC dans un job GitHub Actions (S7) : le jeton signé par GitHub est échangé contre une
# session az de l'identité ARM_CLIENT_ID (etat/creer-identites-ci.sh). Câblage propre à la chaîne ; sur le poste,
# c'est `az login`. Terraform, lui, demande ses jetons tout seul (ARM_USE_OIDC=true).
# Le jeton de GitHub ne vit que quelques minutes, et az en a besoin pour chaque nouveau service (base, coffre,
# stockage) : relancer ce script avant une étape qui suit une longue attente (terraform apply, par exemple).
set -euo pipefail
: "${ACTIONS_ID_TOKEN_REQUEST_URL:?à lancer dans un job GitHub Actions qui a la permission id-token: write}"

jeton="$(curl -fsS -H "Authorization: Bearer $ACTIONS_ID_TOKEN_REQUEST_TOKEN" \
  "$ACTIONS_ID_TOKEN_REQUEST_URL&audience=api://AzureADTokenExchange" | jq -r .value)"
echo "::add-mask::$jeton"
az login --service-principal --username "${ARM_CLIENT_ID:?}" --tenant "${ARM_TENANT_ID:?}" --federated-token "$jeton" \
  --allow-no-subscriptions --output none
echo "Connecté : $(az account show --query '[user.name, name]' -o tsv | paste -sd ' ')"
