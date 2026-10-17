#!/usr/bin/env bash
# Évaluation de la chaîne de réponse dans un environnement jetable (S7, session 4b, ADR-016) : la barrière de qualité
# avant la production. Même commande sur le poste et dans la chaîne (.github/workflows/evaluation.yml).
#   1. clés d'API lues dans le coffre du socle (Mistral, OVHcloud AI Endpoints), jamais écrites sur le disque ;
#   2. sauvegarde du corpus téléchargée du socle (déposée par deploy/exporter-corpus.sh) ;
#   3. PostgreSQL jetable (même image que la production), sauvegarde restaurée, puis migrations du code évalué, comme
#      un déploiement ;
#   4. evaluate-answers (jeu complet, juge de fidélité), puis gate (seuils de l'ADR-014) : code 1 si bloqué.
# Configuration : celle du dépôt seulement (ni user-secrets ni réglages de développement), vectorisation des questions
# comme en production (bge-m3 chez OVHcloud AI Endpoints). Base supprimée à la sortie, même en cas d'échec.
# Coût : ≈ 0,29 $ chez Mistral par exécution (réponses et juge).
#
# Usage : deploy/evaluer.sh [--resultats <dossier>]
#   --resultats  dossier des rapports (défaut : eval/results, versionné ; la chaîne en prend un à part, publié comme
#                artefact)
# Prérequis : az login (poste) ou identité id-limpide-ci-evaluation (chaîne) ; Docker ; SDK .NET.
set -euo pipefail
cd "$(dirname "$0")/.."

COFFRE="kvlimpide024b1e9f"
COMPTE="stlimpidetf024b1e9f"
CONTENEUR="corpus"
BLOB="corpus.dump"
IMAGE_PG="pgvector/pgvector:pg17"                                     # docker-compose.prod.yml
EMBEDDING_ENDPOINT="https://oai.endpoints.kepler.ai.cloud.ovh.net/v1" # docker-compose.prod.yml

RESULTATS="eval/results"
while [[ $# -gt 0 ]]; do
  case "$1" in
    --resultats) RESULTATS="${2:?--resultats demande un dossier}"; shift ;;
    *) echo "option inconnue : $1" >&2; exit 2 ;;
  esac
  shift
done

etape() { printf '\n== %s\n' "$1"; }
debut=$SECONDS
TMP="$(mktemp -d)"
BASE="limpide-evaluation-$$"
nettoyer() { docker rm -f "$BASE" >/dev/null 2>&1 || true; rm -rf "$TMP"; }
trap nettoyer EXIT

etape "1. Clés d'API (coffre $COFFRE)"
secret() { az keyvault secret show --vault-name "$COFFRE" --name "$1" --query value -o tsv; }
MISTRAL="$(secret mistral-api-key)"
OVH="$(secret ovh-ai-endpoints-key)"
# Dépôt public : la chaîne ne masque d'office que les secrets de GitHub ; ces clés, lues en cours de route, sont
# masquées ici dans les journaux.
if [[ -n "${GITHUB_ACTIONS:-}" ]]; then
  echo "::add-mask::$MISTRAL"
  echo "::add-mask::$OVH"
fi
echo "Lues : mistral-api-key, ovh-ai-endpoints-key"

etape "2. Sauvegarde du corpus ($COMPTE/$CONTENEUR/$BLOB)"
blob=(--auth-mode login --account-name "$COMPTE" --container-name "$CONTENEUR" --name "$BLOB")
az storage blob download "${blob[@]}" --file "$TMP/$BLOB" --no-progress --output none
az storage blob show "${blob[@]}" --query metadata -o yaml # source, date, versions, passages, empreinte

etape "3. Base jetable ($IMAGE_PG)"
MOT_DE_PASSE="$(openssl rand -hex 16)"
[[ -n "${GITHUB_ACTIONS:-}" ]] && echo "::add-mask::$MOT_DE_PASSE" # base jetable, masqué par cohérence
docker run -d --name "$BASE" -e POSTGRES_USER=rag -e POSTGRES_DB=rag -e POSTGRES_PASSWORD="$MOT_DE_PASSE" \
  -p 127.0.0.1::5432 "$IMAGE_PG" > /dev/null
# Par TCP : pendant son initialisation, l'image démarre un serveur provisoire qui n'écoute que sur le socket local.
for _ in $(seq 1 60); do
  docker exec "$BASE" pg_isready -h 127.0.0.1 -U rag -d rag -q && break
  sleep 1
done
docker exec "$BASE" pg_isready -h 127.0.0.1 -U rag -d rag -q || { echo "PostgreSQL ne répond pas" >&2; exit 1; }
PORT="$(docker port "$BASE" 5432/tcp | head -1 | sed 's/.*://')"
docker exec -i "$BASE" pg_restore -U rag -d rag --no-owner --no-privileges --exit-on-error < "$TMP/$BLOB"
echo "Restaurée sur le port $PORT"

# Configuration du dépôt seulement : DOTNET_ENVIRONMENT=Production et sans profil de lancement, donc ni user-secrets
# ni réglages du poste. Les clés et la chaîne de connexion passent par l'environnement, jamais en argument.
export DOTNET_ENVIRONMENT=Production
export ConnectionStrings__Rag="Host=127.0.0.1;Port=$PORT;Database=rag;Username=rag;Password=$MOT_DE_PASSE"
export Embedding__Provider=OpenAI Embedding__Endpoint="$EMBEDDING_ENDPOINT" Embedding__ApiKey="$OVH"
export Mistral__ApiKey="$MISTRAL"
export Ingestion__ResultsPath="$RESULTATS"
dotnet build src/Limpide.Ingestion --configuration Release --nologo --verbosity quiet
limpide() { dotnet run --project src/Limpide.Ingestion --configuration Release --no-build --no-launch-profile -- "$@"; }

etape "4. Migrations du code évalué"
limpide migrate

etape "5. Évaluation (evaluate-answers, juge de fidélité)"
limpide evaluate-answers

etape "6. Barrière de qualité (gate)"
code=0
limpide gate || code=$?

duree=$(( SECONDS - debut ))
printf '\nDurée : %d min %02d s ; rapports dans %s\n' $(( duree / 60 )) $(( duree % 60 )) "$RESULTATS"
exit "$code"
