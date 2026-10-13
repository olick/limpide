output "groupe_de_ressources" {
  value = azurerm_resource_group.env.name
}

output "postgresql_hote" {
  value = azurerm_postgresql_flexible_server.db.fqdn
}

output "identite_app_nom" {
  description = "Nom du rôle PostgreSQL de l'application (créé par initialiser-base.sh)."
  value       = azurerm_user_assigned_identity.app.name
}

output "identite_app_client_id" {
  value = azurerm_user_assigned_identity.app.client_id
}

output "coffre" {
  value = azurerm_key_vault.kv.name
}

output "stockage" {
  value = azurerm_storage_account.docs.name
}
