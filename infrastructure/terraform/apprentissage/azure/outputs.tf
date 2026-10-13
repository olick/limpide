output "groupe_de_ressources" {
  value = azurerm_resource_group.apprentissage.name
}

output "compte_de_stockage" {
  value = azurerm_storage_account.apprentissage.name
}

# Les clés d'accès du compte existent aussi dans l'état (attribut sensible) : même constat que le mot de passe de
# l'exercice Docker. En S6, l'application y accédera par identité managée, sans clé.
