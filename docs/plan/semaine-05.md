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

1. **ADR — stratégie de découpage** : découpage structurel contre taille fixe, avec les observations de S1 et S5.
2. **ADR — batch incrémental plutôt que streaming** : fréquence de mise à jour des sources,
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
- ADR : stratégie de découpage, batch incrémental plutôt que streaming (le cycle de vie des versions a pris le n° 008, session 1)

## Ce qui prépare la suite

| Fait cette semaine | Réutilisé en |
|---|---|
| Outil de migrations | S6 : déploiement du schéma en production |
| Rapports de qualité | S8 : tableau de bord, S9 : dossier de gouvernance |
| Démonstration de quarantaine | S9 : vidéo, soutenance BC03 |
| Traçabilité réponse → source | S9 : dossier de gouvernance (auditabilité) |

---

## Journal de réalisation

Repris tel quel de `CLAUDE.md` le 2026-10-15 (rangement : `CLAUDE.md` ne garde que l'état courant, les règles et les pièges ; le détail des sessions vit ici).

**S5 terminée (2026-10-12)** (`docs/plan/semaine-05.md`) — qualité, quarantaine, migrations, back office.

- [x] Session 1 (2026-10-10) : ADR-008. DbUp 7.0.1 (`dbup-postgresql`), migrations dans
  `src/Limpide.Infrastructure/Migrations/` (0001 schéma, 0002 retours : anciens `db/init`, rejouables ; 0003 cycle de
  vie : `status`, `status_reason`, `text_hash`, `published_at`, `quality_report`), commande `migrate`. Statuts
  (`VersionStatus`) : collected → extracted → chunked → (validated) → embedded → published → archived ; discarded,
  quarantined. Nouvelle commande et tâche du DAG `publish`. `VersionStatus.ReadyToEmbed` = `chunked` en attendant
  `validate` (session 2 : passer à `validated`). Vérifié : migration sur copie et base vide (même schéma), cycle
  complet sans fenêtre d'absence, DAG local (2 fausses versions CNIL écartées). Base locale migrée.
  **Déployé en production le 2026-10-10** : sauvegarde, `git pull`, image ingestion, `migrate` (3 versions → published,
  691 passages, 1 retour conservés), `web` reconstruit (plus d'erreur Kerberos dans ses journaux) ; `postgres` recréé
  au passage (montage `db/init` retiré), données intactes. Testé dans le navigateur par Alexandre le 2026-10-10. Avertissement préexistant : clés DataProtection de `web`
  non persistées (perdues à chaque recréation du conteneur) — à traiter avec la sécurité (S8).
- [x] Session 2 (2026-10-10) : commande et tâche du DAG `validate` (entre `chunk` et `embed`), 8 contrôles déterministes
  dans `Limpide.Core/Quality` (`QualityValidator`, 20 tests) : texte minimal, langue (part de mots outils français),
  nombre de passages (± 30 % de la version publiée), longueur, doublons, structure (AI Act ≥ 100 articles, CNIL ≥ 4
  intertitres), résidus HTML, texte d'interface. Seuils en configuration (`Quality`, appsettings de la console),
  calibrés avec marge : `docs/notes/observations-qualite.md`. Candidate → `validated` ou `quarantined` (raison dans
  `status_reason`, rapport JSON dans `quality_report`) ; versions publiées : rapport seul. `ReadyToEmbed` = `validated`.
  Même contenu qu'une version en quarantaine à la collecte : elle y reste (`stillQuarantined`). Quarantaine = code 0
  (compté dans le résumé) : l'alerte sera faite par `bilan` (session 3). Vérifié sur copie : quarantaine bloque
  embed/publish, version publiée servie, levée simulée → validée, vectorisée, publiée.
- [x] Session 3 (2026-10-10) : `bilan` alerte sur une quarantaine (raison + marche à suivre) sans faire échouer
  l'exécution ; résumé JSON avec détails (`quarantinedVersions`, `stillQuarantinedVersions`). Commandes `versions`,
  `release <empreinte> <motif>` (→ validated, publiée au prochain passage), `reject` (définitif : même contenu ignoré
  à la collecte). Démonstration `pipelines/airflow/demo/quarantaine.sh` : fiche CNIL servie tronquée par nginx local
  (`source-demo`, réseau `limpide_default`, `fetchUrl` du corpus de démo) → quarantaine (passages −62 %, 3
  intertitres), alerte reçue, 691 passages servis. Base locale : 2 versions de démonstration en quarantaine
  (c3094b5d, 4e4b03a2), gardées comme exemples. **Reste (Alexandre)** : enregistrer la démonstration pour la vidéo.
  **Sessions 2 et 3 déployées le 2026-10-11** (sauvegarde, image ingestion, `migrate` : rien à appliquer, `web`
  reconstruit, 691 passages servis). `validate` en production : « texte extrait absent » pour les 3 versions — le volume
  `data/` du serveur est vide, le corpus de production vient d'une sauvegarde de la base (S3) et **aucune ingestion
  n'y a jamais tourné** : le corpus de production n'est mis à jour par aucun pipeline (sujet de la S6, hébergement
  d'Airflow).
- [x] Session 4 : traçabilité faite (2026-10-11) — `RetrievedPassage.Origin` (`PassageOrigin` : id du passage,
  empreinte de version, extracteur, découpeur) ; interface : « Version collectée le … · 8ee3433abfca » sous chaque
  extrait cité (SHA-256 complète au survol), colonne « Version » dans « Passages fournis au modèle » ; retours :
  passages cités avec leur version ; commande `trace <question>` (lignage : passage → version → fichiers brut et
  extrait → extracteur, découpeur, embedding → contrôles qualité). Élargissement (2026-10-11) : les 15 autres pages
  de la série CNIL « Les fiches pratiques IA » (licence revérifiée, `docs/sources.md`) ; lignes directrices de la
  Commission non retenues (pas de version française trouvée, PDF : à trancher par Alexandre). Premier vrai test des
  contrôles : 2 pages en quarantaine, fausses alertes du contrôle de longueur (recalibré : ≤ 10 % de passages < 100 ou
  > 2 000 caractères) et un vrai défaut d'extraction (« Pour approfondir », documents joints : extracteur `cnil-html/2`) ;
  décision `retry` ajoutée (recontrôler après correction du code), utilisée pour les 2 pages. 18 documents publiés,
  1 130 passages. Évaluation : recherche top 5 7/10 (8/10 avant), MRR 0,52 ; réponses 11/15, 0 échec, relecture :
  fidèles (`docs/notes/observations-decoupage.md`). **En production le 2026-10-11** : base locale transférée
  (`pg_dump --exclude-table=feedback` pour garder les retours de production), `pg_restore --clean`, `web` reconstruit
  et redémarré ; 18 documents, 1 130 passages, 1 retour conservé. Après chaque recréation de `web` : erreurs
  « antiforgery token could not be decrypted » dans ses journaux (clés DataProtection non persistées, sans effet
  pour les visiteurs).
- [x] Session 5 (2026-10-11) : ADR-009 (batch incrémental, mesuré sur 13 jours) et ADR-010 (découpage structurel,
  **mesuré** contre la taille fixe sur une copie de la base : `FixedSizeChunker`, 1 362 passages, top 5 5/10 contre
  7/10, MRR 0,37 contre 0,52). Index `docs/adr/README.md`, README.
- [x] Back office v1 (2026-10-12, en production sur https://admin.limpide-ia.fr) : `src/Limpide.Admin` (Blazor, port local 5182),
  exposé sur `admin.limpide-ia.fr` (conteneur `admin`, bloc Caddy `ADMIN_ADDRESS`, base seulement : ni clé Mistral ni
  Ollama). Connexion OpenID Connect générique (`AdminAuthentication.cs`, seul fichier qui connaît le protocole ;
  Microsoft comptes personnels, `consumers/v2.0`) ; accès par liste d'identifiants « émetteur#sujet » (`Actor`, Core,
  testé) ; page `/refuse` qui affiche l'identifiant à autoriser. Pages : tableau de bord, versions et détail (rapport
  de contrôles, passages, décision publier / recontrôler / rejeter avec motif), retours (traité, export CSV protégé
  contre l'injection de formules), journal, réglages en lecture seule. Migration 0004 : `feedback.handled_at/by`,
  table `admin_actions` (journal écrit dans la transaction de la décision ; la console écrit « console »).
  Clés DataProtection persistées (`web_keys`, `admin_keys`). Connexion testée en local par Alexandre (inscription
  Entra « comptes personnels », même application pour le poste et la production, donc même identifiant). Piège vu :
  l'identifiant autorisé est « émetteur#sujet » complet (≈ 119 caractères), pas un GUID du portail (l'ID d'annuaire
  `9188040d…` est seulement la partie émetteur). Déployé le 2026-10-12 : migration 0004, `admin` (256 Mo), Caddy
  (`ADMIN_ADDRESS`), `web` recréé avec ses clés ; vérifié : redirect_uri en https, PKCE S256, portées
  `openid profile`, pages protégées → Microsoft. Première connexion en production par Alexandre le 2026-10-12 : tout
  fonctionne. Épisode 22 écrit.

### Étape intermédiaire « rendre Limpide compréhensible par tous » (après la S5)

**En cours : étape intermédiaire « rendre Limpide compréhensible par tous »** (plan global, avant la S6 ; demandée le
2026-10-12). Code fait (2026-10-12) : en-tête « ce qu'il fait / ne fait pas / pourquoi il montre tout » (encart
« Vous échangez avec une IA » gardé : AI Act, article 50) ; « Sous le capot » = récit en 5 étapes construit sur les
valeurs réelles (durées, meilleur score, seuil `MinScore`, passages cités, garde-fous traduits par
`PlainLanguage.cs`), coût en clair, détails techniques repliés. Testé dans le navigateur par Alexandre et déployé le
2026-10-12 ; premier déploiement de `web` sans erreur « antiforgery » (clés DataProtection dans le volume `web_keys`).
**Reste** : validation par 2 ou 3 personnes non techniques (critère : expliquer sans aide ce que fait Limpide et ce
que montre le panneau), retours à noter dans `docs/plan/bilan-phase-1.md`.
