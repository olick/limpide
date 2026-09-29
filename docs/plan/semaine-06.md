# Semaine 6 : Terraform, apprentissage et infrastructure Azure

**Objectif de fin de semaine** : l'infrastructure complète de Limpide est décrite en code.
`terraform destroy` puis `terraform apply` recrée un environnement fonctionnel sans intervention manuelle.

**Budget temps** : environ 12 heures, en 6 sessions. C'est la semaine la plus chargée : la semaine 10 sert de marge.

**Prérequis** : compte Azure, **alerte de budget** configurée avant toute création de ressource.

---

## Les concepts à comprendre avant de coder

| Concept | Ce que c'est | Point d'attention |
|---|---|---|
| **Provider** | Le connecteur vers un fournisseur (ici `azurerm`) | Fixer sa version |
| **Ressource** | Un élément d'infrastructure décrit en code | Terraform calcule l'ordre de création à partir des dépendances |
| **Variable / sortie** | Paramètres d'entrée, valeurs exposées en sortie | Pas de secret en valeur par défaut |
| **État (state)** | Le fichier où Terraform mémorise ce qu'il a créé | **Le point critique** : perdu, Terraform ne sait plus ce qui existe ; il peut contenir des secrets en clair |
| **`plan` / `apply`** | Prévisualiser les changements, puis les appliquer | Toujours lire le `plan` avant l'`apply` |
| **Module** | Un ensemble de ressources réutilisable | Sert à décliner préproduction et production |

---

## Session 1 : prise en main (≈ 1 h 30)

1. Installer Terraform et Azure CLI, se connecter avec `az login`.
2. Créer un groupe de ressources et un compte de stockage, lire le `plan`, appliquer, puis `terraform destroy`.
3. Modifier une ressource à la main dans le portail, relancer `plan` : observer la **dérive**.

**Terminé quand** : tu as fait le cycle complet et observé une dérive.

## Session 2 : l'état distant (≈ 1 h 30)

1. Créer à la main (ou avec un petit script à part) le compte de stockage qui hébergera l'état.
2. Configurer le backend `azurerm` : état distant, verrouillage pendant les opérations.
3. Migrer l'état local vers le backend distant.
4. Vérifier que le fichier d'état n'est **jamais** commité (`.gitignore`).

**Terminé quand** : l'état est distant, et deux `apply` simultanés sont refusés par le verrou.

## Session 3 : données et secrets (≈ 2 h)

1. **PostgreSQL managé** (Flexible Server) : autoriser l'extension `vector` dans les paramètres du serveur, puis l'activer via la migration.
2. **Blob Storage** pour les documents bruts.
3. **Key Vault** : clé d'API du LLM, chaîne de connexion.
4. **Identités managées** : l'application lit les secrets du Key Vault sans aucune clé dans sa configuration.
5. Appliquer les migrations (outil de la semaine 5) sur la base Azure.

**Terminé quand** : la base Azure contient le schéma, et les secrets ne sont visibles que dans le Key Vault.

## Session 4 : application et réseau (≈ 2 h 30)

1. **Environnement Azure Container Apps** et journalisation.
2. Application `Limpide.Web` (image de la semaine 3), avec mise à l'échelle jusqu'à zéro.
3. Tâches d'ingestion en **Container Apps Jobs** (image de la semaine 4).
4. **Réseau** : la base n'est joignable que depuis l'environnement de l'application.
   Deux options à comparer : accès privé par réseau virtuel (plus sûr, plus complexe et plus cher)
   ou accès public restreint par pare-feu. Choisir et justifier.
5. **Embeddings en production** : trancher entre un conteneur Ollama dans Azure et une API d'embeddings (voir ADR-012).

**Terminé quand** : la démo tourne sur Azure, déployée uniquement par Terraform.

## Session 5 : modules et environnements (≈ 2 h)

1. Factoriser en modules : `data`, `app`, `network`, `secrets`.
2. Deux environnements, préproduction et production, avec des tailles différentes (la préproduction au minimum).
3. **Alerte de budget** décrite en Terraform, par environnement.
4. Test final : `destroy` de la préproduction, puis `apply`, puis vérification que tout fonctionne.

**Terminé quand** : la préproduction se détruit et se recrée sans intervention.

## Session 6 : décisions et documentation (≈ 2 h 30)

1. **ADR-010 — Container Apps plutôt qu'AKS** : coût, exploitation, compétences ; AKS comme voie de montée en charge.
2. **ADR-011 — hébergement d'Airflow** : service managé, conteneur dédié, ou petite machine.
   C'est le composant le plus lourd du projet : chiffrer chaque option.
3. **ADR-012 — LLM par API ou auto-hébergé sur GPU** : calcul du coût mensuel pour 100, 1 000 et 10 000 questions par jour,
   seuil de bascule, contraintes de souveraineté. On ne loue pas de GPU : le calcul suffit.
4. Schéma d'architecture physique (ressources, réseau, flux) et comparaison avec le déploiement manuel de la semaine 3.

**Terminé quand** : les trois ADR et le schéma sont commités.

---

## Pièges à éviter

- **Perdre ou commiter l'état** : état distant, verrouillé, jamais dans le dépôt.
- **Des secrets dans les variables Terraform** : ils finissent en clair dans l'état. Les créer dans le Key Vault, les lire par identité managée.
- **Modifier des ressources dans le portail** : toute modification passe par le code, sinon la dérive s'installe.
- **Oublier de détruire la préproduction** : elle coûte même quand personne ne s'en sert.
- **Viser la perfection réseau d'emblée** : une option plus simple, justifiée dans un ADR, vaut mieux qu'une configuration non terminée.

## Livrables

- `infrastructure/terraform/` : backend, modules, environnements
- ADR-010, ADR-011, ADR-012
- Schéma d'architecture physique
- Démo servie depuis Azure

## Ce qui prépare la suite

| Fait cette semaine | Réutilisé en |
|---|---|
| Deux environnements | S7 : déploiement en préproduction avant la production |
| Identités managées | S7 : authentification de GitHub Actions à Azure |
| Journalisation Container Apps | S8 : observabilité |
| Alertes de budget | S8 : FinOps |
| ADR-012 (coût GPU) | Soutenance BC02 : dimensionnement CPU/GPU |
