# ADR-005 : Recherche vectorielle seule (l'hybride, mesurée, ne l'améliore pas) et seuil de pertinence

- **Statut** : accepté
- **Date** : 2026-10-05

## Contexte

La recherche vectorielle (bge-m3, titre + texte vectorisés) retrouve le passage attendu dans le top 5 pour
8 questions de test sur 10 (`eval/questions.json`, mesure du 2026-09-29). Deux faiblesses constatées en semaine 1 :
les considérants passent souvent devant l'article qui fait foi, et certains mots-clés précis
(« webscraping », « haut risque ») ne pèsent pas assez.

La recherche hybride (vectorielle + plein texte, fusion des rangs) est la réponse habituelle à ces faiblesses.
Le plan prévoyait de ne la garder que si elle améliore la mesure.

## Options envisagées

1. **Vectorielle seule** (en place).
2. **Hybride, fusion RRF** (Reciprocal Rank Fusion) : chaque passage reçoit Σ 1 / (60 + rang) sur les deux
   classements, 40 candidats par recherche, le tout en une requête SQL. Côté plein texte :
   - une question en langage naturel ne contient presque jamais tous ses mots dans un passage : en « ET »
     (`websearch_to_tsquery`), la première question de test ne trouve **aucun** passage ; on cherche donc en « OU » ;
   - PostgreSQL ne pondère pas les mots par leur rareté (pas de BM25 natif) : en « OU », « ia » (77 % des passages)
     pèse autant qu'« interdit » (3 %). Chaque mot trouvé compte donc ln(passages / passages qui le contiennent),
     calculé avec `ts_stat` (8 à 16 ms sur 691 passages) ;
   - les mots interrogatifs (quel, comment, doit…) ne sont pas des mots vides pour la configuration `french` ;
     rares dans le corpus, ils dominaient le classement : on les écarte (liste grammaticale, non réglée sur le jeu).
3. **Pondérer les articles face aux considérants** : non mesuré ; règle propre au corpus, à garder en réserve.

## Mesures

`evaluate`, 10 questions, rang du premier bon passage sur 10 résultats.

| Question | Vectorielle | Hybride (texte) | Hybride (titre + texte) |
|---|---|---|---|
| q01 pratiques interdites | 1 | 2 | 1 |
| q02 définition d'un système d'IA | 3 | > 10 | > 10 |
| q03 tri de CV, haut risque | 6 | **1** | **1** |
| q04 chatbot, transparence | 2 | 9 | 8 |
| q05 date d'application | 1 | 2 | 2 |
| q06 amendes | 1 | 2 | 2 |
| q07 « grands modèles de langage » | 5 | > 10 | > 10 |
| q08 banque, impact sur les droits | 4 | 4 | 4 |
| q09 webscraping (CNIL) | 1 | 7 | 1 |
| q10 fiabilité des étiquettes (CNIL) | 10 | 5 | 6 |
| **1er résultat** | **4/10** | 1/10 | 3/10 |
| **Top 5** | **8/10** | 6/10 | 6/10 |
| **MRR** | **0,55** | 0,32 | 0,45 |

La variante « titre + texte » côté plein texte est le pendant de ce qui a amélioré la vectorielle en semaine 1 ;
c'est le seul réglage essayé après la première mesure, pour ne pas ajuster la recherche au jeu de test.

## Décision

Recherche **vectorielle seule** (`Search:Strategy = Vector`). L'hybride (variante titre + texte) reste dans le
code, activable par configuration, pour être remesurée.

## Conséquences

- Pourquoi l'hybride perd ici : le plein texte, même pondéré, fait remonter des passages qui partagent des mots
  avec la question sans en partager le sens (q02, q04, q07) ; la fusion rétrograde alors des articles que la
  vectorielle plaçait en tête. Elle n'aide que quand un terme précis et rare porte la question (q03).
- La faiblesse « considérants avant articles » reste entière : l'hybride ne la corrige pas (q05, q06 : un
  considérant passe devant l'article). Piste suivante, si elle se confirme sur un jeu plus large : pondérer les
  articles, ou afficher l'article lié à un considérant.
- Le vocabulaire des utilisateurs (q07) n'est pas un problème de recherche lexicale : piste de reformulation
  de la question par le LLM, à mesurer plus tard (coût et latence en plus).
- **Limite de la mesure** : 10 questions, écrites par la même personne que le code. Un écart de 2 questions
  dans le top 5 reste fragile ; l'écart de MRR (0,55 contre 0,45) va dans le même sens.
- **Condition de révision** : remesurer en semaine 7 sur le jeu de 50 questions ; si l'hybride y fait mieux,
  la configuration suffit à basculer. À plus grande échelle, `ts_stat` à chaque question deviendrait coûteux :
  il faudrait précalculer la fréquence des mots à l'ingestion.

## Complément : seuil de pertinence (même session)

Si même le meilleur passage est peu proche de la question, on répond « je ne sais pas » **sans appeler le LLM** :
coût nul, aucun risque d'invention. Le seuil porte sur la similarité cosinus du meilleur passage (`RetrievedPassage.Score`),
quelle que soit la stratégie de recherche.

Calibrage (bge-m3+titre, 2026-10-05) — meilleur score de la recherche :

| Questions | Meilleur score |
|---|---|
| 10 questions légitimes (`eval/questions.json`) | 0,541 à 0,705 |
| 4 hors sujet (VPN, météo, football, recette) | 0,305 à 0,427 |
| Dans le domaine, sans réponse : amende CNIL contre Google, prix de ChatGPT Plus | 0,454 ; 0,338 |
| Dans le domaine, sans réponse : coût d'une certification ISO 42001 | 0,541 |
| Dans le domaine, sans réponse : règles sur l'IA aux États-Unis | 0,655 |

**Décision** : `Generation:MinScore = 0,50`. Écarte tout le hors sujet (marge 0,07) et garde toutes les questions
légitimes (marge 0,04, mince).

**Conséquences**

- Deux lignes de défense : le seuil écarte le hors sujet (déterministe, gratuit) ; les questions proches du sujet
  mais sans réponse dans le corpus (ISO 42001, États-Unis) passent le seuil, et c'est la consigne du LLM qui doit
  répondre « je ne sais pas ». Vérifié sur ces deux questions avec Mistral Small.
- Pas de classifieur « hors périmètre » par LLM : le seuil suffit sur les exemples mesurés, un appel de plus coûterait
  sans gain démontré.
- Le seuil dépend du modèle d'embedding et du texte vectorisé : à recalibrer si l'un change (ex. passage à une API
  d'embeddings : ADR-006, puis l'ADR sur l'hébergement des modèles en S6). À recalibrer aussi en semaine 7 sur un jeu plus large ; la marge de 0,04 côté
  questions légitimes peut se révéler trop étroite (une question légitime mal formulée serait refusée).
