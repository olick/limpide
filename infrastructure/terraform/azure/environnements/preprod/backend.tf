# État distant (coffre du socle, ../../../etat) ; un blob par environnement.
terraform {
  backend "azurerm" {
    storage_account_name = "stlimpidetf024b1e9f"
    container_name       = "tfstate"
    key                  = "azure-preprod.tfstate"
    use_azuread_auth     = true
  }
}
