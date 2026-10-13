#!/usr/bin/env bash
# Coffre de l'état Terraform sur Azure (S6, session 2). Créé hors de Terraform : Terraform ne peut pas ranger son état
# dans une ressource qu'il crée lui-même. Relançable sans risque (chaque étape vérifie ou met à jour l'existant).
#
# Seule ressource Azure permanente du projet (exception à l'option B) : la détruire perdrait l'état. Coût : quelques
# centimes par mois.
#
# Prérequis : `az login` sur l'abonnement de Limpide. Usage : ./creer-coffre-etat.sh
set -euo pipefail

LOCATION="francecentral"
RESOURCE_GROUP="rg-limpide-tfstate"
CONTAINER="tfstate"
# Nom unique dans tout Azure, stable pour un abonnement : dérivé de son identifiant (sans le révéler).
SUBSCRIPTION_ID="$(az account show --query id -o tsv)"
ACCOUNT="stlimpidetf$(printf '%s\n' "$SUBSCRIPTION_ID" | sha256sum | cut -c1-8)"
TAGS=(projet=limpide usage=etat-terraform gere_par=script permanent=oui)

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

echo
echo "Coffre prêt. Configuration du backend (backend.tf de chaque dossier Terraform) :"
echo "  storage_account_name = \"$ACCOUNT\""
echo "  container_name       = \"$CONTAINER\""
