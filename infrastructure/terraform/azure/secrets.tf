# Secrets : dans le coffre permanent du socle (../etat/creer-socle.sh), déposés une fois, jamais par Terraform (une
# valeur passée à Terraform finit en clair dans l'état). L'environnement, recréé à chaque séance, ne fait que donner à
# l'identité de l'application le droit de les lire.
#
# Session 3 créait un coffre par environnement : la clé Mistral était alors à redéposer à la main après chaque
# `apply`, une intervention manuelle contraire au critère de la S6 ; remplacé en session 4.
data "azurerm_key_vault" "socle" {
  name                = var.coffre_permanent
  resource_group_name = "rg-limpide-tfstate"
}

resource "azurerm_role_assignment" "app_secrets" {
  scope                = data.azurerm_key_vault.socle.id
  role_definition_name = "Key Vault Secrets User"
  principal_id         = azurerm_user_assigned_identity.app.principal_id
  principal_type       = "ServicePrincipal"
}
