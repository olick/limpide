#!/usr/bin/env bash
# Mémoire réelle de chaque conteneur en marche (démo, Airflow, supervision : ADR-018), lue dans le cgroup : « anon »
# est la mémoire du programme, « file » le cache disque, que le système récupère au besoin (docker stats additionne
# une partie des deux).
# Puis la mémoire disponible de la machine (MemAvailable), la seule qui compte pour la marge du serveur.
#   supervision/memoire.sh            (poste ou serveur, sans sudo)
set -euo pipefail

printf '%-38s %10s %10s %10s %8s\n' conteneur "réelle" cache pic "arrêts"
for name in $(docker ps --format '{{.Names}}' | sort); do
  id=$(docker inspect -f '{{.Id}}' "$name")
  dir=/sys/fs/cgroup/system.slice/docker-$id.scope
  [[ -d $dir ]] || { echo "$name : cgroup introuvable ($dir)"; continue; }
  anon=$(awk '$1 == "anon" { print int($2 / 1048576) }' "$dir/memory.stat")
  file=$(awk '$1 == "file" { print int($2 / 1048576) }' "$dir/memory.stat")
  peak=$(( $(cat "$dir/memory.peak" 2> /dev/null || echo 0) / 1048576 ))
  oom=$(awk '$1 == "oom_kill" { print $2 }' "$dir/memory.events")
  printf '%-38s %7s Mo %7s Mo %7s Mo %8s\n' "$name" "$anon" "$file" "$peak" "$oom"
done
echo
free -m | awk 'NR == 1 || /^(Mem|Swap|Mém|Partition)/'
