#!/usr/bin/env bash
# Analyse des vulnérabilités des images de Limpide publiées sur ghcr.io (S7, session 4a, ADR-015), avec Grype
# (Anchore), lancé depuis son image Docker épinglée par empreinte : rien à installer, et aucune action du catalogue.
# Trivy, l'outil le plus courant, a été écarté après la compromission de sa chaîne de publication (mars 2026,
# CVE-2026-33634 : étiquettes de trivy-action réécrites, binaire piégé, secrets de CI volés).
#
#   deploy/analyser-images.sh [<version>]     version par défaut : deploy/version-images.sh
#   deploy/analyser-images.sh --tierces       images des autres éditeurs lancées sur le serveur (S8, session 5)
#
# --tierces : Caddy et Grafana sont exposés sur Internet, la supervision et Airflow tournent à côté de la démo. Liste
# lue dans les fichiers Compose du serveur (démo, supervision, Airflow), donc à jour sans rien maintenir ; exceptions
# dans deploy/grype-tierces.yaml. Une étiquette flottante (caddy:2, postgres:16) est analysée telle que publiée
# aujourd'hui : le serveur peut tourner sur une copie plus ancienne (corriger : docker compose pull, puis up -d).
# Rapport seulement, jamais d'échec (sauf image introuvable) : presque toutes ces failles viennent de l'éditeur (Go,
# Debian) et la seule action possible est de monter de version quand il en publie une ; ce signal-là vient de
# Dependabot (.github/dependabot.yml), qui ouvre une PR à chaque nouvelle version (décision du 2026-10-22). Lancée
# chaque lundi (analyse-images.yml), jamais avant un déploiement de la démo.
#
# Politique (appliquée ici, pas par Grype, pour que le rapport montre tout) :
#   - BLOQUANT : vulnérabilité de sévérité élevée ou critique (High, Critical) dont un correctif existe : on peut agir ;
#   - signalé : tout le reste, dont les vulnérabilités sans correctif (bloquer sur elles arrêterait la chaîne sans
#     rien à faire, et on finirait par ignorer l'alerte) ;
#   - exceptions : deploy/grype.yaml (une raison et une date par règle), comptées à part dans le rapport.
# Corriger : en général, reconstruire sur une image de base à jour (les images ne sont reconstruites qu'à une nouvelle
# version, donc avec un commit qui touche leurs sources) ou monter la version du paquet NuGet en cause.
#
# Images publiques : aucune connexion au registre n'est nécessaire. Code 3 si la version n'est pas publiée. Sortie : tableau par image (sévérité, correctif),
# détail des vulnérabilités bloquantes, rapports JSON dans $RAPPORTS (défaut : un dossier temporaire). Tableau Markdown
# ajouté à GITHUB_STEP_SUMMARY quand la variable existe (chaîne CI/CD). Code 1 si une image est bloquée.
set -euo pipefail
cd "$(dirname "$0")/.."

# Grype v0.120.0 (publié le 2026-10-02), liste de manifestes multi-architecture. Monter de version : docker buildx
# imagetools inspect anchore/grype:vX.Y.Z (choisir une version publiée depuis au moins deux semaines).
GRYPE="anchore/grype:v0.120.0@sha256:5c88961f4130e830542d441c7ed6c78baa28e799163abac53d2be4923fb5ab7d"
REGISTRE="ghcr.io/olick"
IMAGES=(limpide-web limpide-admin limpide-ingestion)

# Images tierces des fichiers Compose du serveur, valeur par défaut des variables résolue (${X:-image}). Exclues :
# les nôtres (analysées sans --tierces), Ollama (profil jamais démarré sur le serveur, ADR-012), Redis (Airflow allégé,
# LocalExecutor, n'en lance pas).
images_tierces() {
  sed -nE 's/^\s*image:\s*([^ #]+).*/\1/p' docker-compose.prod.yml supervision/docker-compose.yml \
      pipelines/airflow/docker-compose.yaml \
    | sed -E 's/^\$\{[A-Z_]+:-([^}]+)\}$/\1/' | grep -vE '^(ghcr\.io/olick/|ollama/|redis:)' | sort -u
}

if [[ "${1:-}" == "--tierces" ]]; then
  mapfile -t REFS < <(images_tierces)
  NOMS=("${REFS[@]}")
  CONFIG=deploy/grype-tierces.yaml
  TITRE="Analyse des images tierces du serveur"
  BLOQUER=0
else
  VERSION="${1:-$(deploy/version-images.sh)}"
  if [[ ! "$VERSION" =~ ^[0-9a-f]{12,40}$ ]]; then
    echo "Usage : deploy/analyser-images.sh [<version>|--tierces] (12 à 40 caractères hexadécimaux)" >&2
    exit 2
  fi
  REFS=()
  for image in "${IMAGES[@]}"; do REFS+=("$REGISTRE/$image:$VERSION"); done
  NOMS=("${IMAGES[@]}")
  CONFIG=deploy/grype.yaml
  TITRE="Analyse des images ($VERSION)"
  BLOQUER=1
