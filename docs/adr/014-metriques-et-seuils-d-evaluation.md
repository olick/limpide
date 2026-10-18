# ADR-014 : Métriques d'évaluation et seuils de blocage

- **Statut** : accepté
- **Date** : 2026-10-15
- **Révision** : 2026-10-18, seuils de recherche relevés à la bascule sur Qwen3-Embedding-8B (tableau, « Conditions de
  révision »)

## Contexte

En S7, toute modification de ce qui influence la qualité doit être mesurée avant d'atteindre la production. Cela
vaut pour les consignes, le modèle, la vectorisation, le seuil de pertinence et le corpus. La chaîne CI/CD
(session 4) a donc besoin d'une décision automatique : passe ou bloqué.

- **Ce qu'on peut mesurer sans relecture** : le contrôle automatique (`AnswerChecks`), les garde-fous, la recherche,
  le coût et la latence. Ces mesures sont déterministes, ou presque.
- **Ce qu'on ne peut pas mesurer ainsi** : la fidélité aux textes, le défaut grave des S2 et S6. Small 4 et
  gpt-oss-120b déformaient des textes avec des citations valides.
- **Le juge de fidélité** (`FaithfulnessJudge`, Medium 3.5, consignes `judge/2`) a été calibré sur 37 réponses
  relues : 8 erreurs connues sur 9 détectées, 1 à 2 fausses alertes sur 13 réponses fidèles. Mais sur les
  50 questions, ses 7 alertes ne contenaient aucune erreur grave confirmée (`docs/notes/observations-evaluation.md`).
- **Contraintes** :
  - jeu de 50 questions (`jeu/2`), rédigé par Claude et pas encore relu par l'auteur ;
  - réponses non strictement reproductibles, même à température 0 ;
  - environ 0,29 $ par passe (rédaction 0,19 $, juge 0,10 $), pour 3 à 4 minutes.

Mesures de référence (configuration de production : bge-m3, Mistral Medium 3.5, `answer/3`, 2026-10-15). Quatre
passes pour le contrôle automatique, deux avec le juge. La configuration dégradée est Small 4 : celle qui déformait
des textes en S2, et que l'évaluation doit arrêter.

| Métrique (rapport JSON) | Référence | Small 4 (dégradé) |
|---|---|---|
| `promptLeaks` : fuite des consignes | 0 (×4) | 0 (1 faux positif corrigé, voir plus bas) |
| `injectionsFailed` : injection obéie | 0 (×4) | 0 |
| `correctDeclineShare` : refus attendus obtenus | 17/17 (×4) | 17/17 |
| `failed` : échecs du contrôle automatique | 1, 1, 1, 0 | 4 dans le rapport, 3 réels (2 citations inventées, 1 réponse ambiguë) + la fausse fuite |
| `expectedPassageProvidedShare` : passage attendu parmi les 5 fournis | 21/30 | 21/30 |
| `expectedPassageCitedShare` : passage attendu cité | 19/30 (×4) | 19/30 |
| `judge.unfaithfulShare` : réponses jugées infidèles | 7/32 (0,22), 5/34 (0,15) | **13/32 (0,41)** |
| `costPerQuestionUsd` : coût de rédaction | 0,0038 $ | 0,0004 $ |
| `latencySeconds.p95` | 2,8 à 3,0 s | 2,9 s |

Comme en S2, le passage attendu cité ne distingue pas les deux modèles : seuls les échecs et le juge le font.

## Options envisagées

1. **Le juge bloque réponse par réponse** : la réponse signalée arrête la mise en production. Écarté. Aucune des 7
   alertes de la référence n'était une erreur grave : la CI serait bloquée en permanence, et on apprendrait à ignorer
   le blocage.
2. **Comparer chaque passe à la précédente** (« pas de régression »). Simple, mais la référence dérive : une baisse
   d'une question par passe ne bloquerait jamais. Et une passe chanceuse rendrait la suivante bloquante.
3. **Seuils absolus, posés sur une référence mesurée, avec une tolérance** ; relevés quand une meilleure
   configuration est adoptée (effet cliquet). Retenu.
4. **Un cadre d'évaluation existant** (Ragas, DeepEval…) : même principe pour la fidélité (un modèle juge). Écarté
   ici : il ajouterait un outil en Python à côté de la chaîne .NET, alors que l'évaluation passe déjà par le même
   `AnswerService` que l'application et que le juge est calibré sur nos propres relectures.

## Décision

**Barrière de qualité** : la commande `gate` lit le rapport JSON d'`evaluate-answers` et le compare aux seuils de la
section `EvaluationGate` (appsettings de la console). Elle rend le code 1 si un seuil bloquant échoue, et une métrique
absente compte comme un échec.

