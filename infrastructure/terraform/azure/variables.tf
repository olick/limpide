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

variable "coffre_permanent" {
  description = "Coffre à secrets du socle (créé par ../etat/creer-socle.sh, jamais détruit)."
  type        = string
  default     = "kvlimpide024b1e9f"
}

variable "registre" {
  description = "Registre des images (GitHub Container Registry, gratuit pour un dépôt public ; survit aux destroy)."
  type        = string
  default     = "ghcr.io/olick"
}

variable "image_tag" {
  description = "Version des images limpide-web et limpide-ollama à déployer (empreinte courte du commit). La publication des images relève de la CI (S7) ; Terraform ne fait que les référencer."
  type        = string
}

variable "web_min_replicas" {
  description = "Instances minimales de l'application web : 1 pendant une séance (pas de démarrage à froid), 0 pour ne payer qu'à l'usage."
  type        = number
  default     = 1
}
