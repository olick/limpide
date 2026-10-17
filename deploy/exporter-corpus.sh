#!/usr/bin/env bash
# Sauvegarde du corpus pour l'évaluation de la chaîne (S7, session 4b, ADR-016) : pg_dump de la base sans aucune
# donnée de visiteur, déposé dans le conteneur « corpus » du socle Azure, où deploy/evaluer.sh le lit.
# À relancer après chaque publication d'une nouvelle version du corpus : l'évaluation porte sur cette sauvegarde, pas
# sur la base de la production. Son empreinte (versions, passages, hachage) figure dans chaque rapport d'évaluation.
#
# Contenu : schéma et données, journal des migrations compris (l'évaluation applique ensuite les migrations du code
# évalué, comme un déploiement) ; tables feedback et admin_actions vides (questions et retours des visiteurs, journal
# des décisions du back office).
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
IMAGE_PG="pgvector/pgvector:pg17" # celle des deux bases : fournit pg_restore pour la vérification

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

dans_postgres "pg_dump -U \"\$POSTGRES_USER\" -d \"\$POSTGRES_DB\" -Fc --no-owner --no-privileges \
  --exclude-table-data=feedback --exclude-table-data=admin_actions" > "$TMP/$BLOB"
# Une sauvegarde jamais relue n'est pas une sauvegarde (comme deploy/backup-db.sh).
docker run --rm -i "$IMAGE_PG" pg_restore --list < "$TMP/$BLOB" > /dev/null
echo "Sauvegarde : $(du -h "$TMP/$BLOB" | cut -f1), relue par pg_restore"

az storage blob upload --auth-mode login --account-name "$COMPTE" --container-name "$CONTENEUR" --name "$BLOB" \
  --file "$TMP/$BLOB" --overwrite --no-progress --output none \
  --metadata source="$SOURCE" date="$(date -u +%Y-%m-%dT%H:%MZ)" versions="$versions" passages="$passages" \
    empreinte="$hachage"
echo "Déposée : $COMPTE/$CONTENEUR/$BLOB"
