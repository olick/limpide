# Secrets : dans le coffre permanent du socle (../../etat/creer-socle.sh), déposés une fois, jamais par Terraform (une
# valeur passée à Terraform finit en clair dans l'état). L'environnement ne fait que donner à l'identité de
# l'application le droit de les lire.

variable "coffre_permanent" {
  type = string
}

variable "app_principal_id" {
  type = string
}

data "azurerm_key_vault" "socle" {
  name                = var.coffre_permanent
  resource_group_name = "rg-limpide-tfstate"
}

resource "azurerm_role_assignment" "app_secrets" {
  scope                = data.azurerm_key_vault.socle.id
  role_definition_name = "Key Vault Secrets User"
  principal_id         = var.app_principal_id
  principal_type       = "ServicePrincipal"
}

output "embedding_api_key_id" {
  description = "Référence de la clé OVHcloud AI Endpoints (vectorisation, ADR-012)."
  value       = "${data.azurerm_key_vault.socle.vault_uri}secrets/ovh-ai-endpoints-key"
  depends_on  = [azurerm_role_assignment.app_secrets]
}

output "mistral_api_key_id" {
  description = "Référence du secret (pas sa valeur), lue par la plateforme au démarrage de l'application."
  # Dépend du droit de lecture : l'application n'est créée qu'une fois ce droit accordé.
  value      = "${data.azurerm_key_vault.socle.vault_uri}secrets/mistral-api-key"
  depends_on = [azurerm_role_assignment.app_secrets]
}
