# Environnement Azure de Limpide (S6, option B : vitrine recréée à la demande, `destroy` en fin de séance).
# Organisation par rôle (portabilité, plan global) : identite.tf, donnees.tf, stockage.tf, secrets.tf ; modules en
# session 5.

data "azurerm_client_config" "courant" {}

locals {
  nom = "limpide-${var.environnement}"
  tags = {
    projet        = "limpide"
    environnement = var.environnement
    gere_par      = "terraform"
  }
}

resource "azurerm_resource_group" "env" {
  name     = "rg-${local.nom}"
  location = var.location
  tags     = local.tags
}

# Suffixe des noms qui doivent être uniques dans tout Azure (stockage, coffre, serveur PostgreSQL). Recréé à chaque
# `destroy` / `apply` : aucun conflit avec un coffre en cours de purge.
resource "random_string" "suffixe" {
  length  = 6
  special = false
  upper   = false
}
