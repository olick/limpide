#!/usr/bin/env bash
# Analyse des vulnérabilités des images de Limpide publiées sur ghcr.io (S7, session 4a, ADR-015), avec Grype
# (Anchore), lancé depuis son image Docker épinglée par empreinte : rien à installer, et aucune action du catalogue.
# Trivy, l'outil le plus courant, a été écarté après la compromission de sa chaîne de publication (mars 2026,
# CVE-2026-33634 : étiquettes de trivy-action réécrites, binaire piégé, secrets de CI volés).
#
#   deploy/analyser-images.sh [<version>]     version par défaut : deploy/version-images.sh
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

VERSION="${1:-$(deploy/version-images.sh)}"
if [[ ! "$VERSION" =~ ^[0-9a-f]{12,40}$ ]]; then
  echo "Usage : deploy/analyser-images.sh [<version>] (12 à 40 caractères hexadécimaux)" >&2
  exit 2
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
for image in "${IMAGES[@]}"; do
  rapport="$RAPPORTS/$image.json"
  echo "== $REGISTRE/$image:$VERSION"
  # Version jamais publiée (publication en échec) : le dire, plutôt qu'une erreur de Grype noyée dans le journal.
  if ! docker manifest inspect "$REGISTRE/$image:$VERSION" > /dev/null 2>&1; then
    echo "ARRÊT : $REGISTRE/$image:$VERSION introuvable sur ghcr.io (version non publiée ?), rien à analyser." >&2
    exit 3
  fi
  docker run --rm --user "$(id -u):$(id -g)" \
    -v "$CACHE:/cache" -e GRYPE_DB_CACHE_DIR=/cache -e TMPDIR=/cache/tmp -e GRYPE_CHECK_FOR_APP_UPDATE=false \
    -v "$PWD/deploy/grype.yaml:/grype.yaml:ro" \
    "$GRYPE" -c /grype.yaml -o json "registry:$REGISTRE/$image:$VERSION" > "$rapport"

  compte() { jq --arg s "$1" '[.matches[] | select(.vulnerability.severity | IN($s | split(",")[]))] | length' "$rapport"; }
  corrigeables=$(jq '[.matches[] | select(.vulnerability.fix.state == "fixed")] | length' "$rapport")
  exceptions=$(jq '.ignoredMatches | length' "$rapport")
  nb_bloquantes=$(jq "$BLOQUANTES | length" "$rapport")
  if (( nb_bloquantes > 0 )); then
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

base=$(jq -r '"\(.descriptor.name) \(.descriptor.version), base du \(.descriptor.db.status.built)"' "$RAPPORTS/${IMAGES[0]}.json")
texte="### Analyse des images ($VERSION)

$resume

Bloquant : sévérité élevée ou critique **avec correctif disponible** ; détail des bloquantes dans le journal. $base."
echo
echo "$texte"
echo "Rapports JSON : $RAPPORTS"
if [[ -n "${GITHUB_STEP_SUMMARY:-}" ]]; then
  echo "$texte" >> "$GITHUB_STEP_SUMMARY"
fi
exit "$bloquee"
