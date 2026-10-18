#!/usr/bin/env bash
# Version des images de Limpide : empreinte courte (12 caractères) du dernier commit qui touche ce qui entre dans les
# images. Un commit de documentation ou de Terraform ne crée donc pas de nouvelle version.
# Règle unique, utilisée par publier-images.sh, la chaîne CI/CD (.github/workflows/deploiement.yml) et
# infrastructure/terraform/azure/demo.sh.
#
# Ce qui entre dans les images (Dockerfile) : src/ (code et consignes), corpus.json et eval/questions.json (image
# d'ingestion), deploy/ollama (image de secours). Les deux fichiers JSON manquaient jusqu'au 2026-10-17 : modifier le
# jeu de questions gardait la même version pour une image au contenu différent. eval/results/*.json (rapports
# d'évaluation, image web : page /evaluation) depuis le 2026-10-18.
#
# Usage : deploy/version-images.sh [--verifier]
#   --verifier  refuse des modifications non commitées de ces fichiers (une version doit désigner un commit précis)
set -euo pipefail
cd "$(dirname "$0")/.."

SOURCES=(src Dockerfile corpus.json eval/questions.json 'eval/results/*.json' deploy/ollama)

if [[ "${1:-}" == "--verifier" && -n "$(git status --porcelain -- "${SOURCES[@]}")" ]]; then
  echo "Modifications non commitées dans ${SOURCES[*]} : commiter avant de publier (la version doit désigner un commit)." >&2
  exit 1
fi
git log -1 --format=%h --abbrev=12 -- "${SOURCES[@]}"
