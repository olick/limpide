# Stockage objet des documents bruts (aujourd'hui : dossier data/ des commandes d'ingestion). Sans clés d'accès :
# l'application y écrit par son identité managée.
resource "azurerm_storage_account" "docs" {
  name                            = "stlimpide${var.environnement}${random_string.suffixe.result}"
  resource_group_name             = azurerm_resource_group.env.name
  location                        = azurerm_resource_group.env.location
  account_tier                    = "Standard"
  account_replication_type        = "LRS"
  min_tls_version                 = "TLS1_2"
  shared_access_key_enabled       = false
  allow_nested_items_to_be_public = false
  tags                            = local.tags
}

resource "azurerm_storage_container" "brut" {
  name               = "brut"
  storage_account_id = azurerm_storage_account.docs.id
}

resource "azurerm_role_assignment" "app_stockage" {
  scope                = azurerm_storage_account.docs.id
  role_definition_name = "Storage Blob Data Contributor"
  principal_id         = azurerm_user_assigned_identity.app.principal_id
  principal_type       = "ServicePrincipal"
}
