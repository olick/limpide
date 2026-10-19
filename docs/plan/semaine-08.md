# Semaine 8 : observabilité, coûts, sécurité

**Objectif de fin de semaine** : pour n'importe quelle requête de la démo, on retrouve sa trace complète,
son coût et les garde-fous déclenchés, et on suit la santé du système dans le temps.

**Budget temps** : environ 10 heures, en 5 sessions.

**Prérequis** : chaîne CI/CD avec évaluation (semaine 7).

**Reportés de la S7** : garde-fou « Oui » en tête sur la situation de l'utilisateur non détecté (session 4) ; alerte
de budget Mistral (session 3) ; anti-bot et test du message à la 11e question (session 5) ; fidélité jugée plus
basse avec Qwen (0,21 et 0,24 contre 0,09 à 0,15), réponses signalées à relire ; retrait de l'ancienne colonne de
vecteurs et de l'index HNSW.

**Points ouverts des sessions 1 et 2** (relevés le 2026-10-19, détail dans le journal de réalisation) :

| Point | Pourquoi | Quand |
|---|---|---|
| Refus d'un autre compte Microsoft dans Grafana, jamais testé | la règle n'accepte que la personne autorisée au back office (relecture sans point bloquant), mais seul un essai le prouve ; il faut un second compte Microsoft personnel, même jetable | avant de montrer Grafana en rendez-vous |
| Identifiants par défaut de la base d'Airflow sur le serveur (`airflow` / `airflow`) | elle contient les variables d'Airflow (clé OVHcloud, webhook Discord) et Grafana est désormais sur son réseau | session 5 |
| 19 failles élevées ou critiques corrigeables dans Grafana 13.2.4 (Grype) | Grafana est exposé sur Internet ; failles surtout dans des greffons inutilisés, quelques-unes dans Grafana même (`kin-openapi`, plantages) | session 5 : analyse hebdomadaire des images de la supervision (signaler sans bloquer la démo), montée de version à chaque correctif |
| Journaux bruts des conteneurs de la démo sans rotation ni plafond (`docker-compose.prod.yml` n'a pas de bloc `logging`) | ils peuvent contenir le texte de questions : avant la PR #24, l'application l'y écrivait en cas d'erreur | session 5 : purge, puis plafond de taille et de durée (`logging:`) ; nettoyer au passage ≈ 13,5 Go récupérables par Docker |
| Secret client Microsoft partagé entre Grafana et le back office | Grafana compromis exposerait aussi le secret du back office | amélioration, sans urgence : une inscription d'application propre à Grafana |
| Recréer Grafana sur le serveur après la fusion de la PR #30 | messages d'alerte protégés ; la chaîne ne déploie pas la supervision | dès que possible, puis rayer la ligne : `docker compose -f supervision/docker-compose.yml up -d --force-recreate grafana` |

---

## Session 1 : traces avec OpenTelemetry (≈ 2 h, révisée le 2026-10-19)

**Révision** : le plan exportait vers la supervision d'Azure, mais la démo est sur le VPS (ADR-006) et Azure n'est
qu'une vitrine détruite la plupart du temps. Décision avec Alexandre : pile auto-hébergée sur le VPS (collecteur
OpenTelemetry, Tempo, Loki, Prometheus, Grafana) et journal des réponses en base (**ADR-018**, accepté le 2026-10-19 après mesure sur le serveur).

1. **Journal `answer_log`** (migration 0006) : une ligne par question (issue, `trace_id`, modèle, consignes, scores,
   latences par étape, tokens, coût, garde-fous), ni texte ni IP.
2. Instrumenter `Limpide.Web` avec OpenTelemetry : requêtes HTTP, accès base, appels HTTP sortants, journaux.
   **Blazor Server** : une question passe par le circuit SignalR, pas par une requête HTTP ; la trace part de
   l'activité de l'événement Blazor (.NET 10), les étapes du RAG s'y rattachent par le code.
3. **Traces du pipeline RAG** : une activité par étape (embedding de la question, recherche, garde-fous, génération),
   avec en attributs le nombre de passages, les scores, les tokens, le coût, les garde-fous déclenchés.
