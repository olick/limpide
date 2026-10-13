terraform {
  required_version = ">= 1.16"

  required_providers {
    azurerm = {
      source  = "hashicorp/azurerm"
      version = "5.9.0"
    }
    # Génère des valeurs (ici un suffixe de nom) et les garde dans l'état : stables d'un `apply` à l'autre.
    random = {
      source  = "hashicorp/random"
      version = "3.9.1"
    }
  }
}

# Authentification : la session `az login` du poste (aucun secret dans le code).
# Abonnement : variable d'environnement ARM_SUBSCRIPTION_ID, obligatoire depuis la version 4 du fournisseur.
provider "azurerm" {
  features {}

  # Depuis la version 5, le fournisseur n'active plus rien de lui-même sur l'abonnement : chaque service Azure
  # utilisé (« fournisseur de ressources ») se déclare. Un abonnement neuf n'a pas Microsoft.Storage.
  resource_provider_registrations = "none"
  resource_providers_to_register  = ["Microsoft.Storage"]
}
