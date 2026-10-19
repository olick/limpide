---
paths:
  - "deploy/**"
  - "docker-compose.prod.yml"
  - "Dockerfile"
---

# Production (VPS, ADR-006)

- VPS OVHcloud VPS-1 (57.129.175.88, Debian 13, utilisateur `debian`, SSH par clé, pas de root), dépôt dans
  `~/limpide`, projet Compose `limpide-prod` (Caddy, web, admin, postgres ; secrets dans `.env.prod`, jamais affichés).
  Procédure : `deploy/README.md`.
- **Déploiement par la chaîne (S7)** : images publiées sur ghcr.io par la CI, `deploy/deployer.sh <version>` en SSH
  (clé à commande imposée), validation manuelle ; rien n'est construit sur le serveur. Version en place dans
  `.env.version` (alias `limpide` : `--env-file .env.prod --env-file .env.version`). Règle de version unique :
  `deploy/version-images.sh`.
- Vectorisation par OVHcloud AI Endpoints (`OVH_AI_ENDPOINTS_KEY`), Qwen3-Embedding-8B, sans secours (ADR-012
  révisé) : le profil `ollama` ne sert que bge-m3, et y revenir est un changement de configuration (ADR-017).
- Sauvegarde quotidienne de la base (`deploy/backup-db.sh`, 3 h 15 UTC, `~/backups`, 7 jours).
- Chaque déploiement de `web` coupe le site quelques secondes (502). Après un `pg_restore --clean` : redémarrer `web`.
- Mémoire : lire `free -m` (available), pas le graphique d'OVH. Swap de 2 Go.
- Quota : 10 questions/h par IP, 60/jour (`QuestionQuota`) ; plafond Mistral 10 €/mois.
- `GET /sante` : empreinte du corpus servi (lue en base, gardée 30 s), test de fumée d'Azure (`azure/verifier.sh`) et
  du VPS ; 500 si l'application ne lit pas la base. `missingEmbeddings` : passages servis sans vecteur du modèle
  configuré, donc absents de la recherche (0 attendu, ADR-017).
- `ASPNETCORE_FORWARDEDHEADERS_ENABLED=true` : l'application voit l'IP réelle et le HTTPS derrière Caddy.
- Airflow : `.claude/rules/airflow.md` (tunnel SSH vers 8080).
- Supervision (S8, ADR-018) : projet Compose `limpide-supervision` (`supervision/`), hors de la chaîne ; export de
  `web` activé par `OTEL_EXPORTER_OTLP_ENDPOINT` dans `.env.prod` ; Grafana par tunnel SSH (port 3001 du poste).
  Mémoire : `supervision/memoire.sh`. Procédure : `deploy/README.md`, « Supervision ».
