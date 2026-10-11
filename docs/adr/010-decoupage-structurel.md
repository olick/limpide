# ADR-010 : Découpage structurel (article, considérant, section) plutôt qu'à taille fixe

- **Statut** : accepté
- **Date** : 2026-10-11

## Contexte

L'assistant ne lit pas un texte entier à chaque question : la recherche lui fournit 5 passages. Le découpage décide de
ce qu'un passage contient, donc de ce que la recherche peut trouver et de ce que la réponse peut citer.

Contraintes propres au projet :

- **Citer l'unité qui fait foi** : « article 27 », « considérant 96 », une section de fiche CNIL — pas « caractères
  48 000 à 49 000 ». Chaque extrait cité affiche son titre de rattachement et sa source.
- **Licences** : textes reproduits à l'identique (CNIL : CC-BY-ND, aucune modification ; EUR-Lex : ne pas dénaturer).
- **Structure disponible** : l'AI Act est balisé (ancres ELI : `art_5`, `005.001`, `rct_12`, `anx_III`) ; les pages
  CNIL ont des intertitres.

## Options envisagées

1. **Taille fixe** (≈ 1 000 caractères, 200 de chevauchement, sans couper un mot) : la méthode la plus répandue ;
   simple, indépendante de la source ; coupe au milieu d'un article, mélange deux articles dans un passage.
2. **Structurel** : un passage par article, considérant, annexe (AI Act) ou section (CNIL), redécoupé entre paragraphes
   s'il dépasse 1 500 caractères (`Packer` : 500 à 1 500, jamais au milieu d'une phrase).
3. **Sémantique** (couper là où le sens change, par embeddings) : plus coûteux, non déterministe à l'échelle du
   projet, et la structure explicite des textes juridiques fait déjà ce travail.

## Mesure (2026-10-11)

Même corpus (AI Act + 17 pages CNIL), même modèle (bge-m3, titre + texte vectorisés), mêmes 10 questions
(`eval/questions.json`, rang du premier bon passage). Taille fixe mesurée sur une copie de la base
(`--Ingestion:ChunkingStrategy=FixedSize`) ; pour une comparaison loyale, seule la coupe change : titre de
rattachement et ancre sont ceux de l'endroit où commence le passage, calculés comme par les découpeurs structurels.

| Découpage | 1re position | Top 5 | MRR | Passages | Vectorisation |
|---|---|---|---|---|---|
| **Structurel** | **4/10** | **7/10** | **0,52** | 1 130 | — |
| Taille fixe 1 000 / 200 | 2/10 | 5/10 | 0,37 | 1 362 (+ 20 %) | 23 min sur CPU |

Rang du bon passage, structurel → taille fixe : q01 1 → 2, q02 4 → 10, q03 8 → 7, q04 2 → hors des 10, q05 1 → 2,
q06 1 → 1, q07 5 → 2, q08 9 → hors des 10, q09 1 → 1, q10 hors des 10 dans les deux cas. La taille fixe gagne sur
deux questions (q03, q07), en perd six.

**Ce que la mesure dit, et ses limites** :

- Un passage à taille fixe qui commence à la fin de l'article 26 et contient le début de l'article 27 est rattaché à
  l'article 26 : la recherche le compte comme un échec pour une question sur l'article 27. C'est en partie un effet de
  la mesure, mais aussi un vrai défaut pour l'utilisateur : l'extrait serait cité sous le mauvais titre.
- 10 questions écrites par l'auteur : un écart de 2 questions dans le top 5 reste fragile ; l'écart va dans le même sens
  sur les trois indicateurs, et rejoint la mesure de la semaine 1 (le titre de rattachement vectorisé avec le texte
  faisait passer le top 5 de 5/10 à 8/10 : le rattachement à la bonne unité compte).
- Le chevauchement augmente de 20 % le nombre de passages à vectoriser et à stocker.

## Décision

**Découpage structurel**, par source : `ai-act/1` (article, considérant, annexe, préambule) et `cnil-sections/1`
(section, chemin des intertitres en titre de rattachement), avec le `Packer` commun. Le découpage à taille fixe reste
dans le code (`FixedSizeChunker`), activable par configuration, pour être remesuré.

## Conséquences

- Chaque passage cite une unité réelle du texte ; « article 5 » dans l'interface correspond au texte de l'article 5.
- Un découpeur par type de source : une nouvelle source sans structure exploitable (PDF sans balises, par exemple)
  demandera son découpeur ; la taille fixe peut servir de solution de repli, mesurée.
- **Faiblesses connues** (`docs/notes/observations-decoupage.md`) : la suite d'une longue liste perd sa phrase
  d'introduction ; les considérants passent souvent devant l'article qui fait foi ; les passages courts (articles
  brefs, sections d'un paragraphe) sont gardés tels quels, une unité citable ne se fusionne pas avec la suivante.
- Changer un découpeur impose d'incrémenter sa version : la version publiée est redécoupée et revectorisée sur place
  (limite de l'ADR-008).
- **Conditions de révision** : le jeu de 50 questions de la S7 inverse l'écart ; une source sans structure entre dans
  le corpus ; ou une piste mesurée corrige les faiblesses connues (répéter l'introduction d'une liste dans le texte
  vectorisé, pondérer les articles face aux considérants).
