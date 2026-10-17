output "groupe_de_ressources" {
  value = azurerm_resource_group.env.name
}

output "postgresql_hote" {
  value = module.donnees.fqdn
}

output "identite_app_nom" {
  description = "Nom du rôle PostgreSQL de l'application (créé par initialiser-base.sh)."
  value       = module.identite.name
}

output "identite_app_object_id" {
  description = "Identifiant objet (Entra ID) de l'identité de l'application : rôle PostgreSQL créé par identifiant, pas par nom."
  value       = module.identite.principal_id
}

output "identite_app_client_id" {
  value = module.identite.client_id
}

output "stockage" {
  value = module.stockage.name
}

output "url" {
  description = "Adresse publique de Limpide sur Azure."
  value       = module.app.url
}
