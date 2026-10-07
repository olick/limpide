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
alias limpide='docker compose -f docker-compose.prod.yml --env-file .env.prod'
```

## Premier démarrage

```bash
cp .env.prod.example .env.prod && chmod 600 .env.prod   # puis remplir les secrets
limpide up -d --build --wait                              # télécharge bge-m3 au premier démarrage (≈ 1,2 Go)
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

## Nom de domaine et HTTPS

Démo : **https://www.limpide-ia.fr** (zone DNS chez OVH : `www` → A `57.129.175.88`, aucune entrée AAAA).
`SITE_ADDRESS=www.limpide-ia.fr` dans `.env.prod` ; Caddy obtient le certificat Let's Encrypt au démarrage
et le renouvelle seul ; HTTP redirige vers HTTPS.

```bash
limpide up -d caddy        # après toute modification de SITE_ADDRESS (restart ne relit pas .env.prod)
limpide logs caddy | grep -o '"msg":"certificate[^"]*"'
```

Le domaine nu `limpide-ia.fr` pointe encore sur la redirection web d'OVH (213.186.33.5), qui ne sait pas répondre
en HTTPS. Pour le servir aussi : remplacer cette redirection par une entrée A vers `57.129.175.88`, puis
`SITE_ADDRESS=www.limpide-ia.fr, limpide-ia.fr`.

## Évolutions du schéma (en attendant l'outil de migrations, S5)

Les scripts de `db/init/` ne s'exécutent qu'au premier démarrage d'une base vide. Sur la base de production
existante, appliquer à la main chaque nouveau script (tous sont rejouables sans risque) :

```bash
limpide exec -T postgres psql -U rag -d rag -v ON_ERROR_STOP=1 < db/init/002_feedback.sql   # retours des visiteurs
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

```bash
git pull && limpide up -d --build --wait web
```

## Test en local de la configuration de production

Même procédure, avec dans `.env.prod` : `HTTP_PORT=8088`, `HTTPS_PORT=8443`, `SITE_ADDRESS=:80`.
Application sur http://localhost:8088. Arrêt : `limpide down` (les volumes restent ; `down -v` les efface).
