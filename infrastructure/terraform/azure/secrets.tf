# Coffre à secrets. Terraform crée le coffre et les droits, jamais les secrets eux-mêmes : une valeur passée à
# Terraform finit en clair dans l'état. La clé Mistral y est déposée à la main (az keyvault secret set, README).
resource "azurerm_key_vault" "kv" {
  name                       = "kvlimpide${var.environnement}${random_string.suffixe.result}" # 24 caractères au plus
  resource_group_name        = azurerm_resource_group.env.name
  location                   = azurerm_resource_group.env.location
  tenant_id                  = data.azurerm_client_config.courant.tenant_id
  sku_name                   = "standard"
  rbac_authorization_enabled = true # droits Azure (RBAC), pas de politiques d'accès propres au coffre
  soft_delete_retention_days = 7
  purge_protection_enabled   = false # option B : le coffre doit pouvoir être purgé à chaque destroy
  tags                       = local.tags
}

# La personne qui applique Terraform dépose et met à jour les secrets.
resource "azurerm_role_assignment" "admin_secrets" {
  scope                = azurerm_key_vault.kv.id
  role_definition_name = "Key Vault Secrets Officer"
  principal_id         = data.azurerm_client_config.courant.object_id
}

# L'application les lit, sans pouvoir les modifier.
resource "azurerm_role_assignment" "app_secrets" {
  scope                = azurerm_key_vault.kv.id
  role_definition_name = "Key Vault Secrets User"
  principal_id         = azurerm_user_assigned_identity.app.principal_id
  principal_type       = "ServicePrincipal"
}
