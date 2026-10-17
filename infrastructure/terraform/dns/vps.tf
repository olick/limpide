# Le VPS de la démo (ADR-006), LU par Terraform et jamais géré (S7, session 4a). La ressource `ovh_vps` existe, mais
# elle sert à commander un serveur : son `destroy` le résilie, et changer son `image_id` le réinstalle (disque effacé,
# ce que `prevent_destroy` n'empêche pas). Pour ne modifier presque rien (nom affiché, mode de démarrage), le risque
# n'en vaut pas la peine : le contenu du VPS (Compose, Caddy, application) relève du déploiement, pas de Terraform.
#
# Ce qu'apporte la lecture : les entrées DNS prennent l'adresse du serveur lui-même, au lieu d'une valeur recopiée à
# la main ; si le serveur change d'adresse, le `plan` le montre.

data "ovh_vps" "demo" {
  service_name = var.vps_service_name

  # Le fournisseur (2.22.0) avale un refus de l'API (403 NOT_GRANTED_CALL) et renvoie des attributs vides au lieu
  # d'une erreur (constaté le 2026-10-16) : sans cette vérification, un droit manquant passerait inaperçu.
  lifecycle {
    postcondition {
      condition     = self.ips != null && length(self.ips) > 0
      error_message = "VPS ${var.vps_service_name} lu sans adresse : droits de lecture manquants (GET /vps/${var.vps_service_name} et /vps/${var.vps_service_name}/*) ?"
    }
  }
}

locals {
  # `one` échoue si le VPS a zéro ou plusieurs adresses IPv4 : mieux vaut un `plan` en erreur qu'une entrée DNS
  # pointée au hasard.
  vps_ipv4 = one([for ip in data.ovh_vps.demo.ips : ip if !strcontains(ip, ":")])
}

output "vps_ipv4" {
  description = "Adresse IPv4 du VPS, lue chez OVHcloud, cible des entrées @, www et admin."
  value       = local.vps_ipv4
}
