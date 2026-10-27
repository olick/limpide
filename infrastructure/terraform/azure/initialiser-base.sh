#!/usr/bin/env bash
# Initialise la base PostgreSQL Azure après `terraform apply` (S6, session 3 ; corpus depuis le socle, S7). Relançable
# sans risque.
#   1. rôle PostgreSQL de l'identité managée de l'application (connexion par Entra ID, sans mot de passe) ;
#   2. avec --avec-corpus, sur une base neuve : sauvegarde du corpus restaurée depuis le socle (conteneur « corpus »,
#      deploy/exporter-corpus.sh), schéma et données, sans donnée de visiteur ; la même que l'évaluation (ADR-016) ;
#   3. migrations du code courant (commande `migrate`, comme partout ailleurs) : celles que la sauvegarde n'a pas ;
#   4. droits de l'application : lire et écrire les données, rien d'autre.
#
# Aucun mot de passe : chaque connexion utilise un jeton d'accès Entra ID de l'administrateur (personne connectée par
# `az login`, ou identité de la chaîne), valable environ une heure, jamais affiché ni écrit sur le disque.
# Administrateur : TF_VAR_admin_principal_name s'il est défini (chaîne), sinon la personne connectée.
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
IMAGE_PSQL="pgvector/pgvector:0.8.7-pg17-bookworm"   # déjà présente pour la pile locale : fournit psql et pg_restore
COMPTE="stlimpidetf024b1e9f"

HOTE="$(terraform -chdir="$TF" output -raw postgresql_hote)"
ROLE_APP="$(terraform -chdir="$TF" output -raw identite_app_nom)"
OID_APP="$(terraform -chdir="$TF" output -raw identite_app_object_id)"
ADMIN="${TF_VAR_admin_principal_name:-$(az ad signed-in-user show --query userPrincipalName -o tsv)}"
export PGPASSWORD="$(az account get-access-token --resource-type oss-rdbms --query accessToken -o tsv)"
[[ -n "${GITHUB_ACTIONS:-}" ]] && echo "::add-mask::$PGPASSWORD"

psql_azure() { # $1 = base ; la suite = arguments de psql
  local base="$1"; shift
  docker run --rm -i -e PGPASSWORD "$IMAGE_PSQL" \
    psql "host=$HOTE dbname=$base user=$ADMIN sslmode=require" -v ON_ERROR_STOP=1 -q "$@"
}

echo "Environnement : $ENV ; serveur : $HOTE ; administrateur : $ADMIN"

echo "1. Rôle de l'application ($ROLE_APP)"
if [[ "$(psql_azure postgres -tA -c "select count(*) from pg_roles where rolname = '$ROLE_APP'")" == "0" ]]; then
  # Fonction propre à Azure : crée un rôle rattaché à une identité Entra ID (ni administrateur, ni création de rôles).
  # Par identifiant objet plutôt que par nom : un ancien principal du même nom (destruction incomplète) rendrait la
  # recherche par nom ambiguë.
  psql_azure postgres -c "select * from pgaadauth_create_principal_with_oid('$ROLE_APP', '$OID_APP', 'service', false, false);" >/dev/null
  echo "   créé"
else
  echo "   déjà présent"
fi

if [[ -n "$CORPUS" ]]; then
  echo "2. Corpus restauré depuis le socle ($COMPTE/corpus/corpus.dump)"
  if [[ "$(psql_azure rag -tA -c "select count(*) from information_schema.tables where table_schema = 'public'")" != "0" ]]; then
    echo "   la base contient déjà un schéma : rien n'est restauré (environnement à recréer pour repartir du socle)"
  else
    TMP="$(mktemp -d)"
    trap 'rm -rf "$TMP"' EXIT
    az storage blob download --auth-mode login --account-name "$COMPTE" --container-name corpus --name corpus.dump \
      --file "$TMP/corpus.dump" --no-progress --output none
    # Objets créés par l'administrateur, propriétaire de tout (comme avec les migrations) ; droits posés à l'étape 4.
    # Une seule transaction : une erreur annule tout, au lieu de laisser un schéma à moitié restauré qui passerait
    # ensuite pour une base déjà initialisée.
    docker run --rm -i -e PGPASSWORD "$IMAGE_PSQL" \
      pg_restore -d "host=$HOTE dbname=rag user=$ADMIN sslmode=require" --no-owner --no-privileges --single-transaction \
      < "$TMP/corpus.dump"
    psql_azure rag -tA -c "select count(*) || ' passages, dont ' || count(*) filter (where v.is_current) || ' servis' from chunks c join document_versions v on v.id = c.document_version_id"
  fi
fi

echo "3. Migrations"
# La chaîne de connexion passe par l'environnement (jamais en argument : visible dans la liste des processus).
ConnectionStrings__Rag="Host=$HOTE;Port=5432;Database=rag;Username=$ADMIN;Password=$PGPASSWORD;SslMode=Require" \
  dotnet run --project "$DEPOT/src/Limpide.Ingestion" --no-launch-profile -- migrate

echo "4. Droits de l'application"
psql_azure rag <<SQL
GRANT CONNECT ON DATABASE rag TO "$ROLE_APP";
GRANT USAGE ON SCHEMA public TO "$ROLE_APP";
GRANT SELECT, INSERT, UPDATE, DELETE ON ALL TABLES IN SCHEMA public TO "$ROLE_APP";
GRANT USAGE, SELECT ON ALL SEQUENCES IN SCHEMA public TO "$ROLE_APP";
-- Tables créées plus tard par les migrations (même administrateur) : mêmes droits, sans relancer ce script.
ALTER DEFAULT PRIVILEGES IN SCHEMA public GRANT SELECT, INSERT, UPDATE, DELETE ON TABLES TO "$ROLE_APP";
ALTER DEFAULT PRIVILEGES IN SCHEMA public GRANT USAGE, SELECT ON SEQUENCES TO "$ROLE_APP";
SQL

echo "Base prête."