fi
RAPPORTS="${RAPPORTS:-$(mktemp -d)}"
mkdir -p "$RAPPORTS"
# Base de vulnérabilités gardée entre deux lancements sur le poste (≈ 1 Go une fois décompressée).
CACHE="${GRYPE_CACHE:-${XDG_CACHE_HOME:-$HOME/.cache}/limpide-grype}"
# Dossier temporaire dans le cache : Grype tourne sous l'utilisateur courant, qui ne peut pas écrire dans le /tmp de
# son image.
mkdir -p "$CACHE/tmp"

BLOQUANTES='[.matches[] | select((.vulnerability.severity == "High" or .vulnerability.severity == "Critical")
  and .vulnerability.fix.state == "fixed")]'

resume="| Image | Critique | Élevée | Moyenne | Faible | Avec correctif | Exceptions | Résultat |
|---|---|---|---|---|---|---|---|"
bloquee=0
absentes=0
for i in "${!REFS[@]}"; do
  ref="${REFS[$i]}"
  image="${NOMS[$i]}"
  rapport="$RAPPORTS/$(tr '/:' '__' <<< "$image").json"
  echo "== $ref"
  # Version jamais publiée (publication en échec) : le dire, plutôt qu'une erreur de Grype noyée dans le journal.
  if ! docker manifest inspect "$ref" > /dev/null 2>&1; then
    # Images tierces : continuer avec les autres (refus passager de Docker Hub, limite de débit), échouer à la fin.
    if (( BLOQUER == 0 )); then
      echo "$ref introuvable ou refusée par son registre : non analysée." >&2
      resume+=$'\n'"| $image | | | | | | | ❓ non analysée |"
      absentes=1
      continue
    fi
    echo "ARRÊT : $ref introuvable sur son registre (version non publiée ?), rien à analyser." >&2
    exit 3
  fi
  docker run --rm --user "$(id -u):$(id -g)" \
    -v "$CACHE:/cache" -e GRYPE_DB_CACHE_DIR=/cache -e TMPDIR=/cache/tmp -e GRYPE_CHECK_FOR_APP_UPDATE=false \
    -v "$PWD/$CONFIG:/grype.yaml:ro" \
    "$GRYPE" -c /grype.yaml -o json "registry:$ref" > "$rapport"
  dernier="$rapport"

  compte() { jq --arg s "$1" '[.matches[] | select(.vulnerability.severity | IN($s | split(",")[]))] | length' "$rapport"; }
  corrigeables=$(jq '[.matches[] | select(.vulnerability.fix.state == "fixed")] | length' "$rapport")
  exceptions=$(jq '.ignoredMatches | length' "$rapport")
  nb_bloquantes=$(jq "$BLOQUANTES | length" "$rapport")
  if (( nb_bloquantes > 0 && BLOQUER == 0 )); then
    resultat="⚠️ $nb_bloquantes à suivre"
    jq -r "$BLOQUANTES | .[] | \"  \(.vulnerability.severity)  \(.vulnerability.id)  \(.artifact.name) \(.artifact.version) → \(.vulnerability.fix.versions // [] | join(\", \"))\"" "$rapport" | sort -u
  elif (( nb_bloquantes > 0 )); then
    resultat="❌ $nb_bloquantes bloquante(s)"
    bloquee=1
    jq -r "$BLOQUANTES | .[] | \"  \(.vulnerability.severity)  \(.vulnerability.id)  \(.artifact.name) \(.artifact.version) → \(.vulnerability.fix.versions // [] | join(\", \"))\"" "$rapport"
  else
    resultat="✅"
  fi
  ligne="| $image | $(compte Critical) | $(compte High) | $(compte Medium) | $(compte Low,Negligible) | $corrigeables | $exceptions | $resultat |"
  echo "$ligne"
  resume+=$'\n'"$ligne"
done

base="aucune image analysée"
if [[ -n "${dernier:-}" ]]; then
  base=$(jq -r '"\(.descriptor.name) \(.descriptor.version), base du \(.descriptor.db.status.built)"' "$dernier")
fi
texte="### $TITRE

$resume

$( (( BLOQUER )) && echo "Bloquant" || echo "À suivre (sans bloquer : correctif chez l'éditeur)") : sévérité élevée ou critique **avec correctif disponible** ; détail dans le journal. $base."
echo
echo "$texte"
echo "Rapports JSON : $RAPPORTS"
if [[ -n "${GITHUB_STEP_SUMMARY:-}" ]]; then
  echo "$texte" >> "$GITHUB_STEP_SUMMARY"
fi
(( absentes )) && exit 3
exit "$bloquee"
