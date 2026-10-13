# Exercice d'apprentissage Azure : un groupe de ressources et un compte de stockage vide. Coût : quelques centimes au
# plus (un compte de stockage vide ne coûte presque rien) ; `terraform destroy` en fin de session (option B).

locals {
  # Étiquettes posées sur chaque ressource : retrouver dans le portail (et sur la facture) ce que Terraform a créé.
  tags = {
    projet   = "limpide"
    usage    = "apprentissage"
    gere_par = "terraform"
  }
}

# Groupe de ressources : le dossier Azure. Le détruire détruit tout ce qu'il contient.
resource "azurerm_resource_group" "apprentissage" {
  name     = var.resource_group_name
  location = var.location
  tags     = local.tags
}

# Le nom d'un compte de stockage est unique dans tout Azure (il devient une adresse web) : 3 à 24 caractères,
# minuscules et chiffres. Suffixe aléatoire tiré une fois, puis conservé dans l'état.
resource "random_string" "suffixe" {
  length  = 8
  special = false
  upper   = false
}

resource "azurerm_storage_account" "apprentissage" {
  name                     = "stlimpide${random_string.suffixe.result}"
  resource_group_name      = azurerm_resource_group.apprentissage.name
  location                 = azurerm_resource_group.apprentissage.location
  account_tier             = "Standard"
  account_replication_type = "LRS" # 3 copies dans un seul centre de données : le moins cher
  min_tls_version          = "TLS1_2"
  tags                     = local.tags
}
