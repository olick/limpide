# Revue des accès

Qui (ou quoi) peut faire quoi sur Limpide, selon le principe du moindre privilège : chaque personne, programme ou
identité n'a que les droits de son usage. Revue faite le **2026-10-27** (S8, session 5) ; à refaire à chaque nouvel
accès (contributeur, service, identité) et au moins une fois par an.

**Méthode.** Relevé direct, pas de mémoire : `az role assignment list` et `az identity federated-credential list`
(Azure), API de GitHub (règles de `main`, environnements, noms des secrets), commandes en lecture seule sur le serveur
(`authorized_keys`, `sudo -l`, `sshd -T`, `ufw`, ports, rôles PostgreSQL), lecture des fichiers Compose et du code
(tables lues et écrites). Non relevé par programme : la double authentification des comptes et les politiques IAM
d'OVHcloud (déclaration d'Alexandre), les réglages d'Actions que le jeton du poste ne lit pas (interface de GitHub).

## Personnes et comptes

Une seule personne, Alexandre, a tous les accès. Aucun compte partagé.

| Compte | Ce qu'il donne | Double authentification |
|---|---|---|
| GitHub (`olick`, seul collaborateur, administrateur) | le code, la chaîne, l'approbation des déploiements | non |
| Microsoft (Entra ID, propriétaire de l'abonnement Azure) | Azure, le coffre à secrets, le back office et Grafana (connexion) | **oui** |
| OVHcloud (espace client) | le VPS (réinstallation, console, mode secours), la zone DNS, AI Endpoints | non |
| Mistral | les clés de génération, les plafonds de dépense | non |
| Discord | le salon des alertes | non |

**Risque accepté par Alexandre (2026-10-27)** : pas de double authentification hors Microsoft. Les deux comptes qui
comptent sont GitHub et OVHcloud : le premier permet de publier du code que la chaîne déploie (l'approbation de
l'environnement `production` est celle du même compte), le second donne la main sur le serveur sans passer par SSH.
Mistral et Discord sont bornés (plafonds de 10 € par espace ; un salon d'alertes).

## GitHub

| Élément | État |
|---|---|
| Règle « Main protection » sur `main` | PR obligatoire, « Build et tests » et « Évaluation » requis, ni force push ni suppression ; **aucune exception**, administrateur compris |
| Environnements `production`, `azure` | relecteur requis (Alexandre), branche `main` seulement |
| Environnement `evaluation` | ni relecteur ni restriction : pas une barrière (ADR-016) |
| Workflows | tous déclarent `permissions: contents: read` ; écriture accordée job par job (`id-token`, `packages`) : le réglage par défaut du dépôt est sans effet |
| Actions tierces | toutes autorisées par le dépôt ; épinglées par empreinte de commit (`.claude/rules/ci.md`), Grype par empreinte d'image |
| Secrets du dépôt | identifiants Azure (sans accès à eux seuls, OIDC) ; compte OVH `limpide-ci-lecture` |
| Secrets de `production` | compte OVH `limpide-ci-ecriture`, clé SSH de déploiement (commande imposée) |
| Jeton `gh` du poste | ni écriture du contenu ni administration (Claude Code ne peut ni fusionner ni modifier les réglages) |
| PR venues d'un fork | ni secret ni jeton OIDC |

## Azure

Relevé identique au tableau de `infrastructure/terraform/etat/README.md` (« Identités de la CI ») : quatre identités
managées, une par usage, chacune liée à un seul sujet GitHub (`pull_request`, `environment:production`,
`environment:azure`, `environment:evaluation`). Alexandre : propriétaire de l'abonnement, « Key Vault Secrets Officer »
sur le coffre, écriture des conteneurs `tfstate` et `corpus`.

Écart connu, déjà documenté (ADR-016) : `id-limpide-ci-azure` peut s'attribuer la lecture du coffre (la condition
borne les rôles, pas la portée). Protégée par le relecteur et la branche `main` de l'environnement `azure`.

Une inscription d'application Entra : « Limpide Admin » (comptes Microsoft personnels), un secret client qui **expire
le 2027-10-12** ; ce jour-là, le back office et Grafana refusent toute connexion.

## OVHcloud

Comptes de service de la chaîne : `limpide-ci-lecture` (lecture de la zone DNS et du VPS, en PR) et
`limpide-ci-ecriture` (écriture de la zone, environnement `production`). Jeton du poste (`~/.ovh.conf`) : zone et
lecture du VPS. Le VPS est lu par Terraform, jamais géré (`.claude/rules/terraform.md`).

## Serveur

| Élément | État |
|---|---|
| SSH | clés seulement (`passwordauthentication no`), root refusé ; deux clés : le poste d'Alexandre, la chaîne (`restrict,command=` : `deployer.sh` seul) |
| Utilisateur `debian` | `sudo` sans mot de passe, groupe `docker` (équivalent root) |
| Pare-feu (`ufw`) | entrée refusée par défaut ; 22, 80, 443 ouverts |
| Ports à l'écoute | 22, 80, 443 ; Grafana (3000), collecteur (4317, 4318), Airflow (8080) sur 127.0.0.1 ; PostgreSQL non publié ; 5355 (LLMNR, `systemd-resolved`) sur toutes les interfaces, fermé par `ufw` |
| Airflow | socket Docker : contrôle de la machine, d'où l'interface jamais exposée (ADR-007) |

`sudo` sans mot de passe : accepté. Le groupe `docker` donne déjà l'équivalent de root, et l'accès SSH exige la clé du
poste.

## Base de données

| Compte | Qui s'en sert | Droits |
|---|---|---|
| `rag` | migrations, ingestion, back office, sauvegarde | superutilisateur |
| `limpide_web` (rôle `application_web`, migration 0010) | application web publique | lecture du corpus ; écriture et purge des journaux sans les relire |
| `grafana_lecture` (rôle `lecture_supervision`) | Grafana | lecture d'`answer_log` ; dans la base d'Airflow, quelques colonnes de deux tables |
| `airflow` (base d'Airflow) | Airflow | sa base ; mot de passe propre depuis le 2026-10-21 |

**Écart corrigé par la migration 0010** (2026-10-27 ; application sur le serveur : journal de la S8) : `web`, seule partie exposée sur Internet, se connectait en `rag`. Une faille
(injection SQL, exécution de code) aurait donné toute la base, dont les questions conservées, et des commandes dans le
conteneur `postgres` (`COPY … PROGRAM`). Essayé sur le poste avec le compte limité : recherche, rédaction, `/sante`,
écriture et purge des trois journaux (cascade vers `question_log` comprise) passent ; lecture des questions et des
retours, modification du corpus, `admin_actions`, création de table, `COPY … PROGRAM` sont refusées.

## Applications

| Application | Qui entre | Comment |
|---|---|---|
| Démo (`www`) | tout le monde | quota de 10 questions par heure et par adresse, 60 par jour |
| Back office (`admin`) | Alexandre seul (`ADMIN_ALLOWED_USER`) | connexion Microsoft (double authentification) |
| Grafana | Alexandre seul, rôle Editor | connexion Microsoft ; compte administrateur local, mot de passe dans `supervision/.env` |
| Interface d'Airflow | Alexandre | tunnel SSH seulement |

## Écarts et décisions

| Écart | Décision |
|---|---|
| `web` en superutilisateur | **corrigé** : `limpide_web` (migration 0010, `deploy/compte-web.sh`) |
| Back office et ingestion en superutilisateur | reporté : le back office est derrière la connexion Microsoft ; l'ingestion n'est lancée que par Alexandre et Airflow. Même méthode que pour `web` le jour où un second accès apparaît |
| Grafana partage le secret client du back office | session 5 : inscription d'application propre à Grafana |
| Pas de double authentification sur GitHub et OVHcloud | risque accepté par Alexandre |
| Secret client « Limpide Admin » à échéance (2027-10-12) | à renouveler avant ; rappel à poser |
| `id-limpide-ci-azure` peut s'attribuer la lecture du coffre | accepté (ADR-016) |
| LLMNR à l'écoute (5355), fermé par le pare-feu | remarque : désactivable (`LLMNR=no`, `systemd-resolved`), sans urgence |
| Toutes les actions GitHub autorisées | accepté : épinglage par empreinte ; à restreindre si un contributeur arrive |
