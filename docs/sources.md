# Inventaire du corpus

Chaque source doit avoir ses conditions de réutilisation **vérifiées et notées ici avant toute ingestion**.
Ce fichier alimente aussi le dossier de gouvernance (bloc 1 de la RNCP).

| Source  | Document                         | Format | URL                                                                     | Conditions de réutilisation                                                                  | Vérifié le |
| ------- | -------------------------------- | ------ | ----------------------------------------------------------------------- | -------------------------------------------------------------------------------------------- | ---------- |
| EUR-Lex | AI Act (règlement 2024/1689), FR | HTML   | https://eur-lex.europa.eu/legal-content/FR/TXT/HTML/?uri=OJ:L_202401689 | Décision 2011/833/UE : réutilisation autorisée, mention de la source, sans dénaturer le sens | 2026-09-28 |
| CNIL    | Fiches pratiques IA              | HTML   | https://www.cnil.fr/fr/les-fiches-pratiques-ia                          | CC-BY-ND 4.0 FR : attribution, aucune modification. Images exclues (CC-BY-NC-ND)             | 2026-09-28 |

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
- **CNIL** : pages HTML téléchargées directement. Contenu brut stable d'un téléchargement à l'autre
  (vérifié le 2026-09-28), donc l'empreinte SHA-256 du brut suffit à détecter les changements.

## Règles

- Pas de données personnelles dans le corpus.
- Toute réponse de l'assistant cite l'URL et la date de collecte de la version utilisée.
- L'assistant aide à naviguer dans les textes ; il ne qualifie pas la situation juridique de l'utilisateur.
- Les extraits affichés sont reproduits sans aucune modification, avec source et licence.
  La réponse générée est présentée comme une formulation de l'assistant, distincte des textes cités.
