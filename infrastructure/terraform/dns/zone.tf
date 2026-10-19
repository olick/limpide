# Entrées DNS de Limpide, gérées par Terraform. Les entrées posées d'office par OVH (NS, MX, SPF, ftp, TXT de la page
# d'accueil) ne sont pas gérées ici : une erreur dans ce code ne peut pas casser la délégation ni le courrier.
#
# Entrées créées à la main en S3 et S5, reprises par import : les blocs « import » font entrer chaque entrée existante
# dans l'état au prochain `apply` (identifiant OVH « <id>.<zone> », lu dans l'inventaire). Une fois importés, ces blocs
# ne servent plus à rien ; on les laisse comme trace de l'origine de chaque entrée.

# Domaine nu : Caddy le redirige (301) vers www (REDIRECT_FROM, 2026-10-10).
import {
  to = ovh_domain_zone_record.apex
  id = "5440339129.${var.zone}"
}

resource "ovh_domain_zone_record" "apex" {
  zone      = var.zone
  subdomain = ""
  fieldtype = "A"
  target    = local.vps_ipv4
}

# La démo publique (S3, 2026-10-07).
import {
  to = ovh_domain_zone_record.www
  id = "5440027580.${var.zone}"
}

resource "ovh_domain_zone_record" "www" {
  zone      = var.zone
  subdomain = "www"
  fieldtype = "A"
  target    = local.vps_ipv4
}

# Le back office (S5, 2026-10-12).
import {
  to = ovh_domain_zone_record.admin
  id = "5441276966.${var.zone}"
}

resource "ovh_domain_zone_record" "admin" {
  zone      = var.zone
  subdomain = "admin"
  fieldtype = "A"
  target    = local.vps_ipv4
}

# Grafana (S8, ADR-018) : sous-domaine à part du back office (isolation par le navigateur), servi par Caddy sur le VPS.
resource "ovh_domain_zone_record" "grafana" {
  zone      = var.zone
  subdomain = "grafana"
  fieldtype = "A"
  target    = local.vps_ipv4
}

# Supprimée par Terraform le 2026-10-12 : limpide-ia.fr.limpide-ia.fr (A), erreur de saisie de la S5 (sous-domaine
# « limpide-ia.fr » au lieu de vide). Importée, puis son bloc retiré : le `plan` suivant a annoncé sa destruction.
