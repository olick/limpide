#!/usr/bin/env bash
# Code 0 si l'environnement Azure n'existe pas (état Terraform vide), 1 sinon (S7, session 4b-2).
# Garde de la chaîne : elle ne détruit jamais un environnement qu'elle n'a pas créé (une préproduction montée à la main
# par demo.sh, par exemple). Utilisé aussi par demo.sh --neuf.
#
# Usage, depuis infrastructure/terraform/azure : ./est-vide.sh [preprod|prod]   (preprod par défaut)
set -euo pipefail
ENV="${1:-preprod}"
[[ "$ENV" == "preprod" || "$ENV" == "prod" ]] || { echo "environnement inconnu : $ENV" >&2; exit 2; }
cd "$(dirname "$0")/environnements/$ENV"
terraform init -input=false >/dev/null
ressources="$(terraform state list | wc -l)"
if [[ "$ressources" -eq 0 ]]; then
  echo "Environnement $ENV absent (état vide)."
else
  echo "Environnement $ENV présent ($ressources ressources dans l'état) : rien n'est créé ni détruit." >&2
  exit 1
fi
