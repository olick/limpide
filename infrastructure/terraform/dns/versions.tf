terraform {
  required_version = ">= 1.16"

  required_providers {
    ovh = {
      source  = "ovh/ovh"
      version = "2.22.0"
    }
  }
}

# Identifiants de l'API OVHcloud : jamais ici. Le fournisseur les lit dans ~/.ovh.conf (hors du dépôt, chmod 600),
# ou dans les variables d'environnement OVH_APPLICATION_KEY, OVH_APPLICATION_SECRET, OVH_CONSUMER_KEY.
provider "ovh" {
  endpoint = "ovh-eu"
}
