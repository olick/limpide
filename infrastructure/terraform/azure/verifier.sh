#!/usr/bin/env bash
# Test de fumée d'un environnement Azure (S7, session 4b-2) : la page d'accueil répond, et l'application lit la base
# par son identité managée (point /sante : empreinte du corpus servi, au moins un passage). Code 1 sinon.
# Appelé par demo.sh (étape 4) et par la chaîne (.github/workflows/azure.yml).
#
# Usage, depuis infrastructure/terraform/azure : ./verifier.sh [preprod|prod]   (preprod par défaut)
set -euo pipefail
ENV="${1:-preprod}"
[[ "$ENV" == "preprod" || "$ENV" == "prod" ]] || { echo "environnement inconnu : $ENV" >&2; exit 2; }
URL="$(terraform -chdir="$(dirname "$0")/environnements/$ENV" output -raw url)"

# Premier démarrage d'une révision : l'image se télécharge, puis l'application démarre (jusqu'à quelques minutes).
code=000
for _ in $(seq 1 30); do
  code="$(curl -s --max-time 15 -o /dev/null -w '%{http_code}' "$URL" || true)"
  [[ "$code" == "200" ]] && break
  sleep 10
done
[[ "$code" == "200" ]] || { echo "Accueil : pas de réponse 200 (dernier code : $code) ; journaux de ca-web dans le portail" >&2; exit 1; }
echo "Accueil : 200 ($URL)"

# Premier jeton Entra ID de l'application vers la base : quelques secondes au plus, d'où quelques essais.
sante=""
for _ in 1 2 3 4 5; do
  sante="$(curl -fsS --max-time 15 "$URL/sante" 2>/dev/null)" && break
  sleep 10
done
[[ -n "$sante" ]] || { echo "/sante en échec : l'application ne lit pas la base" >&2; exit 1; }
echo "Base    : $sante"
passages="$(jq -r .passages <<<"$sante")"
[[ "$passages" =~ ^[0-9]+$ && "$passages" -gt 0 ]] || { echo "Aucun passage servi : corpus absent" >&2; exit 1; }
