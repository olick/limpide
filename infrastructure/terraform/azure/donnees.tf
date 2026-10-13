# PostgreSQL managé (Flexible Server), sans aucun mot de passe : connexion par identité Microsoft (Entra ID) seulement.
# - la personne qui applique Terraform en est l'administrateur (migrations, création des rôles) ;
# - l'application s'y connecte avec son identité managée (rôle créé par ../azure/initialiser-base.sh).
resource "azurerm_postgresql_flexible_server" "db" {
  name                = "psql-${local.nom}-${random_string.suffixe.result}"
  resource_group_name = azurerm_resource_group.env.name
  location            = azurerm_resource_group.env.location
  version             = "17"
  sku_name            = "B_Standard_B1ms" # 1 vCore partagé, 2 Go : la plus petite taille
  storage_mb          = 32768
  zone                = "1"

  backup_retention_days         = 7
  public_network_access_enabled = true # session 3 : pare-feu limité au poste ; réseau définitif en session 4

  authentication {
    active_directory_auth_enabled = true
    password_auth_enabled         = false
    tenant_id                     = data.azurerm_client_config.courant.tenant_id
  }

  tags = local.tags
}

resource "azurerm_postgresql_flexible_server_active_directory_administrator" "admin" {
  server_name         = azurerm_postgresql_flexible_server.db.name
  resource_group_name = azurerm_resource_group.env.name
  tenant_id           = data.azurerm_client_config.courant.tenant_id
  object_id           = data.azurerm_client_config.courant.object_id
  principal_name      = var.admin_principal_name
  principal_type      = "User"
}

# pgvector : Azure n'autorise que les extensions listées ici ; la migration 0001 l'active ensuite (CREATE EXTENSION).
resource "azurerm_postgresql_flexible_server_configuration" "extensions" {
  name      = "azure.extensions"
  server_id = azurerm_postgresql_flexible_server.db.id
  value     = "VECTOR"
}

resource "azurerm_postgresql_flexible_server_database" "rag" {
  name      = "rag"
  server_id = azurerm_postgresql_flexible_server.db.id
  charset   = "UTF8"
  collation = "en_US.utf8"
}

# Session 4 (réseau, choix argumenté dans l'ADR d'infrastructure) : accès public filtré plutôt qu'un réseau privé.
# Cette règle Azure (0.0.0.0) ouvre la base aux services hébergés sur Azure, dont Container Apps, dont les adresses
# de sortie ne sont pas fixes ; elle les ouvre aussi à ceux des autres clients d'Azure. La vraie barrière est
# l'authentification : Entra ID seulement, aucun mot de passe à deviner. Réseau privé (VNet) : voie de production.
resource "azurerm_postgresql_flexible_server_firewall_rule" "services_azure" {
  name             = "services-azure"
  server_id        = azurerm_postgresql_flexible_server.db.id
  start_ip_address = "0.0.0.0"
  end_ip_address   = "0.0.0.0"
}

resource "azurerm_postgresql_flexible_server_firewall_rule" "poste" {
  name             = "poste-administrateur"
  server_id        = azurerm_postgresql_flexible_server.db.id
  start_ip_address = var.poste_ipv4
  end_ip_address   = var.poste_ipv4
}
