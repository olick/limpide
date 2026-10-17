#!/usr/bin/env bash
# Déploie sur le VPS (ADR-006) une version de Limpide publiée sur ghcr.io par la chaîne CI/CD (S7, session 4a) :
# rien n'est construit ici, l'image en production est celle que la chaîne a construite.
#
#   deploy/deployer.sh <version>     version = étiquette des images (deploy/version-images.sh)
#
# Appelé par la chaîne (.github/workflows/deploiement.yml) en SSH, avec une clé dédiée dont la ligne de
# ~/.ssh/authorized_keys impose ce script (restrict,command="…") : la clé ne permet rien d'autre. L'argument arrive
# alors dans SSH_ORIGINAL_COMMAND ; il n'est jamais exécuté, seulement comparé à un motif hexadécimal.
#
# Étapes : dépôt à jour (fichier Compose, Caddyfile, ce script), images tirées, migrations AVANT l'application, web et
# admin redémarrés (coupure de quelques secondes), image d'ingestion ré-étiquetée pour le DAG Airflow
# (`limpide-ingestion`), vérification du site.
# Retour arrière : relancer avec la version précédente, affichée au début (lue dans .env.version).
set -euo pipefail

# Tout le script est dans une fonction : bash la lit en entier avant de l'exécuter, si bien que le `git merge`
# qui met ce fichier à jour pendant l'exécution ne peut pas mélanger l'ancienne et la nouvelle version.
main() {
  local version="${1:-}"
  if [[ -z "$version" && -n "${SSH_ORIGINAL_COMMAND:-}" ]]; then
    version="${SSH_ORIGINAL_COMMAND##* }"   # « <version> » ou « deploy/deployer.sh <version> »
  fi
  if [[ ! "$version" =~ ^[0-9a-f]{12,40}$ ]]; then
    echo "Usage : deploy/deployer.sh <version> (12 à 40 caractères hexadécimaux, deploy/version-images.sh)" >&2
    exit 2
  fi

  cd "$(dirname "$(readlink -f "$0")")/.."
  touch .env.version
  local precedente
  precedente="$(sed -n 's/^IMAGE_TAG=//p' .env.version)"
  echo "== Version en place : ${precedente:-aucune (images construites sur le serveur)} ; version demandée : $version"

  echo "== Dépôt"
  if ! git diff --quiet || ! git diff --cached --quiet; then
    echo "ARRÊT : modifications locales dans ~/limpide (git status) : à régler à la main." >&2
    exit 1
  fi
  git fetch --quiet origin main
  git merge --ff-only --quiet origin/main
  if ! git merge-base --is-ancestor "$version" HEAD 2> /dev/null; then
    echo "ARRÊT : $version n'est pas un commit de main." >&2
    exit 1
  fi

  export IMAGE_TAG="$version"
  local compose=(docker compose -f docker-compose.prod.yml --env-file .env.prod --env-file .env.version)

  echo "== Images"
  "${compose[@]}" --profile outils pull --quiet web admin ingestion

  echo "== Migrations (avant l'application)"
  "${compose[@]}" --profile outils run --rm ingestion migrate

  echo "== Application"
  "${compose[@]}" up -d --no-build --wait web admin
  printf 'IMAGE_TAG=%s\n' "$version" > .env.version
  docker tag "ghcr.io/olick/limpide-ingestion:$version" limpide-ingestion:latest   # image lancée par Airflow

  echo "== Vérification"
  local essai
  for essai in 1 2 3 4 5 6; do
    if curl -fsS -o /dev/null --max-time 10 https://www.limpide-ia.fr/; then
      echo "Site en ligne, version $version."
      docker image prune -f > /dev/null   # images sans étiquette seulement
      return
    fi
    sleep 5
  done
  echo "ÉCHEC : https://www.limpide-ia.fr/ ne répond pas. Retour arrière : deploy/deployer.sh ${precedente:-<version>}" >&2
  exit 1
}

main "$@"
