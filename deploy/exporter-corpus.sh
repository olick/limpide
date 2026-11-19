#!/usr/bin/env bash
# Sauvegarde du corpus pour l'évaluation de la chaîne (S7, session 4b, ADR-016) : pg_dump de la base sans aucune
# donnée de visiteur, déposé dans le conteneur « corpus » du socle Azure, où deploy/evaluer.sh le lit.
# À relancer après chaque publication d'une nouvelle version du corpus : l'évaluation porte sur cette sauvegarde, pas
# sur la base de la production. Son empreinte (versions, passages, hachage) figure dans chaque rapport d'évaluation.
#
# Contenu : schéma de toutes les tables ; données des seules tables du corpus et du journal des migrations
# (TABLES_CORPUS : l'évaluation applique ensuite les migrations du code évalué, comme un déploiement). Toute autre
# table part vide, y compris une table créée après ce script : liste blanche, pas liste noire. Jusqu'au 2026-11-19, une
# liste noire (feedback, admin_actions) laissait passer answer_log et question_log (docs/gouvernance/rgpd.md).
# Vérifié après coup sur la sauvegarde elle-même : une table hors de la liste avec des données fait échouer l'export.
# Le dépôt écrase la sauvegarde précédente, qui reste récupérable 30 jours (historique des versions du compte).
#
# Usage : deploy/exporter-corpus.sh [--vps]
#   (défaut) base locale du poste (docker compose)
#   --vps    base de la production, par SSH (pg_dump : lecture seule)
# Prérequis : az login ; droit d'écriture sur le conteneur (infrastructure/terraform/etat/creer-socle.sh).
set -euo pipefail
cd "$(dirname "$0")/.."

COMPTE="stlimpidetf024b1e9f"
CONTENEUR="corpus"
BLOB="corpus.dump"
VPS="debian@57.129.175.88"
IMAGE_PG="pgvector/pgvector:0.8.7-pg17-bookworm" # celle des deux bases : fournit pg_restore pour la vérification
# Seules tables dont les données partent dans la sauvegarde. En ajouter une : vérifier d'abord qu'elle ne contient
# aucune donnée de visiteur ni du back office (docs/gouvernance/classification-des-donnees.md).
TABLES_CORPUS="sources documents document_versions chunks chunk_embeddings schemaversions"

SOURCE="locale"
case "${1:-}" in
  "") ;;
  --vps) SOURCE="vps" ;;
  *) echo "option inconnue : $1" >&2; exit 2 ;;
esac

# Lance une commande dans le conteneur PostgreSQL de la source ; utilisateur et base : variables de l'image officielle.
# Commande sans apostrophe (elle passe entre apostrophes par SSH).
dans_postgres() {
  if [[ "$SOURCE" == "vps" ]]; then
    ssh -o BatchMode=yes "$VPS" \
      "cd ~/limpide && docker compose -f docker-compose.prod.yml --env-file .env.prod exec -T postgres sh -c '$1'"
  else
    docker compose exec -T postgres sh -c "$1"
  fi
}

TMP="$(mktemp -d)"
trap 'rm -rf "$TMP"' EXIT

# Même calcul que CorpusFingerprint (ChunkStore) : versions servies, passages, début du MD5 des empreintes triées.
SQL="SELECT count(DISTINCT v.id) || chr(32) || count(c.id) || chr(32) || md5(coalesce(string_agg(DISTINCT v.content_hash, chr(44) ORDER BY v.content_hash), chr(32)))
     FROM document_versions v LEFT JOIN chunks c ON c.document_version_id = v.id WHERE v.is_current"
read -r versions passages hachage < <(dans_postgres "psql -U \"\$POSTGRES_USER\" -d \"\$POSTGRES_DB\" -tA -c \"$SQL\"")
[[ -n "${hachage:-}" ]] || { echo "Empreinte du corpus illisible : base $SOURCE joignable ?" >&2; exit 1; }
hachage="${hachage:0:12}"
echo "Base $SOURCE : $versions versions servies, $passages passages, empreinte $hachage"

