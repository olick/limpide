# Exercice d'apprentissage : une base PostgreSQL + pgvector, comme le service postgres de docker-compose.yml, mais
# décrite en Terraform et sous d'autres noms (préfixe tf-apprentissage) : la pile de développement n'est pas touchée.
#
# Différence de fond avec Docker Compose : Compose lance des conteneurs ; Terraform décrit des ressources, mémorise
# dans son état ce qu'il a créé, et calcule à chaque `plan` l'écart entre le code, l'état et la réalité.

# Image : téléchargée si absente. keep_locally = true : `terraform destroy` ne la supprime pas (elle sert aussi à la
# pile de développement).
resource "docker_image" "pgvector" {
  name         = "pgvector/pgvector:pg17"
  keep_locally = true
}

resource "docker_network" "apprentissage" {
  name = "tf-apprentissage"
}

# Volume des données : détruit par `terraform destroy`, comme toute ressource gérée par Terraform.
resource "docker_volume" "pgdata" {
  name = "tf-apprentissage-pgdata"  
}

# Les références (docker_image.pgvector.image_id, docker_volume.pgdata.name...) créent les dépendances : Terraform en
# déduit l'ordre de création (image, réseau et volume d'abord, conteneur ensuite), sans depends_on.
resource "docker_container" "postgres" {
  name    = "tf-apprentissage-postgres"
  image   = docker_image.pgvector.image_id
  restart = "unless-stopped"

  env = [
    "POSTGRES_USER=rag",
    "POSTGRES_PASSWORD=${var.postgres_password}",
    "POSTGRES_DB=rag",
  ]

  # Toujours sur 127.0.0.1 : Docker contourne le pare-feu (règle du projet).
  ports {
    internal = 5432
    external = var.postgres_port
    ip       = "127.0.0.1"
  }

  volumes {
    volume_name    = docker_volume.pgdata.name
    container_path = "/var/lib/postgresql/data"
  }

  networks_advanced {
    name = docker_network.apprentissage.name
  }

  healthcheck {
    test     = ["CMD-SHELL", "pg_isready -U rag -d rag"]
    interval = "5s"
    timeout  = "5s"
    retries  = 10
  }

  # `apply` ne rend la main qu'une fois la base prête (équivalent de `docker compose up --wait`).
  wait         = true
  wait_timeout = 60
}
