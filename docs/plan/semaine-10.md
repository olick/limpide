# Semaine 10 : marge et préparation de la soutenance

**Objectif** : absorber le retard accumulé, puis consolider. Si la certification se confirme,
préparer la soutenance.

**Budget temps** : selon le retard.

---

## Priorité 1 : rattraper le retard

Reprendre les critères « terminé quand » des semaines 1 à 9 et lister ceux qui ne sont pas atteints.
Les traiter dans cet ordre :

1. **Ce qui casse la démo publique** : disponibilité, sécurité, budget.
2. **L'évaluation (S7)** : c'est ce qui distingue le projet.
3. **Le dossier de gouvernance (S9)** et les ADR.
4. Le reste.

Ce qui ne peut pas être terminé devient une **limite documentée** dans le README, avec la raison et la piste de solution.
Une limite assumée vaut mieux qu'une fonctionnalité bâclée.

## Priorité 2 : consolider

- **Rétrospective** : `docs/retrospective.md`. Ce qui a fonctionné, ce qui a coûté plus cher que prévu,
  ce que tu referais autrement. C'est une excellente matière pour les entretiens.
- **Temps réel passé** par semaine et par sujet, comparé au plan : utile pour chiffrer une mission similaire chez un client.
- **Relecture croisée** : faire relire le dépôt par un pair, noter ses questions. Ce sont celles d'un jury ou d'un client.

## Priorité 3 : préparer la soutenance (si la certification se confirme)

1. **Vérifier auprès de Jedha** le format exact : livrables attendus par bloc, durée, composition du jury,
   et si le projet personnel est accepté comme support (question posée en fin de semaine 3).
2. **Tableau de correspondance** entre chaque compétence du référentiel RNCP41993 et une preuve du dépôt
   (fichier, ADR, démonstration, métrique). Les compétences sans preuve sont à combler ou à assumer.
3. **Support de présentation** par bloc :
   - BC01 : gouvernance, licences, registre des risques, classification AI Act ;
   - BC02 : Terraform, réseau, secrets, arbitrages d'hébergement et de GPU, FinOps ;
   - BC03 : Airflow, contrôles qualité, quarantaine, batch plutôt que streaming ;
   - BC04 : évaluation en CI, blocage du déploiement, observabilité, garde-fous.
4. **Préparer les questions difficiles** :
   - « Pourquoi pas Kubernetes ? Pourquoi pas Kafka ? »
   - « Comment ce système passerait-il à cent fois plus de trafic ? »
   - « Que se passe-t-il si le fournisseur du LLM change ses prix ou disparaît ? »
   - « Comment savez-vous que votre LLM juge est fiable ? »
   - « Qu'est-ce qui ne fonctionne pas bien aujourd'hui ? »
5. **Répétition chronométrée**, idéalement devant quelqu'un.

## Livrables

- Limites documentées dans le README
- `docs/retrospective.md`, suivi du temps passé
- Si certification : tableau de correspondance compétences / preuves, support de soutenance