# Vecteurs des passages servis, par modèle (ADR-017) : l'évaluation d'une PR qui change de modèle lit les siens ici,
# et un passage sans vecteur de ce modèle y est absent de la recherche. Échec si la base n'a pas la table (pas migrée).
SQL="SELECT string_agg(embedding_model || chr(58) || chr(32) || n, chr(44) || chr(32) ORDER BY embedding_model)
     FROM (SELECT e.embedding_model, count(*) AS n FROM chunk_embeddings e JOIN chunks c ON c.id = e.chunk_id
           JOIN document_versions v ON v.id = c.document_version_id AND v.is_current GROUP BY 1) m"
vecteurs="$(dans_postgres "psql -U \"\$POSTGRES_USER\" -d \"\$POSTGRES_DB\" -v ON_ERROR_STOP=1 -tA -c \"$SQL\"")"
[[ -n "$vecteurs" ]] || { echo "Aucun vecteur pour les passages servis : lancer embed avant d'exporter" >&2; exit 1; }
echo "Vecteurs servis (sur $passages passages) : $vecteurs"

# Tables de la base hors de TABLES_CORPUS : leurs données sont exclues (schéma gardé).
SQL="SELECT string_agg(tablename, chr(32) ORDER BY tablename) FROM pg_tables WHERE schemaname = current_schema()"
tables="$(dans_postgres "psql -U \"\$POSTGRES_USER\" -d \"\$POSTGRES_DB\" -v ON_ERROR_STOP=1 -tA -c \"$SQL\"")"
exclusions=""
for table in $tables; do
  case " $TABLES_CORPUS " in
    *" $table "*) ;;
    *) exclusions+=" --exclude-table-data=$table" ;;
  esac
done
echo "Tables exportées vides :${exclusions//--exclude-table-data=/}"

dans_postgres "pg_dump -U \"\$POSTGRES_USER\" -d \"\$POSTGRES_DB\" -Fc --no-owner --no-privileges$exclusions" \
  > "$TMP/$BLOB"
# Une sauvegarde jamais relue n'est pas une sauvegarde (comme deploy/backup-db.sh).
docker run --rm -i "$IMAGE_PG" pg_restore --list < "$TMP/$BLOB" > "$TMP/liste"
echo "Sauvegarde : $(du -h "$TMP/$BLOB" | cut -f1), relue par pg_restore"

# Contrôle sur la sauvegarde elle-même, pas sur la commande : des données hors du corpus ne partent jamais.
# Entrées de données, en « schéma.nom » : tables, et vues matérialisées (absentes de pg_tables, donc jamais exclues).
donnees="$(awk '$4 == "TABLE" && $5 == "DATA" { print $6 "." $7 }
               $4 == "MATERIALIZED" && $5 == "VIEW" && $6 == "DATA" { print $7 "." $8 }' "$TMP/liste")"
# Contrôle positif : une liste illisible (format de pg_restore changé) ne doit pas passer pour une liste propre.
[[ $'\n'"$donnees"$'\n' == *$'\n'"public.chunks"$'\n'* ]] \
  || { echo "Liste de la sauvegarde illisible (public.chunks introuvable), rien n'est déposé" >&2; exit 1; }
autorisees=" $(for t in $TABLES_CORPUS; do printf 'public.%s ' "$t"; done)"
hors_corpus=""
for entree in $donnees; do
  [[ "$autorisees" == *" $entree "* ]] || hors_corpus+=" $entree"
done
if [[ -n "$hors_corpus" ]]; then
  echo "Données hors du corpus dans la sauvegarde, rien n'est déposé :$hors_corpus" >&2
  exit 1
fi
echo "Données exportées : ${donnees//$'\n'/ }"

az storage blob upload --auth-mode login --account-name "$COMPTE" --container-name "$CONTENEUR" --name "$BLOB" \
  --file "$TMP/$BLOB" --overwrite --no-progress --output none \
  --metadata source="$SOURCE" date="$(date -u +%Y-%m-%dT%H:%MZ)" versions="$versions" passages="$passages" \
    empreinte="$hachage"
echo "Déposée : $COMPTE/$CONTENEUR/$BLOB"
