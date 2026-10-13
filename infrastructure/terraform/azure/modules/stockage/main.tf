# Stockage objet des documents bruts (aujourd'hui : dossier data/ des commandes d'ingestion). Sans clés d'accès :
# l'application y écrit par son identité managée.

variable "environnement" {
  type = string
}

variable "suffixe" {
  type = string
}

variable "resource_group_name" {
  type = string
}

variable "location" {
  type = string
}

variable "tags" {
  type = map(string)
}

variable "app_principal_id" {
  description = "Identité managée de l'application, autorisée à lire et écrire."
  type        = string
}

resource "azurerm_storage_account" "docs" {
  name                            = "stlimpide${var.environnement}${var.suffixe}" # 24 caractères au plus
  resource_group_name             = var.resource_group_name
  location                        = var.location
  account_tier                    = "Standard"
  account_replication_type        = "LRS"
  min_tls_version                 = "TLS1_2"
  shared_access_key_enabled       = false
  allow_nested_items_to_be_public = false
  tags                            = var.tags
}

resource "azurerm_storage_container" "brut" {
  name               = "brut"
  storage_account_id = azurerm_storage_account.docs.id
}

resource "azurerm_role_assignment" "app_stockage" {
  scope                = azurerm_storage_account.docs.id
  role_definition_name = "Storage Blob Data Contributor"
  principal_id         = var.app_principal_id
  principal_type       = "ServicePrincipal"
}

output "name" {
  value = azurerm_storage_account.docs.name
}
