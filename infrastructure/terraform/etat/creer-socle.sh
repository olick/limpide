#!/usr/bin/env bash
# Socle permanent de Limpide sur Azure, créé hors de Terraform et jamais détruit (exception à l'option B).
# Relançable sans risque (chaque étape vérifie ou met à jour l'existant). Coût : quelques centimes par mois.
#   - le coffre de l'état Terraform (S6, session 2) : Terraform ne peut pas ranger son état dans une ressource qu'il
#     crée lui-même ;
#   - le coffre à secrets permanent (S6, session 4) : les secrets déposés une fois (clé Mistral) survivent aux
#     environnements recréés à chaque séance, qui les lisent par leur identité managée ; sans lui, il faudrait les
#     redéposer à la main après chaque `apply`.
#
# Prérequis : `az login` sur l'abonnement de Limpide. Usage : ./creer-socle.sh
set -euo pipefail

LOCATION="francecentral"
RESOURCE_GROUP="rg-limpide-tfstate"
CONTAINER="tfstate"
# Nom unique dans tout Azure, stable pour un abonnement : dérivé de son identifiant (sans le révéler).
SUBSCRIPTION_ID="$(az account show --query id -o tsv)"
SUFFIX="$(printf '%s\n' "$SUBSCRIPTION_ID" | sha256sum | cut -c1-8)"
ACCOUNT="stlimpidetf$SUFFIX"
VAULT="kvlimpide$SUFFIX"
TAGS=(projet=limpide usage=socle gere_par=script permanent=oui)

echo "Abonnement : $(az account show --query name -o tsv)"
echo "Compte de stockage : $ACCOUNT ($RESOURCE_GROUP, $LOCATION)"

az group create --name "$RESOURCE_GROUP" --location "$LOCATION" --tags "${TAGS[@]}" --output none

# Compte : HTTPS seul, TLS 1.2, aucun accès public, clés d'accès désactivées (accès par identité Microsoft
# seulement : il n'existe aucune clé à voler).
az storage account create \
  --name "$ACCOUNT" --resource-group "$RESOURCE_GROUP" --location "$LOCATION" \
  --sku Standard_LRS --kind StorageV2 \
  --https-only true --min-tls-version TLS1_2 \
  --allow-blob-public-access false --allow-shared-key-access false \
  --tags "${TAGS[@]}" --output none

# Historique : chaque écriture de l'état garde la version précédente ; un état supprimé ou abîmé se restaure (30 jours).
az storage account blob-service-properties update \
  --account-name "$ACCOUNT" --resource-group "$RESOURCE_GROUP" \
  --enable-versioning true \
  --enable-delete-retention true --delete-retention-days 30 \
  --enable-container-delete-retention true --container-delete-retention-days 30 \
  --output none

# Conteneur créé par l'API de gestion (ARM) : pas besoin d'accès aux données pour cette étape.
az storage container-rm create \
  --storage-account "$ACCOUNT" --resource-group "$RESOURCE_GROUP" --name "$CONTAINER" \
  --public-access off --output none

# Droit de lire et d'écrire l'état, limité à ce seul conteneur, pour la personne connectée. Être propriétaire de
# l'abonnement ne suffit pas : la lecture des données d'un compte de stockage est un droit à part.
USER_ID="$(az ad signed-in-user show --query id -o tsv)"
SCOPE="$(az storage account show --name "$ACCOUNT" --resource-group "$RESOURCE_GROUP" --query id -o tsv)/blobServices/default/containers/$CONTAINER"
if [[ -z "$(az role assignment list --assignee "$USER_ID" --role "Storage Blob Data Contributor" --scope "$SCOPE" --query '[].id' -o tsv)" ]]; then
  az role assignment create --assignee-object-id "$USER_ID" --assignee-principal-type User \
    --role "Storage Blob Data Contributor" --scope "$SCOPE" --output none
  echo "Droit accordé ; il peut mettre quelques minutes à s'appliquer."
fi

# Coffre à secrets permanent : droits Azure (RBAC), suppression réversible 90 jours et protection contre la purge
# (un secret supprimé par erreur se récupère ; personne ne peut l'effacer définitivement avant ce délai).
if ! az keyvault show --name "$VAULT" --resource-group "$RESOURCE_GROUP" --output none 2>/dev/null; then
  az keyvault create --name "$VAULT" --resource-group "$RESOURCE_GROUP" --location "$LOCATION" \
    --enable-rbac-authorization true --retention-days 90 --enable-purge-protection true \
    --tags "${TAGS[@]}" --output none
fi
VAULT_ID="$(az keyvault show --name "$VAULT" --resource-group "$RESOURCE_GROUP" --query id -o tsv)"
if [[ -z "$(az role assignment list --assignee "$USER_ID" --role "Key Vault Secrets Officer" --scope "$VAULT_ID" --query '[].id' -o tsv)" ]]; then
  az role assignment create --assignee-object-id "$USER_ID" --assignee-principal-type User \
    --role "Key Vault Secrets Officer" --scope "$VAULT_ID" --output none
  echo "Droit de dépôt des secrets accordé ; il peut mettre quelques minutes à s'appliquer."
fi

echo
echo "Socle prêt."
echo "  état Terraform  : storage_account_name = \"$ACCOUNT\", container_name = \"$CONTAINER\" (backend.tf)"
echo "  secrets         : coffre \"$VAULT\" (variable coffre_permanent de ../azure)"
