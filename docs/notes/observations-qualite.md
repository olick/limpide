# Observations sur les contrôles qualité

Calibrage des seuils de `validate` (S5, session 2), matière de l'ADR à venir sur la qualité des données si elle évolue.
Code : `src/Limpide.Core/Quality/QualityValidator.cs` ; seuils : section `Quality` de
`src/Limpide.Ingestion/appsettings.json`.

## Calibrage (2026-10-10)

Mesuré sur les trois documents publiés, puis comparé à un contre-exemple quand c'était possible. Chaque seuil laisse
une marge des deux côtés : il ne doit pas bloquer une version saine, et doit bloquer la panne qu'il vise.

| Contrôle | Seuil | AI Act | CNIL Annoter | CNIL Informer | Contre-exemple |
|---|---|---|---|---|---|
| `texte-minimal` | ≥ 2 000 caractères | 640 907 | 26 798 | 27 404 | page d'erreur : quelques centaines |
| `langue-francaise` | ≥ 0,20 de mots outils français | 0,424 | 0,432 | 0,420 | texte anglais (`LICENSE`) : 0 |
| `nombre-de-passages` | écart ≤ 30 % avec la version publiée | 0 | 0 | 0 | — |
| `longueur-des-passages` | ≤ 20 % hors de 500 à 1 500 caractères | 12 % (76/634) | 3,8 % (1/26) | 9,7 % (3/31) | — |
| `doublons` | ≤ 5 % de passages identiques | 0 | 0 | 0 | — |
| `structure` | AI Act ≥ 100 articles ; CNIL ≥ 4 intertitres | 113 | 8 | 17 | — |
| `residus-html` | 0 passage | 0 | 0 | 0 | — |
| `texte-d-interface` | 0 passage | 0 | 0 | 0 | — |

**Langue** : part des mots outils (le, de, et, que…) parmi tous les mots ; aucune bibliothèque, explicable en une
phrase. Les deux langues sont nettement séparées (≈ 0,42 contre 0) ; le seuil à 0,20 tolère un texte mêlant citations
anglaises et français. Limite : ne distingue pas le français d'une autre langue romane proche sans mots outils communs
(rare pour nos sources).

**Texte d'interface** : expressions précises (« accepter les cookies », « aller au contenu », « partager sur »…),
jamais le mot « cookie » seul : la CNIL publie des fiches sur les cookies, qui deviendront peut-être des sources.

**Résidus HTML** : une vraie balise n'a pas d'espace après `<` ; « si x < y et z > w » n'est pas une balise (testé).

## Vérifié sur une copie de la base (2026-10-10)

- seuil durci pour l'essai (`--Quality:MinSections=50`) : la candidate CNIL passe en quarantaine
  (« structure : au moins 50 intertitres, mesuré 8 »), n'est ni vectorisée ni publiée, la version publiée reste
  servie (691 passages) ; une nouvelle collecte du même contenu la laisse en quarantaine ;
- seuils normaux : la même candidate est validée, vectorisée, publiée.

## Limites

- Les contrôles ne jugent pas le **sens** : une page dont le texte change de fond (article modifié) passe, et c'est
  voulu ; c'est l'évaluation (S7) qui dira si les réponses se dégradent.
- Seuils calibrés sur trois documents : à revoir à l'élargissement du corpus (S5, session 4), où chaque nouvelle
  source devra passer les contrôles avant d'être publiée.
