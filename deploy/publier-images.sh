#!/usr/bin/env bash
# Construit et publie les images de Limpide sur GitHub Container Registry, avec l'empreinte courte du commit comme
# version (variable image_tag de Terraform). En S7, la CI fera exactement ceci à chaque fusion.
# Refuse un dépôt modifié : une version doit correspondre à un commit précis.
#
# Prérequis : `docker login ghcr.io` (jeton GitHub avec le droit write:packages). Usage : deploy/publier-images.sh
set -euo pipefail
cd "$(dirname "$0")/.."

REGISTRE="ghcr.io/olick"
if [[ -n "$(git status --porcelain --untracked-files=no)" ]]; then
  echo "Modifications non commitées : commiter avant de publier (la version doit désigner un commit)." >&2
  exit 1
fi
TAG="$(git rev-parse --short=12 HEAD)"

docker build --target web -t "$REGISTRE/limpide-web:$TAG" .
docker build -t "$REGISTRE/limpide-ollama:$TAG" deploy/ollama
docker push "$REGISTRE/limpide-web:$TAG"
docker push "$REGISTRE/limpide-ollama:$TAG"

echo
echo "Images publiées, version $TAG :"
echo "  export TF_VAR_image_tag=$TAG"
