# Calcul : Azure Container Apps (conteneurs gérés, facturés à l'usage). L'ADR « Container Apps plutôt qu'AKS »
# (session 6) justifie le choix.

variable "nom" {
  type = string
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

variable "registre" {
  type = string
}

variable "image_tag" {
  type = string
}

variable "identite" {
  description = "Identité managée de l'application (module identite)."
  type = object({
    id        = string
    client_id = string
    name      = string
  })
}

variable "postgresql_fqdn" {
  type = string
}

variable "mistral_api_key_id" {
  description = "Référence du secret dans le coffre (module secrets)."
  type        = string
}

variable "embedding_api_key_id" {
  description = "Référence de la clé de l'API d'embeddings dans le coffre (module secrets)."
  type        = string
}

variable "embedding_endpoint" {
  description = "API d'embeddings compatible OpenAI : bge-m3 chez OVHcloud AI Endpoints (ADR-012)."
  type        = string
  default     = "https://oai.endpoints.kepler.ai.cloud.ovh.net/v1"
}

variable "web_min_replicas" {
  description = "Instances minimales de l'application : 1 pendant une séance (pas de démarrage à froid), 0 pour ne payer qu'à l'usage."
  type        = number
}

variable "web_max_replicas" {
  type = number
}

variable "web_cpu" {
  type = number
}

variable "web_memory" {
  type = string
}

# Journaux de tous les conteneurs (stdout/stderr) : base de l'observabilité de la S8.
resource "azurerm_log_analytics_workspace" "logs" {
  name                = "log-${var.nom}"
  resource_group_name = var.resource_group_name
  location            = var.location
  sku                 = "PerGB2018"
  retention_in_days   = 30
  tags                = var.tags
}

resource "azurerm_container_app_environment" "env" {
  name                = "cae-${var.nom}"
  resource_group_name = var.resource_group_name
  location            = var.location
  # azurerm 5 : sans destination explicite, les journaux sont seulement diffusés en direct, pas conservés.
  logs_destination           = "log-analytics"
  log_analytics_workspace_id = azurerm_log_analytics_workspace.logs.id
  tags                       = var.tags
}

# L'application web (image de la S3), sans aucun secret dans sa configuration :
# - PostgreSQL : jeton Entra ID de son identité (Database:Authentication = EntraId) ;
# - clés Mistral et OVHcloud : lues dans le coffre du socle par la plateforme, au démarrage, grâce à la même identité ;
# - vectorisation des questions : bge-m3 par l'API d'OVHcloud (ADR-012 ; plus de conteneur Ollama, ≈ 80 % du coût).
resource "azurerm_container_app" "web" {
  name                         = "ca-web"
  container_app_environment_id = azurerm_container_app_environment.env.id
  resource_group_name          = var.resource_group_name
  revision_mode                = "Single"
  tags                         = var.tags

  identity {
    type         = "UserAssigned"
    identity_ids = [var.identite.id]
  }

  secret {
    name                = "mistral-api-key"
    key_vault_secret_id = var.mistral_api_key_id
    identity            = var.identite.id
  }

  secret {
    name                = "embedding-api-key"
    key_vault_secret_id = var.embedding_api_key_id
    identity            = var.identite.id
  }

  template {
    min_replicas = var.web_min_replicas
    max_replicas = var.web_max_replicas

    container {
      name   = "web"
      image  = "${var.registre}/limpide-web:${var.image_tag}"
      cpu    = var.web_cpu
      memory = var.web_memory

      env {
        name  = "ConnectionStrings__Rag"
        value = "Host=${var.postgresql_fqdn};Port=5432;Database=rag;Username=${var.identite.name};SslMode=Require"
      }
      env {
        name  = "Database__Authentication"
        value = "EntraId"
      }
      env {
        name  = "Database__ManagedIdentityClientId"
        value = var.identite.client_id
      }
      env {
        name  = "Embedding__Provider"
        value = "OpenAI"
      }
      env {
        name  = "Embedding__Endpoint"
        value = var.embedding_endpoint
      }
      env {
        name        = "Embedding__ApiKey"
        secret_name = "embedding-api-key"
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
}

output "url" {
  value = "https://${azurerm_container_app.web.ingress[0].fqdn}"
}
