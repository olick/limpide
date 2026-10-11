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
| `longueur-des-passages` | ~~≤ 20 % hors de 500 à 1 500 caractères~~ recalibré le 2026-10-11, voir plus bas | 12 % (76/634) | 3,8 % (1/26) | 9,7 % (3/31) | — |
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

## Premier vrai test : l'élargissement du corpus (2026-10-11)

15 nouvelles pages CNIL ingérées par le DAG : 13 publiées, **2 en quarantaine**, toutes deux sur
`longueur-des-passages` (règle d'alors : au plus 20 % de passages hors de 500 à 1 500 caractères).

| Page | Mesuré | Analyse |
|---|---|---|
| Déterminer le régime juridique applicable | 28,6 % (2 sur 7) | deux vraies sections, courtes : **fausse alerte** ; sur un petit document, une part est trop sensible |
| Les recommandations en bref (synthèse) | 26 % (20 sur 77) | page faite de sections d'un paragraphe : **fausse alerte**, mais ses 2 derniers passages étaient du texte du site (« Pour approfondir… Document reference », lien vers un PDF) : **vrai défaut d'extraction** |

Corrections :

- **Extracteur `cnil-html/2`** : écarte les rubriques « Pour approfondir » (`.ctn-gen-textes-references`), les documents
  joints (`.ctn-gen-documents`) et les étiquettes masquées (`.text-hide`). Test sur la structure réelle de la page.
- **Contrôle de longueur recalibré** : il visait un découpage cassé (miettes, blocs énormes), pas l'écart aux bornes
  *visées* par le découpeur. Nouvelle règle : au plus 10 % de passages de moins de 100 ou de plus de 2 000 caractères
  (limite du découpeur). Mesuré sur les 18 pages : 0 à 3,2 % (5,2 % pour la synthèse avant correction de l'extracteur).
- **Décision humaine `retry`** : renvoie une version en quarantaine dans tout le circuit après une correction du code ;
  `release` l'aurait publiée avec ses résidus, `reject` aurait écarté à tort une page légitime.

**Leçon** : calibrer sur 3 documents ne suffit pas ; le premier lot de pages inconnues a montré une règle mal posée
(elle mesurait l'écart à une cible, pas une anomalie) et, au passage, un défaut réel que la règle avait fait remonter.
C'est le rôle d'une quarantaine : rien n'a été publié par erreur, et la décision est restée humaine.

## Limites

- Les contrôles ne jugent pas le **sens** : une page dont le texte change de fond (article modifié) passe, et c'est
  voulu ; c'est l'évaluation (S7) qui dira si les réponses se dégradent.
- Seuils calibrés sur trois documents : à revoir à l'élargissement du corpus (S5, session 4), où chaque nouvelle
  source devra passer les contrôles avant d'être publiée.
