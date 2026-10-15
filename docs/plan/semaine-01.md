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

---

## Journal de réalisation

Repris tel quel de `CLAUDE.md` le 2026-10-15 (rangement : `CLAUDE.md` ne garde que l'état courant, les règles et les pièges ; le détail des sessions vit ici).

**S1 terminée** (`docs/plan/semaine-01.md`) :

- [x] Session 1 : Docker, pgvector, Ollama, solution .NET créée
- [x] Session 2 : corpus (`corpus.json`) et commande `fetch`
- [x] Session 3 : commandes `extract` et `chunk`, extracteurs et découpeurs AI Act / CNIL testés
- [x] Session 4 : commande `embed`, mesures dans l'ADR-002 (691 passages, 9 min 31 s sur CPU, 816 ms par passage)
- [x] Session 5 : `search`, `evaluate`, 10 questions (`eval/questions.json`), score dans le README :
  top 5 = 5/10 (texte seul) puis **8/10 (titre + texte, retenu)**

Constats de la semaine 1 à reprendre en S2 (détail : `docs/notes/observations-decoupage.md`) :
les considérants évincent les articles en tête des résultats (recherche hybride, ADR-005) ; le vocabulaire
des utilisateurs diffère de celui du règlement (« grands modèles de langage » contre « modèles d'IA à usage général »).
`evaluate` donne le score à chaque modification de la recherche : le relancer avant/après, noter dans le README.
Observations de découpage à compléter au fil de l'eau : `docs/notes/observations-decoupage.md` (matière de l'ADR-010).

Notes de la session 2 :

- EUR-Lex bloque les scripts (défi AWS WAF, `202` vide) : l'AI Act est téléchargé via l'API CELLAR
  (`fetchUrl` dans `corpus.json`). Le fichier brut est du XHTML Formex/CONVEX, pas la page EUR-Lex :
  en tenir compte pour l'extraction.
- `fetch` exige un `200` non vide et le bon type de contenu : un blocage ne crée jamais de version.
- Mot de passe PostgreSQL local dans `dotnet user-secrets` (projet Ingestion), jamais dans `appsettings.json`.

Choix de la session 3 (voir la discussion du 2026-09-29) :

- Deux commandes `extract` puis `chunk`, qui échangent via `data/extracted/<source>/<sha256>.json` :
  le texte extrait se relit à l'œil, et on peut comparer des découpages sans réextraire (futur ADR).
- Chaque extracteur et découpeur a une version (`cnil-html/1`...) enregistrée avec son résultat :
  la changer relance l'étape, sinon la commande ne refait rien.
- Extracteurs dans Core (AngleSharp, sans E/S), choisis selon la source. Ils ne produisent que du texte présent
  dans la source ; seuls les espaces sont normalisés. Blocs : titre (niveau), paragraphe, élément de liste
  (profondeur), avec l'ancre ELI pour l'AI Act (`art_5`, `005.001`, `rct_1`, `anx_III`).
- AI Act : titre, en-tête et pied du JO, notes, formule finale et signatures écartés ; considérants gardés.
- Découpeurs (`ai-act/1`, `cnil-sections/1`) : unités article / considérant / annexe / préambule pour l'AI Act,
  section pour la CNIL ; `Packer` commun (500 à 1 500 caractères, coupe entre groupes, puis blocs, puis phrases,
  jamais dans une phrase). Modifier `Packer` impose d'incrémenter la version des deux découpeurs.
- Table `chunks` : `anchor` (`art_5`, `rct_12`, `anx_III`, clé des questions de test), `extractor_version`
  et `chunker_version` ; `chunk` remplace tous les passages d'une version dans une transaction (COPY binaire).
- Pour recréer la base sans perdre bge-m3 : `docker compose rm -sf postgres && docker volume rm limpide_pgdata`,
  jamais `docker compose down -v` (efface aussi le volume Ollama) ; puis `migrate` (le schéma n'est plus créé au
  démarrage du conteneur depuis la S5).
- La fiche CNIL « Annoter les données » contient un paragraphe en double dans la page elle-même : laissé tel quel.
- **Fausses nouvelles versions CNIL** (constaté le 2026-09-29) : le HTML brut contient des identifiants aléatoires
  (menu, jeton `form_build_id`) qui changent d'un jour à l'autre ; chaque `fetch` crée une version dont le texte
  extrait est identique. Sans gravité tant que la collecte est manuelle ; à traiter en S5 avec le cycle de vie
  des versions : ne publier une version que si son texte extrait diffère de la version publiée.

Console (`src/Limpide.Ingestion/Cli/`) : sans argument, mode interactif (`limpide>`, `help`, `exit`) ;
avec une commande, exécution unique et code de sortie (mode d'Airflow en S4, à préserver). La liste des
commandes (`CommandCatalog`) alimente l'aide et la validation : toute nouvelle commande s'y déclare.
Configuration et services reconstruits à chaque commande, pour que `--Section:Cle=valeur` ne vaille que pour elle.
VS Code : profils `Debug` / `Release` (`.vscode/launch.json`), `DOTNET_ENVIRONMENT=Development` pour user-secrets.

Choix de la session 4 :

- `embed` traite les passages des versions courantes sans embedding **ou** calculés avec un autre modèle que
  `Embedding:Model` : changer de modèle en configuration relance le calcul. Lots enregistrés un par un
  (reprise après interruption). Configuration : section `Embedding` d'`appsettings.json`
  (`--Embedding:BatchSize=32` en ligne de commande pour essayer une autre taille de lot).
- Ollama tourne sur CPU : le GPU du poste n'est pas exposé au conteneur (pas de NVIDIA Container Toolkit).
  Volontaire : c'est la configuration la plus proche de l'hébergement probable. Question ouverte dans l'ADR-002 :
  où tourne le modèle qui vectorise les questions en ligne, et combien il coûte au repos (ADR-006, puis l'ADR sur l'hébergement des modèles, S6).

Choix de la session 5 :

- `evaluate` : rang du premier bon passage sur 10 résultats ; top 1, top 5, MRR. Passage attendu par `anchor`
  (AI Act) ou morceau du titre de rattachement (CNIL, sans ancre). Questions formulées sans reprendre les mots du titre.
- `Embedding:IncludeHeading = true` : on vectorise « titre + texte » ; `embedding_model = bge-m3+titre`.
  La recherche ne compare que des vecteurs de même libellé.
