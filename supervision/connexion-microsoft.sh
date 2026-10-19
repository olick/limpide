#!/usr/bin/env bash
# Connexion à Grafana par le compte Microsoft du back office (option A, S8) : même inscription d'application, même
# personne autorisée. Recopie dans supervision/.env, sans rien afficher, les valeurs OpenID Connect du back office
# (ADMIN_OIDC_*, ADMIN_ALLOWED_USER) et la règle d'accès de Grafana : seul l'identifiant « émetteur#sujet » autorisé
# au back office entre, avec le rôle Editor (consulter, explorer ; l'administration de Grafana passe par le tunnel
# SSH) ; toute autre personne est refusée (role_attribute_strict).
# Prérequis : l'adresse de retour de Grafana ajoutée à l'inscription d'application (deploy/README.md, « Supervision »).
#
#   supervision/connexion-microsoft.sh ../.env http://localhost:3000/                       poste
#   supervision/connexion-microsoft.sh ../.env.prod https://grafana.limpide-ia.fr/          serveur
# Puis : docker compose -f supervision/docker-compose.yml up -d --wait --force-recreate grafana
set -euo pipefail
cd "$(dirname "$0")"

source_env="${1:?fichier de configuration du back office (../.env ou ../.env.prod)}"
adresse="${2:?adresse publique de Grafana, terminée par / (ex. https://grafana.limpide-ia.fr/)}"
[[ -f .env ]] || { echo "supervision/.env absent : le créer d'abord (supervision/.env.example)." >&2; exit 1; }

valeur() { grep -m1 "^$1=" "$source_env" | cut -d= -f2-; }
autorite=$(valeur ADMIN_OIDC_AUTHORITY)
client_id=$(valeur ADMIN_OIDC_CLIENT_ID)
secret=$(valeur ADMIN_OIDC_CLIENT_SECRET)
autorise=$(valeur ADMIN_ALLOWED_USER)
for v in autorite client_id secret autorise; do
  [[ -n "${!v}" ]] || { echo "Valeur manquante dans $source_env (ADMIN_OIDC_* ou ADMIN_ALLOWED_USER)." >&2; exit 1; }
done
[[ "$autorise" == *"#"* ]] || { echo "ADMIN_ALLOWED_USER n'a pas la forme « émetteur#sujet »." >&2; exit 1; }
for v in "$autorise" "$secret" "$client_id" "$autorite"; do
  [[ "$v" != *"'"* && "$v" != *'$'* && "$v" != *'"'* ]] \
    || { echo "Caractère inattendu (', \" ou \$) dans les valeurs du back office." >&2; exit 1; }
done

# « https://login.microsoftonline.com/consumers/v2.0 » → points d'accès OAuth 2.0 du même fournisseur.
base="${autorite%/v2.0}"
# JMESPath évalué par Grafana sur le jeton d'identité : Editor pour la seule personne autorisée, rien sinon (refus).
role="iss == '${autorise%%#*}' && sub == '${autorise#*#}' && 'Editor'"

umask 077
grep -vE '^GRAFANA_(ADRESSE|COOKIE_SECURE|OIDC_[A-Z_]+)=' .env > .env.tmp || true
{
  echo "GRAFANA_ADRESSE=$adresse"
  # Cookie de session marqué « Secure » en HTTPS (sur le poste, http://localhost : sinon, plus de connexion).
  [[ "$adresse" == https://* ]] && echo "GRAFANA_COOKIE_SECURE=true" || echo "GRAFANA_COOKIE_SECURE=false"
  echo "GRAFANA_OIDC_ACTIF=true"
  echo "GRAFANA_OIDC_CLIENT_ID=$client_id"
  echo "GRAFANA_OIDC_CLIENT_SECRET=$secret"
  echo "GRAFANA_OIDC_AUTH_URL=$base/oauth2/v2.0/authorize"
  echo "GRAFANA_OIDC_TOKEN_URL=$base/oauth2/v2.0/token"
  echo "GRAFANA_OIDC_ROLE=$role"
} >> .env.tmp
mv .env.tmp .env
echo "Connexion Microsoft de Grafana configurée ($adresse ; adresse de retour : ${adresse}login/generic_oauth)."
echo "Recréer Grafana pour qu'il la lise."
