# Classification des données

Toutes les données que Limpide reçoit, produit ou garde, avec leur sensibilité, leur lieu de stockage, leur durée de
conservation et qui y accède. Relevé le **2026-11-19** dans le code, les migrations et la configuration (pas de
mémoire) ; à reprendre à chaque nouvelle table, nouveau journal ou nouveau destinataire.

## Niveaux

| Niveau | Définition | Règle de manipulation |
|---|---|---|
| **Public** | publié ou publiable sans risque : textes officiels, dépôt Git | licences respectées (`registre-des-sources.md`) |
| **Interne** | technique, sans donnée personnelle ; sa fuite gêne, sans plus | hors du dépôt quand il n'a rien à y faire, sans autre exigence |
| **Personnel** | se rapporte, même indirectement, à un visiteur ou à l'administrateur | finalité, fondement et durée écrits dans `rgpd.md`, accès nommé, purge automatique |
| **Secret** | donne un accès ou engage une dépense : clés, mots de passe, jetons | jamais commité, jamais affiché ; un lieu de stockage par secret (`docs/acces.md`) |

Une donnée **pseudonyme** (un identifiant tiré au hasard, sans lien avec une personne nommée) reste au niveau
Personnel : le RGPD s'applique tant qu'un lien est possible.

## Inventaire

| Données | Niveau | Où | Combien de temps | Qui y accède | Preuve |
|---|---|---|---|---|---|
| Textes du corpus : fichiers bruts, textes extraits, passages, vecteurs | Public | base PostgreSQL et volume de fichiers du serveur ; poste ; sauvegarde du corpus dans Azure (conteneur `corpus`) | sans limite : chaque version reste (historique, ADR-008) | lecture : tout le monde, par les extraits cités ; écriture : l'ingestion et le back office (compte `rag`), jamais `web`, qui ne fait que lire | `docs/sources.md`, `docs/acces.md` (« Base de données ») |
| Métadonnées du corpus : versions, empreintes, statuts, rapports qualité, motifs de quarantaine | Interne | base | sans limite | back office, ingestion | ADR-008 |
| Jeu d'évaluation et rapports | Public | dépôt (`eval/`), artefacts de la chaîne | dépôt : permanent ; artefacts : 90 jours | tout le monde | `.claude/rules/evaluation.md` |
| **Texte des questions conservées** (`question_log`) | Personnel | base | 30 jours, puis 8 jours au plus dans les sauvegardes ; rien en cas d'opposition ni de refus par le quota | back office (Alexandre) ; ni Grafana ni le compte de `web` | ADR-019, migration 0009, `KeptQuestion.cs` |
| Fil de la visite : questions et réponses affichées | Personnel | mémoire du serveur (circuit Blazor) | la visite ; effacé peu après la fermeture de la page | personne : jamais écrit, jamais renvoyé au modèle | ADR-022 |
| Journal des réponses (`answer_log`) : issue, scores, durées, coût, garde-fous, longueur de la question | Personnel (pseudonyme) | base | 12 mois | `web` l'écrit sans le relire ; Grafana (`lecture_supervision`) ; back office | migration 0006, `AnswerLogEntry.cs` |
| Traces et journaux de la supervision, avec un identifiant de visite tiré au hasard | Personnel (pseudonyme) | Tempo et Loki, sur le serveur | 14 jours | Grafana (Alexandre) | ADR-018, `supervision/tempo.yaml`, `supervision/loki.yaml` |
| Métriques (latence, volume, mémoire) | Interne | Prometheus, sur le serveur | 30 jours | Grafana | ADR-018 |
| Journaux bruts des conteneurs | Interne | Docker, sur le serveur | 3 fichiers de 10 Mo par conteneur | Alexandre (SSH) | `docker-compose.prod.yml` (`x-logging`) |
| Adresse IP | Personnel | mémoire de `web` (`QuestionQuota`) | la fenêtre du quota ; effacée au redémarrage ; écrite dans aucun journal | personne | `QuestionQuota.cs`, `.claude/rules/interfaces-web.md` |
| Retours des visiteurs (`feedback`), question et réponse jointes avec accord | Personnel (texte libre) | base | 12 mois | back office | migration 0002, `VisitorFeedback.cs` |
| Journal des décisions (`admin_actions`) : identifiant et nom de l'administrateur | Personnel (administrateur) | base | **sans limite** : jamais purgé (point ouvert, `rgpd.md`) | back office | migration 0004 |
| Sauvegardes de la base (contiennent les lignes ci-dessus) | Personnel | `~/backups` du serveur ; copie quotidienne d'OVHcloud | 7 jours ; copie d'OVHcloud : 1 jour | Alexandre | `deploy/backup-db.sh`, `deploy/README.md` |
| Choix d'apparence ; cookie de protection des formulaires | Interne | navigateur du visiteur | stockage local jusqu'à « Automatique » ; cookie de session | le visiteur | page `/confidentialite` |
| Secrets : mots de passe de la base, clés Mistral et OVHcloud, secrets clients Microsoft, clé SSH de déploiement, webhook Discord | Secret | `.env.prod` et `supervision/.env` (serveur) ; user-secrets (poste) ; coffre Azure ; secrets GitHub ; `pipelines/airflow/.env` | jusqu'à rotation ; secrets clients Microsoft : échéance le 2027-10-12 | Alexandre ; chaque identité de la chaîne, ses seuls secrets | `docs/acces.md`, `deploy/analyser-secrets.sh` (Gitleaks en CI) |
| État Terraform | Secret (peut contenir des valeurs sensibles) | conteneur `tfstate` du socle Azure | sans limite | Alexandre, identités de la chaîne | `infrastructure/terraform/etat/README.md` |

## Liens entre les données personnelles

La sensibilité d'une ligne dépend de ce à quoi elle est reliée :

- `question_log` est reliée à `answer_log` (même clé, suppression en cascade) : pendant 30 jours, une ligne du journal
  des réponses peut être rattachée au texte d'une question ;
- `answer_log` garde l'identifiant de la trace : pendant 14 jours, une ligne peut être rattachée à l'identifiant de
  visite. La migration 0006 dit vrai (« ni identifiant de visiteur » dans la table), mais le lien passe par la trace ;
- passé 30 jours, une ligne d'`answer_log` n'est plus reliée à aucun texte ni à aucune visite. L'adresse IP n'est
  reliée à rien d'écrit.

## Écart relevé le 2026-11-19

**La sauvegarde du corpus déposée dans Azure contient des données de visiteurs.** `deploy/exporter-corpus.sh` se dit
« sans aucune donnée de visiteur », mais il n'exclut que `feedback` et `admin_actions`. Depuis la migration 0009
(2026-10-21), un export depuis la production (`--vps`) emporte aussi `answer_log` et `question_log`. Le fichier en
place vient de la production (2026-11-06). Chaque évaluation de la chaîne le restaure sur une machine de GitHub. Détail,
conséquences et correction : `rgpd.md`, « Écarts et points ouverts ».
