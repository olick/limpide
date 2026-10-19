#!/usr/bin/env bash
# Comptes PostgreSQL de Grafana (S8, session 2), en lecture seule, l'un et l'autre nommés grafana_lecture :
#   - base de la démo : membre du rôle lecture_supervision (migration 0007), qui ne lit qu'answer_log ;
#   - base d'Airflow : ne lit que quelques colonnes de dag_run et import_error (dernière exécution réussie, DAG cassé),
#     pas dag_run.conf (paramètres d'une exécution) ni import_error.stacktrace.
# Crée chaque compte, ou change son mot de passe s'il existe. Écrit dans supervision/.env les mots de passe et le nom du
# conteneur de la base de la démo (Grafana est sur deux réseaux qui ont chacun un service « postgres » : le nom du
# conteneur lève l'ambiguïté), jamais à l'écran. Puis recréer Grafana pour qu'il les lise :
#   docker compose -f supervision/docker-compose.yml up -d --wait --force-recreate grafana
#
#   supervision/compte-lecture.sh                             poste (limpide-postgres-1)
#   supervision/compte-lecture.sh limpide-prod-postgres-1     serveur
# Prérequis : migration 0007 appliquée ; Airflow démarré (conteneur airflow-postgres-1). À relancer après une mise à
# jour d'Airflow qui recréerait ses tables (les droits sur dag_run et import_error seraient perdus).
set -euo pipefail
cd "$(dirname "$0")"

demo="${1:-limpide-postgres-1}"
airflow=airflow-postgres-1
[[ -f .env ]] || { echo "supervision/.env absent : le créer d'abord (supervision/.env.example)." >&2; exit 1; }
# Les deux bases d'abord : sinon le mot de passe de la première serait changé sans que .env puisse suivre, et Grafana
# perdrait sa connexion.
for c in "$demo" "$airflow"; do
  [[ "$(docker inspect -f '{{.State.Running}}' "$c" 2> /dev/null)" == true ]] \
    || { echo "Conteneur $c arrêté ou absent : démarrer la démo et Airflow d'abord." >&2; exit 1; }
done

# Crée ou met à jour grafana_lecture dans une base ($1 : conteneur ; $2 : droits accordés ; renvoie le mot de passe).
# Chaque appel change le mot de passe (rotation) : relancer le script, puis recréer Grafana.
compte() {
  local conteneur=$1 droits=$2 motdepasse
  # Hexadécimal : ni $ (interprété par Docker Compose) ni guillemet (SQL).
  motdepasse=$(openssl rand -hex 16)
  docker exec -i "$conteneur" sh -c 'psql -v ON_ERROR_STOP=1 -q -U "$POSTGRES_USER" -d "$POSTGRES_DB"' <<SQL
DO \$\$
BEGIN
    IF NOT EXISTS (SELECT FROM pg_roles WHERE rolname = 'grafana_lecture') THEN
        CREATE ROLE grafana_lecture LOGIN;
    END IF;
END
\$\$;
ALTER ROLE grafana_lecture WITH LOGIN PASSWORD '$motdepasse';
$droits
SQL
  echo "$motdepasse"
}

pg_demo=$(compte "$demo" "GRANT lecture_supervision TO grafana_lecture;")
# Colonnes seulement (REVOKE d'abord : retire les droits sur toute la table de la première version du script).
pg_airflow=$(compte "$airflow" "REVOKE SELECT ON dag_run, import_error FROM grafana_lecture;
GRANT SELECT (dag_id, state, end_date) ON dag_run TO grafana_lecture;
GRANT SELECT (id, filename, timestamp) ON import_error TO grafana_lecture;")

# Remplace les lignes si elles existent, les ajoute sinon ; le fichier reste lisible par son seul propriétaire.
umask 077
grep -vE '^(GRAFANA_PG_PASSWORD|GRAFANA_AIRFLOW_PASSWORD|LIMPIDE_PG_HOTE)=' .env > .env.tmp || true
printf 'GRAFANA_PG_PASSWORD=%s\nGRAFANA_AIRFLOW_PASSWORD=%s\nLIMPIDE_PG_HOTE=%s\n' "$pg_demo" "$pg_airflow" "$demo" >> .env.tmp
mv .env.tmp .env
echo "Comptes grafana_lecture prêts (démo : $demo, Airflow : $airflow ; mots de passe dans supervision/.env)."
echo "Recréer Grafana pour qu'il les lise."
