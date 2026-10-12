variable "zone" {
  description = "Zone DNS gérée chez OVHcloud."
  type        = string
  default     = "limpide-ia.fr"
}

variable "vps_ipv4" {
  description = "Adresse IPv4 du VPS de la démo (ADR-006) : un seul endroit à changer si le serveur change."
  type        = string
  default     = "57.129.175.88"
}
