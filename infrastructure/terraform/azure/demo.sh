#!/usr/bin/env bash
# Monte la vitrine Azure de Limpide de bout en bout (option B : une séance = demo.sh au début, detruire.sh à la fin).
#   1. vérifications : session az, socle (coffre de l'état, clé Mistral), images publiées, base locale ;
#   2. terraform apply (après lecture du plan et confirmation) ;
#   3. base : rôle de l'application, migrations, droits, corpus (initialiser-base.sh) ;
#   4. vérification que l'application répond ; adresse et durées.
#
# Usage, depuis infrastructure/terraform/azure :
#   ./demo.sh                 préproduction, images de la version courante du code (déjà publiées)
#   ./demo.sh --env prod      la production (vitrine ; plus grande, budget plus élevé)
#   ./demo.sh --publier       publie d'abord les images (deploy/publier-images.sh), après une modification du code
#   ./demo.sh --tag <version> une autre version déjà publiée
# Version courante : dernier commit qui touche src/, Dockerfile ou deploy/ollama (même règle que publier-images.sh).
# Coût pendant la séance : ≈ 0,25 € de l'heure. Ne pas oublier ./detruire.sh.
set -euo pipefail
cd "$(dirname "$0")"
ICI="$(pwd)"
DEPOT="$(cd ../../.. && pwd)"
REGISTRE="ghcr.io/olick"
COFFRE="kvlimpide024b1e9f"

etape() { printf '\n== %s\n' "$1"; }
duree() { printf '%d min %02d s' $(( $1 / 60 )) $(( $1 % 60 )); }
echec() { echo "ARRÊT : $1" >&2; exit 1; }

TAG="$(git -C "$DEPOT" log -1 --format=%h --abbrev=12 -- src Dockerfile deploy/ollama)"
ENV="preprod"
PUBLIER=""
while [[ $# -gt 0 ]]; do
  case "$1" in
    --publier) PUBLIER=1 ;;
    --tag) TAG="${2:?--tag demande une version}"; shift ;;
    --env) ENV="${2:?--env demande preprod ou prod}"; shift ;;
    *) echec "option inconnue : $1" ;;
  esac
  shift
done
[[ "$ENV" == "preprod" || "$ENV" == "prod" ]] || echec "environnement inconnu : $ENV"
[[ -n "$PUBLIER" ]] && "$DEPOT/deploy/publier-images.sh"
cd "$ICI/environnements/$ENV"
debut=$SECONDS

etape "1. Vérifications"
az account show --output none 2>/dev/null || echec "pas de session Azure : az login"
echo "Abonnement : $(az account show --query name -o tsv)"
for secret in mistral-api-key ovh-ai-endpoints-key; do
  [[ "$(az keyvault secret list --vault-name "$COFFRE" --query "[?name=='$secret'] | length(@)" -o tsv 2>/dev/null)" == "1" ]] \
    || echec "secret $secret absent du coffre $COFFRE (infrastructure/terraform/etat/README.md)"
done
for image in limpide-web; do
  docker manifest inspect "$REGISTRE/$image:$TAG" >/dev/null 2>&1 \
    || echec "$REGISTRE/$image:$TAG introuvable ou privée : ./demo.sh --publier, puis rendre le paquet public"
done
echo "Images : version $TAG"
docker compose -f "$DEPOT/docker-compose.yml" exec -T postgres pg_isready -q 2>/dev/null \
  || echec "base locale arrêtée (source du corpus) : docker compose up -d --wait"

export ARM_SUBSCRIPTION_ID="$(az account show --query id -o tsv)"
export TF_VAR_admin_principal_name="$(az ad signed-in-user show --query userPrincipalName -o tsv)"
export TF_VAR_poste_ipv4="$(curl -fsS https://api.ipify.org)"
export TF_VAR_image_tag="$TAG"

etape "2. Infrastructure (terraform, $ENV)"
terraform init -input=false >/dev/null
terraform plan -input=false -out=demo.tfplan | grep -E "will be|must be replaced|^Plan:|No changes" || true
read -rp "Appliquer ce plan ? (oui/non) " reponse
[[ "$reponse" == "oui" ]] || { rm -f demo.tfplan; echec "rien n'est appliqué"; }
t=$SECONDS
terraform apply -input=false demo.tfplan
rm -f demo.tfplan
infra=$(( SECONDS - t ))

etape "3. Base de données"
t=$SECONDS
"$ICI/initialiser-base.sh" "$ENV" --avec-corpus
base=$(( SECONDS - t ))

etape "4. L'application répond-elle ?"
URL="$(terraform output -raw url)"
code=000
for _ in $(seq 1 30); do
  code="$(curl -s -o /dev/null -w '%{http_code}' "$URL" || true)"
  [[ "$code" == "200" ]] && break
  sleep 10
done
[[ "$code" == "200" ]] && echo "OK ($URL)" || echo "Pas encore de réponse 200 (dernier code : $code) : journaux de ca-web dans le portail"

etape "Vitrine prête"
echo "Adresse         : $URL"
echo "Infrastructure  : $(duree $infra)"
echo "Base            : $(duree $base)"
echo "Total           : $(duree $(( SECONDS - debut )))"
echo "Fin de séance   : ./detruire.sh $ENV"
