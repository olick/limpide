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
- Vectorisation par OVHcloud AI Endpoints (`OVH_AI_ENDPOINTS_KEY`) ; Ollama en secours (profil `ollama`,
  `deploy/ollama`).
- Sauvegarde quotidienne de la base (`deploy/backup-db.sh`, 3 h 15 UTC, `~/backups`, 7 jours).
- Chaque déploiement de `web` coupe le site quelques secondes (502). Après un `pg_restore --clean` : redémarrer `web`.
- Mémoire : lire `free -m` (available), pas le graphique d'OVH. Swap de 2 Go.
- Quota : 10 questions/h par IP, 60/jour (`QuestionQuota`) ; plafond Mistral 10 €/mois.
- `ASPNETCORE_FORWARDEDHEADERS_ENABLED=true` : l'application voit l'IP réelle et le HTTPS derrière Caddy.
- Airflow : `.claude/rules/airflow.md` (tunnel SSH vers 8080).