4. Instrumenter l'appel au LLM : Microsoft.Extensions.AI propose une instrumentation OpenTelemetry qui suit
   les conventions sémantiques de l'IA générative. **Ne pas enregistrer le texte des questions** par défaut (voir
   session 5), ni dans les traces, ni dans les messages (`Home.razor` l'écrit aujourd'hui dans le message d'erreur).
5. Tout exporter vers le collecteur ; pile de supervision en local (`docker compose`), **mémoire mesurée** sous
   charge avant de fixer les plafonds et de déployer sur le VPS.

**Terminé quand** : une question dans la démo produit une trace complète, étape par étape, et une ligne du journal
qui y mène.

## Session 2 : tableaux de bord et alertes (≈ 2 h)

1. **Tableau de bord technique** : latence (médiane, 95e centile) par étape, taux d'erreur, volume de requêtes.
2. **Tableau de bord qualité** : taux de « je ne sais pas », de refus, de citations invalides, score moyen des passages.
3. **Suivi de la dérive** : l'évolution de ces taux dans le temps. Une hausse du « je ne sais pas » peut signaler
   des questions d'un nouveau type, ou une dégradation du corpus.
4. **Alertes** (vers Discord) : taux d'erreur, latence anormale, échec du DAG, quarantaine ; **DAG qui n'a pas
   tourné** (aujourd'hui, une exécution réussie et une exécution absente sont muettes toutes les deux, constaté le
   2026-10-19) ; serveur : mémoire **disponible**, swap, disque.

**Terminé quand** : les deux tableaux de bord existent et une alerte a été déclenchée en test.

## Session 3 : FinOps et GreenOps (≈ 2 h)

1. **Coût par requête** (tokens) et **coût par ingestion** (embeddings, calcul), agrégés par jour.
2. **Coût d'infrastructure** par environnement : factures OVHcloud (VPS, AI Endpoints) et Mistral pour la démo,
   facturation Azure pour la vitrine et l'évaluation. **Alerte de budget Mistral** (reportée de la S7).
3. Mesurer l'effet du seuil de pertinence (questions sans appel au LLM) ; la **mise à l'échelle jusqu'à zéro** ne
   concerne que la vitrine Azure.
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
   **Relevé en session 2** (`relecteur-infra`) : la base d'Airflow du serveur garde les identifiants par défaut
   (`airflow` / `airflow`, `pipelines/airflow/docker-compose.yaml`) ; elle est joignable depuis son réseau, où Grafana
   est désormais attaché. Les changer (elle contient les variables d'Airflow, dont la clé OVHcloud et le webhook).
2. **Analyse des images** de conteneurs dans la chaîne CI (déjà amorcée en S7) : traiter les vulnérabilités critiques.
   **Ajouté le 2026-10-19** : Grafana est exposé sur Internet (sous-domaine `grafana.limpide-ia.fr`) ; Grype y trouve 19 failles élevées
   ou critiques corrigeables (13.2.4), surtout dans des greffons inutilisés. Analyser chaque semaine les images de la
   supervision (signaler sans bloquer le déploiement de la démo), monter de version à chaque correctif ; envisager de
   retirer les greffons inutilisés de l'image.
3. **Rotation des secrets** : procédure documentée pour la clé d'API du LLM.
4. **Questions des utilisateurs** : elles peuvent contenir des données personnelles.
   Décider et documenter : pas de conservation du texte, ou conservation courte et pseudonymisée pour l'amélioration.
   Mettre à jour la mention d'information de la démo en conséquence, **y compris pour ce qui existe déjà depuis la
   session 1** : journal `answer_log` (12 mois, sans texte) et journaux des visites (Loki, 14 jours : identifiant de
   visite aléatoire, pages, longueur des questions, mesures ; donnée pseudonyme).
   **Si le texte est conservé** (demandé le 2026-10-19 : les journaux de la session 1 ne montrent que des mesures),
   décider aussi :
   - quoi : la question seule, ou la question et la réponse ;
   - où : dans les journaux (Loki, 14 jours, avec la visite et le `trace_id`, à côté des messages actuels), ou dans
     une table à part liée à `answer_log` (durée choisie, purge, consultable dans le back office) ; pas dans les
     traces (Tempo n'a ni purge ciblée ni contrôle d'accès fin) ;
   - comment : durée, accès (Grafana derrière la connexion Microsoft, rôle Editor ; back office authentifié), droit d'opposition ou case à cocher,
     filtre des données personnelles évidentes (adresse électronique, téléphone) avant écriture ;
   - **déjà à traiter, quelle que soit la décision** : les journaux bruts des conteneurs de la démo (`docker logs`,
     sans rotation) peuvent contenir le texte de questions : avant la PR #24 (2026-10-19), `Home.razor` l'écrivait
     dans le message d'erreur. Les purger, puis plafonner leur taille et leur durée (`logging:` dans
     `docker-compose.prod.yml`, comme `supervision/`) ; nettoyer au passage les ≈ 13,5 Go récupérables par Docker ;
   - puis l'appliquer : message « Question reçue » enrichi (`Visit.cs`), règle « jamais le texte » de
     `.claude/rules/supervision.md` et ADR-018 révisés, mention d'information, registre des traitements (S9).

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

---

## Journal de réalisation

**Session 1 : supervision (commencée le 2026-10-19)** :

- Première exécution automatique du DAG depuis la bascule Qwen (lundi 2026-10-19, 4 h UTC) : **réussie**, 1 min 50 s,
  aucune publication (empreinte `65e689755765` inchangée). Aucune alerte Discord : `bilan` ne prévient qu'en cas
  d'échec ou de quarantaine, vérifié par `airflow dags list-runs ingestion` (la règle Airflow disait l'inverse,
  corrigée).
