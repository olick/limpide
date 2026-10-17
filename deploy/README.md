# Déploiement de la démo (ADR-006)

Une seule machine (VPS OVHcloud, 2 vCores, 4 Go), Docker Compose. Seul Caddy est exposé (80/443) ;
PostgreSQL et Ollama restent sur le réseau interne Docker.

```
Internet ──▶ caddy (80/443, HTTPS automatique avec un nom de domaine)
               └──▶ web (Blazor, 8080, utilisateur non root)
                      ├──▶ postgres (pgvector)
                      ├──▶ ollama (bge-m3 gardé en mémoire, préchauffé au démarrage de web)
                      └──▶ API Mistral (UE)
ingestion : à la demande (docker compose run --rm ingestion <commande>), jamais démarrée par « up »
```

Mémoire mesurée en local (2026-10-06) : caddy 12 Mo, web 39 Mo, postgres 37 Mo, ollama 1,27 Go.
Limites fixées : 128 Mo, 512 Mo, 512 Mo, 2 Go.

## Fichiers

| Fichier | Rôle |
|---|---|
| `Dockerfile` (racine) | images `web` et `ingestion` (compilation partagée, utilisateur non root) |
| `docker-compose.prod.yml` | la pile ; projet `limpide-prod`, volumes distincts du développement |
| `deploy/Caddyfile` | reverse proxy ; `SITE_ADDRESS` = `:80` sans domaine, le domaine ensuite |
| `.env.prod` (jamais commité) | secrets : mot de passe PostgreSQL, clé Mistral ; modèle : `.env.prod.example` |

## Raccourci

```bash
alias limpide='docker compose -f docker-compose.prod.yml --env-file .env.prod --env-file .env.version'
```

`.env.version` (jamais commité) contient la version des images en place (`IMAGE_TAG=…`), écrite par
`deploy/deployer.sh` ; sans lui, une commande `limpide` manuelle reviendrait aux images construites sur le serveur.
Avant le premier déploiement par la chaîne, il n'existe pas : `touch .env.version`.

## Premier démarrage

```bash
cp .env.prod.example .env.prod && chmod 600 .env.prod   # puis remplir les secrets
limpide up -d --build --wait                              # télécharge bge-m3 au premier démarrage (≈ 1,2 Go)
limpide run --rm ingestion migrate                        # crée le schéma (base vide)
```

## Données : transférer la base plutôt que tout recalculer

Recalculer les 691 vecteurs sur 2 vCores prendrait 30 à 40 minutes ; la sauvegarde de la base de développement
fait 3,7 Mo et contient déjà tout (passages, vecteurs, versions).

```bash
# Sur le poste de développement
docker compose exec -T postgres pg_dump -U rag -d rag -Fc > data/limpide-$(date +%Y%m%d).dump

# Sur la machine de production (après copie du fichier)
limpide exec -T postgres pg_restore -U rag -d rag --clean --if-exists --no-owner < limpide-AAAAMMJJ.dump
limpide exec -T postgres psql -U rag -d rag -tAc "SELECT count(*), count(embedding) FROM chunks"   # 691|691
limpide restart web    # OBLIGATOIRE si l'application tournait pendant la restauration (voir ci-dessous)
```

**Pourquoi redémarrer l'application** : `--clean` supprime puis recrée l'extension pgvector, et le type `vector`
change d'identifiant interne (16386 → 16791 au premier déploiement). L'application, connectée avant la restauration,
garde l'ancien identifiant en mémoire : toute recherche échoue (`cache lookup failed for type 16386`), et la page
affiche « service de recherche ou de génération indisponible ». Les commandes de la console, lancées après coup, ne
voient pas le problème : ce sont de nouveaux processus. Constaté en production le 2026-10-06.

## Vérifier

```bash
limpide ps
limpide logs web | grep -E "chargé|listening"        # « Modèle d'embedding chargé en … s »
curl -s -o /dev/null -w "%{http_code} %{content_type}\n" http://localhost/_framework/blazor.web.js   # 200 text/javascript
# Sans ce script, la page s'affiche mais rien n'est interactif (bouton sans effet) : vérifier aussi dans un navigateur.
limpide run --rm ingestion evaluate                  # recherche : 8/10 dans le top 5 (même score qu'en développement)
limpide run --rm ingestion ask Quelles pratiques d\'IA sont interdites \?
```

