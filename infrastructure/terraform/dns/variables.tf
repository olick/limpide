variable "zone" {
  description = "Zone DNS gérée chez OVHcloud."
  type        = string
  default     = "limpide-ia.fr"
}

variable "vps_service_name" {
  description = "Nom de service OVHcloud du VPS de la démo (ADR-006), dont les entrées DNS prennent l'adresse (vps.tf)."
  type        = string
  default     = "vps-90f0733b.vps.ovh.net"
}
