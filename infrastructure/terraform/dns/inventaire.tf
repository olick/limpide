# Inventaire de la zone telle qu'elle existe (créée à la main dans l'espace client OVH en S3).
#
# Une source de données (« data ») LIT une ressource sans la gérer : Terraform ne la crée, ne la modifie et ne la
# détruit jamais. On s'en sert ici pour connaître les identifiants des entrées avant de les importer.

data "ovh_domain_zone_records" "toutes" {
  zone_name = var.zone
}

# for_each : une lecture par identifiant (les clés d'un for_each sont des chaînes).
data "ovh_domain_zone_record" "entree" {
  for_each  = toset([for id in data.ovh_domain_zone_records.toutes.ids : tostring(id)])
  zone_name = var.zone
  id        = each.value
}

output "inventaire" {
  description = "Entrées de la zone : identifiant => « sous-domaine type cible (ttl) »."
  value = {
    for id, r in data.ovh_domain_zone_record.entree :
    id => "${r.sub_domain == "" ? "@" : r.sub_domain} ${r.field_type} ${r.target} (ttl ${r.ttl})"
  }
}
