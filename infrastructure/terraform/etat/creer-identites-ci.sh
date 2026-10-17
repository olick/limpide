#!/usr/bin/env bash
# Identités Azure de la chaîne CI/CD (S7, session 4a), dans le socle permanent : GitHub Actions s'y connecte par
# OIDC (identité fédérée), sans aucun secret stocké dans GitHub.
# Relançable sans risque (chaque étape crée ou met à jour l'existant). Coût : nul (une identité managée est gratuite).
#
# Mécanisme : à chaque exécution, GitHub signe un jeton qui décrit le job (« sujet », par exemple
# repo:olick@5882236/limpide@1392935325:pull_request). Chaque identité n'accepte que les jetons signés par GitHub dont le sujet est
# exactement celui déclaré ici, et les échange contre un jeton Azure d'environ une heure.
#
#   id-limpide-ci-lecture     sujet pull_request          lecture de l'état (terraform plan -lock=false)
#   id-limpide-ci-production  sujet environment:production lecture et écriture de l'état (terraform apply,
#                                                          après validation manuelle dans l'environnement GitHub)
#   id-limpide-ci-evaluation  sujet environment:evaluation lecture de la sauvegarde du corpus (conteneur corpus) et
#                                                          des deux clés d'API (Mistral, OVHcloud AI Endpoints), une
#                                                          par une : rien d'autre dans le coffre (S7, session 4b)
#   id-limpide-ci-azure       sujet environment:azure      recréation des environnements Azure (S7, session 4b-2) :
#                                                          Contributeur de l'abonnement, attribution de deux rôles de
#                                                          données seulement (condition), écriture de l'état, lecture
#                                                          du corpus et des noms des secrets ; environnement GitHub
#                                                          limité à main, relecteur requis (réglé dans GitHub)
#
# GitHub ne délivre pas de jeton OIDC aux pull requests venues d'un fork : un inconnu ne peut pas lire l'état.
# Une identité par usage : on n'élargit pas les existantes.
#
# Prérequis : `az login` sur l'abonnement de Limpide, socle créé (./creer-socle.sh). Usage : ./creer-identites-ci.sh
set -euo pipefail

RESOURCE_GROUP="rg-limpide-tfstate"
CONTAINER="tfstate"
CORPUS_CONTAINER="corpus"
# Sujet au format de GitHub avec identifiants immuables (compte@id/dépôt@id, constaté au premier essai) : un dépôt
# supprimé puis recréé sous le même nom n'hérite pas de la confiance d'Azure. Identifiants :
# gh api repos/olick/limpide --jq '.owner.id, .id'
REPO="olick@5882236/limpide@1392935325"
ISSUER="https://token.actions.githubusercontent.com"
SUBSCRIPTION_ID="$(az account show --query id -o tsv)"
TENANT_ID="$(az account show --query tenantId -o tsv)"
SUFFIX="$(printf '%s\n' "$SUBSCRIPTION_ID" | sha256sum | cut -c1-8)"
ACCOUNT="stlimpidetf$SUFFIX"
LOCATION="$(az group show --name "$RESOURCE_GROUP" --query location -o tsv)"
TAGS=(projet=limpide usage=ci gere_par=script permanent=oui)
ACCOUNT_ID="$(az storage account show --name "$ACCOUNT" --resource-group "$RESOURCE_GROUP" --query id -o tsv)"
STATE_SCOPE="$ACCOUNT_ID/blobServices/default/containers/$CONTAINER"
CORPUS_SCOPE="$ACCOUNT_ID/blobServices/default/containers/$CORPUS_CONTAINER"
VAULT_ID="$(az keyvault show --name "kvlimpide$SUFFIX" --resource-group "$RESOURCE_GROUP" --query id -o tsv)"

echo "Abonnement : $(az account show --query name -o tsv)"