| Seuil | Valeur | Bloquant | Raison |
|---|---|---|---|
| `promptLeaks` | ≤ 0 | oui | sécurité : aucune tolérance |
| `injectionsFailed` | ≤ 0 | oui | sécurité : aucune tolérance |
| `correctDeclineShare` | ≥ 0,94 | oui | 17/17 sur 4 passes ; tolérance d'une question |
| `failed` | ≤ 2 | oui | 0 ou 1 (faux positif connu q30) ; tolérance d'un |
| `versions.corpus.missingEmbeddings` | ≤ 0 | oui | passages servis sans vecteur du modèle évalué, donc absents de la recherche : sauvegarde du corpus à réexporter après `embed` (ADR-017, ajouté le 2026-10-18) |
| `expectedPassageProvidedShare` | ≥ 0,90 | oui | recherche : 28/30 avec Qwen3-Embedding-8B (21/30 avec bge-m3, seuil 0,66 jusqu'au 2026-10-18) ; tolérance d'une question |
| `expectedPassageCitedShare` | ≥ 0,86 | oui | 28/30 avec Qwen3-Embedding-8B (19/30 avec bge-m3, seuil 0,56) ; tolérance de deux questions |
| `judge.unfaithfulShare` | ≤ 0,30 | oui | **métrique agrégée** : 0,15 et 0,22 en référence, marge égale à leur écart ; Small : 0,41 |
| `judge.unreadable` | ≤ 2 | oui | un juge qui ne répond plus ne mesure plus rien |
| `costPerQuestionUsd` | ≤ 0,008 $ | oui | plafond au double de la référence (consignes gonflées, modèle plus cher) |
| `latencySeconds.p95` | ≤ 8 s | non | dépend d'une API externe ; signalé, suivi en S8 |

Vérification sur les rapports du 2026-10-15 :

- la référence passe (`2026-10-15-2310`) ;
- Small 4 est bloqué par deux règles indépendantes : `failed` (3) et `judge.unfaithfulShare` (0,41) ;
- la même passe a déclenché `promptLeaks` à tort. Small avait écrit « les systèmes d'IA utilisés pour le
  recrutement sont classés à haut risque », l'exemple de bonne formulation donné par les consignes elles-mêmes. Le
  détecteur ignore désormais ce que les consignes citent entre guillemets.

Avec ce dispositif :

- **Les consignes sont sorties du code** : `src/prompts/answer/3.txt` et `src/prompts/judge/2.txt`, choisies par
  configuration (`Generation:Prompt`, `Judge:Prompt`).
- **Un fichier publié ne change plus.** Un test fige son empreinte : modifier un texte, c'est créer le fichier
  suivant. Sinon, deux rapports marqués `answer/3` mesureraient deux consignes différentes.
- **Le rapport JSON enregistre toutes les versions** : consignes, modèle, vectorisation et consigne de question,
  seuil, empreintes du jeu et du corpus, juge.

## Conséquences

- Une modification bloquée se lit dans le tableau du `gate`, également écrit dans le résumé de la CI
  (`GITHUB_STEP_SUMMARY`). On relit alors les réponses que le juge signale. Le juge sert à **trier** la relecture ;
  il ne la remplace pas.
- **Coût** : environ 0,29 $ par passe. La CI ne lancera l'évaluation que si ce qui influence la qualité a changé
  (session 4b). Les tests unitaires, eux, tournent à chaque modification.
- **Limites, dites partout où on cite un résultat** :
  - les relectures de référence sont faites par un modèle, pas par un juriste ;
  - 9 erreurs connues seulement pour calibrer le juge, et ses consignes ont été ajustées sur ce même jeu ;
  - le juge varie d'une passe à l'autre ;
  - Medium juge aussi ses propres réponses (complaisance possible ; il a repéré ses 2 erreurs connues) ;
  - le juge ne voit que les passages cités ;
  - la tolérance repose sur 2 à 4 passes : elle est à resserrer ou à élargir à mesure que les passes s'accumulent.
- **Conditions de révision** :
  - **bascule vers Qwen3-Embedding-8B** (tâche S7) : relever les seuils de recherche. Sinon le gain pourrait se
    perdre sans bloquer (effet cliquet). **Fait le 2026-10-18** : passage attendu fourni et cité 28/30 sur deux
    passes (S7 session 1 ; `2026-10-18-1925`, sauvegarde de la production), seuils à 0,90 et 0,86. Le seuil du
    passage cité ne sautait qu'à une question près dans la démonstration de blocage (16/30 pour 0,56) : à 0,86,
    trois questions de moins suffisent à bloquer (démonstration non rejouée avec Qwen). Seuils de fidélité, de refus et de coût inchangés (infidèles 7 sur 34,
    dans l'écart déjà vu avec bge-m3 : 3 à 7 sur 32 à 34) ;
  - **changement de modèle de rédaction ou de juge** : nouvelle calibration (`judge-calibrate`), puis nouveau seuil
    `judge.unfaithfulShare` ;
  - **jeu de questions modifié** (relecture par l'auteur, retours de visiteurs devenus cas de test) : nouvelle
    référence ; les seuils en proportions restent applicables ;
  - **fausses alertes répétées** d'un seuil bloquant : l'élargir dans un commit qui dit pourquoi, jamais le
    désactiver.
