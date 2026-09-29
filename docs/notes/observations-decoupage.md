# Observations sur l'extraction et le découpage

Notes prises au fil de l'eau pour l'ADR-008 (stratégie de découpage, semaine 5).

## Semaine 1 — premier découpage structurel (2026-09-29)

Versions : `eur-lex-xhtml/1` + `ai-act/1`, `cnil-html/1` + `cnil-sections/1`. Bornes visées : 500 à 1 500 caractères.

| Document | Passages | Min | Médiane | Max | < 500 | > 1 500 |
|---|---|---|---|---|---|---|
| AI Act | 634 | 149 | 970 | 1 945 | 38 | 38 |
| CNIL — Annoter les données | 26 | 518 | 1 115 | 1 530 | 0 | 1 |
| CNIL — Informer les personnes concernées | 31 | 91 | 677 | 1 938 | 2 | 1 |

**Ce qui marche**

- AI Act : la structure ELI rend le découpage fiable. 113 articles, 180 considérants et 13 annexes, chacun dans
  ses propres passages ; un article long est coupé entre ses paragraphes numérotés (l'article 3, 68 définitions,
  donne 14 passages).
- Aucun passage ne se termine au milieu d'une phrase, sauf quand la source elle-même le fait
  (listes sans ponctuation, considérant 180 qui finit par une virgule).
- Le texte est reproduit sans modification : vérifié par les tests (concaténation des passages = texte extrait).

**Ce qui dépasse les bornes, et pourquoi on l'accepte**

- Les passages de moins de 500 caractères sont des articles ou considérants courts. On ne fusionne pas deux
  articles : un passage = une unité citable. À vérifier en semaine 1, session 5 : ces passages courts
  remontent-ils trop souvent (peu de texte, forte similarité) ?
- Au-delà de 1 500 : une phrase unique très longue (considérants 16, 22, 134), qu'on ne coupe jamais,
  ou un reste de moins de 500 caractères rattaché au passage précédent (plafond 2 000).

**Faiblesses constatées**

- CNIL : une liste plus longue que 1 500 caractères avec sa phrase d'introduction est coupée en plein milieu ;
  le passage suivant commence par un élément de liste sans son introduction (3 cas). Le titre de rattachement
  garde le contexte de section, pas celui de la phrase.
- CNIL : certains intertitres sont des paragraphes en gras (`<p><strong>`), pas des `<h3>`/`<h4>`
  (ex. « Cas particulier de la collecte de données accessibles en ligne »). L'extracteur en fait des paragraphes :
  ils peuvent finir un passage, séparés du texte qu'ils introduisent. Piste : les reconnaître comme titres
  dans `cnil-html/2`.
- AI Act : un passage qui n'est pas le premier d'un article perd la phrase d'introduction du paragraphe
  (ex. « h) l'utilisation de systèmes… » sans « 1. Les pratiques suivantes sont interdites: »).
  Piste à mesurer : répéter le titre de rattachement dans le texte vectorisé (pas dans le texte affiché).
- CNIL : la fiche « Annoter les données » contient un paragraphe en double dans la page elle-même,
  donc deux passages presque identiques. Laissé tel quel (licence : aucune modification).

## Semaine 1 — premier score de recherche (2026-09-29)

Recherche vectorielle seule, bge-m3, 10 questions (`eval/questions.json`), rang du premier bon passage
sur 10 résultats.

| Question | Texte seul | Titre + texte |
|---|---|---|
| q01 pratiques interdites (art. 5) | 1 | 1 |
| q02 définition d'un système d'IA (art. 3) | 4 | 3 |
| q03 tri de CV, haut risque (art. 6, annexe III) | 9 | 6 |
| q04 chatbot, transparence (art. 50) | 1 | 2 |
| q05 date d'application (art. 113) | 1 | 1 |
| q06 amendes (art. 99) | 1 | 1 |
| q07 « grands modèles de langage » (art. 53) | 10 | 5 |
| q08 banque, impact sur les droits (art. 27) | > 10 | 4 |
| q09 webscraping (CNIL, notice d'information) | > 10 | 1 |
| q10 fiabilité des étiquettes (CNIL, qualité de l'annotation) | > 10 | 10 |
| **Top 5** | **5/10** | **8/10** |
| **MRR** | 0,45 | 0,55 |

**Décision** : on vectorise « titre de rattachement + texte » (`Embedding:IncludeHeading`, `embedding_model = bge-m3+titre`).
Le texte stocké et affiché ne change pas. Le gain vient des cas où le mot-clé n'est que dans le titre
(q09 : « webscraping » ; q08 : « analyse d'impact… sur les droits fondamentaux ») ou dans le titre de l'article
quand le passage est une suite de liste.

**Limites de la mesure**, à garder en tête avant d'en tirer des conclusions :

- 10 questions, écrites par la même personne que le code : un écart d'une ou deux questions n'est pas significatif.
  Le jeu de 50 questions de la semaine 7 le dira mieux.
- Le passage attendu est un choix : les considérants 57 et 58 (recrutement) répondent en substance à q03
  mais ne sont pas comptés, parce qu'on attend l'article ou l'annexe qui fait foi.
- Durée de calcul non comparable entre les deux passes (1 015 ms contre 816 ms par passage) :
  la seconde a partagé le CPU avec des compilations et des tests.

**Ce que la mesure montre, pour la semaine 2**

- **Les considérants évincent les articles** (q03, q07, q08, q10 : premier résultat = un considérant).
  Ils sont rédigés en langage explicatif, proche d'une question, et représentent 28 % des passages.
  Pistes à mesurer : recherche hybride avec le plein texte (ADR-005), ou pondération des articles face aux
  considérants. Un assistant qui cite un considérant plutôt que l'article qui fait foi est moins utile.
- **Le vocabulaire de l'utilisateur n'est pas celui du texte** (q07 « grands modèles de langage » contre
  « modèles d'IA à usage général » ; q10 « étiquettes » contre « labels », « annotation »).
  La recherche plein texte n'aidera pas sur ces cas ; une reformulation de la question par le LLM, peut-être.
