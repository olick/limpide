# Semaine 3 : mise en ligne, démo accessible

**Objectif de fin de semaine** : une personne extérieure peut utiliser Limpide depuis une URL publique en HTTPS,
et un usage abusif ne peut pas dépasser le budget fixé.

**Budget temps** : environ 9 heures, en 5 sessions.

**Prérequis** : semaine 2 terminée, l'application fonctionne de bout en bout en local.

> Le déploiement est **volontairement manuel** cette semaine. Il servira de point de comparaison
> quand Terraform le remplacera en semaine 6 : c'est une partie du récit POC → production.

---

## Session 1 : choisir l'hébergement (≈ 1 h 30)

Rédiger l'ADR-006 en comparant deux options :

| | Petit serveur (VPS européen) + Docker Compose | Azure Container Apps (portail) |
|---|---|---|
| Coût mensuel | Faible et fixe | Variable, peut descendre à zéro hors usage |
| Ollama (embeddings des questions) | Tourne sur le même serveur | Conteneur à prévoir avec assez de mémoire, coût non négligeable |
| Proximité avec la cible | Faible | Forte (Terraform en S6) |
| Mise en place | Rapide, tu maîtrises Docker Compose | Portail Azure à découvrir |

Point d'attention : en production, il faut aussi calculer l'**embedding de chaque question**.
Soit Ollama tourne à côté de l'application (un texte court se vectorise vite sur CPU), soit on passe
par une API d'embeddings. Dans ce cas, tout le corpus doit être revectorisé avec le même modèle. Ce choix mérite d'être noté dans l'ADR.

**Terminé quand** : l'ADR-006 est rédigé et l'hébergement réservé.

## Session 2 : conteneuriser (≈ 2 h)

1. **Dockerfile multi-étapes** pour `Limpide.Web` : image de build SDK, image finale runtime ASP.NET,
   utilisateur non root, port configurable.
2. `docker-compose.prod.yml` : application, PostgreSQL, Ollama. Seul le proxy HTTPS est exposé,
   la base et Ollama restent sur le réseau interne Docker.
3. **Configuration par variables d'environnement** : chaîne de connexion, clé d'API du LLM, prix par token.
   Aucun secret dans l'image ni dans le dépôt.
4. Tester l'image en local.

**Terminé quand** : `docker compose -f docker-compose.prod.yml up` fait tourner l'application complète en local.

## Session 3 : déployer (≈ 2 h)

1. Préparer l'hôte : mises à jour, accès SSH par clé uniquement, pare-feu (seuls 22, 80 et 443 ouverts).
2. Proxy HTTPS avec certificat automatique (Caddy est le plus simple : quelques lignes de configuration).
3. Nom de domaine pointant vers l'hôte.
4. **Charger le corpus en production** : exporter la base locale (`pg_dump`) et la restaurer,
   plutôt que relancer l'ingestion depuis le serveur.
5. Sauvegarde quotidienne de la base.

**Terminé quand** : l'URL publique répond en HTTPS et une question fonctionne.

## Session 4 : protéger la démo publique (≈ 2 h)

1. **Limitation du débit par IP** avec le middleware intégré d'ASP.NET Core (`AddRateLimiter`),
   par exemple 10 questions par heure et par IP.
2. **Plafond de dépenses** configuré chez le fournisseur du LLM, et alerte à 50 % du budget.
3. **Protection anti-bot** : un défi invisible (type Turnstile) avant l'envoi d'une question.
4. **Limites côté application** : longueur maximale de la question, nombre maximal de tokens en sortie.
5. Tester : envoyer rapidement 20 questions et vérifier que la limite se déclenche.

**Terminé quand** : la limite par IP et le plafond de dépenses sont vérifiés.

## Session 5 : README et bilan de la phase 1 (≈ 1 h 30)

