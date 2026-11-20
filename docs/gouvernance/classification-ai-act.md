# Classification de Limpide au regard de l'AI Act

Où se situe Limpide dans le règlement (UE) 2024/1689, et quelles obligations en découlent. Rédigé le **2026-11-20**
sur la **version consolidée au 27 juillet 2026** (Omnibus numérique, règlement 2026/1744 ; ADR-023), celle que la démo
sert. Articles lus dans le corpus de Limpide, cités par leur numéro. **Analyse de l'auteur, pas relue par un juriste** :
elle situe Limpide lui-même, jamais la situation d'un visiteur.

## En bref

| Question | Réponse | Articles |
|---|---|---|
| Limpide est-il un système d'IA ? | oui | 3, point 1 |
| Quel rôle pour l'EURL ? | **fournisseur** du système (mis en service sous son nom, gratuitement), **fournisseur en aval** du modèle de Mistral ; aussi **déployeur** | 3, points 3, 4 et 68 |
| Pratique interdite ? | non | 5 |
| Haut risque ? | **non** : aucun domaine de l'annexe III, ni produit de l'annexe I | 6, annexe III |
| Système d'IA à usage général ? | non : une seule finalité | 3, point 66 |
| Obligations qui s'appliquent | transparence (article 50.1 : **remplie** ; article 50.2 : **écart**) ; maîtrise de l'IA (article 4) | 4, 50, 113 |
| Obligations des modèles à usage général | celles de **Mistral AI**, pas de Limpide | chapitre V |

## Système d'IA, et qui en répond

**Système d'IA** (article 3, point 1) : Limpide « déduit, à partir des entrées qu'il reçoit, la manière de générer
des sorties » : la réponse rédigée par Mistral Medium 3.5 à partir de la question et des passages retrouvés. La recherche
seule (vectorisation, seuil) n'en ferait peut-être pas un ; la rédaction, si.

**Rôles** :

- **Fournisseur** (article 3, point 3) : Alexandre Leclerc - EURL développe Limpide et le met en service sous son
  propre nom, « à titre onéreux ou gratuit » : la gratuité de la démo n'y change rien.
- **Fournisseur en aval** (article 3, point 68) : Limpide intègre un modèle d'IA à usage général fourni par une autre
  entité (Mistral AI, par contrat).
- **Déployeur** (article 3, point 4) : l'EURL utilise aussi le système sous sa propre autorité, sur son site.
- **Champ** (article 2.1, a et b) : fournisseur établi dans l'Union, système mis en service dans l'Union. Les
  exclusions ne jouent pas : la démo est publique, ce n'est ni de la recherche scientifique (2.6), ni un essai avant
  mise en service (2.8), ni une activité personnelle (2.10). Le code est sous licence Apache 2.0, mais l'exclusion des
  licences libres (2.12) ne vaut pas pour l'article 50.

**Mise en service** : démo en ligne depuis le **2026-10-06** (`docs/plan/semaine-03.md`), donc après le 2 août 2026,
date d'application générale (article 113). Aucune disposition transitoire pour les systèmes antérieurs ne s'applique
(article 111.4).

## Pratiques interdites (article 5) : aucune

Limpide ne manipule ni n'exploite de vulnérabilité, ne note pas les personnes, ne prédit pas d'infraction, ne
constitue pas de base de reconnaissance faciale, ne reconnaît pas d'émotions, ne catégorise pas biométriquement, ne fait
pas d'identification biométrique. Il ne traite que du texte, et n'évalue pas la personne qui l'écrit.

## Haut risque (article 6) : non

- **Article 6.1** : Limpide n'est ni un produit ni un composant de sécurité d'un produit couvert par l'annexe I.
- **Article 6.2 et annexe III** : le domaine le plus proche est le **point 8 a)**, « systèmes d'IA destinés à être
  utilisés **par les autorités judiciaires ou en leur nom**, pour les aider à rechercher et à interpréter les faits
  ou la loi », ou lors du règlement extrajudiciaire d'un litige. Limpide aide à naviguer dans des textes juridiques,
  mais il n'est destiné ni à une autorité judiciaire ni à un règlement de litige. Sa destination (article 3, point
  12) est publique : page d'accueil, « À propos », mention permanente « ce n'est pas un conseil juridique ». Aucun autre
  point de l'annexe III (biométrie, infrastructures, éducation, emploi, services essentiels, répression, migration,
  élections) ne correspond.
- La dérogation de l'article 6.3 n'a donc pas à être invoquée.

**Ce qui ferait basculer** : vendre ou configurer Limpide pour un tribunal, un médiateur ou un arbitre (annexe III,
8 a) ; l'utiliser pour trier des candidats ou évaluer des personnes (point 4). Ce serait une autre destination, donc une
nouvelle classification avant toute mise en service.

## Usage général (article 3, point 66) : non

