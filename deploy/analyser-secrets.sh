#!/usr/bin/env bash
# Recherche de secrets dans l'historique Git (S10), avec Gitleaks, lancé depuis son image Docker épinglée par empreinte :
# rien à installer, aucune action du catalogue (mêmes raisons que Grype, deploy/analyser-images.sh).
#
#   deploy/analyser-secrets.sh
#
# Le dépôt est public : une clé commitée est une clé exposée, même effacée au commit suivant. Tout l'historique est donc
# relu à chaque fois (quelques secondes), pas seulement les nouveaux commits, et sur toutes les branches du clone
# (comportement par défaut de Gitleaks, vérifié) : une clé sur une autre branche poussée bloque aussi, à raison. Il faut
# un clone complet : en CI, fetch-depth: 0.
#
# Un constat bloque (code 1, tout autre code est une erreur de l'analyse). Conduite à tenir :
#   - vrai secret : le renouveler d'abord, il est compromis ; réécrire l'historique ne suffit pas ;
#   - faux positif : ajouter son empreinte (Fingerprint, dans le rapport) à .gitleaksignore, à la racine du dépôt,
#     sous une ligne de commentaire qui donne la raison et la date.
#
# Secrets masqués (--redact) : les journaux de la CI sont publics. Conteneur sans réseau, dépôt monté en lecture seule.
# Détection par motifs : les clés sans préfixe connu (Mistral, OVHcloud) ne sont repérées que dans une affectation
# (MISTRAL_API_KEY=…, "ApiKey": "…"), pas seules sur une ligne. Filet, pas preuve d'absence.
set -euo pipefail
cd "$(dirname "$0")/.."

# Gitleaks v8.30.1 (publié le 2026-03-21), liste de manifestes multi-architecture. Monter de version : docker buildx
# imagetools inspect ghcr.io/gitleaks/gitleaks:vX.Y.Z (choisir une version publiée depuis au moins deux semaines).
GITLEAKS="ghcr.io/gitleaks/gitleaks:v8.30.1@sha256:c00b6bd0aeb3071cbcb79009cb16a60dd9e0a7c60e2be9ab65d25e6bc8abbb7f"

if [[ "$(git rev-parse --is-shallow-repository)" == "true" ]]; then
  echo "Clone partiel : l'historique serait incomplet (en CI : fetch-depth: 0)." >&2
  exit 2
fi

code=0
docker run --rm --network none --user "$(id -u):$(id -g)" -e HOME=/tmp -v "$PWD:/depot:ro" "$GITLEAKS" \
  git /depot --redact --verbose --no-banner || code=$?

if [[ -n "${GITHUB_STEP_SUMMARY:-}" ]]; then
  case "$code" in
    0) echo "Secrets : aucun constat dans l'historique Git." ;;
    1) echo "**Secrets : constat dans l'historique Git**, détail masqué dans le journal du job." ;;
    *) echo "**Secrets : analyse en erreur (code $code)**, l'historique n'a pas été vérifié." ;;
  esac >> "$GITHUB_STEP_SUMMARY"
fi
exit "$code"
