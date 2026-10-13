# Identité managée de l'application : une identité Azure attribuée à l'application elle-même, sans mot de passe ni
# clé. Créée avant l'application pour recevoir ses droits : lire les secrets, écrire dans le stockage, se connecter
# à PostgreSQL (rôle créé par initialiser-base.sh).

variable "nom" {
  description = "Nom court de l'environnement (limpide-preprod...)."
  type        = string
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

resource "azurerm_user_assigned_identity" "app" {
  name                = "id-${var.nom}"
  resource_group_name = var.resource_group_name
  location            = var.location
  tags                = var.tags
}

output "id" {
  value = azurerm_user_assigned_identity.app.id
}

output "principal_id" {
  value = azurerm_user_assigned_identity.app.principal_id
}

output "client_id" {
  value = azurerm_user_assigned_identity.app.client_id
}

output "name" {
  description = "Nom du rôle PostgreSQL de l'application."
  value       = azurerm_user_assigned_identity.app.name
}
