# Identité managée de l'application : une identité Azure attribuée à l'application elle-même, sans mot de passe ni
# clé. Créée avant l'application (session 4) pour recevoir ses droits dès maintenant : lire les secrets du coffre,
# écrire dans le stockage, se connecter à PostgreSQL.
resource "azurerm_user_assigned_identity" "app" {
  name                = "id-${local.nom}"
  resource_group_name = azurerm_resource_group.env.name
  location            = azurerm_resource_group.env.location
  tags                = local.tags
}
