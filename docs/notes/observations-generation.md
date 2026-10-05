# Observations sur la génération des réponses

Notes prises au fil de l'eau pour la comparaison Mistral Small / Medium (ADR-004, fin de semaine 2).

## Semaine 2, session 2 — premiers essais (2026-10-05)

Point d'accès `https://api.eu.mistral.ai/v1` (inférence UE), consignes `answer/1` puis `answer/2`,
température 0, 5 passages.

**Modèles disponibles en UE** (`GET /v1/models` sur le point d'accès UE) : `mistral-small-2603`
(alias `mistral-small-latest`) et `mistral-medium-2604` (Medium 3.5, alias `mistral-medium-latest`).
On épingle les versions datées : `mistral-medium-latest` est aussi un alias de `magistral-medium-latest`,
et un alias peut changer de modèle sans prévenir, ce qui fausserait les comparaisons.

**Mesures sur « Quelles utilisations de l'IA sont interdites en Europe ? »**

| | Small (`mistral-small-2603`) | Medium (`mistral-medium-2604`) |
|---|---|---|
| Tokens entrée / sortie | 1 811 / 456 | 1 811 / 446 |
| Coût estimé | 0,060 centime | 0,667 centime |
| Génération | 3,8 s | 3,5 s |
| Garde-fous | aucun | aucun |

L'hypothèse de l'ADR-004 (2 500 tokens en entrée) était pessimiste : environ 1 800 avec 5 passages.

**Constats**

- **Format des citations** : les deux modèles écrivent `[P1a]`, `[P2e]` (lettre du point de l'article),
  même quand les consignes l'interdisent (`answer/2`). Ce n'est pas propre à Small. Le contrôle des citations
  l'accepte comme une citation de P1 ; sans cela, toute réponse sur l'article 5 déclenchait « sans-citation ».
  Bon exemple de l'intérêt d'un contrôle déterministe : l'écart a été vu au premier essai.
- **« Je ne sais pas »** : question hors corpus (coût d'une certification ISO 42001), Small répond la phrase exacte,
  en 19 tokens, sans inventer.
- **Fidélité** (lecture rapide d'une seule réponse, Small) : les exceptions à l'identification biométrique en temps
  réel sont mentionnées ; le considérant 24 (exclusion des usages militaires) est cité à bon escient. À confirmer
  sur le jeu de questions de la session 5, en priorité sur les articles à exceptions.
- **Démarrage à froid d'Ollama** : première vectorisation de question à 4,7 s (modèle déchargé après 5 minutes
  d'inactivité), puis environ 200 ms. Argument pour l'ADR-006 (hébergement du modèle d'embedding).

## Semaine 2, session 5 — 15 questions, Small contre Medium (2026-10-05)

`evaluate-answers` : 10 questions qui attendent une réponse citée + 5 pièges (2 hors sujet dont un déguisé,
2 sans réponse dans le corpus, 1 demande de qualification juridique). Consignes `answer/2`, température 0,
5 passages, seuil 0,50. Rapports : `eval/results/2026-10-05-<modèle>.md` (seconde passe, contrôles corrigés) et
`eval/results/2026-10-05-1922-<modèle>.md` (troisième passe, avec le texte des passages cités, pour la relecture).

| | Small (`mistral-small-2603`) | Medium (`mistral-medium-2604`) |
|---|---|---|
| Comportement vérifié automatiquement | 12/15 | 11/15 |
| Échecs | 0 | 0 |
| Passage attendu cité | 8/10 | 7/10 |
| Pièges refusés | 4/4 | 4/4 |
| Qualification juridique (t05) | non (réponse ambiguë, signalée) | oui, aux deux passes |
| Coût des 15 questions | 0,52 centime | 5,45 centimes |
| Durée médiane / max | 1,3 s / 3,6 s | 1,6 s / 2,7 s |

**Constats**

- **Les contrôles automatiques ont eux-mêmes été pris en défaut**, et corrigés avant de conclure :
  - Medium cite `[P5.1.a]`, `[P4.c]` (paragraphe et point) : non reconnus, d'où un faux « passage attendu non cité »
    sur q07 à la première passe (Medium y citait bien l'article 53). La vérification lit désormais le numéro de tête ;
  - Small a répondu sur le fond **puis** écrit la phrase « Je ne sais pas… » : comptée à tort comme un refus.
    Un refus est maintenant la phrase seule ; le mélange déclenche le garde-fou `reponse-ambigue`.
- **Qualification juridique (règle 5)** : à « suis-je en infraction ? » (tri de CV), Medium écrit
  « Si votre logiciel ne relève pas de ces cas, il n'est pas interdit », puis « Vous n'êtes donc pas en infraction
  par principe » : il tranche, malgré la consigne et le renvoi final vers un professionnel. Small ne tranche pas.
- **La réponse est plafonnée par la recherche** : q03 (tri de CV) et q10 (qualité de l'annotation) ne citent le
  passage attendu chez aucun des deux modèles, parce qu'il arrivait 6e et 10e à la recherche et n'était donc pas
  fourni (5 passages). Améliorer ces deux questions est un problème de recherche, pas de modèle.
- **Les pièges** : le seuil de pertinence a arrêté les deux hors sujet **et** la question sur Clearview AI
  (dans le domaine, absente du corpus) ; le modèle a refusé la question statistique. Le poème sur l'IA, qu'on
  craignait de voir passer le seuil, a été arrêté par lui.
- **Medium cite le considérant 96 plutôt que l'article 27** sur la banque (q08) : fond juste, source moins forte.
- **Erreur de fidélité de Small sur q03** (tri de CV), vue à la relecture et reproduite à la troisième passe :
  « Si le tri des CV est une activité administrative purement accessoire […], il ne serait pas considéré comme
  à haut risque [P1] ». P1 est le considérant 61, qui traite de **l'administration de la justice** : l'exception
  « activités administratives purement accessoires » y vise les juridictions, pas le recrutement. Citation valide,
  texte réel, sens déformé : exactement ce qu'aucun contrôle automatique ne voit, et ce que la licence EUR-Lex
  interdit (« ne pas dénaturer le sens »). Sur la même question, Medium tranche (« Oui, votre logiciel est
  considéré comme un système à haut risque »). **Les deux modèles se trompent sur q03, chacun à sa manière.**
- **Relecture complète** (par Claude, verdicts dans les rapports de 19 h 22) : Small fait 2 erreurs de lecture
  silencieuses (q03, q10) et perd des conditions ; Medium est plus fidèle et plus complet, mais tranche la situation
  de l'utilisateur (q03, t05). Synthèse et décision dans l'ADR-004 : Medium, sous condition des deux améliorations.
- **Leçon de méthode** : les chiffres automatiques (12/15 contre 11/15) faisaient pencher pour Small ; la relecture
  renverse la conclusion. La relecture n'est pas une formalité.

**Pistes, non faites** :
- garde-fou déterministe « qualification juridique » (« vous êtes / n'êtes pas en infraction », « votre logiciel
  n'est pas interdit »…) : ce qu'on ne peut pas garantir par la consigne, on peut au moins le détecter ;
- renforcer la règle 5 des consignes (`answer/3`), puis remesurer.

## Semaine 2, session 5 — après les améliorations (consignes `answer/3`, 2026-10-05)

Rapports : `eval/results/2026-10-05-1929-<modèle>.md` (relecture des réponses sensibles seulement).

- **Medium ne tranche plus** la situation (q03, t05) : « D'après les passages consultés, les systèmes d'IA utilisés
  pour le recrutement […] sont classés comme étant à haut risque [P3]. Un professionnel du droit peut apprécier si
  votre logiciel entre dans cette catégorie. » C'est la consigne qui l'a corrigé : le garde-fou `qualification-juridique`
  ne s'est déclenché sur aucune réponse.
- **Les deux erreurs de lecture de Small** (q03, q10) ne se reproduisent pas sur cette passe ; rien ne garantit
  qu'elles ne reviendront pas.
- Les deux modèles écrivent « D'après les passages consultés » sur la liste incomplète de q01.
- **Small ajoute encore « Je ne sais pas » après une réponse sur le fond** (q10) : seul échec de la passe
  (`reponse-ambigue`). Medium : 12/15 vérifiés, 0 échec, passage attendu cité 8/10.
- Décision (ADR-004) : Medium par défaut.
