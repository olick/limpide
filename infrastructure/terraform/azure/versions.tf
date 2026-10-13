terraform {
  required_version = ">= 1.16"

  required_providers {
    azurerm = {
      source  = "hashicorp/azurerm"
      version = "5.9.0"
    }
    random = {
      source  = "hashicorp/random"
      version = "3.9.1"
    }
  }
}

# Authentification : session `az login` (poste), plus tard OIDC (CI, S7). Abonnement : ARM_SUBSCRIPTION_ID.
provider "azurerm" {
  # Comptes de stockage sans clés d'accès : le fournisseur passe par l'identité Microsoft pour les opérations sur
  # les données.
  storage_use_azuread = true

  features {
    key_vault {
      # Option B (environnement recréé à chaque séance) : un coffre supprimé est purgé tout de suite, sinon il reste
      # 7 jours en « suppression réversible » et bloque son nom.
      purge_soft_delete_on_destroy = true
    }
  }

  # azurerm 5 n'active aucun service sur l'abonnement : chaque service utilisé se déclare.
  resource_provider_registrations = "none"
  resource_providers_to_register = [
    "Microsoft.DBforPostgreSQL",
    "Microsoft.KeyVault",
    "Microsoft.ManagedIdentity",
    "Microsoft.Storage",
  ]
}