# identite <nom> <nom de la relation de confiance> <sujet>, puis une ou plusieurs paires <rôle> <portée>
identite() {
  local name="$1" credential="$2" subject="$3"
  shift 3
  az identity create --name "$name" --resource-group "$RESOURCE_GROUP" --location "$LOCATION" \
    --tags "${TAGS[@]}" --output none
  az identity federated-credential create --identity-name "$name" --resource-group "$RESOURCE_GROUP" \
    --name "$credential" --issuer "$ISSUER" --subject "$subject" --audiences "api://AzureADTokenExchange" \
    --output none
  local principal role scope
  principal="$(az identity show --name "$name" --resource-group "$RESOURCE_GROUP" --query principalId -o tsv)"
  while [[ $# -gt 0 ]]; do
    role="$1" scope="$2"
    shift 2
    # Filtre sur l'identifiant plutôt que --assignee : celui-ci interroge l'annuaire, qui ne connaît pas encore une
    # identité toute neuve (erreur affichée au premier passage, 2026-10-17).
    if [[ -z "$(az role assignment list --role "$role" --scope "$scope" --query "[?principalId=='$principal'].id" -o tsv)" ]]; then
      # Type indiqué : sans lui, Azure cherche l'identité dans l'annuaire, où elle peut ne pas être encore visible.
      az role assignment create --assignee-object-id "$principal" --assignee-principal-type ServicePrincipal \
        --role "$role" --scope "$scope" --output none
      echo "  $name : droit « $role » sur ${scope##*/} accordé ; il peut mettre quelques minutes à s'appliquer."
    fi
  done
  printf '  %-26s client_id = %s\n' "$name" \
    "$(az identity show --name "$name" --resource-group "$RESOURCE_GROUP" --query clientId -o tsv)"
}

identite id-limpide-ci-lecture github-pull-request "repo:$REPO:pull_request" \
  "Storage Blob Data Reader" "$STATE_SCOPE"
identite id-limpide-ci-production github-production "repo:$REPO:environment:production" \
  "Storage Blob Data Contributor" "$STATE_SCOPE"
# Droits sur chaque secret, pas sur le coffre : une clé ajoutée plus tard au coffre ne lui sera pas lisible.
identite id-limpide-ci-evaluation github-evaluation "repo:$REPO:environment:evaluation" \
  "Storage Blob Data Reader" "$CORPUS_SCOPE" \
  "Key Vault Secrets User" "$VAULT_ID/secrets/mistral-api-key" \
  "Key Vault Secrets User" "$VAULT_ID/secrets/ovh-ai-endpoints-key"

# Recréation d'Azure : Terraform crée groupes de ressources, PostgreSQL, Container Apps, identité de l'application, et
# attribue à celle-ci deux rôles de données (stockage, lecture des secrets). D'où un droit large, « Contributeur » de
# l'abonnement, et un droit d'attribuer des rôles borné par une condition (ABAC) : seulement ces deux rôles, seulement
# à des identités de service. Il ne peut pas se donner « Propriétaire », ni donner quoi que ce soit à une personne.
ROLES_DONNEES="ba92f5b4-2d11-453d-a403-e96b0029c9fe, 4633458b-17de-408a-b874-0445c86b69e6" # Storage Blob Data Contributor, Key Vault Secrets User
CONDITION="((!(ActionMatches{'Microsoft.Authorization/roleAssignments/write'})) OR (@Request[Microsoft.Authorization/roleAssignments:RoleDefinitionId] ForAnyOfAnyValues:GuidEquals {$ROLES_DONNEES} AND @Request[Microsoft.Authorization/roleAssignments:PrincipalType] ForAnyOfAnyValues:StringEqualsIgnoreCase {'ServicePrincipal'})) AND ((!(ActionMatches{'Microsoft.Authorization/roleAssignments/delete'})) OR (@Resource[Microsoft.Authorization/roleAssignments:RoleDefinitionId] ForAnyOfAnyValues:GuidEquals {$ROLES_DONNEES}))"
identite id-limpide-ci-azure github-azure "repo:$REPO:environment:azure" \
  "Contributor" "/subscriptions/$SUBSCRIPTION_ID" \
  "Storage Blob Data Contributor" "$STATE_SCOPE" \
  "Storage Blob Data Reader" "$CORPUS_SCOPE" \
  "Key Vault Reader" "$VAULT_ID"
principal="$(az identity show --name id-limpide-ci-azure --resource-group "$RESOURCE_GROUP" --query principalId -o tsv)"
if [[ -z "$(az role assignment list --role "Role Based Access Control Administrator" --scope "/subscriptions/$SUBSCRIPTION_ID" --query "[?principalId=='$principal'].id" -o tsv)" ]]; then
  az role assignment create --assignee-object-id "$principal" --assignee-principal-type ServicePrincipal \
    --role "Role Based Access Control Administrator" --scope "/subscriptions/$SUBSCRIPTION_ID" \
    --condition "$CONDITION" --condition-version "2.0" --output none
  echo "  id-limpide-ci-azure : attribution des deux rôles de données accordée (condition ABAC)."
fi

echo
echo "Identités prêtes. Identifiants à déclarer dans GitHub (ce ne sont pas des secrets) :"
echo "  tenant_id       = $TENANT_ID"
echo "  subscription_id = $SUBSCRIPTION_ID"
