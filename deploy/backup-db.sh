#!/usr/bin/env bash
# Sauvegarde de la base de la démo (pg_dump, format personnalisé), vérifiée, avec 7 jours d'historique.
# Lancée chaque nuit par le minuteur systemd limpide-backup.timer ; peut aussi se lancer à la main.
# Les fichiers restent sur le VPS, dont le disque est sauvegardé chaque jour par OVH (copie hors serveur).
set -euo pipefail

cd "$(dirname "$0")/.."
BACKUP_DIR="${BACKUP_DIR:-$HOME/backups}"
KEEP_DAYS="${KEEP_DAYS:-7}"
set -a; . ./.env.prod; set +a
compose() { docker compose -f docker-compose.prod.yml --env-file .env.prod "$@"; }

mkdir -p "$BACKUP_DIR" && chmod 700 "$BACKUP_DIR"
file="$BACKUP_DIR/limpide-$(date +%Y%m%d-%H%M).dump"

# Écriture dans un fichier temporaire : une sauvegarde interrompue ne remplace jamais une sauvegarde valide.
compose exec -T postgres pg_dump -U "${POSTGRES_USER:-rag}" -d "${POSTGRES_DB:-rag}" -Fc > "$file.tmp"
# Vérification : le fichier doit être lisible par pg_restore (une sauvegarde jamais relue n'est pas une sauvegarde).
compose exec -T postgres pg_restore --list < "$file.tmp" > /dev/null
mv "$file.tmp" "$file"

find "$BACKUP_DIR" -name 'limpide-*.dump' -mtime +"$KEEP_DAYS" -delete
echo "$(date -Is) sauvegarde $file ($(du -h "$file" | cut -f1)), $(ls "$BACKUP_DIR"/limpide-*.dump | wc -l) conservée(s)"
