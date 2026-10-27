#!/usr/bin/env bash
# Connexion à Grafana par un compte Microsoft (S8). Inscription d'application propre à Grafana (« Limpide Grafana »,
# session 5) : Grafana compromis n'expose plus le secret du back office, et chaque machine a son secret (un perdu se
# révoque seul). Une seule personne entre, identifiée par « émetteur#sujet » ; avec un compte Microsoft personnel, le
# sujet est propre à chaque inscription : ce n'est pas celui du back office (ADMIN_ALLOWED_USER). Rôle Editor
# (consulter, explorer ; l'administration de Grafana passe par le tunnel SSH) ; toute autre personne est refusée
# (role_attribute_strict).
#
# Le secret arrive par l'entrée standard, directement depuis az (ni écran, ni historique, ni fichier intermédiaire) ;
# entrée vide (az en échec) : arrêt, rien n'est changé. Garder celui de supervision/.env (changer d'adresse ou de
# personne autorisée) : 4e argument --garder-secret.
#   az ad app credential reset --id <id> --append --display-name poste --end-date AAAA-MM-JJ --query password -o tsv \
#     | supervision/connexion-microsoft.sh http://localhost:3000/ <id> '<émetteur#sujet>'                  poste
#   … --display-name serveur … | ssh debian@57.129.175.88 \
#     "~/limpide/supervision/connexion-microsoft.sh https://grafana.limpide-ia.fr/ <id> '<émetteur#sujet>'"  serveur
# Puis : docker compose -f supervision/docker-compose.yml up -d --wait --force-recreate grafana
# Sujet encore inconnu (première connexion) : deploy/README.md, « Supervision ».
set -euo pipefail
cd "$(dirname "$0")"

adresse="${1:?adresse publique de Grafana, terminée par / (ex. https://grafana.limpide-ia.fr/)}"
client_id="${2:?appId (identifiant) de « Limpide Grafana »}"
autorise="${3:?personne autorisée, « émetteur#sujet »}"
[[ -f .env ]] || { echo "supervision/.env absent : le créer d'abord (supervision/.env.example)." >&2; exit 1; }

secret=""
if [[ "${4:-}" == --garder-secret ]]; then
  secret=$(grep -m1 '^GRAFANA_OIDC_CLIENT_SECRET=' .env | cut -d= -f2- || true)
elif [[ ! -t 0 ]]; then
  read -r secret || true
fi
secret=${secret//[[:space:]]/}   # retour chariot ou espace parasite : jamais dans un secret
[[ -n "$secret" ]] \
  || { echo "Secret absent (az en échec ?) : rien n'est changé. Le passer par l'entrée standard." >&2; exit 1; }
[[ "$autorise" == *"#"* ]] || { echo "Personne autorisée : forme « émetteur#sujet » attendue." >&2; exit 1; }
for v in "$autorise" "$secret" "$client_id" "$adresse"; do
  [[ "$v" != *"'"* && "$v" != *'$'* && "$v" != *'"'* ]] \
    || { echo "Caractère inattendu (', \" ou \$) dans une valeur." >&2; exit 1; }
done

# Comptes Microsoft personnels : points d'accès OAuth 2.0 de « consumers ».
base="https://login.microsoftonline.com/consumers"
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
