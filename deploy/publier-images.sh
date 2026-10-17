#!/usr/bin/env bash
# Construit et publie les images de Limpide sur GitHub Container Registry : web, admin et ingestion, étiquetées par
# leur version (deploy/version-images.sh, aussi la variable image_tag de Terraform). Une image déjà publiée à cette
# version n'est pas reconstruite.
# Lancé par la chaîne CI/CD après chaque fusion (.github/workflows/deploiement.yml, S7) ; à la main en secours.
# Refuse des modifications non commitées de ce qui entre dans les images : une version doit correspondre à un commit.
#
# Le VPS (deploy/deployer.sh) et la vitrine Azure tirent ces images : rien n'est construit sur les serveurs.
# L'image Ollama (deploy/ollama, secours, ADR-012) seulement avec --avec-ollama.
#
# Prérequis : `docker login ghcr.io` (jeton avec le droit write:packages ; dans la chaîne, le jeton de l'exécution).
# Usage : deploy/publier-images.sh [--avec-ollama]
set -euo pipefail
cd "$(dirname "$0")/.."

REGISTRE="ghcr.io/olick"
TAG="$(deploy/version-images.sh --verifier)"

# publier <nom> <arguments de docker build>
publier() {
  local image="$REGISTRE/$1:$TAG"
  shift
  if docker manifest inspect "$image" > /dev/null 2>&1; then
    echo "$image : déjà publiée"
    return
  fi
  # Le label « source » rattache l'image au dépôt sur GitHub (visibilité, droits de la chaîne).
  docker build --label "org.opencontainers.image.source=https://github.com/olick/limpide" \
    --label "org.opencontainers.image.revision=$TAG" -t "$image" "$@"
  docker push "$image"
}

publier limpide-web --target web .
publier limpide-admin --target admin .
publier limpide-ingestion --target ingestion .
if [[ "${1:-}" == "--avec-ollama" ]]; then
  publier limpide-ollama deploy/ollama
fi

echo
echo "Images publiées, version $TAG :"
echo "  export TF_VAR_image_tag=$TAG"
