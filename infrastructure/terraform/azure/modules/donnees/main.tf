# PostgreSQL managé (Flexible Server), sans aucun mot de passe : connexion par identité Microsoft (Entra ID) seulement.
# - la personne qui applique Terraform en est l'administrateur (migrations, création des rôles) ;
# - l'application s'y connecte avec son identité managée (rôle créé par initialiser-base.sh).

variable "nom" {
  type = string
}

variable "suffixe" {
  description = "Suffixe aléatoire : le nom du serveur est unique dans tout Azure."
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

variable "sku_name" {
  description = "Taille du serveur (B_Standard_B1ms : 1 vCore partagé, 2 Go, la plus petite)."
  type        = string
}

variable "storage_mb" {
  type    = number
  default = 32768
}

variable "backup_retention_days" {
  type    = number
  default = 7
}

variable "tenant_id" {
  type = string
}

variable "admin_object_id" {
  description = "Identifiant Entra ID de l'administrateur (la personne qui applique Terraform)."
  type        = string
}

variable "admin_principal_name" {
  type = string
}

variable "poste_ipv4" {
  description = "Seule adresse extérieure à Azure autorisée : le poste de l'administrateur (migrations)."
  type        = string
}

resource "azurerm_postgresql_flexible_server" "db" {
  name                = "psql-${var.nom}-${var.suffixe}"
  resource_group_name = var.resource_group_name
  location            = var.location
  version             = "17"
  sku_name            = var.sku_name
  storage_mb          = var.storage_mb
  zone                = "1"

  backup_retention_days         = var.backup_retention_days
  public_network_access_enabled = true # accès public filtré (pare-feu ci-dessous) ; réseau privé : voie de production

  authentication {
    active_directory_auth_enabled = true
    password_auth_enabled         = false
    tenant_id                     = var.tenant_id
  }

  tags = var.tags
}

resource "azurerm_postgresql_flexible_server_active_directory_administrator" "admin" {
  server_name         = azurerm_postgresql_flexible_server.db.name
  resource_group_name = var.resource_group_name
  tenant_id           = var.tenant_id
  object_id           = var.admin_object_id
  principal_name      = var.admin_principal_name
  principal_type      = "User"

  # Azure tronque le nom à 63 caractères (limite des noms de rôle PostgreSQL) ; l'UPN d'un compte Microsoft
  # personnel invité est plus long. Sans cette ligne, chaque `plan` voit un écart et veut remplacer l'administrateur,
  # donc le supprimer (DROP ROLE refusé : il possède les tables). Constaté au deuxième `apply`, 2026-10-13.
  lifecycle {
    ignore_changes = [principal_name]
  }
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

# Accès public filtré plutôt qu'un réseau privé (choix de la session 4, ADR d'infrastructure). Cette règle Azure
# (0.0.0.0) ouvre la base aux services hébergés sur Azure, dont Container Apps, dont les adresses de sortie ne sont pas
# fixes ; elle les ouvre aussi à ceux des autres clients d'Azure. La vraie barrière est l'authentification : Entra ID
# seulement, aucun mot de passe à deviner.
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

output "fqdn" {
  value = azurerm_postgresql_flexible_server.db.fqdn
}
