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
