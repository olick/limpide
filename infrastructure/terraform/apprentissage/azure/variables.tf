variable "location" {
  description = "Région Azure : France Centre (Paris), données en France."
  type        = string
  default     = "francecentral"
}

variable "resource_group_name" {
  description = "Groupe de ressources : le dossier qui contient tout l'exercice."
  type        = string
  default     = "rg-limpide-apprentissage"
}
