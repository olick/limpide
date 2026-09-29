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
