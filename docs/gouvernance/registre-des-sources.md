# Registre des sources

Vue de gouvernance des sources du corpus : ce que chaque licence impose, et comment le système le garantit.
L'inventaire détaillé (URL, accès technique, dates de vérification) reste dans [`docs/sources.md`](../sources.md),
qui fait foi. Ce registre ne recopie pas ses faits, il les relie aux preuves.

## Sources

| Source | Éditeur | Licence | Ce qu'elle impose | Comment Limpide le respecte | Preuve |
|---|---|---|---|---|---|
| AI Act, règlement (UE) 2024/1689, version consolidée au 27.7.2026, en français | Office des publications de l'UE (EUR-Lex) | décision 2011/833/UE | mentionner la source ; ne pas dénaturer le sens ; pour la version consolidée, rappeler qu'elle n'a pas d'effet juridique | extraits reproduits à l'identique ; URL EUR-Lex, date de collecte et conditions sous chaque extrait ; avertissement de la version consolidée dans `reuseTerms`, donc sous chaque extrait, et dans les mentions légales | `corpus.json` (`reuseTerms`), ADR-023 |
| Les fiches pratiques IA, 17 pages | CNIL | CC-BY-ND 4.0 FR ; images en CC-BY-NC-ND | attribution ; **aucune modification** ; images exclues | extraits à l'identique, avec source et licence ; images, figures et iconographies retirées à l'extraction | `corpus.json`, `CnilExtractor.cs` (balises exclues) |

## La règle de citation à l'identique

Les deux licences convergent : ce qui est montré comme un texte officiel doit l'être, mot pour mot. Dans Limpide :

1. **Un extrait affiché est le texte du passage tel qu'extrait**, espaces normalisés, jamais réécrit. Le découpage
   suit la structure du texte : article, paragraphe, section (ADR-010).
2. **La réponse générée est une formulation de l'assistant**, visuellement distincte des textes officiels : autre
   police, autre fond, autre couleur, en clair comme en sombre (`.claude/rules/interfaces-web.md`).
3. **Les garde-fous signalent, ils ne réécrivent pas** : un garde-fou ne modifie ni la réponse ni un extrait.
4. **Une citation inventée est signalée** (`citation-inventee`) : la réponse ne peut renvoyer qu'à des passages
   effectivement fournis au modèle.
5. **Chaque extrait porte sa version** : URL, date de collecte, empreinte de la version servie (ADR-008).

## Cycle de vie d'une source

| Étape | Qui (RACI) | Ce qui la garantit |
|---|---|---|
| Vérifier les conditions de réutilisation, les noter avec la date | PD | `docs/sources.md` : « vérifiées et notées ici **avant toute ingestion** » |
| Déclarer la source | DEV | `corpus.json` (source, URL, `reuseTerms`) |
| Collecter, extraire, découper, contrôler, vectoriser | DEV, puis Airflow chaque lundi | commandes séparées et idempotentes ; empreinte SHA-256 du contenu brut ; même empreinte, rien à faire |
| Publier | PD | une version collectée reste **candidate** jusqu'à `publish` ; contrôles qualité et quarantaine (ADR-008) |
| Mesurer l'effet sur les réponses | RIA | `evaluate` avant et après ; `expectedFacts` revus à chaque nouvelle version d'un texte |
| Revérifier la licence | PD | à chaque élargissement du corpus et à chaque nouvelle version consolidée, comme jusqu'ici (`docs/sources.md`) ; **proposé** : au moins une fois par an, comme la revue des accès |

## Sources écartées

| Source | Pourquoi | Décision |
|---|---|---|
| Lignes directrices de la Commission sur les pratiques interdites et sur la définition d'un système d'IA (février 2025) | pas de version française trouvée ; il faudrait un extracteur PDF et un corpus bilingue, que le contrôle de langue bloquerait | non retenue, à trancher par Alexandre (`docs/sources.md`) |

## Règles qui valent pour toute source

- **Pas de donnée personnelle dans le corpus** : textes institutionnels publics seulement.
- **Pas de contournement d'une protection d'accès** : le défi JavaScript d'EUR-Lex n'est pas contourné, le texte vient de
  l'API CELLAR de l'Office des publications (`docs/sources.md`, « Accès technique »).
- **Une source n'entre pas sans sa licence vérifiée.** Une licence qui change (par exemple une modification interdite
  pour une nouvelle catégorie de contenu) suspend les mises à jour de la source jusqu'à la revérification.
