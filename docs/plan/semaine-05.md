# Semaine 5 : qualité des données, quarantaine, versionnement

**Objectif de fin de semaine** : une version de document défectueuse est bloquée avant d'atteindre la démo,
la version précédente reste en service, et une alerte est envoyée.

**Budget temps** : environ 10 heures, en 5 sessions.

**Prérequis** : DAG d'ingestion fonctionnel (semaine 4).

---

## Session 1 : revoir le cycle de vie d'une version (≈ 2 h)

**Problème à corriger** : aujourd'hui, `fetch` rend une nouvelle version courante dès son téléchargement.
Une page mal extraite remplace donc immédiatement une version saine. Il faut qu'une version ne devienne
courante **qu'après validation**.

1. Nouveau cycle de vie :
   ```
   collectée → extraite → validée → vectorisée → publiée
                    └──→ en quarantaine (la version publiée précédente reste en service)
   ```
2. **Mettre en place un outil de migrations.** Les scripts de `db/init` ne s'exécutent qu'à la création du volume :
   ils ne suffisent plus dès qu'il faut faire évoluer une base existante (et celle de production en particulier).
   Un outil de migrations .NET simple, fondé sur des scripts SQL numérotés, suffit (DbUp par exemple).
3. Migration `002` : colonne `status` sur `document_versions`, colonne `quality_report` (JSON),
   et bascule de `is_current` au moment de la **publication** plutôt que de la collecte.
4. Adapter `fetch` et `embed` au nouveau cycle.

**Terminé quand** : la migration s'applique sur la base locale existante sans perte de données.

## Session 2 : les contrôles qualité (≈ 2 h 30)

Nouvelle commande `validate`, entre `chunk` et `embed` (elle contrôle aussi les passages produits). Contrôles, chacun avec un seuil configurable :

| Contrôle | Détecte |
|---|---|
| Texte extrait non vide, longueur minimale | Page d'erreur, extraction ratée |
| Langue française | Mauvaise page, version anglaise |
| Nombre de passages proche de la version précédente (± 30 %) | Structure changée, découpage cassé |
| Passages ni trop courts ni trop longs | Découpage défaillant |
| Pas de passages dupliqués | Contenu répété (menus, pieds de page) |
| Marqueurs de structure présents (« Article » pour l'AI Act) | Page qui n'est plus le texte attendu |
| Absence de résidus HTML ou de boilerplate (cookies, navigation) | Nettoyage insuffisant |

Le résultat est enregistré dans `quality_report` : chaque contrôle, sa valeur, son seuil, réussi ou non.

**Terminé quand** : `validate` produit un rapport pour chaque version, testé unitairement.

## Session 3 : quarantaine et alerte (≈ 2 h)

1. Ajouter la tâche `validate` au DAG : fetch → extract → chunk → **validate** → embed → publish.
2. Une version en échec passe en quarantaine, la suite du DAG ne la traite pas, **l'alerte part avec le rapport**.
3. **Démonstration** : créer une source volontairement corrompue (fichier local servi par un petit serveur,
   ou copie modifiée d'une page) et montrer :
   - la version bloquée ;
   - la démo qui continue à répondre avec la version précédente ;
   - l'alerte reçue.
4. Procédure pour lever une quarantaine après vérification manuelle.

**Terminé quand** : la démonstration fonctionne de bout en bout. Enregistre-la : elle servira dans la vidéo.

## Session 4 : traçabilité et élargissement du corpus (≈ 2 h)

1. **Traçabilité dans l'interface** : chaque extrait cité affiche la version (date de collecte, empreinte courte).
2. Requête de lignage : pour une réponse donnée, retrouver les passages, leurs versions et les fichiers bruts d'origine.
3. **Élargir le corpus** : d'autres fiches CNIL, les lignes directrices européennes pertinentes.
   Vérifier et noter les conditions de réutilisation de chaque nouvelle source dans `docs/sources.md` **avant** l'ingestion.
4. Relancer l'évaluation des 15 questions : l'élargissement ne doit pas dégrader les résultats.

**Terminé quand** : le corpus élargi passe les contrôles qualité et les scores sont stables.

## Session 5 : ADR et documentation (≈ 1 h 30)

1. **ADR-008 — stratégie de découpage** : découpage structurel contre taille fixe, avec les observations de S1 et S5.
2. **ADR-009 — batch incrémental plutôt que streaming** : fréquence de mise à jour des sources,
   coût et complexité d'un flux temps réel, condition de révision.
3. Mettre à jour le schéma du pipeline et `CLAUDE.md`.

**Terminé quand** : les deux ADR sont commités.

---

## Pièges à éviter

- **Des seuils trop stricts** : la quarantaine se déclenche à chaque mise à jour légitime. Les calibrer sur les vraies versions, et documenter leur valeur.
- **Modifier `db/init` au lieu d'écrire une migration** : la base de production ne serait jamais mise à jour.
- **Ajouter une source sans vérifier sa licence** : la règle de `docs/sources.md` s'applique à chaque nouvelle source.
- **Oublier la procédure de sortie de quarantaine** : sans elle, un faux positif bloque une source indéfiniment.

## Livrables

- Outil de migrations et migration `002`
- Commande `validate`, rapports de qualité, tâche Airflow associée
- Démonstration de quarantaine enregistrée
- Corpus élargi, `docs/sources.md` à jour
- ADR-008, ADR-009

## Ce qui prépare la suite

| Fait cette semaine | Réutilisé en |
|---|---|
| Outil de migrations | S6 : déploiement du schéma en production |
| Rapports de qualité | S8 : tableau de bord, S9 : dossier de gouvernance |
| Démonstration de quarantaine | S9 : vidéo, soutenance BC03 |
| Traçabilité réponse → source | S9 : dossier de gouvernance (auditabilité) |
