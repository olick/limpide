#!/usr/bin/env bash
# Identités Azure de la chaîne CI/CD (S7, session 4a), dans le socle permanent : GitHub Actions s'y connecte par
# OIDC (identité fédérée), sans aucun secret stocké dans GitHub.
# Relançable sans risque (chaque étape crée ou met à jour l'existant). Coût : nul (une identité managée est gratuite).
#
# Mécanisme : à chaque exécution, GitHub signe un jeton qui décrit le job (« sujet », par exemple
# repo:olick/limpide:pull_request). Chaque identité n'accepte que les jetons signés par GitHub dont le sujet est
# exactement celui déclaré ici, et les échange contre un jeton Azure d'environ une heure.
#
#   id-limpide-ci-lecture     sujet pull_request          lecture de l'état (terraform plan -lock=false)
#   id-limpide-ci-production  sujet environment:production lecture et écriture de l'état (terraform apply,
#                                                          après validation manuelle dans l'environnement GitHub)
#
# GitHub ne délivre pas de jeton OIDC aux pull requests venues d'un fork : un inconnu ne peut pas lire l'état.
# La préproduction éphémère (session 4b) aura sa propre identité, aux droits plus larges : on n'élargit pas celles-ci.
#
# Prérequis : `az login` sur l'abonnement de Limpide, socle créé (./creer-socle.sh). Usage : ./creer-identites-ci.sh
set -euo pipefail

RESOURCE_GROUP="rg-limpide-tfstate"
CONTAINER="tfstate"
REPO="olick/limpide"
ISSUER="https://token.actions.githubusercontent.com"
SUBSCRIPTION_ID="$(az account show --query id -o tsv)"
TENANT_ID="$(az account show --query tenantId -o tsv)"
SUFFIX="$(printf '%s\n' "$SUBSCRIPTION_ID" | sha256sum | cut -c1-8)"
ACCOUNT="stlimpidetf$SUFFIX"
LOCATION="$(az group show --name "$RESOURCE_GROUP" --query location -o tsv)"
TAGS=(projet=limpide usage=ci gere_par=script permanent=oui)
SCOPE="$(az storage account show --name "$ACCOUNT" --resource-group "$RESOURCE_GROUP" --query id -o tsv)/blobServices/default/containers/$CONTAINER"

echo "Abonnement : $(az account show --query name -o tsv)"

# identite <nom> <nom de la relation de confiance> <sujet> <rôle sur le conteneur de l'état>
identite() {
  local name="$1" credential="$2" subject="$3" role="$4"
  az identity create --name "$name" --resource-group "$RESOURCE_GROUP" --location "$LOCATION" \
    --tags "${TAGS[@]}" --output none
  az identity federated-credential create --identity-name "$name" --resource-group "$RESOURCE_GROUP" \
    --name "$credential" --issuer "$ISSUER" --subject "$subject" --audiences "api://AzureADTokenExchange" \
    --output none
  local principal
  principal="$(az identity show --name "$name" --resource-group "$RESOURCE_GROUP" --query principalId -o tsv)"
  if [[ -z "$(az role assignment list --assignee "$principal" --role "$role" --scope "$SCOPE" --query '[].id' -o tsv)" ]]; then
    # Type indiqué : sans lui, Azure cherche l'identité dans l'annuaire, où elle peut ne pas être encore visible.
    az role assignment create --assignee-object-id "$principal" --assignee-principal-type ServicePrincipal \
      --role "$role" --scope "$SCOPE" --output none
    echo "  $name : droit « $role » accordé ; il peut mettre quelques minutes à s'appliquer."
  fi
  printf '  %-26s client_id = %s\n' "$name" \
    "$(az identity show --name "$name" --resource-group "$RESOURCE_GROUP" --query clientId -o tsv)"
}

identite id-limpide-ci-lecture github-pull-request "repo:$REPO:pull_request" "Storage Blob Data Reader"
identite id-limpide-ci-production github-production "repo:$REPO:environment:production" "Storage Blob Data Contributor"

echo
echo "Identités prêtes. Identifiants à déclarer dans GitHub (ce ne sont pas des secrets) :"
echo "  tenant_id       = $TENANT_ID"
echo "  subscription_id = $SUBSCRIPTION_ID"