1. README : lien vers la démo, capture du panneau « sous le capot », architecture en un schéma, limites connues.
2. **Bilan des limites de la V1**, dans `docs/plan/bilan-phase-1.md` :
   ingestion lancée à la main, déploiement manuel, pas d'évaluation automatique, pas de supervision,
   secrets gérés à la main. Cette liste est le programme de la phase 2.
3. **Contacter Jedha** : un projet personnel peut-il servir de support d'évaluation pour la RNCP41993 ?
4. Faire tester la démo par 2 ou 3 personnes, noter leurs retours.

**Terminé quand** : le bilan est écrit et au moins une personne extérieure a utilisé la démo.

---

## Pièges à éviter

- **Exposer PostgreSQL ou Ollama sur Internet** : ils restent sur le réseau interne Docker.
- **Oublier le plafond de dépenses** : une démo publique sans limite peut coûter cher en une nuit.
- **Mettre la clé d'API dans l'image ou le dépôt** : variables d'environnement uniquement.
- **Vouloir automatiser dès maintenant** : ce sera l'objet des semaines 6 et 7.

## Livrables

- Dockerfile, `docker-compose.prod.yml`, configuration du proxy
- ADR-006 (hébergement de la démo)
- Démo publique en HTTPS
- `docs/plan/bilan-phase-1.md`

## Ce qui prépare la suite

| Fait cette semaine | Réutilisé en |
|---|---|
| Dockerfile de l'application | S4 : même principe pour les tâches d'ingestion, S6 : déploiement Azure |
| Bilan des limites de la V1 | Toute la phase 2, et le récit de soutenance |
| Déploiement manuel documenté | S6 : comparaison avec Terraform |
| Protections de la démo | S8 : sécurité et FinOps |

---

## Journal de réalisation

Repris tel quel de `CLAUDE.md` le 2026-10-15 (rangement : `CLAUDE.md` ne garde que l'état courant, les règles et les pièges ; le détail des sessions vit ici).

**S3 terminée** (`docs/plan/semaine-03.md`) — mise en ligne de la démo

- [x] Session 1 : ADR-006 accepté — **OVHcloud VPS-1** (2 vCores, 4 Go, ≈ 3,81 € HT/mois, France) + Docker Compose,
  Ollama compris (≈ 2,3 Go utilisés sur 4) ; VPS-2 en rupture. Azure ≈ 10 fois plus cher pour un service toujours
  allumé. Pas de nom de domaine pour l'instant (démo d'abord sur l'IP). **Reste** : commande du VPS par Alexandre.
- [x] Session 2 : `Dockerfile` (cibles `web` et `ingestion`, non root), `docker-compose.prod.yml` (projet `limpide-prod` :
  Caddy, web, postgres, ollama + `ollama-model`, ingestion en profil `outils`), `deploy/Caddyfile`, `.env.prod` (ignoré).
  Testée en local : page via Caddy, préchauffage bge-m3 (2,3 s), base restaurée (691/691), `evaluate` 8/10, `ask` OK,
  puis dans le navigateur par Alexandre, qui a trouvé `blazor.web.js` en 404 : la restauration faite sur les seuls .csproj
  n'inclut pas le paquet des fichiers Blazor (le SDK ne l'ajoute qu'en voyant les .razor). Corrigé (publication sans
  `--no-restore`) et verrouillé : l'image ne se construit pas si `blazor.web.js` manque. Leçon : tester dans un navigateur.
  Procédure : `deploy/README.md`.
