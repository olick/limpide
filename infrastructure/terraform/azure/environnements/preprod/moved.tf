# Session 5 (2026-10-13) : les ressources ont changé d'adresse en passant dans des modules. Sans ces blocs, Terraform
# croirait qu'elles ont disparu et que d'autres sont apparues : il détruirait puis recréerait tout (base comprise).
# Un bloc « moved » dit : c'est la même ressource, à une nouvelle adresse. Le blob d'état est le même qu'avant
# (azure-preprod.tfstate) ; l'environnement étant détruit au moment du changement, ils n'ont rien eu à déplacer,
# mais ils protègent tout état qui contiendrait encore l'ancienne organisation.

moved {
  from = azurerm_user_assigned_identity.app
  to   = module.identite.azurerm_user_assigned_identity.app
}

moved {
  from = azurerm_postgresql_flexible_server.db
  to   = module.donnees.azurerm_postgresql_flexible_server.db
}

moved {
  from = azurerm_postgresql_flexible_server_active_directory_administrator.admin
  to   = module.donnees.azurerm_postgresql_flexible_server_active_directory_administrator.admin
}

moved {
  from = azurerm_postgresql_flexible_server_configuration.extensions
  to   = module.donnees.azurerm_postgresql_flexible_server_configuration.extensions
}

moved {
  from = azurerm_postgresql_flexible_server_database.rag
  to   = module.donnees.azurerm_postgresql_flexible_server_database.rag
}

moved {
  from = azurerm_postgresql_flexible_server_firewall_rule.services_azure
  to   = module.donnees.azurerm_postgresql_flexible_server_firewall_rule.services_azure
}

moved {
  from = azurerm_postgresql_flexible_server_firewall_rule.poste
  to   = module.donnees.azurerm_postgresql_flexible_server_firewall_rule.poste
}

moved {
  from = azurerm_storage_account.docs
  to   = module.stockage.azurerm_storage_account.docs
}

moved {
  from = azurerm_storage_container.brut
  to   = module.stockage.azurerm_storage_container.brut
}

moved {
  from = azurerm_role_assignment.app_stockage
  to   = module.stockage.azurerm_role_assignment.app_stockage
}

moved {
  from = azurerm_role_assignment.app_secrets
  to   = module.secrets.azurerm_role_assignment.app_secrets
}

moved {
  from = azurerm_log_analytics_workspace.logs
  to   = module.app.azurerm_log_analytics_workspace.logs
}

moved {
  from = azurerm_container_app_environment.env
  to   = module.app.azurerm_container_app_environment.env
}

moved {
  from = azurerm_container_app.ollama
  to   = module.app.azurerm_container_app.ollama
}

moved {
  from = azurerm_container_app.web
  to   = module.app.azurerm_container_app.web
}
