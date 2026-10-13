# Préproduction : la plus petite taille partout ; c'est elle que la CI de la S7 évaluera.
# Mêmes modules que l'autre environnement (../../modules) ; seules les tailles et le budget changent.

locals {
  environnement = "preprod"
  nom           = "limpide-${local.environnement}"
  tags = {
    projet        = "limpide"
    environnement = local.environnement
    gere_par      = "terraform"
  }
}

data "azurerm_client_config" "courant" {}

resource "azurerm_resource_group" "env" {
  name     = "rg-${local.nom}"
  location = var.location
  tags     = local.tags
}

# Suffixe des noms uniques dans tout Azure (serveur PostgreSQL, stockage) ; recréé à chaque destroy / apply.
resource "random_string" "suffixe" {
  length  = 6
  special = false
  upper   = false
}

module "identite" {
  source              = "../../modules/identite"
  nom                 = local.nom
  resource_group_name = azurerm_resource_group.env.name
  location            = azurerm_resource_group.env.location
  tags                = local.tags
}

module "donnees" {
  source                = "../../modules/donnees"
  nom                   = local.nom
  suffixe               = random_string.suffixe.result
  resource_group_name   = azurerm_resource_group.env.name
  location              = azurerm_resource_group.env.location
  tags                  = local.tags
  sku_name              = "B_Standard_B1ms"
  backup_retention_days = 7
  tenant_id             = data.azurerm_client_config.courant.tenant_id
  admin_object_id       = data.azurerm_client_config.courant.object_id
  admin_principal_name  = var.admin_principal_name
  poste_ipv4            = var.poste_ipv4
}

module "stockage" {
  source              = "../../modules/stockage"
  environnement       = local.environnement
  suffixe             = random_string.suffixe.result
  resource_group_name = azurerm_resource_group.env.name
  location            = azurerm_resource_group.env.location
  tags                = local.tags
  app_principal_id    = module.identite.principal_id
}

module "secrets" {
  source           = "../../modules/secrets"
  coffre_permanent = var.coffre_permanent
  app_principal_id = module.identite.principal_id
}

module "app" {
  source              = "../../modules/app"
  nom                 = local.nom
  resource_group_name = azurerm_resource_group.env.name
  location            = azurerm_resource_group.env.location
  tags                = local.tags
  registre            = var.registre
  image_tag           = var.image_tag
  identite = {
    id        = module.identite.id
    client_id = module.identite.client_id
    name      = module.identite.name
  }
  postgresql_fqdn      = module.donnees.fqdn
  mistral_api_key_id   = module.secrets.mistral_api_key_id
  embedding_api_key_id = module.secrets.embedding_api_key_id
  web_min_replicas     = 1
  web_max_replicas     = 1
  web_cpu              = 0.5
  web_memory           = "1Gi"
}

module "budget" {
  source            = "../../modules/budget"
  nom               = local.nom
  resource_group_id = azurerm_resource_group.env.id
  montant           = 10
}
