# Dossier de gouvernance

Comment Limpide gouverne ses données et son comportement. **Chaque règle renvoie à une preuve** dans le dépôt ou la
démo : un fichier, une migration, une ADR, un contrôle de la chaîne. Une règle sans preuve est signalée comme écart.

## Partie données (S9, session 1, 2026-11-19)

| Document | Ce qu'il répond |
|---|---|
| [Classification des données](classification-des-donnees.md) | Quelles données, quelle sensibilité, où, combien de temps, qui y accède ? |
| [Rôles et responsabilités (RACI)](raci.md) | Qui décide quoi, et quel contrôle ne dépend pas de sa vigilance ? |
| [Registre des sources](registre-des-sources.md) | Que permettent les licences du corpus, et comment le système les respecte-t-il ? |
| [Données personnelles (RGPD)](rgpd.md) | Registre des traitements, fondements, droits, sous-traitants, écarts |

## Partie IA (S9, session 2, 2026-11-20)

| Document | Ce qu'il répond |
|---|---|
| [Classification AI Act](classification-ai-act.md) | Quel rôle, quelle catégorie de risque, quelles obligations, articles à l'appui ? |
| [Registre des risques IA](registre-des-risques-ia.md) | Qu'est-ce qui peut mal tourner dans la réponse, qu'est-ce qui le limite, où le vérifier ? |
| [Fiche descriptive du système](fiche-systeme.md) | Modèles, données, usage prévu et exclu, mesures, limites connues |
| [Procédure d'incident](procedure-incident.md) | Détecter, qualifier, corriger, communiquer, retenir ; incidents déjà survenus |

## Documents liés

- [`docs/sources.md`](../sources.md) : inventaire du corpus, accès technique, vérifications de licence ;
- [`docs/acces.md`](../acces.md) : revue des accès (personnes, identités, serveur, base) ;
- [`docs/finops.md`](../finops.md) : coûts et plafonds ;
- [`docs/adr/`](../adr/README.md) : décisions, en particulier ADR-008 (versions et quarantaine), ADR-014
  (évaluation), ADR-018 (supervision), ADR-019 (questions des visiteurs), ADR-021 (contrôle de la question).
