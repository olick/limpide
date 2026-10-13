# État distant (S6, session 2) : blob « dns.tfstate » du coffre créé par ../etat/creer-socle.sh.
# Accès par l'identité Microsoft (session `az login`, plus tard OIDC dans la CI) : aucune clé. Verrou automatique
# pendant chaque opération (bail sur le blob) : deux `apply` simultanés sont refusés.
# Ces noms ne sont pas des secrets : sans droit sur le conteneur, le compte est inaccessible.
terraform {
  backend "azurerm" {
    storage_account_name = "stlimpidetf024b1e9f"
    container_name       = "tfstate"
    key                  = "dns.tfstate"
    use_azuread_auth     = true
  }
}
