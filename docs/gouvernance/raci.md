# Rôles et responsabilités (RACI)

Qui fait, qui décide, qui est consulté, qui est informé, pour chaque activité qui engage les données ou le
comportement de Limpide.

**Une seule personne tient tous les rôles** : Alexandre. Le tableau ne décrit donc pas une organisation. Il sert à
trois choses : séparer les casquettes (une même personne ne décide pas de la même façon comme propriétaire des données
et comme développeur), nommer le contrôle qui ne dépend pas de sa vigilance, et dire ce qui change le jour où une
seconde personne arrive.

## Rôles

| Rôle | Ce qu'il porte | Tenu par |
|---|---|---|
| **Propriétaire des données** (PD) | le corpus et ses licences ; les données des visiteurs, au nom du responsable de traitement (Alexandre Leclerc - EURL) | Alexandre |
| **Responsable du système IA** (RIA) | ce que l'assistant répond : modèles, consignes, seuils, évaluation | Alexandre |
| **Sécurité** (SEC) | accès, secrets, serveur, incidents | Alexandre |
| **Développement** (DEV) | code, chaîne, déploiement | Alexandre, assisté d'un agent de code (Claude Code), qui n'a ni droit de fusion ni droit d'administration (`docs/acces.md`) |

Acteurs extérieurs : les **visiteurs** ; les **sous-traitants** (OVHcloud, Mistral AI, Microsoft pour Azure et GitHub,
`rgpd.md`) ; la **CNIL**, autorité de contrôle ; les **éditeurs des sources** (CNIL, Office des publications de l'UE).

R : réalise. A : décide et en répond (un seul par ligne). C : consulté avant. I : informé après.

## Activités

| Activité | PD | RIA | SEC | DEV | Extérieurs | Contrôle qui ne dépend pas de la vigilance |
|---|---|---|---|---|---|---|
| Ajouter une source au corpus | A | C | | R | | conditions de réutilisation notées dans `docs/sources.md` **avant** l'ingestion ; `validate` (langue, qualité) ; `evaluate` |
| Publier une nouvelle version d'une source | A | I | | R (Airflow, chaque lundi) | | contrôles qualité, quarantaine sans publication, message Discord à chaque exécution (ADR-008) |
| Lever une quarantaine (`release`, `retry`, `reject`) | A, R | C | | | | motif obligatoire, écrit dans `admin_actions` dans la même transaction |
| Changer de modèle de génération ou de consignes | I | A | C (garde-fous) | R | | nouveau fichier de consignes (l'ancien est figé par un test) ; évaluation requise sur la PR et barrière `gate` (ADR-014, ADR-015) |
| Changer de modèle de vectorisation | I | A | | R | | recalibrage de `Generation:MinScore` et évaluation complète (ADR-017) |
| Modifier un seuil d'évaluation | | A, R | | C | | élargissement dans un commit qui dit pourquoi, ADR-014 mis à jour ; jamais désactivé |
| Déployer en production | | A | C | R | | « Build et tests » et « Évaluation » requis sur `main` ; analyse Grype ; approbation de l'environnement `production` (ADR-015) |
| Donner ou retirer un accès (personne, programme, identité) | I | | A, R | C | | revue des accès (`docs/acces.md`), à chaque accès et au moins une fois par an |
| Ajouter un traitement de données personnelles ou un destinataire | A | C | C | R | visiteurs : I | page « Vos données » mise à jour dans la même PR (`.claude/rules/interfaces-web.md`) ; `rgpd.md` mis à jour |
| Répondre à une demande d'exercice des droits | A, R | | | | visiteur : I | délai d'un mois (RGPD, article 12.3) ; suppression d'une question dans le back office, notée sans son texte |
| Relire les questions conservées et les retours | A, R | C | | | | purge automatique (30 jours, 12 mois) ; aucun versement automatique dans le jeu d'évaluation (ADR-019) |
| Traiter un incident (sécurité, données, réponse dangereuse) | C | A si comportement | A si sécurité ou données | R | CNIL : I sous 72 h en cas de violation de données à risque (article 33) ; visiteurs : I si risque élevé (article 34) ; sous-traitant concerné : I | alertes Grafana et Discord (ADR-018) ; procédure d'incident (`procedure-incident.md`) |
| Renouveler un secret | | | A, R | | | échéances des secrets clients Microsoft (2027-10-12) ; Gitleaks en CI |

## Ce qui change avec une seconde personne

- **Séparer qui écrit et qui approuve** : aujourd'hui, la même personne ouvre la PR, la fusionne et approuve le
  déploiement. Avec deux personnes, l'approbation de `production` revient à l'autre.
- **Comptes de base nominatifs** : le back office et l'ingestion passeraient d'un superutilisateur à des comptes
  limités, comme `web` (écart reporté dans `docs/acces.md`).
- **Restreindre les actions GitHub autorisées** (écart accepté dans `docs/acces.md`).
- **Lever une quarantaine à deux** : la personne qui lève n'est pas celle qui a écrit l'extracteur en cause.
