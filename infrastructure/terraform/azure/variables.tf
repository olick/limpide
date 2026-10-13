variable "environnement" {
  description = "Nom court de l'environnement, repris dans le nom des ressources (préproduction pour l'instant ; production en session 5)."
  type        = string
  default     = "preprod"
}

variable "location" {
  description = "Région Azure : France Centre (Paris)."
  type        = string
  default     = "francecentral"
}

variable "admin_principal_name" {
  description = "Nom de connexion (UPN) de l'administrateur Entra de PostgreSQL : la personne qui applique Terraform et les migrations. Fourni par l'environnement (TF_VAR_admin_principal_name), jamais écrit dans le dépôt."
  type        = string
}

variable "poste_ipv4" {
  description = "Adresse IPv4 publique du poste, seule autorisée à joindre la base pendant la session 3 (TF_VAR_poste_ipv4). Réseau définitif : session 4."
  type        = string
}
