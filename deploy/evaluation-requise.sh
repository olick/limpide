#!/usr/bin/env bash
# L'évaluation (deploy/evaluer.sh : ≈ 0,25 € et quelques minutes) est-elle nécessaire ? Répond « oui » si un fichier
# modifié depuis <base> peut changer les réponses ou leur mesure (liste des fichiers en cause sur la sortie d'erreur),
# « non » sinon. Sans argument (lancement à la main) : toujours « oui ».
# Liste large exprès : un oubli laisserait passer en production une modification non évaluée ; un excès ne coûte que
# quelques dizaines de centimes.
#
# Usage : deploy/evaluation-requise.sh [<commit de base>]   (ex. origin/main ; dans la chaîne : HEAD^1)
set -euo pipefail
cd "$(dirname "$0")/.."

if [[ $# -eq 0 ]]; then
  echo oui
  exit 0
fi

# Ce qui change les réponses ou leur mesure :
#   src/prompts/                  consignes du modèle et du juge
#   src/appsettings.shared.json   modèle, seuil, recherche, vectorisation
#   src/Limpide.Core/             génération (AnswerService), garde-fous, contrôle automatique
#   src/Limpide.Infrastructure/   recherche, appel aux modèles, vectorisation des questions, migrations
#   src/Limpide.Ingestion/        evaluate-answers, gate et ses seuils, juge (appsettings.json)
#   eval/questions.json           jeu d'évaluation
#   Directory.*.props, global.json, *.slnx : versions des paquets et du SDK
#   l'évaluation elle-même        deploy/evaluer.sh, ce script, son workflow
# Exclus exprès : eval/calibration/ (juge calibré à part, judge-calibrate) ; docker-compose.prod.yml, que l'évaluation
# ne lit pas (adresse de vectorisation recopiée dans evaluer.sh) : l'évaluer donnerait une fausse garantie.
MOTIF='^(src/prompts/|src/appsettings\.shared\.json$|src/Limpide\.(Core|Infrastructure|Ingestion)/|eval/questions\.json$|Directory\.[^/]*\.props$|global\.json$|[^/]*\.slnx$|deploy/(evaluer|evaluation-requise)\.sh$|\.github/workflows/evaluation\.yml$)'

en_cause="$(git diff --name-only "$1" HEAD | grep -E "$MOTIF" || true)"
if [[ -n "$en_cause" ]]; then
  echo oui
  sed 's/^/  /' <<<"$en_cause" >&2
else
  echo non
fi