## Sauvegarde quotidienne de la base

`deploy/backup-db.sh` : `pg_dump`, vérification du fichier (`pg_restore --list`), 7 jours d'historique dans `~/backups`.
Lancé chaque nuit à 3 h 15 par un minuteur systemd (rattrapé au démarrage si le serveur était éteint).
Les sauvegardes sont sur le disque du VPS, lui-même sauvegardé chaque jour par OVH (copie hors serveur, 1 jour).

```bash
# Installation (une fois)
sudo cp deploy/limpide-backup.service deploy/limpide-backup.timer /etc/systemd/system/
sudo systemctl daemon-reload && sudo systemctl enable --now limpide-backup.timer

systemctl list-timers limpide-backup.timer       # prochaine exécution
sudo systemctl start limpide-backup.service      # sauvegarde immédiate
journalctl -u limpide-backup.service -n 5        # résultat des dernières sauvegardes
ls -lh ~/backups

# Restaurer une sauvegarde, puis redémarrer l'application (voir « Données » ci-dessus)
limpide exec -T postgres pg_restore -U rag -d rag --clean --if-exists --no-owner < ~/backups/limpide-AAAAMMJJ-HHMM.dump
limpide restart web
```

## Mémoire : swap de secours

Le VPS n'avait pas de swap : si la mémoire manque vraiment (par exemple une construction d'image .NET pendant que
tout tourne), le noyau tue un processus, le plus probablement Ollama (1,4 Go). Ajouté le 2026-10-09 : 2 Go, utilisé
en dernier recours seulement (`vm.swappiness=10`).

```bash
# Installation (une fois)
sudo fallocate -l 2G /swapfile && sudo chmod 600 /swapfile
sudo mkswap /swapfile && sudo swapon /swapfile
echo '/swapfile none swap sw 0 0' | sudo tee -a /etc/fstab
echo 'vm.swappiness=10' | sudo tee /etc/sysctl.d/99-limpide.conf && sudo sysctl -p /etc/sysctl.d/99-limpide.conf

free -m      # lire « available » (mémoire réellement disponible), pas « used »
```

**Le graphique « RAM » d'OVH compte le cache disque comme de la mémoire utilisée** : il affiche ≈ 100 % alors que
1,6 Go restent disponibles (2026-10-09 : 2,2 Go utilisés, 1,8 Go de cache, 1,6 Go disponibles, aucun processus tué).
Linux remplit la mémoire libre avec le cache des fichiers lus (sauvegarde de 3 h 15, mises à jour) et le rend dès
qu'un programme en a besoin. L'indicateur à surveiller (et à alerter en S8) est la mémoire **disponible**.

## Nom de domaine et HTTPS

Démo : **https://www.limpide-ia.fr** (zone DNS chez OVH : `www` → A `57.129.175.88`, aucune entrée AAAA).
`SITE_ADDRESS=www.limpide-ia.fr` dans `.env.prod` ; Caddy obtient le certificat Let's Encrypt au démarrage
et le renouvelle seul ; HTTP redirige vers HTTPS.

```bash
limpide up -d caddy        # après toute modification de SITE_ADDRESS (restart ne relit pas .env.prod)
limpide logs caddy | grep -o '"msg":"certificate[^"]*"'
```

