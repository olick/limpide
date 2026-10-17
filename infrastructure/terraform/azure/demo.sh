#!/usr/bin/env bash
# Monte un environnement Azure de Limpide de bout en bout (option B : une séance = demo.sh au début, detruire.sh à la
# fin ; la chaîne fait de même sur demande, .github/workflows/azure.yml).
#   1. vérifications : session az, clés dans le coffre, images publiées ;
#   2. terraform apply (après lecture du plan et confirmation, sauf --oui) ;
#   3. base : rôle de l'application, corpus restauré depuis le socle, migrations, droits (initialiser-base.sh) ;
#   4. test de fumée : accueil et lecture de la base par l'application (verifier.sh) ; adresse et durées.
#
# Usage, depuis infrastructure/terraform/azure :
#   ./demo.sh                 préproduction, images de la version courante du code (déjà publiées)
#   ./demo.sh --env prod      la production (vitrine ; plus grande, budget plus élevé)
#   ./demo.sh --publier       publie d'abord les images (deploy/publier-images.sh), après une modification du code
#   ./demo.sh --tag <version> une autre version déjà publiée
#   ./demo.sh --oui           sans confirmation après le plan (chaîne CI/CD)
#   ./demo.sh --neuf          refuse si l'environnement existe déjà (état non vide) : la chaîne ne détruit jamais un
#                             environnement qu'elle n'a pas créé
#   ./demo.sh --sans-base     s'arrête après terraform (la chaîne enchaîne initialiser-base.sh et verifier.sh après
#                             une nouvelle connexion : le jeton OIDC de GitHub ne vit que quelques minutes)
# Administrateur PostgreSQL : la personne connectée (az login) ; la chaîne passe le sien par TF_VAR_admin_principal_name
# et TF_VAR_admin_principal_type=ServicePrincipal.
# Version courante : deploy/version-images.sh (même règle que publier-images.sh et la chaîne CI/CD).
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

TAG="$("$DEPOT/deploy/version-images.sh")"
ENV="preprod"
PUBLIER=""
OUI=""
NEUF=""
SANS_BASE=""
while [[ $# -gt 0 ]]; do
  case "$1" in
    --publier) PUBLIER=1 ;;
    --tag) TAG="${2:?--tag demande une version}"; shift ;;
    --env) ENV="${2:?--env demande preprod ou prod}"; shift ;;
    --oui) OUI=1 ;;
    --neuf) NEUF=1 ;;
    --sans-base) SANS_BASE=1 ;;
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

export ARM_SUBSCRIPTION_ID="${ARM_SUBSCRIPTION_ID:-$(az account show --query id -o tsv)}"
export TF_VAR_admin_principal_name="${TF_VAR_admin_principal_name:-$(az ad signed-in-user show --query userPrincipalName -o tsv)}"
export TF_VAR_poste_ipv4="$(curl -fsS https://api.ipify.org)"
export TF_VAR_image_tag="$TAG"
echo "Administrateur PostgreSQL : $TF_VAR_admin_principal_name (${TF_VAR_admin_principal_type:-User})"

etape "2. Infrastructure (terraform, $ENV)"
[[ -n "$NEUF" ]] && { "$ICI/est-vide.sh" "$ENV" || echec "--neuf : environnement $ENV déjà présent"; }
terraform init -input=false >/dev/null
terraform plan -input=false -out=demo.tfplan | grep -E "will be|must be replaced|^Plan:|No changes" || true
if [[ -z "$OUI" ]]; then
  read -rp "Appliquer ce plan ? (oui/non) " reponse
  [[ "$reponse" == "oui" ]] || { rm -f demo.tfplan; echec "rien n'est appliqué"; }
fi
t=$SECONDS
terraform apply -input=false demo.tfplan
rm -f demo.tfplan
infra=$(( SECONDS - t ))
echo "Infrastructure : $(duree $infra)"
[[ -n "$SANS_BASE" ]] && exit 0

etape "3. Base de données"
t=$SECONDS
"$ICI/initialiser-base.sh" "$ENV" --avec-corpus
base=$(( SECONDS - t ))

etape "4. Test de fumée"
"$ICI/verifier.sh" "$ENV"

etape "Environnement prêt"
echo "Adresse         : $(terraform output -raw url)"
echo "Infrastructure  : $(duree $infra)"
echo "Base            : $(duree $base)"
echo "Total           : $(duree $(( SECONDS - debut )))"
echo "Fin de séance   : ./detruire.sh $ENV"
