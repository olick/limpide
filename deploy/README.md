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
```

## Vérifier

```bash
limpide ps
limpide logs web | grep -E "chargé|listening"        # « Modèle d'embedding chargé en … s »
curl -s -o /dev/null -w "%{http_code} %{content_type}\n" http://localhost/_framework/blazor.web.js   # 200 text/javascript
# Sans ce script, la page s'affiche mais rien n'est interactif (bouton sans effet) : vérifier aussi dans un navigateur.
limpide run --rm ingestion evaluate                  # recherche : 8/10 dans le top 5 (même score qu'en développement)
limpide run --rm ingestion ask Quelles pratiques d\'IA sont interdites \?
```

## Mettre à jour l'application

```bash
git pull && limpide up -d --build --wait web
```

## Test en local de la configuration de production

Même procédure, avec dans `.env.prod` : `HTTP_PORT=8088`, `HTTPS_PORT=8443`, `SITE_ADDRESS=:80`.
Application sur http://localhost:8088. Arrêt : `limpide down` (les volumes restent ; `down -v` les efface).