Domaine nu `limpide-ia.fr` : entrée A vers `57.129.175.88` (zone DNS OVH, à la place de la redirection web d'OVH,
qui ne savait pas répondre en HTTPS), et `REDIRECT_FROM=limpide-ia.fr` dans `.env.prod` : Caddy obtient son certificat
et le redirige (301) vers `https://www.limpide-ia.fr`, l'adresse canonique (2026-10-10). Après modification de
`.env.prod` : `limpide up -d caddy` (recréé pour lire la variable, coupure d'une seconde).

## Schéma de la base : migrations (S5)

Le schéma évolue par des scripts SQL numérotés (`src/Limpide.Infrastructure/Migrations/`, outil DbUp), appliqués par
la commande `migrate` de la console. Chaque script ne s'exécute qu'une fois par base, dans sa propre transaction ;
la table `schemaversions` garde la liste des scripts appliqués. Jamais au démarrage de l'application web : un
changement de schéma de production est un acte explicite de la mise à jour.

```bash
limpide run --rm ingestion migrate     # {"applied":N,...} ; sans effet si tout est déjà appliqué
limpide exec -T postgres psql -U rag -d rag -c "SELECT scriptname, applied FROM schemaversions"
```

Les bases créées avant la S5 (scripts `db/init`, supprimés depuis) reçoivent 0001 et 0002 sans effet (`IF NOT
EXISTS`) : DbUp les inscrit simplement dans son journal. Vérifié le 2026-10-10 sur une copie de la base locale et
sur une base vide : même schéma à l'arrivée, aucune donnée perdue.

## Back office (admin.limpide-ia.fr)

Application séparée (`src/Limpide.Admin`, conteneur `admin`) : versions et quarantaine (publier, recontrôler, rejeter,
motif obligatoire), retours des visiteurs (traité, export CSV), journal des décisions, réglages en lecture seule.
Connexion **OpenID Connect générique** : le fournisseur n'est qu'une configuration (`AdminAuthentication.cs`, seul
fichier qui le connaît). Accès réservé aux identifiants listés (« émetteur#sujet », stable chez tout fournisseur).

**Mise en place (Microsoft, comptes personnels)**

1. Zone DNS OVH : entrée A `admin` → `57.129.175.88`.
2. Portail Azure → Microsoft Entra ID → Inscriptions d'applications → Nouvelle inscription : « Limpide Admin »,
   comptes Microsoft personnels uniquement ; URI de redirection (Web) `https://admin.limpide-ia.fr/signin-oidc`, et
   `http://localhost:5182/signin-oidc` pour le poste. Noter l'ID d'application ; créer un secret client.
3. `.env.prod` (jamais commité) :
   ```
   ADMIN_ADDRESS=admin.limpide-ia.fr
   ADMIN_OIDC_AUTHORITY=https://login.microsoftonline.com/consumers/v2.0
   ADMIN_OIDC_CLIENT_ID=<ID d'application>
   ADMIN_OIDC_CLIENT_SECRET=<secret client>
   ADMIN_ALLOWED_USER=
   ```
4. `limpide up -d --build --wait admin caddy` ; se connecter sur https://admin.limpide-ia.fr : la page « Accès refusé »
   affiche l'identifiant du compte ; le copier dans `ADMIN_ALLOWED_USER`, puis `limpide up -d admin`.

**Sur le poste** : mêmes valeurs en user-secrets (`Admin:Authority`, `Admin:ClientId`, `Admin:ClientSecret`,
`Admin:AllowedUsers:0`), puis `dotnet run --project src/Limpide.Admin --launch-profile http` (http://localhost:5182).
Sans débogueur : service `admin` du `docker-compose.yml` local, sur le même port, avec les mêmes valeurs dans `.env`
(`ADMIN_OIDC_*`, `ADMIN_ALLOWED_USER`) ; `docker compose up -d --build admin` après une modification du code,
`docker compose stop admin` avant de le déboguer (port partagé).
Sans ces valeurs, l'application démarre et l'accueil indique « connexion non configurée ».

**Changer de fournisseur** (Google, Pocket ID, Authelia...) : nouvelle inscription chez lui, puis les trois valeurs
`ADMIN_OIDC_*` et le nouvel identifiant autorisé ; aucun changement de code.

**Clés de chiffrement** : `web` et `admin` gardent leurs clés DataProtection dans des volumes (`web_keys`,
`admin_keys`) : plus de déconnexion ni d'erreurs « antiforgery » à chaque déploiement.

## Versions en quarantaine

**Ingestion en production depuis le 2026-10-13** (ADR-013) : Airflow allégé sur ce serveur lance la chaîne chaque
lundi à 4 h UTC (procédure : `pipelines/airflow/README.md`, « Production » ; interface par tunnel SSH :
`ssh -L 8080:127.0.0.1:8080 debian@57.129.175.88`). Les fichiers bruts et extraits sont dans le volume
`limpide-prod_ingestion_data`. Une quarantaine envoie une alerte Discord.

Les contrôles qualité (`validate`) bloquent une version douteuse ; la version publiée reste en service. Décision
humaine, après lecture du texte (procédure complète : `pipelines/airflow/README.md`, « Quarantaine ») :

```bash
limpide run --rm ingestion versions                                   # état, empreintes, raisons
limpide run --rm ingestion release <empreinte> "<motif>"              # publiée au prochain passage d'embed et publish
limpide run --rm ingestion retry <empreinte> "<motif>"                # recontrôlée après une correction du code
limpide run --rm ingestion reject <empreinte> "<motif>"               # rejet définitif
```

## Retours des visiteurs

Formulaire en bas de page : trois questions facultatives, dernière question et réponse jointes seulement avec
l'accord du visiteur (case décochée par défaut), aucune adresse IP, conservation 12 mois (purge à chaque envoi),
5 envois par heure et par visiteur. Lecture :

```bash
limpide run --rm ingestion feedback        # Markdown, les plus récents d'abord
```

## Protections de la démo publique (S3, session 4)

- **Quota de questions** (`Protection` dans `src/Limpide.Web/appsettings.json`) : 10 par heure et par adresse IP,
  60 par jour au total (≈ 9 €/mois au plus avec Mistral Medium, sous le plafond de 10 € fixé chez Mistral).
  En mémoire : remis à zéro au redémarrage de `web`.
- **Adresse du visiteur** : Caddy remplace tout `X-Forwarded-For` reçu d'Internet par l'adresse réelle (un visiteur ne
  peut pas usurper une autre adresse), et Docker transmet l'adresse publique d'origine. Vérifié le 2026-10-07.
- **Docker contourne `ufw`** pour les ports publiés : un conteneur de test publié sur 8099 était joignable depuis
  Internet malgré le pare-feu (vérifié le 2026-10-07). Ne publier que les ports de Caddy ; jamais ceux de PostgreSQL
  ni d'Ollama.

## Mettre à jour l'application

**Par la chaîne CI/CD (S7)** : chaque fusion sur `main` qui touche l'application publie les images sur ghcr.io
(`.github/workflows/deploiement.yml`), puis, après validation manuelle (environnement GitHub `production`), lance en
SSH `deploy/deployer.sh <version>` : dépôt à jour, images tirées (rien n'est construit sur le serveur), migrations
**avant** l'application, `web` et `admin` redémarrés (coupure de quelques secondes), image d'ingestion ré-étiquetée
`limpide-ingestion` pour Airflow, vérification du site.

À la main, même script (retour arrière : la version précédente, affichée au début de chaque déploiement) :

```bash
deploy/deployer.sh <version>                 # version : deploy/version-images.sh, ou l'étiquette sur ghcr.io
```

Ce que change `deployer.sh` : Caddy n'est pas redémarré (après une modification du `Caddyfile` :
`limpide restart caddy`).

### Clé SSH de la chaîne (une fois)

Une clé dédiée, qui ne permet **que** de lancer `deployer.sh` : volée, elle ne donnerait ni terminal, ni copie de
fichiers, ni redirection de ports. Sur le poste :

```bash
ssh-keygen -t ed25519 -N "" -C "github-actions-deploiement" -f /tmp/limpide-deploiement
cat /tmp/limpide-deploiement.pub             # à ajouter sur le serveur, voir ci-dessous
```

Sur le serveur, une ligne dans `~/.ssh/authorized_keys` (`restrict` : aucun terminal, aucune redirection ;
`command` : la seule commande permise, l'argument transmis arrive dans `SSH_ORIGINAL_COMMAND`) :

```
restrict,command="/home/debian/limpide/deploy/deployer.sh" ssh-ed25519 AAAA… github-actions-deploiement
```

La clé privée (`/tmp/limpide-deploiement`) va dans le secret `SSH_DEPLOY_KEY` de l'environnement GitHub `production`
(Settings → Environments → production), puis les deux fichiers sont effacés du poste. L'empreinte du serveur est
dans la variable `VPS_KNOWN_HOSTS` (vérifiée contre `~/.ssh/known_hosts` du poste) : la chaîne refuse un autre
serveur.

Avant la chaîne (jusqu'au 2026-10-17), le serveur construisait les images : `git pull`, puis
`limpide --profile outils build ingestion`, `limpide run --rm ingestion migrate`, `limpide up -d --build --wait web`.

## Test en local de la configuration de production

Même procédure, avec dans `.env.prod` : `HTTP_PORT=8088`, `HTTPS_PORT=8443`, `SITE_ADDRESS=:80`.
Application sur http://localhost:8088. Arrêt : `limpide down` (les volumes restent ; `down -v` les efface).
