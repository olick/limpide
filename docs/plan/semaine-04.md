# Semaine 4 : Airflow, apprentissage et DAG d'ingestion

**Objectif de fin de semaine** : l'ingestion ne se lance plus à la main. Un DAG Airflow enchaîne
`fetch` → `extract` → `chunk` → `embed` dans des conteneurs, chaque semaine, avec relances et alerte en cas d'échec.

**Budget temps** : environ 10 heures, en 5 sessions. C'est une semaine d'apprentissage : prévoir de déborder.

**Prérequis** : bilan de la phase 1 rédigé.

---

## Les concepts à comprendre avant de coder

| Concept | Ce que c'est | Ce que ça implique pour Limpide |
|---|---|---|
| **DAG** | Un graphe de tâches sans cycle, écrit en Python, qui décrit **quoi** lancer et **dans quel ordre** | Un fichier `ingestion.py` : fetch → extract → chunk → embed |
| **Tâche / opérateur** | Une étape du DAG ; l'opérateur définit **comment** elle s'exécute | Chaque tâche lance un conteneur de `Limpide.Ingestion` avec une commande |
| **Planificateur** | Déclenche les exécutions selon un calendrier | Une exécution par semaine |
| **Exécution (run)** | Une instance du DAG pour une date donnée | Chaque run doit pouvoir être relancé sans effet de bord |
| **Relances** | Nouvelle tentative automatique d'une tâche en échec | Possible **seulement** parce que les commandes sont idempotentes |
| **XCom** | Petit canal d'échange entre tâches | Uniquement pour des métadonnées (nombre de documents) ; les données passent par le stockage et la base |

Point clé : **Airflow orchestre, il ne traite pas les données.** Le travail reste dans le code .NET.

> Vérifier la version installée : Airflow 3 a changé plusieurs choses (interface, API, paramètres de planification)
> par rapport à la majorité des tutoriels, écrits pour la version 2. Se fier à la documentation officielle de la version utilisée.

---

## Session 1 : prise en main (≈ 2 h)

1. Lancer Airflow en local avec le Docker Compose officiel de la documentation.
   Limiter les ports à `127.0.0.1`, comme pour le reste du projet.
2. Écrire un DAG trivial de deux tâches qui affichent un message. L'exécuter, lire les logs dans l'interface.
3. Provoquer volontairement une erreur : observer l'état de la tâche, les relances, les logs.

**Terminé quand** : tu sais lancer un DAG, lire ses logs et relancer une tâche en échec.

## Session 2 : conteneuriser l'ingestion (≈ 1 h 30)

1. Dockerfile pour `Limpide.Ingestion` : la commande (`fetch`, `extract`, `chunk`, `embed`) est passée en argument.
2. Configuration par variables d'environnement (connexion à la base, URL d'Ollama, chemins des fichiers bruts et extraits).
3. Les fichiers bruts et extraits vont dans un **volume partagé** (ou un stockage objet local), pas dans le conteneur.
4. Tester chaque commande avec `docker run`.

**Terminé quand** : les quatre commandes tournent en conteneur, et `fetch` lancé deux fois reste idempotent.

## Session 3 : le DAG d'ingestion (≈ 2 h 30)

1. DAG `ingestion` : quatre tâches enchaînées, chacune lançant le conteneur avec sa commande.
   En local, l'opérateur Docker suffit. En production, ce sera l'opérateur qui lance des pods ou des tâches cloud (décision en S6).
2. Paramètres :
   - planification hebdomadaire ;
   - **pas de rattrapage** des exécutions passées ;
   - relances : 2 tentatives, espacées de quelques minutes ;
   - une seule exécution à la fois.
3. Chaque tâche remonte un résumé (nombre de documents inchangés, nouvelles versions, erreurs).

**Terminé quand** : le DAG complet tourne et produit le même état de base qu'une ingestion manuelle.

## Session 4 : échecs et alertes (≈ 2 h)

1. Notification en cas d'échec (e-mail ou webhook vers une messagerie).
2. Tester trois pannes :
   - une URL source indisponible ;
   - Ollama arrêté pendant `embed` ;
   - la base arrêtée.
   Dans chaque cas : la tâche échoue proprement, les relances s'enchaînent, l'alerte arrive, et une relance après réparation termine le travail **sans doublon**.
3. Noter ce qui s'est mal passé : c'est la matière de l'ADR.

**Terminé quand** : les trois scénarios sont testés et documentés.

## Session 5 : ADR et documentation (≈ 1 h 30)

1. **ADR-007** : Airflow orchestrant des tâches .NET conteneurisées, plutôt que du code Python dans les tâches.
   Arguments : réutilisation du code existant, un seul langage à maintenir, tâches testables hors d'Airflow.
   Contrepartie : un conteneur par tâche, un démarrage un peu plus lent.
2. Schéma du pipeline dans `docs/`.
3. Mettre à jour `CLAUDE.md`.

**Terminé quand** : l'ADR-007 et le schéma sont commités.

---

## Pièges à éviter

- **Faire transiter les données par XCom** : il est fait pour des métadonnées, pas pour des documents.
- **Monter le socket Docker sans y penser** : en local c'est acceptable, mais ça donne à Airflow un contrôle total de Docker. À noter dans l'ADR comme choix de développement, pas de production.
- **Laisser le rattrapage activé** : Airflow lancerait toutes les exécutions « manquées » depuis la date de début.
- **Écrire de la logique métier dans le DAG** : le DAG décrit l'ordre des tâches, le code .NET fait le travail.

## Livrables

- Dockerfile de `Limpide.Ingestion`
- `pipelines/airflow/dags/ingestion.py`
- ADR-007, schéma du pipeline
- Scénarios de panne documentés

## Ce qui prépare la suite

| Fait cette semaine | Réutilisé en |
|---|---|
| DAG d'ingestion | S5 : ajout de la tâche de contrôle qualité |
| Alertes d'échec | S5 : alerte de quarantaine |
| Conteneur d'ingestion | S6 : exécution dans Azure |
| Scénarios de panne | Soutenance BC03 : fiabilité des pipelines |
