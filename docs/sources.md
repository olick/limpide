# Inventaire du corpus

Chaque source doit avoir ses conditions de réutilisation **vérifiées et notées ici avant toute ingestion**.
Ce fichier alimente aussi le dossier de gouvernance (bloc 1 de la RNCP).

| Source  | Document                         | Format | URL                                                                     | Conditions de réutilisation                                                                  | Vérifié le |
| ------- | -------------------------------- | ------ | ----------------------------------------------------------------------- | -------------------------------------------------------------------------------------------- | ---------- |
| EUR-Lex | AI Act (règlement 2024/1689), FR | HTML   | https://eur-lex.europa.eu/legal-content/FR/TXT/HTML/?uri=OJ:L_202401689 | Décision 2011/833/UE : réutilisation autorisée, mention de la source, sans dénaturer le sens | 2026-09-28 |
| CNIL    | Fiches pratiques IA (17 pages)   | HTML   | https://www.cnil.fr/fr/les-fiches-pratiques-ia                          | CC-BY-ND 4.0 FR : attribution, aucune modification. Images exclues (CC-BY-NC-ND)             | 2026-10-11 |

Vérification du 2026-10-11 (élargissement, S5) : la page « Informations publiques » de la CNIL
(https://www.cnil.fr/fr/informations-publiques) indique « Licence CC BY-ND » pour les « fiches web », et
« CC BY-NC-ND » pour les images et iconographies (exclues par l'extracteur). Inchangé depuis le 2026-09-28.

## Périmètre

**Depuis le 2026-10-11** : l'AI Act et les 17 pages de la série CNIL « Les fiches pratiques IA » (synthèse,
introduction, fiches 1 à 13 dont la 4 en deux parties, fiche focus sur le moissonnage). Liste exacte : `corpus.json`.

**Envisagé, non retenu pour l'instant** : les lignes directrices de la Commission européenne sur les pratiques
interdites et sur la définition d'un système d'IA (février 2025). Version française non trouvée (documents PDF en
anglais d'après les sources consultées) : il faudrait un extracteur PDF et décider d'un corpus bilingue, que le
contrôle de langue (`validate`) bloquerait aujourd'hui. À trancher par Alexandre.

## Périmètre de la phase 1

Volontairement réduit : 2 ou 3 documents suffisent pour valider la chaîne de bout en bout.
L'AI Act seul fait plusieurs centaines de passages après découpage.

La liste exacte est dans [`corpus.json`](../corpus.json) :

- AI Act, version française ;
- CNIL, fiche « IA : Annoter les données » ;
- CNIL, fiche « IA : Informer les personnes concernées ».

## Accès technique

- **EUR-Lex** : `eur-lex.europa.eu` impose un défi JavaScript (AWS WAF) aux scripts, qui reçoivent
  une réponse `202` vide. On ne le contourne pas : le texte est téléchargé par l'API CELLAR de l'Office des
  publications (`http://publications.europa.eu/resource/celex/32024R1689`, en-têtes
  `Accept: application/xhtml+xml` et `Accept-Language: fra`). Même texte officiel, même licence.
  L'URL EUR-Lex reste celle qui est citée aux utilisateurs.
- **CNIL** : pages HTML téléchargées directement. Le HTML brut change plusieurs fois par jour (identifiants
  techniques) sans que le texte change (constaté à partir du 2026-09-29) : `extract` compare l'empreinte du texte
  extrait à celle de la version publiée et écarte ces fausses nouvelles versions (ADR-008).

## Règles

- Pas de données personnelles dans le corpus.
- Toute réponse de l'assistant cite l'URL et la date de collecte de la version utilisée.
- L'assistant aide à naviguer dans les textes ; il ne qualifie pas la situation juridique de l'utilisateur.
- Les extraits affichés sont reproduits sans aucune modification, avec source et licence.
  La réponse générée est présentée comme une formulation de l'assistant, distincte des textes cités.
