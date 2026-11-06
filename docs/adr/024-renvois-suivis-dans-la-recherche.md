# ADR-024 : Suivre le renvoi de l'article 6 vers l'annexe III

- **Statut** : accepté
- **Date** : 2026-11-06

## Contexte

« Mon logiciel qui trie automatiquement les CV est-il un système à haut risque ? » (`q03`) recevait l'extrait de l'annexe
III sur la biométrie, jamais celui sur le recrutement. La réponse disait le recrutement absent de l'annexe, ou parlait
d'enseignement, et le contrôle automatique la validait : l'ancre `anx_III` accepte n'importe quel extrait de l'annexe.

La cause est l'écart de vocabulaire : le texte dit « recrutement » et « candidatures », la question dit « CV », et les
passages de l'article 6 occupent le haut du classement dès qu'une question dit « haut risque ». L'extrait utile arrive
41e sur `q03` (0,723 pour un 6e passage à 0,774), 2e si la question dit « recrutement ».

## Options mesurées

| Option | Résultat sur `q03` | Décision |
|---|---|---|
| Titre du point en tête de l'extrait | Rang 55 au lieu de 41 | Écartée |
| Extrait limité au point 4 (découpage par point) | Rang 35 : reste hors des 6 passages | Écartée, impose de tout revectoriser |
| Meilleurs extraits de l'annexe seule | Le bon extrait n'y est que 5e sur 7 | Écartée |
| Plus de passages (`PassageCount`) | Il en faudrait 41 | Écartée |
| **Toute l'annexe quand l'article 6 ou l'annexe remonte** | Réponse juste aux 4 essais et aux 3 passes | **Retenue** |

Recherche hybride : écartée par l'ADR-005, non remesurée ici ; le mot « CV » n'est pas dans le texte.

## Décision

`Search:Follow` associe une ancre trouvée à une ancre ajoutée en entier, dans l'ordre du texte, après les résultats :
`art_6` et `anx_III` vers `anx_III`. La recherche reste vectorielle (ADR-005) ; le classement ne change pas. Le mécanisme
reproduit ce que fait un lecteur : l'article 6 renvoie à l'annexe III, il va la lire. Il est déterministe et s'explique en
une phrase, ce qui compte pour une démo dont l'angle est la transparence. Le panneau affiche « renvois suivis ».

Un fait attendu sur `q03` vérifie désormais le bon extrait.

## Mesures

`rag_consolide` (empreinte `d1ab6824e180`), `answer/8`, 3 passes de part et d'autre. Infidèles : verdicts d'un modèle, pas d'un
juriste. `q03` et `q16` : phrases comptées par recherche de texte dans les réponses des rapports (`q16` peut rester infidèle
pour un autre motif). Classement : sortie d'`evaluate` avant et après, non versionnée ; il ne peut pas changer, le renvoi
s'ajoute après les résultats.

| | Sans renvoi | Avec renvoi |
|---|---|---|
| `q03` donne la bonne règle | 0/3 | **3/3** |
| `q16` risques éthiques sous l'annexe XIII | 3/3 | **0/3** |
| Infidèles sur 36 | 6, 6, 5 | 8, 5, 3 |
| Bon passage en 1re position | 23/33 | 23/33 |
| Coût par question | 0,0035 € | 0,0038 € |

`gate` passée aux trois passes. 5 questions du jeu reçoivent l'annexe (`q03`, `q12`, `q20`, `q31`, `t05`), parfois `q16`.

## Conséquences

- Environ 2 000 tokens de plus sur ces questions, 12 ou 13 passages au lieu de 6.
- **Effet de bord** : `t05` ajoute un paragraphe sur la notation sociale, hors sujet, jugé infidèle 2 fois sur 3.
- **Limite** : la table des renvois s'écrit à la main et ne couvre que ce cas. Un fait attendu ne protège que `q03` : le faux
  OK de l'ancre peut exister sur d'autres longs textes.
- `q33` n'est pas réglée : l'article 113 n'est pas fourni, et il reste à vérifier si l'article 5 y renvoie.
- Une annexe qui grandit augmente le coût : à revoir si une autre entrée s'ajoute à la table.
