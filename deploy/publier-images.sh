#!/usr/bin/env bash
# Construit et publie les images de Limpide sur GitHub Container Registry. Version (variable image_tag de Terraform) :
# l'empreinte courte du dernier commit qui touche ce qui entre dans les images (src/, Dockerfile, deploy/ollama) ; un
# commit de documentation ou de Terraform ne crée donc pas de nouvelle version. En S7, la CI fera la même chose.
# Refuse des modifications non commitées de ces fichiers : une version doit correspondre à un commit précis.
#
# Depuis l'ADR-012, Azure n'utilise plus Ollama (bge-m3 par OVHcloud AI Endpoints) : seule l'image web est publiée ;
# l'image Ollama (deploy/ollama, secours) seulement avec --avec-ollama.
#
# Prérequis : `docker login ghcr.io` (jeton GitHub avec le droit write:packages).
# Usage : deploy/publier-images.sh [--avec-ollama]
set -euo pipefail
cd "$(dirname "$0")/.."

REGISTRE="ghcr.io/olick"
SOURCES=(src Dockerfile deploy/ollama)
if [[ -n "$(git status --porcelain -- "${SOURCES[@]}")" ]]; then
  echo "Modifications non commitées dans ${SOURCES[*]} : commiter avant de publier (la version doit désigner un commit)." >&2
  exit 1
fi
TAG="$(git log -1 --format=%h --abbrev=12 -- "${SOURCES[@]}")"

docker build --target web -t "$REGISTRE/limpide-web:$TAG" .
docker push "$REGISTRE/limpide-web:$TAG"
if [[ "${1:-}" == "--avec-ollama" ]]; then
  docker build -t "$REGISTRE/limpide-ollama:$TAG" deploy/ollama
  docker push "$REGISTRE/limpide-ollama:$TAG"
fi

echo
echo "Images publiées, version $TAG :"
echo "  export TF_VAR_image_tag=$TAG"