Limpide repose sur un modèle à usage général, mais lui-même ne sert qu'à une finalité : répondre sur un corpus fermé,
en citant. Hors périmètre, il ne répond pas : seuil de pertinence, sans appel au modèle (ADR-005), contrôle de la
question (ADR-021). Les obligations du chapitre V (documentation du modèle, droit d'auteur, résumé des données
d'entraînement) pèsent sur **Mistral AI**, fournisseur du modèle.

## Obligations qui s'appliquent

### Article 50.1 : dire qu'on échange avec une IA (remplie)

« Les fournisseurs veillent à ce que les systèmes d'IA destinés à interagir directement avec des personnes physiques
soient conçus et développés de manière que les personnes physiques concernées soient informées qu'elles interagissent
avec un système d'IA », au plus tard à la première interaction, de manière « claire et reconnaissable » et accessible
(article 50.5).

| Exigence | Preuve |
|---|---|
| Information avant la première question, sur toutes les pages | bandeau « Vous échangez avec une IA : elle peut se tromper, ce n'est pas un conseil juridique », dans l'en-tête fixe (`src/Limpide.Web/Components/Layout/MainLayout.razor`, ADR-022) |
| Réponse distincte des textes officiels | « Formulation de l'assistant, à vérifier dans les textes dessous » (`Components/Thread/ThreadAnswer.razor`) ; styles distincts (`.claude/rules/interfaces-web.md`) |
| Accessibilité | texte, pas une image ; relu par `relecteur-interface` (WCAG 2.2) |

### Article 50.2 : marquer le texte généré dans un format lisible par machine (écart)

« Les fournisseurs de systèmes d'IA [...] qui génèrent des contenus de synthèse de type [...] texte, veillent à ce que
les sorties des systèmes d'IA soient marquées dans un format lisible par machine et identifiables comme ayant été
générées ou manipulées par une IA. »

- **Applicable** depuis le 2 août 2026 (article 113). Le délai jusqu'au 2 décembre 2026 (article 111.4) ne vaut que
  pour les systèmes mis sur le marché avant le 2 août 2026 : pas Limpide.
- **Exception** : pas d'obligation si le système remplit « une fonction d'assistance pour la mise en forme standard »
  ou « ne modifient pas de manière substantielle les données d'entrée fournies par le déployeur ou leur sémantique ».
  Une réponse rédigée à partir de six passages est une synthèse, pas une mise en forme. Limpide ne s'en prévaut pas.
- **Constat** : la réponse est signalée **aux personnes** (50.1), pas **aux machines**. Rien, dans le HTML de la
  réponse ni dans une sortie de l'application, ne la marque comme générée par une IA.
- **Mesure proposée**, à décider : marquer chaque réponse dans le HTML (attribut et métadonnées du bloc de réponse),
  en attendant les normes et le code de bonne pratique de l'article 50.7. Inscrit au registre des risques (R13).

### Article 50.4, second alinéa : textes publiés pour informer le public (non concerné)

Il vise les déployeurs qui **publient** un texte généré pour informer le public sur des questions d'intérêt public.
Une réponse de Limpide est remise au seul visiteur qui l'a demandée, pas publiée. Elle est de toute façon présentée
comme générée (50.1).

### Article 4 : maîtrise de l'IA

Fournisseurs et déployeurs « prennent des mesures pour favoriser le développement de la maîtrise de l'IA par leur
personnel et les autres personnes s'occupant du fonctionnement et de l'utilisation des systèmes d'IA pour leur
compte ». Ici, une seule personne : Alexandre. Mesures : la certification « Architecte en intelligence artificielle »
(RNCP41993) en cours, et ce projet, dont l'évaluation mesurée (ADR-014) et les ADR documentent les limites des
modèles. L'article n'impose pas de niveau précis par personne. **Avec une seconde personne** : une prise en main
écrite (ce dossier, `fiche-systeme.md`), à tracer dans `raci.md`.

### Ce qui ne s'applique pas

Les obligations du haut risque (chapitre III : gestion des risques, documentation technique, enregistrement,
contrôle humain, analyse d'impact sur les droits fondamentaux), le marquage CE et la déclaration de conformité.
Limpide en reprend volontairement l'esprit, sans obligation : registre des risques (`registre-des-risques-ia.md`),
fiche descriptive (`fiche-systeme.md`), procédure d'incident (`procedure-incident.md`), journal des réponses (ADR-018).

## Quand revoir cette classification

- changement de destination (public visé, client, domaine), en particulier vers une autorité judiciaire ou un
  règlement de litige ;
- un nouvel acte modifiant l'AI Act, une ligne directrice de la Commission sur l'article 6 ou 50, une norme de
  marquage : la veille des actes modificatifs est manuelle (ADR-023, points ouverts) ;
- ajout d'une capacité qui change la nature du système : mémoire de conversation (ADR-022), documents fournis par
  l'utilisateur, image ou voix.
