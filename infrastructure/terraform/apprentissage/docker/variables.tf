# Paramètres d'entrée. Une variable sans valeur par défaut est demandée à chaque commande, ou lue dans
# l'environnement : TF_VAR_postgres_password.
variable "postgres_password" {
  description = "Mot de passe PostgreSQL de la base d'apprentissage."
  type        = string
  # Masqué dans l'affichage du plan... mais écrit en clair dans l'état (terraform.tfstate) : à vérifier en exercice.
  sensitive = true
}

variable "postgres_port" {
  description = "Port publié sur 127.0.0.1 (différent de celui de la pile de développement, 5432)."
  type        = number
  default     = 5442
}
