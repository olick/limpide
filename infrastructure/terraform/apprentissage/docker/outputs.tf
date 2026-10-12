# Sorties : valeurs affichées après `apply` et lisibles par `terraform output` (ou par un autre code Terraform).
output "connexion" {
  description = "Chaîne de connexion, sans le mot de passe."
  value       = "Host=127.0.0.1;Port=${var.postgres_port};Database=rag;Username=rag"
}

output "conteneur" {
  value = docker_container.postgres.name
}