- Décision (avec Alexandre) : pile auto-hébergée sur le VPS, **avec** collecteur OpenTelemetry (une seule adresse pour
  l'application, filtrage des attributs sensibles, destination changeable sans toucher le code) et **avec** Loki ;
  Aspire Dashboard écarté comme repli ; journal `answer_log` en base pour l'enregistrement métier (ADR-018, proposé).
  Estimation non mesurée : 600 à 850 Mo, ≈ 1,3 Go de plafonds, pour ≈ 1,9 Go disponibles.
- [x] Plan révisé (session 1 : VPS au lieu d'Azure ; session 2 : alerte « DAG qui n'a pas tourné » et alertes du
  serveur ; session 3 : factures OVHcloud et Mistral, alerte de budget Mistral), ADR-018 écrit.
- [x] **Journal des réponses** : migration `0006_journal_des_reponses.sql` (`answer_log`, ni texte ni IP, longueur de
  la question seulement, purge à 12 mois à chaque écriture), `AnswerLogEntry` (Core), `AnswerLogStore`. Issues :
  réponse, je-ne-sais-pas, hors-perimetre, quota-visiteur, quota-journalier, delai-depasse, erreur (type de
  l'exception, jamais son message). Un échec d'écriture ne prive pas le visiteur de sa réponse. `Home.razor`
  n'écrit plus la question dans le message d'erreur (elle partirait dans Loki).
- [x] **Instrumentation** : `AnswerTelemetry` (Core, bibliothèque standard), deux activités (« Réponse à une
  question », « Recherche des passages ») avec issue, scores, tokens, coût, un événement par garde-fou ;
  `UseOpenTelemetry()` de Microsoft.Extensions.AI sur la rédaction et la vectorisation, `EnableSensitiveData = false`
  fixé dans le code (la variable `OTEL_INSTRUMENTATION_GENAI_CAPTURE_MESSAGE_CONTENT` l'activerait sinon) ; web :
  ASP.NET Core, HttpClient, Npgsql, Blazor (.NET 10 : une activité par événement, racine de la trace d'une question),
  journaux en OTLP. Export seulement si `OTEL_EXPORTER_OTLP_ENDPOINT` est défini. Paquets : OpenTelemetry 1.20.0,
  Microsoft.Extensions.AI 10.10.0 (10.10.1 n'existe pas).
- [x] **Pile locale** (`supervision/`, projet Compose `limpide-supervision`) : collecteur 0.162.0, Tempo 3.1.0
  (un seul processus, sans Kafka), Loki 3.7.8, Prometheus 3.15.0 (récepteur OTLP), Grafana 13.2.3 (sources créées au
  démarrage, liens trace ↔ messages), node-exporter 1.12.1. Seul Grafana publie un port (127.0.0.1:3000).
- [x] Vérifié de bout en bout (6 questions d'Alexandre) : chaque ligne d'`answer_log` porte le `trace_id` de sa
  trace ; trace complète (événement Blazor → réponse → recherche → vectorisation OVHcloud, SQL → Mistral → écriture
  du journal) ; **aucun texte libre** dans les attributs ni dans les messages ; question hors sujet : 0,343, modèle
  non appelé, coût nul, 222 ms. Répartition typique : Mistral 1,8 à 3,1 s, vectorisation 115 à 865 ms, SQL 18 à
  51 ms (219 ms à froid).
- **Constat** : une vectorisation à **5,5 s** chez OVHcloud (question de 09:25:05, total 7,6 s), contre 115 à 865 ms
  pour les autres. Première matière pour l'alerte de latence (session 2).
- **Mémoire** (détail : `supervision/README.md`) : ≈ 650 Mo réels sous charge, 350 Mo au repos, plafonds ≈ 1,55 Go.
  Grafana 13 (353 Mo) et Tempo 3 (250 Mo avant `GOMEMLIMIT`, 120 Mo après) dépassent l'estimation ; `GOMEMLIMIT`
  ajouté à tous les conteneurs Go.
- [x] Relecture (`relecteur-regles`) : le **message d'une exception** partait dans Tempo (`AddException`, événement
  `gen_ai.client.operation.exception` de Microsoft.Extensions.AI) et dans Loki, alors qu'il peut citer la question.
  Corrigé : le code ne trace que le type ; transformation `transform/messages-d-erreur` du collecteur (statut,
  `exception.message`, `exception.stacktrace` vidés). Vérifié par un envoi forgé (5 faux textes de question, adresse
  IP) : rien n'arrive dans Tempo ni Loki, `error.type` et `exception.type` restent. Source PostgreSQL de Grafana et
  compte en lecture seule : session 2.
- [x] **Journaux des opérations** (demande d'Alexandre : Loki ne montrait que le démarrage, l'application n'écrivait
  presque rien). `Visit.cs` : identifiant de visite aléatoire par circuit Blazor (ni IP ni cookie), messages
  structurés (`LoggerMessage`) : visite ouverte, page, question reçue (longueur), recherche (passages, meilleur score,
  seuil, durées), rédaction (modèle, consignes, tokens, coût, durée), garde-fou déclenché (avertissement), réponse
  (issue, passages cités, durée totale), hors périmètre, retour envoyé, visite terminée (pages, questions, durée
  active). Texte des questions toujours exclu (option écartée : décision de la session 5). Vérifié (parcours
  d'Alexandre, visite `bc9593bb`) : 4 questions racontées étape par étape avec leur `trace_id`, fin de visite écrite
  ≈ 3 min après la fermeture de l'onglet (aucun signal de déconnexion reçu : Blazor garde le circuit 3 min).
  Point 4 de la session 5 complété : où et comment conserver le texte s'il l'est, et mention d'information à
  étendre aux journaux déjà en place.
- [x] **Préparation du déploiement** : `OTEL_EXPORTER_OTLP_ENDPOINT` transmis à `web` (vide par défaut : la fusion
  n'active rien), procédure dans `deploy/README.md` (« Supervision » : prérequis, mise en place, tunnel SSH sur le
  port 3001, vérification, retour arrière, mises à jour), `supervision/memoire.sh` (mémoire réelle de chaque
  conteneur, lue dans le cgroup, et `MemAvailable`). Relectures `relecteur-infra` et `relecteur-regles` : rien de
  bloquant ; corrigé : la supervision est tuée en premier en cas de manque de mémoire (`oom_score_adj: 500`), sans
  droit au swap (`memswap_limit`), journaux des conteneurs plafonnés (3 × 10 Mo), conservation de Tempo écrite
  (`backend_worker.compaction.block_retention`), `GOMEMLIMIT` du collecteur sous son seuil de refus, fichier de
  configuration modifié = `--force-recreate`.
- **Constat hors sujet** (`relecteur-infra`) : les conteneurs de la démo (`docker-compose.prod.yml`) n'ont pas de
  rotation de leurs journaux : ils grossissent sans limite sur le disque du serveur. À traiter en session 5
  (sécurité), avec la mesure du disque.
- [x] **Message Discord de succès du DAG** (PR #25) : `bilan` envoie aussi un message quand l'exécution réussit
  (documents vérifiés, versions collectées, écartées, publiées, durée). Déclenché à la main sur le serveur : message
  reçu.
- [x] **Déploiement sur le VPS (2026-10-19)**, PR #24 et #25 fusionnées. Avant : 1 977 Mo disponibles, disque à 63 %
  (15 Go libres). Pile démarrée (`supervision/.env` créé sur le serveur, mot de passe jamais affiché),
  `OTEL_EXPORTER_OTLP_ENDPOINT=http://collecteur:4317` ajouté à `.env.prod` (copie `.env.prod.avant-supervision`),
  `web` recréé. Vérifié : 3 questions dans `answer_log` avec leur `trace_id`, traces dans Tempo, démo inchangée
  (`/sante`). Mémoire (`supervision/memoire.sh`) : pile 394 Mo réels, 1 541 Mo disponibles ; au repos après une
  ingestion, pile 505 Mo, **1 354 Mo disponibles**, swap 140 Mo, aucun arrêt. **ADR-018 accepté.** Vitrine des
  technologies : groupe « Supervision » (OpenTelemetry, Grafana, Prometheus).
- **Constats** :
  - **une modification du DAG n'est pas déployée par la chaîne** : le workflow « Déploiement » ne se déclenche que
    sur `src/`, `deploy/`, `docker-compose.prod.yml`… ; le DAG du serveur ne change qu'au `git merge` de
    `deployer.sh`, donc au prochain déploiement de l'application. Contourné à la main (`git pull --ff-only` sur le
    serveur). Décidé avec Alexandre : `pipelines/airflow/dags/**` ajouté aux chemins du workflow « Déploiement »
    (même version d'images : ni reconstruction, ni redémarrage ; relu par `relecteur-infra`). Idée pour plus tard :
    contrôler l'import du DAG en CI avant fusion (un DAG cassé n'apparaît que dans l'interface d'Airflow) ;
  - Docker garde ≈ 13,5 Go récupérables sur le serveur (images 10 Go, cache de construction 3,5 Go, restes des
    constructions sur le serveur d'avant la S7) : à nettoyer en session 5, avec la rotation des journaux ;
  - l'ajout de la ligne à `.env.prod` avait été donné sans commande : `web` a d'abord été « recréé » sans la
    variable (Compose ne voit rien changer, « Healthy » sans « Recreated »). Une seule commande (sauvegarde, ajout,
    recréation, vérification dans le conteneur) a suffi ensuite ;
  - creux de mémoire pendant l'ingestion : à lire dans Prometheus (`node_memory_MemAvailable_bytes`), la mesure
    manuelle est arrivée après la fin de l'exécution.
- Reste pour la session 1 : rien. Session 2 : tableaux de bord, alertes (dont « DAG qui n'a pas tourné »), source
  PostgreSQL de Grafana.
- [x] Chaîne : `pipelines/airflow/dags/**` déclenche le déploiement (PR #27). Vérifié à sa fusion : images « déjà
  publiée », `web` et `admin` « Running » (non recréés, démarrés à 17:43:43 avant comme après), site en ligne.

**Session 2 : tableaux de bord et alertes (2026-10-19)** :

- [x] **Grafana lit deux bases en lecture seule** (compte `grafana_lecture`, `supervision/compte-lecture.sh`, mot de
  passe dans `supervision/.env`) : `answer_log` dans la base de la démo (rôle `lecture_supervision`, migration
  `0007_lecture_supervision.sql` ; vérifié : `answer_log` lisible, `feedback` refusée), `dag_run` et `import_error`
  dans celle d'Airflow. Hôtes désignés par le nom du conteneur : Grafana est sur deux réseaux qui ont chacun un
  service `postgres`.
- [x] **Deux tableaux de bord versionnés** (`supervision/grafana/dashboards/`, chargés au démarrage, dossier
  « Limpide ») : « technique » (questions par issue, latence par étape, erreurs, coût par jour, 5xx, mémoire et swap,
  disque, dernières questions sans texte avec lien vers la trace) et « qualité et dérive » (« je ne sais pas » et hors
  périmètre, meilleur score face au seuil, garde-fous, passages cités). Les 14 requêtes passent.
- [x] **8 alertes sur Discord** (`grafana/provisioning/alerting/alertes.yaml`), chacune avec « que faire » et
  l'étiquette `environnement` (poste, serveur) : mémoire disponible < 300 Mo, swap > 1 Go, disque > 85 %, démo
  muette 10 min, ≥ 3 erreurs en 15 min, médiane > 15 s, **aucune ingestion réussie depuis 8 jours**, DAG cassé.
  L'échec et la quarantaine restent dans le message du DAG.
- **Écart au plan** : métriques d'Airflow essayées (OpenTelemetry, `otel_on`, déjà dans l'image 3.3.1) puis
  **abandonnées** pour l'alerte « le DAG n'a pas tourné » : un compteur n'existe qu'à partir de sa première mesure et
  repart de zéro au redémarrage de l'ordonnanceur (alerte à tort après un redémarrage), et reste figé quand le DAG est
  en pause sans redémarrage (alerte jamais déclenchée). Chaque tâche apparaissait en plus comme une instance
  distincte. Grafana lit `dag_run` à la place.
- **Défaut antérieur corrigé** : en mode allégé sur le poste, l'ordonnanceur ne recevait pas la clé OVHcloud
  (`AIRFLOW_VAR_LIMPIDE_EMBEDDING_API_KEY`, donnée au serveur seulement) : toute exécution locale échouait au rendu du
  modèle de la tâche (« Variable limpide_embedding_api_key not found »). Constaté sur l'échéance manquée du jour,
  lancée au démarrage d'Airflow (piège connu) ; après correction, exécution réussie.
- [x] **Alerte déclenchée en test** : « La démo n'envoie plus de mesures » (démo locale arrêtée), déclenchée à
  18:11:04 UTC, envoyée à Discord sans erreur ; démo relancée, alerte **résolue** à 18:14:02 UTC (message de
  résolution envoyé sans erreur).
- [x] Relectures (`relecteur-regles`, `relecteur-infra`) : rien de bloquant. Corrigé : droits **par colonne** dans
  la base d'Airflow (`dag_run` : `dag_id`, `state`, `end_date` ; `import_error` : `id`, `filename`, `timestamp` ;
  vérifié : `dag_run.conf` et `variable` refusées, les 8 alertes saines) ; `compte-lecture.sh` vérifie d'abord que les
  deux bases tournent ; procédure du serveur rejouable, Airflow dans les prérequis, ordre démarrage/arrêt, webhook
  obligatoire pour toute commande de la pile, restauration d'une sauvegarde. Vérifié sans objet : `$values` des
  messages d'alerte conservé par le provisionnement. Reporté en session 5 : identifiants par défaut de la base
  d'Airflow sur le serveur.
- Reste : installation sur le serveur (`deploy/README.md`, « Supervision » : webhook, `LIMPIDE_ENVIRONNEMENT=serveur`,
  `compte-lecture.sh limpide-prod-postgres-1`, Grafana recréé). Ensuite (demandé par Alexandre) : Grafana accessible
  depuis le back office, connexion Microsoft gérée par Grafana (option A, PR à part).

**Grafana depuis le back office (2026-10-19, demande d'Alexandre, option A)** :

- Choix (ADR-018 révisé) : Grafana servi par Caddy, lien « Supervision » du back office, **connexion Microsoft gérée
  par Grafana** (même inscription d'application et même personne autorisée que le back office : `iss` et `sub`
  d'`ADMIN_ALLOWED_USER`, rôle strict, toute autre refusée). Écartés : le back office comme porte d'entrée (confiance
  dans un en-tête transmis par Caddy), le tunnel seul.
- [x] `supervision/connexion-microsoft.sh` (recopie `ADMIN_OIDC_*` et la règle d'accès dans `supervision/.env`, sans
  affichage) ; Grafana : `generic_oauth` avec PKCE, connexion automatique, formulaire désactivé ; lien du back office
  (`Site:SupervisionUrl`, administrateurs seulement).
- [x] Vérifié sur le poste : connexion Microsoft d'Alexandre, compte créé (« Generic OAuth ») ; API : 200 avec le mot
  de passe, 401 sans. Le refus d'un autre compte n'a pas été essayé (pas de second compte).
- **Constat** (Grype, avant d'exposer) : Grafana 13.2.3 a 21 failles élevées ou critiques corrigeables (201
  occurrences), 13.2.4 en a 19 : passage en 13.2.4. Surtout dans des greffons inclus et inutilisés (MSSQL, MySQL,
  OpenTSDB, Cloud Monitoring) ou en HTTP/2 (non atteint : Caddy parle HTTP/1.1 à Grafana) ; dans Grafana même,
  `kin-openapi` (surtout des plantages). Suivi en session 5.
- **Revu après `relecteur-infra`** : d'abord servi sous https://admin.limpide-ia.fr/grafana/, **écarté** : même
  origine que le back office, une faille de Grafana exécutant du JavaScript agirait dans le back office avec la
  session de l'administrateur. Décision avec Alexandre : **sous-domaine `grafana.limpide-ia.fr`** (entrée DNS par
  Terraform, bloc Caddy `{$GRAFANA_ADDRESS}`, vide = Grafana fermé au public, ce qui sert de secours). Durcissements :
  Caddy refuse l'authentification par mot de passe venue d'Internet (403 ; mot de passe `admin` par tunnel seulement)
  et masque `/metrics` ; métriques internes de Grafana coupées ; cookie « Secure » en HTTPS ; HSTS ; rôle **Editor**
  (pas d'ajout de source de données depuis Internet) ; Caddy recréé et non redémarré (fichier monté) ; rayon
  d'action d'un Grafana compromis écrit dans l'ADR (mots de passe de lecture, webhook, secret client, réseaux).
- Seconde relecture : rien de bloquant ; corrigé : filtre de Caddy insensible à la casse (`(?i)^basic\s`), secours
  (désactiver aussi la connexion Microsoft pour retrouver le formulaire par le tunnel), DNS vérifié avant d'exposer
  (certificat), `GRAFANA_ADDRESS` écrit sans doublon. Amélioration notée : une inscription d'application propre à
  Grafana, pour ne plus partager le secret client avec le back office.
- [x] Adresse de retour du portail remplacée par `https://grafana.limpide-ia.fr/login/generic_oauth` (Alexandre).
- [x] **Installation sur le serveur (2026-10-19)**, PR #28 et #29 fusionnées : deux déploiements et `terraform apply`
  (une entrée DNS ajoutée, `grafana.limpide-ia.fr` → 57.129.175.88). Session 2 : migration 0007 présente, webhook
  copié, comptes de lecture créés, Grafana recréé ; vérifié sur le serveur : 2 tableaux de bord, les deux bases
  « Database Connection OK », 8 alertes saines. Exposition : connexion Microsoft configurée, Grafana recréé,
  `GRAFANA_ADDRESS` posé, Caddy et `admin` recréés. Vérifié depuis le poste : certificat Let's Encrypt, accueil →
  connexion (302), mot de passe refusé (403), `/metrics` 404, HSTS, redirection vers Microsoft avec l'adresse de
  retour `https://grafana.limpide-ia.fr/login/generic_oauth` et PKCE ; démo et back office à 200. **Connexion
  d'Alexandre par le lien « Supervision » du back office : réussie.**
- Constat : une erreur « Error in expanding template » (alerte « Réponses anormalement lentes ») vue une fois sur le
  serveur, juste après un démarrage de Grafana, non reproduite (ni sur le serveur ensuite, ni sur le poste) :
  messages protégés par précaution (`{{ with $values.A }}…{{ else }}n/d{{ end }}`, PR à part). Vérifié sur le poste
  (8 alertes saines, aucune erreur de gabarit) ; sur le serveur, à recréer Grafana après la fusion.
