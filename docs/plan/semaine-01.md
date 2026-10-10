# Semaine 1 : environnement local, ingestion simple, pgvector

**Objectif de fin de semaine** : 2 ou 3 documents réels découpés, vectorisés et stockés dans PostgreSQL,
avec une requête de recherche qui ramène des passages pertinents. Pas encore de LLM, pas encore d'interface.

**Budget temps** : environ 8 à 10 heures, réparties en 5 sessions.

---

## Session 1 : environnement (≈ 1 h 30)

1. Installer le SDK .NET 10 : `bash scripts/install-dotnet-debian.sh`
2. Vérifier Docker Compose v2 : `docker compose version`
3. Démarrer les services :
   ```bash
   cp .env.example .env          # changer le mot de passe
   docker compose up -d
   docker compose exec ollama ollama pull bge-m3
   ```
4. Vérifier la base : `docker compose exec postgres psql -U rag -d rag -c "\dt"` doit lister
   `sources`, `documents`, `document_versions`, `chunks`.
5. Tester les embeddings :
   ```bash
   curl -s http://localhost:11434/api/embed -d '{"model":"bge-m3","input":"Qu'\''est-ce qu'\''un système d'\''IA à haut risque ?"}' \
     | python3 -c "import json,sys; print(len(json.load(sys.stdin)['embeddings'][0]))"
   ```
   Doit afficher `1024`.
6. Créer la solution : `bash scripts/bootstrap.sh`
7. Premier commit, dépôt GitHub public.

**Terminé quand** : `dotnet build` passe et les deux conteneurs sont en bonne santé.

## Session 2 : corpus et collecte (≈ 1 h 30)

1. Vérifier les conditions de réutilisation de chaque source et compléter `docs/sources.md`.
2. Commande `fetch` :
   - télécharge chaque URL de la liste dans `data/raw/<source>/<sha256>.<ext>` ;
   - calcule l'empreinte SHA-256 du contenu brut ;
   - si l'empreinte existe déjà pour ce document : ne rien faire (**idempotence**) ;
   - sinon : nouvelle ligne dans `document_versions`, l'ancienne passe à `is_current = false`.
3. Un `User-Agent` explicite et une pause entre les requêtes : on reste poli avec les sites publics.

**Terminé quand** : lancer `fetch` deux fois de suite ne crée qu'une seule version par document.

## Session 3 : extraction et découpage (≈ 2 h 30) — la session la plus importante

Deux commandes séparées : `extract` (fichier brut → `data/extracted/<source>/<sha256>.json`)
puis `chunk` (texte extrait → table `chunks`). Le texte extrait se relit à l'œil, et on peut comparer
plusieurs découpages sans réextraire.

1. Extraction :
   - HTML (AngleSharp) : ne garder que le contenu principal, retirer menus, pieds de page, bandeaux cookies ;
   - PDF (PdfPig) : texte page par page, repérer en-têtes et pieds de page répétés et les retirer.
2. Découpage **structurel** plutôt qu'à taille fixe :
   - pour l'AI Act : un passage par article, redécoupé par paragraphes numérotés si l'article est long ;
   - pour la CNIL : par titres de section ;
   - cible : 500 à 1 500 caractères, avec le titre de rattachement dans `heading`
     (ex. « Article 6 — Règles relatives à la classification… »).
3. Tests unitaires du découpeur sur 2 ou 3 extraits réels copiés dans les tests.

**Terminé quand** : les tests passent et une inspection manuelle de 20 passages au hasard ne montre
ni passage vide, ni article coupé au milieu d'une phrase, ni menu de navigation.

> Pourquoi c'est important : la qualité du découpage détermine davantage la qualité finale du RAG
> que le choix du LLM. Note ce que tu observes, ça nourrira un ADR sur la stratégie de découpage.

## Session 4 : embeddings et stockage (≈ 1 h 30)

1. Commande `embed` :
   - sélectionne les passages sans embedding de la version courante ;
   - calcule les embeddings par lots (16 ou 32) via `IEmbeddingGenerator` ;
   - écrit en base avec Npgsql + Pgvector (`dataSourceBuilder.UseVector()`), en renseignant `embedding_model`.
2. Mesurer et noter : nombre de passages, durée totale, durée par passage.

**Terminé quand** : `SELECT count(*) FROM chunks WHERE embedding IS NULL;` renvoie `0`.

## Session 5 : première recherche et bilan (≈ 1 h 30)

1. Commande `search "<question>"` : embedding de la question puis
   ```sql
   SELECT c.heading, left(c.content, 200), 1 - (c.embedding <=> @q) AS score
   FROM chunks c
   JOIN document_versions v ON v.id = c.document_version_id AND v.is_current
   ORDER BY c.embedding <=> @q
   LIMIT 5;
   ```
2. Écrire **10 questions de test** avec, pour chacune, le passage attendu
   (ex. « Quelles pratiques d'IA sont interdites ? » → Article 5).
   C'est l'amorce du jeu d'évaluation de la semaine 7.
3. Compter combien de questions ramènent le bon passage dans les 5 premiers résultats.
4. **Mesure complémentaire, une fois le score de référence noté** : vectoriser le titre de rattachement avec le texte
   (`heading + "\n" + content`), sans changer le texte stocké ni affiché. Un passage qui n'est pas le premier de
   son article perd la phrase qui introduit sa liste (ex. « h) l'utilisation de systèmes… » sans
   « 1. Les pratiques suivantes sont interdites: ») ; le titre (« Article 5 — Pratiques interdites… ») peut compenser.
   Recalculer les embeddings, repasser les 10 questions, comparer les deux scores. On ne garde la variante que si
   elle améliore le résultat, et on note la mesure dans `docs/notes/observations-decoupage.md` (matière de l'ADR sur la stratégie de découpage).
5. Compléter l'ADR-002 avec les mesures (temps d'ingestion, qualité observée).

**Terminé quand** : le score est noté dans le README, même s'il est mauvais. C'est le point de référence.

---

## Pièges à éviter cette semaine

- **Vouloir tout le corpus** : 2 ou 3 documents suffisent. L'élargissement viendra avec Airflow.
- **Optimiser trop tôt** : pas de recherche hybride ni de reclassement avant d'avoir le score de référence.
- **Oublier l'idempotence** : chaque commande doit pouvoir être relancée sans effet de bord.
  C'est ce qui rendra le passage à Airflow simple.
- **Commiter `data/` ou `.env`** : ils sont dans `.gitignore`, vérifier avant le premier push.

## Ce qui prépare la suite

| Fait cette semaine | Réutilisé en |
|---|---|
| Commandes séparées et idempotentes | S4 : tâches Airflow |
| Versions de documents horodatées | S5 : versionnement et traçabilité |
| 10 questions de test | S7 : jeu d'évaluation en CI |
| Mesures de temps d'ingestion | S8 : FinOps, dimensionnement |
| ADR-001 à 003 | Dossier de soutenance |
