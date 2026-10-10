#!/bin/bash
# Démonstration de la quarantaine (S5, session 3) : la fiche CNIL « IA : Annoter les données » est servie TRONQUÉE
# (code 200, HTML valide, mais les trois quarts du contenu manquent) par un petit serveur local. Le DAG la collecte
# comme une nouvelle version du vrai document ; les contrôles qualité la bloquent ; la version publiée reste en service ;
# l'alerte arrive sur Discord.
#
#   pipelines/airflow/demo/quarantaine.sh            prépare la page, démarre le serveur, déclenche le DAG
#   pipelines/airflow/demo/quarantaine.sh arreter    arrête le serveur de démonstration
#
# Prérequis : pile du dépôt et Airflow démarrés, image limpide-ingestion à jour. Ne touche à rien d'autre que la base
# locale (une version en quarantaine de plus) et data/pannes/.
set -euo pipefail
cd "$(dirname "$0")/../../.."   # racine du dépôt

if [ "${1:-}" = "arreter" ]; then
  docker rm -f source-demo >/dev/null 2>&1 && echo "Serveur de démonstration arrêté." || echo "Aucun serveur de démonstration."
  exit 0
fi

URL="https://www.cnil.fr/fr/ia-annoter-les-donnees"
mkdir -p data/pannes/www

# 1. La page tronquée, à partir du fichier brut de la version publiée : on coupe au début de la 2e section (<h2>).
#    Un commentaire daté rend chaque essai unique (nouvelle empreinte) : une 2e prise montre de nouveau la quarantaine.
raw=$(docker compose exec -T postgres psql -U rag -d rag -Atc \
  "SELECT v.raw_path FROM document_versions v JOIN documents d ON d.id = v.document_id WHERE v.is_current AND d.url = '$URL'")
python3 - "data/raw/$raw" data/pannes/www/ia-annoter-les-donnees.html <<'PY'
import sys
html = open(sys.argv[1], encoding="utf-8").read()
content = html.index("ctn-gen")
second = html.index("<h2", html.index("<h2", content) + 1)
import datetime
stamp = datetime.datetime.now().isoformat(timespec="seconds")
# Dans <head>, hors du contenu extrait : seul le fichier brut change, pas le texte.
page = html[:second].replace("<head>", f"<head><!-- démonstration du {stamp} -->", 1) + "</main></body></html>"
open(sys.argv[2], "w", encoding="utf-8").write(page)
print(f"Page tronquée : {second:,} caractères sur {len(html):,} (introduction et première section seulement)")
PY

# 2. Le corpus de démonstration : le VRAI document (même url), téléchargé depuis le serveur local (fetchUrl).
cat > data/pannes/source-corrompue.json <<JSON
{
  // Démonstration de la quarantaine : le vrai document CNIL, servi tronqué par le conteneur source-demo.
  "sources": [
    {
      "name": "CNIL",
      "baseUrl": "https://www.cnil.fr",
      "reuseTerms": "CC-BY-ND 4.0 FR : attribution, aucune modification. Images exclues (CC-BY-NC-ND).",
      "documents": [
        { "title": "IA : Annoter les données", "url": "$URL", "format": "html",
          "fetchUrl": "http://source-demo/ia-annoter-les-donnees.html" }
      ]
    }
  ]
}
JSON

# 3. Le serveur de la page tronquée, sur le réseau de la pile (joignable par les conteneurs d'ingestion).
docker rm -f source-demo >/dev/null 2>&1 || true
docker pull -q nginx:alpine >/dev/null
docker run -d --rm --name source-demo --network limpide_default \
  -v "$PWD/data/pannes/www:/usr/share/nginx/html:ro" nginx:alpine >/dev/null
echo "Serveur source-demo démarré."

# 4. L'ingestion, comme le lundi matin, mais avec ce corpus.
run_id="demo-quarantaine-$(date +%Y%m%d-%H%M%S)"
(cd pipelines/airflow && docker compose exec -T airflow-scheduler airflow dags trigger ingestion \
  --run-id "$run_id" -c '{"corpus": "data/pannes/source-corrompue.json"}' >/dev/null)
echo "DAG déclenché : http://localhost:8080/dags/ingestion/runs/$run_id"
echo "Ensuite : versions (console), l'alerte Discord, et une question sur l'annotation dans l'application web."
