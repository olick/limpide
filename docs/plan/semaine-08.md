# Semaine 8 : observabilité, coûts, sécurité

**Objectif de fin de semaine** : pour n'importe quelle requête de la démo, on retrouve sa trace complète,
son coût et les garde-fous déclenchés, et on suit la santé du système dans le temps.

**Budget temps** : environ 10 heures, en 5 sessions.

**Prérequis** : chaîne CI/CD avec évaluation (semaine 7).

---

## Session 1 : traces avec OpenTelemetry (≈ 2 h)

1. Instrumenter `Limpide.Web` avec OpenTelemetry : requêtes HTTP, accès base, appels HTTP sortants.
2. **Traces du pipeline RAG** : une activité par étape (embedding de la question, recherche, garde-fous, génération),
   avec en attributs le nombre de passages, les scores, les tokens, le coût, les garde-fous déclenchés.
3. Instrumenter l'appel au LLM : Microsoft.Extensions.AI propose une instrumentation OpenTelemetry qui suit
   les conventions sémantiques de l'IA générative. **Ne pas enregistrer le texte des questions** par défaut (voir session 5).
4. Exporter vers le service de supervision Azure (ou un tableau de bord local en développement).

**Terminé quand** : une question dans la démo produit une trace complète, étape par étape.

## Session 2 : tableaux de bord et alertes (≈ 2 h)

1. **Tableau de bord technique** : latence (médiane, 95e centile) par étape, taux d'erreur, volume de requêtes.
2. **Tableau de bord qualité** : taux de « je ne sais pas », de refus, de citations invalides, score moyen des passages.
3. **Suivi de la dérive** : l'évolution de ces taux dans le temps. Une hausse du « je ne sais pas » peut signaler
   des questions d'un nouveau type, ou une dégradation du corpus.
4. **Alertes** : taux d'erreur, latence anormale, échec du DAG, quarantaine.

**Terminé quand** : les deux tableaux de bord existent et une alerte a été déclenchée en test.

## Session 3 : FinOps et GreenOps (≈ 2 h)

1. **Coût par requête** (tokens) et **coût par ingestion** (embeddings, calcul), agrégés par jour.
2. **Coût d'infrastructure** par environnement, à partir des données de facturation Azure.
3. Mesurer l'effet de la **mise à l'échelle jusqu'à zéro** et du seuil de pertinence (questions sans appel au LLM).
4. **Estimation énergétique** : ordre de grandeur par requête, en précisant la méthode et ses limites.
5. Page `docs/finops.md` : coût mensuel réel, projection selon le trafic, leviers d'optimisation, avec chiffres.

**Terminé quand** : le coût d'une requête et le coût mensuel sont connus et documentés.

## Session 4 : garde-fous v2 (≈ 2 h)

1. **Détection d'injection de prompt** en entrée : règles simples, et éventuellement un service de détection dédié. Comparer sur les questions d'injection du jeu d'évaluation.
2. **Filtrage de la sortie** : pas de fuite de la consigne système, pas de contenu hors sujet, citations obligatoires.
3. **Journal des refus** : chaque garde-fou déclenché est tracé (sans le texte de la question par défaut).
4. Ajouter 5 nouvelles tentatives d'injection au jeu d'évaluation et vérifier les scores.

**Terminé quand** : les nouvelles questions d'injection sont bloquées et les scores ne régressent pas.

## Session 5 : sécurité et données personnelles (≈ 2 h)

1. **Revue des accès** : qui peut faire quoi sur Azure, GitHub et la base. Principe du moindre privilège.
2. **Analyse des images** de conteneurs dans la chaîne CI (déjà amorcée en S7) : traiter les vulnérabilités critiques.
3. **Rotation des secrets** : procédure documentée pour la clé d'API du LLM.
4. **Questions des utilisateurs** : elles peuvent contenir des données personnelles.
   Décider et documenter : pas de conservation du texte, ou conservation courte et pseudonymisée pour l'amélioration.
   Mettre à jour la mention d'information de la démo en conséquence.

**Terminé quand** : la décision sur les questions des utilisateurs est appliquée dans le code et documentée.

---

## Pièges à éviter

- **Tout journaliser** : les questions des utilisateurs dans les traces, c'est un traitement de données personnelles non maîtrisé.
- **Des tableaux de bord que personne ne regarde** : peu d'indicateurs, chacun relié à une décision ou une alerte.
- **Un coût estimé jamais confronté à la facture** : comparer l'estimation par tokens à la facturation réelle.
- **Des garde-fous non évalués** : chaque garde-fou ajouté a ses questions de test dans le jeu d'évaluation.

## Livrables

- Instrumentation OpenTelemetry, tableaux de bord, alertes
- `docs/finops.md` avec chiffres réels
- Garde-fous v2, jeu d'évaluation enrichi
- Politique de traitement des questions des utilisateurs

## Ce qui prépare la suite

| Fait cette semaine | Réutilisé en |
|---|---|
| Traces par requête | S9 : vidéo (montrer une trace), dossier de gouvernance (auditabilité) |
| Politique sur les questions | S9 : dossier RGPD |
| FinOps chiffré | Soutenance BC02 et BC04 |
| Journal des garde-fous | S9 : registre des risques |