- [x] Session 3 : **démo en ligne sur https://www.limpide-ia.fr** (VPS 57.129.175.88 ; testée par Alexandre le 2026-10-06). Fait : mises à jour
  + `unattended-upgrades`, SSH par clé seule (`/etc/ssh/sshd_config.d/00-limpide.conf`, pas de root), pare-feu `ufw`
  (22, 80, 443), Docker, dépôt cloné dans `~/limpide`, `.env.prod`, base restaurée (691/691), recherche 8/10 sur le
  serveur, sauvegarde quotidienne (`deploy/backup-db.sh`, minuteur systemd 3 h 15 UTC, `~/backups`, 7 jours ;
  disque sauvegardé par OVH en plus), HTTPS Let's Encrypt par Caddy (`SITE_ADDRESS=www.limpide-ia.fr`, 2026-10-07).
  Domaine nu `limpide-ia.fr` : entrée A vers le VPS, redirigé (301) vers `www` par Caddy (`REDIRECT_FROM` dans
  `.env.prod`, certificat Let's Encrypt propre), 2026-10-10.

- [x] Session 4 : quota en production (2026-10-07) — `QuestionQuota` (Core) : 10 questions/h par IP, 60/jour au total
  (≈ 9 €/mois max avec Medium, sous le plafond Mistral de 10 €), appliqué dans `Home.razor` avant l'appel au modèle ;
  IP relevée dans `App.razor` (seule requête HTTP de la visite), transmise en paramètre du composant racine, non
  journalisée. Vérifié : Caddy écarte un `X-Forwarded-For` forgé, Docker transmet l'IP publique réelle, et Docker
  contourne `ufw` pour les ports publiés. **Reste** : test du message à la 11e question,
  anti-bot (proposé : reporter en S8). La console Mistral n'a pas d'alerte de seuil documentée (vérifié le
  2026-10-10) : seulement le plafond mensuel (10 €, posé) et les tableaux de consommation ; l'alerte de budget est à
  construire en S8 (le coût par question est déjà calculé).
  Chaque déploiement de `web` coupe le site quelques secondes (502 pendant le redémarrage).
- [x] Session 5 : README réorganisé pour un lecteur extérieur (architecture en schéma, choix et ADR, mesures, garde-fous,
  limites) ; bilan `docs/plan/bilan-phase-1.md` (limites de la V1 = programme de la phase 2). **Reste (Alexandre)** :
  contacter Jedha, faire tester la démo par 2 ou 3 personnes et noter leurs retours dans le bilan.
  Formulaire de retours ajouté (`FeedbackForm.razor`, table `feedback` (migration 0002), `FeedbackStore`,
  commande `feedback`) : 3 questions facultatives, question jointe seulement si le visiteur coche la case, aucune IP,
  conservation 12 mois (purge à chaque envoi), 5 envois/h par visiteur (quota à clé `"feedback"`).

Choix de la S3 :

- VPS OVHcloud VPS-1 (2 vCores, 3,7 Go, Debian 13), IP 57.129.175.88, utilisateur `debian`, connexion par clé ;
  mot de passe changé par Alexandre (jamais transmis). Domaine **`limpide-ia.fr`** (« ia », pas « ai »), démo sur
  `www` ; `limpide.fr/.eu/.io/.app` déjà pris par un même titulaire.
- Données transférées par `pg_dump`/`pg_restore` (3,7 Mo) plutôt que recalculées sur le VPS (30 à 40 min).
  **Après une restauration, redémarrer `web`** : `--clean` recrée pgvector, le type `vector` change d'identifiant, et
  Npgsql garde l'ancien en cache (« cache lookup failed for type 16386 », vu en production le 2026-10-06).
- `ASPNETCORE_FORWARDEDHEADERS_ENABLED=true` dans l'image web : l'application voit l'IP du visiteur et le schéma
  HTTPS derrière Caddy (nécessaire à la limite par IP de la session 4).
- `EmbeddingWarmup` (web) : vectorise une phrase au démarrage pour charger bge-m3 ; `OLLAMA_KEEP_ALIVE=-1` le garde.
- Swap de 2 Go sur le VPS (`/swapfile`, `vm.swappiness=10`, 2026-10-09) : filet de sécurité (construction d'images
  sur le serveur pendant que tout tourne). Le graphique RAM d'OVH compte le cache disque : lire `free -m` → available.
