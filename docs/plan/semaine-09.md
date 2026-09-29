# Semaine 9 : gouvernance, documentation, démonstration

**Objectif de fin de semaine** : quelqu'un qui ne connaît pas le projet comprend en 5 minutes ce que fait Limpide,
comment, et pourquoi ces choix. Le dossier de gouvernance est complet.

**Budget temps** : environ 10 heures, en 5 sessions.

**Prérequis** : semaines 1 à 8 terminées (ou arbitrées : ce qui manque est listé comme limite).

---

## Session 1 : dossier de gouvernance, partie données (≈ 2 h)

Dans `docs/gouvernance/` :

1. **Classification des données** : corpus public, métadonnées techniques, questions des utilisateurs,
   traces, secrets. Pour chaque catégorie : sensibilité, lieu de stockage, durée de conservation, accès.
2. **RACI** : même seul sur le projet, définir les rôles (propriétaire des données, responsable du système IA,
   sécurité, développement) et qui est responsable, décide, est consulté, est informé pour chaque activité
   (ajout d'une source, changement de modèle, levée de quarantaine, incident).
3. **Registre des sources** : reprendre `docs/sources.md` avec les licences et la règle de citation à l'identique.
4. **RGPD** : traitement des questions des utilisateurs (décision de la semaine 8), base légale, information, durées.

**Terminé quand** : les quatre documents sont rédigés.

## Session 2 : dossier de gouvernance, partie IA (≈ 2 h)

1. **Classification de Limpide au regard de l'AI Act** : quelle catégorie de risque, quelles obligations
   (en particulier la transparence envers les utilisateurs). À vérifier sur le texte, avec les articles cités.
   C'est un test de crédibilité : un projet sur l'AI Act doit savoir se situer lui-même.
2. **Registre des risques IA** : pour chaque risque, probabilité, impact, mesure en place, preuve.
   | Risque | Mesure | Preuve |
   |---|---|---|
   | Réponse inventée | Seuil de pertinence, citations vérifiées, « je ne sais pas » | Évaluation S7 |
   | Texte source dénaturé | Extraits reproduits à l'identique, réponse distincte | Interface S2 |
   | Version défectueuse d'une source | Contrôles qualité, quarantaine | Démonstration S5 |
   | Injection de prompt | Garde-fous v1 et v2 | Questions d'injection S7-S8 |
   | Dérive des coûts | Plafonds, alertes de budget | FinOps S8 |
   | Dépendance au fournisseur du LLM | Abstraction `IChatClient` | ADR-004 |
   | Conseil juridique implicite | Avertissement, refus de qualification | Évaluation S7 |
3. **Fiche descriptive du système** : modèles et versions, données, usage prévu et usages exclus,
   métriques d'évaluation, limites connues.
4. **Procédure d'incident** : détection, qualification, correction, communication, retour d'expérience.

**Terminé quand** : les quatre documents sont rédigés, chaque mesure pointe vers une preuve dans le dépôt.

## Session 3 : ADR et README (≈ 2 h)

1. **Relire les 13 ADR** : mettre à jour les statuts, ajouter les mesures obtenues depuis leur rédaction
   (l'ADR-002 sur les embeddings en particulier).
2. **README final** :
   - en une phrase : ce que fait Limpide et ce qui le distingue ;
   - lien vers la démo et la page d'évaluation ;
   - schéma d'architecture ;
   - le récit POC → production, avec le bilan de la phase 1 ;
   - résultats d'évaluation, coût par requête ;
   - limites connues et suites possibles ;
   - index de la documentation (ADR, gouvernance, FinOps).

**Terminé quand** : une personne extérieure relit le README et sait dire ce que fait le projet.

## Session 4 : vidéo de démonstration (≈ 2 h)

Deux à trois minutes, avec un script écrit à l'avance :

1. Le problème en une phrase, et ce que Limpide fait différemment.
2. Une question : la réponse, les extraits cités, le panneau « sous le capot ».
3. Une question hors sujet et une question sans réponse : les garde-fous.
4. Une source corrompue bloquée par la quarantaine (enregistrement de la semaine 5).
5. Une modification de prompt bloquée par l'évaluation (enregistrement de la semaine 7).
6. Une trace et le coût d'une requête.
7. Conclusion : les choix d'architecture en trois points.

**Terminé quand** : la vidéo est publiée et liée depuis le README.

## Session 5 : diffusion (≈ 2 h)

1. Mettre à jour les profils (Malt, LinkedIn) : lien vers la démo, la vidéo et le dépôt.
2. Rédiger un court article ou billet sur le récit POC → production, ou sur un apprentissage marquant
   (la contrainte CC-BY-ND, la quarantaine, le blocage par l'évaluation).
3. Préparer une version « rendez-vous client » de la démonstration (5 minutes) :
   ce que Limpide montrerait sur les documents du prospect.

**Terminé quand** : le projet est visible publiquement, avec une démonstration prête pour un rendez-vous.

---

## Pièges à éviter

- **Une gouvernance théorique** : chaque règle doit pointer vers une preuve dans le dépôt ou la démo.
- **Cacher les limites** : un projet qui dit ce qu'il ne sait pas faire est plus crédible, et c'est précisément le propos de Limpide.
- **Une vidéo trop longue** : au-delà de 3 minutes, elle ne sera pas regardée en entier.
- **Un README écrit pour soi** : il s'adresse à un DSI, un recruteur ou un jury, pas à un développeur du projet.

## Livrables

- `docs/gouvernance/` : classification, RACI, sources, RGPD, classification AI Act, registre des risques, fiche système, procédure d'incident
- ADR relus et à jour
- README final
- Vidéo de démonstration
- Profils mis à jour, version « rendez-vous client »
