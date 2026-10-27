#!/usr/bin/env bash
# Compte PostgreSQL de l'application web publique (S8, session 5) : limpide_web, membre du rôle application_web
# (migration 0010 : lecture du corpus, écriture des journaux), à la place du superutilisateur de l'image PostgreSQL.
# Crée le compte, ou change son mot de passe s'il existe (rotation : relancer, puis recréer web). Écrit le mot de passe
# dans le fichier d'environnement (WEB_DB_PASSWORD), jamais à l'écran, puis :
#   limpide up -d --no-build --wait web
#
#   deploy/compte-web.sh                                          serveur, ou test local de la production
#   deploy/compte-web.sh <conteneur> <fichier d'environnement>    autre base (essai sur le poste)
# Peut tourner avant la migration 0010 (il crée alors le rôle application_web, encore sans droits) : c'est l'ordre du
# premier déploiement, où docker-compose.prod.yml exige WEB_DB_PASSWORD avant que la migration ne passe.
set -euo pipefail
cd "$(dirname "$0")/.."

conteneur="${1:-limpide-prod-postgres-1}"
fichier="${2:-.env.prod}"
[[ -f "$fichier" ]] || { echo "$fichier absent." >&2; exit 1; }
[[ "$(docker inspect -f '{{.State.Running}}' "$conteneur" 2> /dev/null)" == true ]] \
  || { echo "Conteneur $conteneur arrêté ou absent." >&2; exit 1; }

# Hexadécimal : ni $ (interprété par Docker Compose) ni guillemet (SQL) ni caractère spécial d'une chaîne de connexion.
motdepasse=$(openssl rand -hex 24)
# Par l'entrée standard : le mot de passe n'apparaît pas dans ps.
docker exec -i "$conteneur" sh -c 'psql -v ON_ERROR_STOP=1 -q -U "$POSTGRES_USER" -d "$POSTGRES_DB"' <<SQL
SET client_min_messages = warning;
-- En cas d'erreur, ne pas écrire la requête (et donc le mot de passe) dans le journal de PostgreSQL.
SET log_min_error_statement = panic;
DO \$\$
BEGIN
    IF NOT EXISTS (SELECT FROM pg_roles WHERE rolname = 'application_web') THEN
        CREATE ROLE application_web NOLOGIN;
    END IF;
    IF NOT EXISTS (SELECT FROM pg_roles WHERE rolname = 'limpide_web') THEN
        CREATE ROLE limpide_web LOGIN;
    END IF;
END
\$\$;
ALTER ROLE limpide_web WITH LOGIN NOSUPERUSER NOCREATEDB NOCREATEROLE PASSWORD '$motdepasse';
GRANT application_web TO limpide_web;
SQL

# Essai du mot de passe par l'adresse réseau du conteneur : en local (127.0.0.1, socket), l'image PostgreSQL accepte
# tout sans mot de passe, l'essai ne prouverait rien. Mot de passe par l'entrée standard, jamais en argument.
docker exec -i "$conteneur" sh -c 'read -r PGPASSWORD; export PGPASSWORD
  psql -h "$(hostname -i | cut -d" " -f1)" -U limpide_web -d "$POSTGRES_DB" -Atqc "SELECT current_user"' <<< "$motdepasse" \
  | grep -qx limpide_web || { echo "Connexion de limpide_web refusée : $fichier inchangé." >&2; exit 1; }

# Remplace la ligne si elle existe, l'ajoute sinon ; droits du fichier gardés (cp -p), lisible par son seul propriétaire.
cp -p "$fichier" "$fichier.tmp"
grep -v '^WEB_DB_PASSWORD=' "$fichier" > "$fichier.tmp" || true
printf 'WEB_DB_PASSWORD=%s\n' "$motdepasse" >> "$fichier.tmp"
mv "$fichier.tmp" "$fichier"
echo "Compte limpide_web prêt dans $conteneur (mot de passe dans $fichier). Recréer web pour qu'il le lise."
