# ADR-014 : Cycle de vie des versions (candidate → publiée) et migrations du schéma

- **Statut** : accepté
- **Date** : 2026-10-10

## Contexte

Deux constats de la semaine 4 (pannes testées, `pipelines/airflow/README.md`) :

- **Un document disparaissait de la recherche entre `fetch` et la fin d'`embed`.** `fetch` rendait la nouvelle
  version courante dès son téléchargement, alors que ses passages n'étaient pas encore vectorisés ; la recherche ne
  lit que les passages vectorisés des versions courantes. Pendant la panne d'Ollama du 2026-10-08, les deux fiches
  CNIL ont été absentes de la recherche ≈ 17 minutes ; une panne non réparée les aurait retirées jusqu'à réparation.
- **Fausses nouvelles versions CNIL** : le HTML des pages change plusieurs fois par jour (identifiants techniques),
  sans que le texte change (3 fois le 2026-10-08). Chaque fois : nouvelle version, redécoupage, ≈ 1 min de
  vectorisation, et la fenêtre d'absence ci-dessus.

Et une contrainte : faire évoluer le schéma d'une base de production qui contient des données à garder. Les scripts
`db/init` ne s'exécutaient qu'à la création du volume ; la table des retours avait été ajoutée à la main en production.

## Options envisagées

1. **Garder la bascule à `fetch`, mais tout traiter dans une seule transaction** (télécharger, extraire, découper,
   vectoriser, puis basculer) : une transaction de plusieurs minutes, une seule commande au lieu de quatre
   (contraire à ADR-003 : commandes séparées, relançables, observables dans Airflow).
2. **Cycle de vie explicite** : une version collectée est une **candidate**, qui avance d'étape en étape ; elle ne
   devient courante qu'à une **publication** séparée, atomique.
3. **Deux bases ou deux schémas** (préparer dans l'un, basculer vers l'autre) : lourd pour 3 documents, et le
   problème est par document, pas pour tout le corpus.

Migrations : **DbUp** (bibliothèque .NET, scripts SQL numérotés, journal en base), plutôt qu'Entity Framework
(aucun ORM dans le projet : Npgsql direct) ou Flyway (outil Java de plus à installer).

## Décision

Option 2, et DbUp.

```
collected → extracted → chunked → (validated, S5 session 2) → embedded → published → archived
                │                          └→ quarantined (S5 session 3)
                └→ discarded : texte identique à la version publiée, ou candidate remplacée par une collecte plus récente
```

- `fetch` crée une candidate et **ne touche jamais à la version publiée** ; une collecte plus récente remplace une
  candidate inachevée (au plus une candidate par document : index unique partiel).
- `extract` calcule une **empreinte du texte extrait** (`TextFingerprint` : blocs, nature, niveau, ancre) ; une
  candidate au texte identique à celui de la version publiée est écartée (`discarded`), sans découpage ni
  vectorisation. Une collecte qui retrouve le HTML d'une version ainsi écartée compte comme « inchangée ».
- `publish` (nouvelle commande, nouvelle tâche du DAG) bascule, dans une transaction, chaque candidate entièrement
  vectorisée : l'ancienne version passe en `archived`, la candidate en `published`.
- `is_current` garde son sens (la version en service) ; une contrainte garantit `is_current = (status = 'published')`.
  **La recherche et l'application web ne changent pas.**
- Migrations : `src/Limpide.Infrastructure/Migrations/NNNN_*.sql`, appliquées par la commande `migrate`, chacune une
  seule fois par base et dans sa propre transaction (journal : table `schemaversions`). **Jamais au démarrage de
  l'application** : un changement de schéma de production est un acte explicite de la mise à jour. Les anciens
  scripts `db/init` deviennent 0001 et 0002, rejouables (`IF NOT EXISTS`), donc sans effet sur les bases existantes.

## Conséquences

- **Vérifié le 2026-10-10** :
  - migration sur une copie de la base locale et sur une base vide : même schéma à l'arrivée ; sur la copie,
    10 versions, 888 passages, 831 vecteurs et 2 retours conservés ; seconde exécution : 0 migration appliquée ;
  - cycle complet sur la copie (texte d'une version publiée modifié pour simuler un vrai changement) : la recherche
    sert 691 passages à chaque étape, y compris pendant les 42 s de vectorisation, puis bascule vers la nouvelle
    version ; une seconde exécution complète ne fait rien ;
  - DAG sur la base locale : 2 nouvelles versions CNIL (HTML changé), écartées par `extract` (texte identique) :
    aucun découpage, aucune vectorisation, recherche intacte.
- Les étapes travaillent sur les versions publiées **et** les candidates : les commandes restent idempotentes
  (changer de modèle d'embedding revectorise aussi la version publiée).
- **Limite assumée** : changer de version d'extracteur ou de découpeur retraite encore la version **publiée** sur
  place ; ses passages disparaissent de la recherche le temps de les revectoriser. Rare et déclenché par nous ;
  à traiter si cela devient fréquent (retraiter en créant une candidate, publiée à la fin).
- Le schéma de production évolue désormais par `migrate` (procédure : `deploy/README.md`).
- Les statuts préparent les sessions suivantes : `validated` et `quarantined` (contrôles qualité), `quality_report`
  (rapport des contrôles), et le back office (voir une version bloquée, la publier ou la rejeter).
