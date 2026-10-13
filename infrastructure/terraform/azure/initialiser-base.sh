#!/usr/bin/env bash
# Initialise la base PostgreSQL Azure après `terraform apply` (S6, session 3). Relançable sans risque.
#   1. rôle PostgreSQL de l'identité managée de l'application (connexion par Entra ID, sans mot de passe) ;
#   2. schéma par les migrations (commande `migrate`, comme partout ailleurs) ;
#   3. droits de l'application : lire et écrire les données, rien d'autre ;
#   4. avec --avec-corpus : copie du corpus de la base locale (données seules, sans les retours des visiteurs).
#
# Aucun mot de passe : chaque connexion utilise un jeton d'accès Entra ID de la personne connectée (`az login`),
# valable environ une heure, jamais affiché ni écrit sur le disque.
#
# Usage, depuis infrastructure/terraform/azure : ./initialiser-base.sh [preprod|prod] [--avec-corpus]
set -euo pipefail

ICI="$(cd "$(dirname "$0")" && pwd)"
DEPOT="$(cd "$ICI/../../.." && pwd)"
ENV="preprod"
CORPUS=""
for arg in "$@"; do
  case "$arg" in
    preprod|prod) ENV="$arg" ;;
    --avec-corpus) CORPUS=1 ;;
    *) echo "argument inconnu : $arg" >&2; exit 1 ;;
  esac
done
TF="$ICI/environnements/$ENV"
IMAGE_PSQL="pgvector/pgvector:pg17"   # déjà présente pour la pile locale : fournit psql

HOTE="$(terraform -chdir="$TF" output -raw postgresql_hote)"
ROLE_APP="$(terraform -chdir="$TF" output -raw identite_app_nom)"
ADMIN="$(az ad signed-in-user show --query userPrincipalName -o tsv)"
export PGPASSWORD="$(az account get-access-token --resource-type oss-rdbms --query accessToken -o tsv)"

psql_azure() { # $1 = base ; la suite = arguments de psql
  local base="$1"; shift
  docker run --rm -i -e PGPASSWORD "$IMAGE_PSQL" \
    psql "host=$HOTE dbname=$base user=$ADMIN sslmode=require" -v ON_ERROR_STOP=1 -q "$@"
}

echo "Environnement : $ENV ; serveur : $HOTE"

echo "1. Rôle de l'application ($ROLE_APP)"
if [[ "$(psql_azure postgres -tA -c "select count(*) from pg_roles where rolname = '$ROLE_APP'")" == "0" ]]; then
  # Fonction propre à Azure : crée un rôle rattaché à une identité Entra ID (ni administrateur, ni création de rôles).
  psql_azure postgres -c "select * from pgaadauth_create_principal('$ROLE_APP', false, false);" >/dev/null
  echo "   créé"
else
  echo "   déjà présent"
fi

echo "2. Migrations"
# La chaîne de connexion passe par l'environnement (jamais en argument : visible dans la liste des processus).
ConnectionStrings__Rag="Host=$HOTE;Port=5432;Database=rag;Username=$ADMIN;Password=$PGPASSWORD;SslMode=Require" \
  dotnet run --project "$DEPOT/src/Limpide.Ingestion" -- migrate

echo "3. Droits de l'application"
psql_azure rag <<SQL
GRANT CONNECT ON DATABASE rag TO "$ROLE_APP";
GRANT USAGE ON SCHEMA public TO "$ROLE_APP";
GRANT SELECT, INSERT, UPDATE, DELETE ON ALL TABLES IN SCHEMA public TO "$ROLE_APP";
GRANT USAGE, SELECT ON ALL SEQUENCES IN SCHEMA public TO "$ROLE_APP";
-- Tables créées plus tard par les migrations (même administrateur) : mêmes droits, sans relancer ce script.
ALTER DEFAULT PRIVILEGES IN SCHEMA public GRANT SELECT, INSERT, UPDATE, DELETE ON TABLES TO "$ROLE_APP";
ALTER DEFAULT PRIVILEGES IN SCHEMA public GRANT USAGE, SELECT ON SEQUENCES TO "$ROLE_APP";
SQL

if [[ -n "$CORPUS" ]]; then
  echo "4. Corpus copié depuis la base locale"
  if [[ "$(psql_azure rag -tA -c "select count(*) from chunks")" != "0" ]]; then
    echo "   la base Azure contient déjà des passages : rien n'est copié"
  else
    # Données seules (le schéma vient des migrations) ; ni le journal des migrations ni les retours des visiteurs.
    docker compose -f "$DEPOT/docker-compose.yml" exec -T postgres \
      pg_dump -U rag -d rag --data-only --no-owner \
        --exclude-table-data=schemaversions --exclude-table-data=feedback --exclude-table-data=admin_actions \
      | psql_azure rag
    psql_azure rag -tA -c "select count(*) || ' passages, dont ' || count(*) filter (where v.is_current) || ' servis' from chunks c join document_versions v on v.id = c.document_version_id"
  fi
fi

echo "Base prête."
