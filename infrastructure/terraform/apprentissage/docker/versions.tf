# Version de Terraform et des fournisseurs (providers) : épinglées, pour que le même code donne le même résultat
# sur chaque poste. `terraform init` télécharge le fournisseur et note son empreinte dans .terraform.lock.hcl
# (fichier à commiter, comme un packages.lock.json).
terraform {
  required_version = ">= 1.16"

  required_providers {
    docker = {
      source  = "kreuzwerker/docker"
      version = "4.6.0"
    }
  }
}

# Le fournisseur docker parle au démon Docker local (socket unix:///var/run/docker.sock), comme la commande docker.
provider "docker" {}
