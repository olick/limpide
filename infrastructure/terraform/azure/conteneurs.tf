# Calcul : Azure Container Apps (conteneurs gérés, facturés à l'usage, mise à l'échelle jusqu'à zéro). L'ADR « Container
# Apps plutôt qu'AKS » (session 6) justifie le choix.

# Journaux de tous les conteneurs (stdout/stderr) : base de l'observabilité de la S8.
resource "azurerm_log_analytics_workspace" "logs" {
  name                = "log-${local.nom}"
  resource_group_name = azurerm_resource_group.env.name
  location            = azurerm_resource_group.env.location
  sku                 = "PerGB2018"
  retention_in_days   = 30
  tags                = local.tags
}

resource "azurerm_container_app_environment" "env" {
  name                       = "cae-${local.nom}"
  resource_group_name        = azurerm_resource_group.env.name
  location                   = azurerm_resource_group.env.location
  log_analytics_workspace_id = azurerm_log_analytics_workspace.logs.id
  tags                       = local.tags
}

# Vectorisation des questions : Ollama et bge-m3, comme sur le VPS (mêmes vecteurs, même seuil). Image avec le modèle
# inclus (deploy/ollama/Dockerfile) : rien à télécharger au démarrage. Joignable seulement depuis l'environnement.
resource "azurerm_container_app" "ollama" {
  name                         = "ca-ollama"
  container_app_environment_id = azurerm_container_app_environment.env.id
  resource_group_name          = azurerm_resource_group.env.name
  revision_mode                = "Single"
  tags                         = local.tags

  template {
    min_replicas = 1 # modèle gardé en mémoire : pas d'attente de chargement à la première question
    max_replicas = 1

    container {
      name   = "ollama"
      image  = "${var.registre}/limpide-ollama:${var.image_tag}"
      cpu    = 2
      memory = "4Gi" # bge-m3 en utilise ≈ 2,3 Go (mesure du VPS, ADR-006)

      env {
        name  = "OLLAMA_KEEP_ALIVE"
        value = "-1"
      }
    }
  }

  ingress {
    external_enabled = false # interne à l'environnement : Ollama n'a pas d'authentification
    target_port      = 11434
    transport        = "http"

    traffic_weight {
      latest_revision = true
      percentage      = 100
    }
  }
}

# L'application web (image de la S3), sans aucun secret dans sa configuration :
# - PostgreSQL : jeton Entra ID de son identité (Database:Authentication = EntraId) ;
# - clé Mistral : lue dans le coffre du socle par la plateforme, au démarrage, grâce à la même identité.
resource "azurerm_container_app" "web" {
  name                         = "ca-web"
  container_app_environment_id = azurerm_container_app_environment.env.id
  resource_group_name          = azurerm_resource_group.env.name
  revision_mode                = "Single"
  tags                         = local.tags

  identity {
    type         = "UserAssigned"
    identity_ids = [azurerm_user_assigned_identity.app.id]
  }

  secret {
    name                = "mistral-api-key"
    key_vault_secret_id = "${data.azurerm_key_vault.socle.vault_uri}secrets/mistral-api-key"
    identity            = azurerm_user_assigned_identity.app.id
  }

  template {
    min_replicas = var.web_min_replicas
    max_replicas = 2

    container {
      name   = "web"
      image  = "${var.registre}/limpide-web:${var.image_tag}"
      cpu    = 0.5
      memory = "1Gi"

      env {
        name  = "ConnectionStrings__Rag"
        value = "Host=${azurerm_postgresql_flexible_server.db.fqdn};Port=5432;Database=rag;Username=${azurerm_user_assigned_identity.app.name};SslMode=Require"
      }
      env {
        name  = "Database__Authentication"
        value = "EntraId"
      }
      env {
        name  = "Database__ManagedIdentityClientId"
        value = azurerm_user_assigned_identity.app.client_id
      }
      env {
        name  = "Embedding__Endpoint"
        value = "http://${azurerm_container_app.ollama.name}"
      }
      env {
        name        = "Mistral__ApiKey"
        secret_name = "mistral-api-key"
      }
    }
  }

  ingress {
    external_enabled = true
    target_port      = 8080
    transport        = "auto"

    traffic_weight {
      latest_revision = true
      percentage      = 100
    }
  }

  # Le droit de lecture du coffre doit exister avant que la plateforme y cherche la clé.
  depends_on = [azurerm_role_assignment.app_secrets]
}
