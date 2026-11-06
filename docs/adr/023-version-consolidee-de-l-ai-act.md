# ADR-023 : AI Act servi dans sa version consolidée, sans considérants

- **Statut** : accepté
- **Date** : 2026-11-05

## Contexte

Le corpus servait le texte d'origine de l'AI Act, publié au Journal officiel le 12 juillet 2024 (CELEX 32024R1689).
Deux textes l'ont modifié depuis :

- un **rectificatif** (JO du 9 octobre 2025) ;
- l'**Omnibus numérique sur l'IA** (règlement (UE) 2026/1744, JO du 24 juillet 2026, en vigueur le 27 juillet 2026).
  Il reporte les obligations des systèmes à haut risque (annexe III : 2 décembre 2027 ; annexe I : 2 août 2028),
  réécrit l'article 4 (maîtrise de l'IA : « prennent des mesures pour favoriser », au lieu d'une obligation de
  résultat), ajoute les articles 4 bis, 60 bis, 75 bis à 75 quinquies et l'annexe XIV, et modifie au moins 33 autres
  articles ou annexes (41 au total, nouveaux compris : décompte des articles dont le texte change de plus de 3 %).

Conséquence constatée le 2026-11-04 : la démo citait, à l'identique et avec sa source, un article 113 qui n'est plus
le droit en vigueur (« haut risque au 2 août 2026 »). L'évaluation ne le voyait pas : la question `q05` vérifie que
l'article 113 est cité, pas la date donnée.

## Options envisagées

1. **Ajouter le règlement 2026/1744 comme second document.** Un règlement modificatif se lit mal (« au point c), les
   termes … sont remplacés par … ») : le modèle devrait recomposer le texte en vigueur, et l'extrait affiché ne
   serait le texte d'aucun article.
2. **Servir la version consolidée** (CELEX 02024R1689-20260727), qui intègre les modifications. Elle n'est qu'un
   « outil de documentation » sans effet juridique (avertissement porté par le texte lui-même), et **elle ne reprend
   pas les considérants**, comme toute consolidation d'EUR-Lex.
3. **Version consolidée, plus les considérants du texte d'origine** dans un document à part. Plus de code (source
   ou découpage dédiés) ; et des considérants de 2024 qui décrivent parfois l'ancien texte (article 4), à côté du
   texte modifié.

## Décision

Option 2, sans considérants (choix d'Alexandre, 2026-11-05, après mesure).

- **Nouvel extracteur** `eur-lex-consolide/1` : le format HTML d'une consolidation n'a rien de commun avec celui du
  Journal officiel (paragraphes sans identifiant, énumérations en grilles, marqueurs « ▼M1 » à retirer, article
  « art_4a »). L'extracteur est choisi **selon le format du fichier**, pas selon la source : changer l'un ne relance
  ni l'extraction, ni le découpage, ni la vectorisation de la version publiée de l'autre. Les ancres de paragraphe sont
  reconstituées sur le schéma du Journal officiel (« 005.001 ») : le découpeur `ai-act/1` sert tel quel, et découpe
  à l'identique les 92 articles et annexes que l'Omnibus n'a pas modifiés (vérifié passage par passage).
  Seule retouche du découpeur, sans effet sur le texte d’origine (634 passages identiques au caractère près) : il accepte
  « art_4a ». Sa version n'est donc pas incrémentée, ce qui aurait supprimé les vecteurs de la version publiée.
- **Renommage du document** (`previousUrls` dans `corpus.json`) : l'URL identifie un document et sert de lien sous
  chaque extrait. La nouvelle URL est celle de la version consolidée datée (le lien montre exactement le texte cité),
  et le document garde ses versions au lieu d'en créer un second, qui laisserait l'ancien en service (il n'existe
  pas de retrait de document). À chaque nouvelle consolidation : nouvelle date dans `url`, `fetchUrl` et `title`,
  ancienne URL ajoutée à `previousUrls`.
- **Avertissement** repris de la version consolidée dans les conditions de réutilisation (sous chaque extrait) et
  dans les mentions légales ; le lien de la carte garde le libellé « Texte officiel »,
  avec un nom accessible propre à chaque document.

## Mesures (2026-11-05, copie de la base locale `rag_consolide`)

| | Journal officiel 2024 (634 passages, dont 250 de considérants) | Version consolidée (429 passages) |
|---|---|---|
| `evaluate` : bon passage en 1re position | 17/30 | **21/30** |
| `evaluate` : dans le top 5 | 28/30 | **29/30** |
| `evaluate` : MRR | 0,72 | **0,80** |
| `evaluate-answers` : vérifiées automatiquement | 47/55 (rapport du 2026-10-28) | **49/55** |
| Échecs | 3 | **1** |
| Passage attendu cité | 28/30 | 28/30 |
| Réponses jugées infidèles | 7/34 | 8/33 |

Barrière `gate` : passée (rapport `eval/results/2026-11-05-2009-mistral-medium-2604`). Vectorisation de la candidate :
120 950 tokens, 0,012 €. `q05` donne désormais les dates de l'Omnibus. Les considérants devançaient souvent le bon
article dans la recherche (`q15`, `q16`, `q18`, `q22`) : sans eux, l'article remonte en tête.

**Régression** : `q06` (montant maximal des amendes) échoue (citation inventée, signalée par le garde-fou) : le passage
« 35 000 000 EUR » n'est plus parmi les 5 fournis. Il n'a pas changé (633 caractères, paragraphes 2 et 3 de l'article
99) ; il commence par l'information de la Commission, et d'autres passages de l'article 99 le devancent. Le modèle écrit
le montant de mémoire (constat du 2026-11-06, ADR-014, « Faits attendus »). Point ouvert, pas une raison de garder un
texte périmé.

## Conséquences

- La première collecte met la version consolidée **en quarantaine** (32 % de passages en moins, seuil 30 %) : levée
  par `release` après vérification, comme prévu par le cycle de vie (ADR-008). Entre `fetch` et `publish`, le titre et
  le lien affichés sont déjà ceux de la version consolidée alors que le texte servi est encore celui de 2024 :
  enchaîner `release`, `embed` et `publish` sans attendre.
- Après la publication en production : `deploy/exporter-corpus.sh --vps`, pour que la chaîne évalue le nouveau
  corpus (ADR-016).
- Une consolidation suit toujours le texte modificatif avec quelques semaines de retard ; la veille (repérer un acte
  modificatif) reste manuelle.
- L'évaluation ne contrôle pas le contenu des réponses (une date, un montant) : à ajouter au jeu (`q05`, article 4).
