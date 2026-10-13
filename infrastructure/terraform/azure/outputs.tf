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

output "url" {
  description = "Adresse publique de Limpide sur Azure."
  value       = "https://${azurerm_container_app.web.ingress[0].fqdn}"
}

output "stockage" {
  value = azurerm_storage_account.docs.name
}
