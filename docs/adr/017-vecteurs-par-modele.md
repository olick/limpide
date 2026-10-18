# ADR-017 : Une table de vecteurs par modèle, pour changer de modèle d'embedding sans interruption

- **Statut** : accepté
- **Date** : 2026-10-18

## Contexte

La session 1 de la S7 a montré que Qwen3-Embedding-8B trouve mieux les passages que bge-m3 (top 5 28/30 contre
21/30). La bascule doit passer par la chaîne CI/CD (ADR-015, ADR-016), comme toute modification qui change les
réponses. Jusqu'ici, chaque passage n'avait qu'un vecteur (`chunks.embedding`, avec `chunks.embedding_model`), ce
qui posait deux obstacles :

1. **Interruption** : revectoriser en place sort les passages de la recherche, qui filtre sur le modèle configuré,
   pendant ≈ 10 minutes.
2. **Évaluation impossible** : la sauvegarde que restaure l'évaluation (`corpus.dump`) ne porte qu'un vecteur par
   passage. La PR qui passe à Qwen serait évaluée sur des vecteurs bge-m3, et la recherche ne trouverait rien.

Un constat mesuré le 2026-10-18 sur la base locale (même volume qu'en production : 1 130 passages servis) : le
planificateur de PostgreSQL **n'utilise pas** l'index HNSW `chunks_embedding_hnsw`. Il parcourt les passages et
calcule toutes les distances, en ≈ 7 ms à chaud. La démo sert donc déjà une recherche **exacte**.

## Options envisagées

1. **Une table de vecteurs par modèle**, `chunk_embeddings (chunk_id, embedding_model, embedding)`. Les modèles
   coexistent ; `embed` remplit celui de la configuration, la recherche lit celui de la configuration. Coût : une
   migration et les requêtes de vectorisation, de recherche et de publication.
2. **Copie de la base**, revectorisée avec le nouveau modèle, puis changement de chaîne de connexion. Pas de code.
   En contrepartie : deux sauvegardes à tenir, l'ingestion gelée pendant toute la bascule, et une évaluation qui
   doit choisir sa sauvegarde selon la configuration.
3. **Revectoriser en place**, la démo dégradée pendant le calcul. Ne résout pas l'évaluation (obstacle 2).

## Décision

**Option 1** : les vecteurs vivent dans `chunk_embeddings`, clé `(chunk_id, embedding_model)`, sans index vectoriel.
Le modèle servi est une donnée de configuration (`Embedding`, libellé `EmbeddingOptions.Label`), jamais une donnée
de la base.

- **Migration `0005`** : la table et la copie des vecteurs existants. `chunks.embedding` et `chunks.embedding_model`
  ne sont plus ni lus ni écrits. Ils restent en place pour pouvoir redéployer l'image précédente, et seront retirés
  par une migration après la bascule.
- **`embed`** calcule les vecteurs **du modèle configuré** qui manquent, pour les versions publiées comme pour les
  candidates. Les autres modèles ne sont pas touchés. Il reste idempotent : un passage qui a déjà son vecteur pour
  ce modèle est ignoré. Pour un modèle qui n'est pas encore servi, on surcharge la configuration le temps de la
  commande (`--Embedding:Model=…`), sans option nouvelle.
- **Publication** : `embed` ne marque une candidate `embedded`, seul statut que `publish` accepte, que quand tous ses
  passages en ont un.
- **Recherche** (vectorielle et plein texte de l'hybride) : seuls les passages qui ont un vecteur du modèle
  configuré.
- **Pas d'index vectoriel**. On garde la recherche exacte, qu'on sert déjà. Un index HNSW commun aux modèles
  appliquerait le filtre sur le modèle **après** la recherche approchée, et pourrait rendre moins de passages que
  demandé.
- **`/sante`** affiche le modèle servi et le nombre de passages servis qui n'ont **pas** de vecteur de ce modèle
  (`missingEmbeddings`), donc qui sont absents de la recherche. La valeur attendue est 0.

## Conséquences

- **Changer de modèle devient un changement de configuration**, une fois ses vecteurs calculés en production.
  L'évaluation de la PR lit les vecteurs du modèle de la PR dans la sauvegarde, qui porte tous les modèles
  (`deploy/exporter-corpus.sh` en affiche le décompte).
- **Le retour arrière n'est pas gratuit.** Les vecteurs de l'ancien modèle ne sont tenus à jour que tant qu'il est
  servi. Un document publié après la bascule n'a que les vecteurs du nouveau modèle. Revenir en arrière, c'est donc
  changer la configuration **puis** lancer `embed`. La même règle vaut dans l'autre sens : entre le calcul des
  vecteurs du nouveau modèle et son déploiement, une publication d'Airflow n'aurait que ceux de l'ancien. On
  **suspend le DAG `ingestion`** pendant cette fenêtre, et on vérifie `missingEmbeddings = 0` sur `/sante` après
  le déploiement.
- **Redéployer l'image d'avant `0005`** reste possible tant qu'aucun passage n'a été vectorisé depuis : elle relit les
  anciennes colonnes, figées à la migration.
- **Vérifié avant la fusion** (base locale copiée, 2026-10-18) :
  - top 10 de 100 requêtes, ancienne requête contre nouvelle : 1 000 lignes, 0 différence de passage, de rang ou de
    score ;
  - `evaluate` identique ligne à ligne (10/30 en tête, 21/30 dans le top 5, MRR 0,51) ;
  - avec un second libellé, `embed` n'a calculé que les passages qui lui manquaient, et les vecteurs de
    `bge-m3+titre` n'ont pas changé (empreinte identique) ;
  - une candidate ne passe en `embedded` qu'avec tous les vecteurs du libellé configuré ;
  - `/sante` compte les passages qui n'ont pas de vecteur ;
  - recherche hybride (non servie, ADR-005) : une question change de rang (q30, 3 → 2). Cause : des ex aequo du
    classement plein texte, départagés selon le plan d'exécution. Défaut antérieur, que le changement de plan
    révèle (journal de la S7, session 6).
- **Volume** : ≈ 4 Ko par vecteur, soit ≈ 5 Mo par modèle pour le corpus actuel. Négligeable.
- **Même dimension pour tous les modèles** (`vector(1024)`). Qwen3-Embedding est réduit à 1 024 dimensions
  (`Embedding:RequestDimensions`). Un modèle d'une autre dimension demanderait une migration.
- **À revoir** :
  - à la mise à l'échelle (après le plan), si la recherche exacte devient trop lente : index HNSW partiel par
    modèle, ou parcours itératif des index de pgvector 0.8 (`hnsw.iterative_scan`). Comparer alors les scores, pas
    seulement la latence ;
  - si l'on abandonne l'idée de faire coexister deux modèles : on reviendrait à une colonne, au prix des deux
    obstacles du contexte.
