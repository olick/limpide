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
  description = "Adresse IPv4 publique du poste, autorisée à joindre la base (migrations) : TF_VAR_poste_ipv4."
  type        = string
}

variable "image_tag" {
  description = "Version des images limpide-web et limpide-ollama (empreinte courte du commit, deploy/publier-images.sh)."
  type        = string
}

variable "registre" {
  description = "Registre des images (GitHub Container Registry, gratuit pour un dépôt public ; survit aux destroy)."
  type        = string
  default     = "ghcr.io/olick"
}

variable "coffre_permanent" {
  description = "Coffre à secrets du socle (créé par ../../../etat/creer-socle.sh, jamais détruit)."
  type        = string
  default     = "kvlimpide024b1e9f"
}
