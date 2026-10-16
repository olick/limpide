# Semaine 6 : Terraform, apprentissage et infrastructure Azure

**Objectif de fin de semaine** : l'infrastructure complète de Limpide est décrite en code.
`terraform destroy` puis `terraform apply` recrée un environnement fonctionnel sans intervention manuelle.

**Budget temps** : environ 12 heures, en 6 sessions. C'est la semaine la plus chargée : la semaine 10 sert de marge.

**Prérequis** : compte Azure, **alerte de budget** configurée avant toute création de ressource.

**Décision du 2026-10-12 (option B)** : la démo publique **reste sur le VPS** (≈ 4 €/mois, ADR-006). Azure sert de
**vitrine d'architecture** : infrastructure décrite en code, préproduction et production identiques, Key Vault et
identités managées, ingestion en tâches cloud, stockage objet des fichiers bruts. Les environnements Azure sont
**recréés à la demande** (`apply` en début de session, `destroy` à la fin) : coût visé, quelques euros par mois, au
lieu de 60 à 90 € pour un fonctionnement permanent. Avant Azure, apprendre Terraform sans frais : fournisseur `docker`
(pile locale), puis fournisseur `ovh` (zone DNS de `limpide-ia.fr`, utile tout de suite). Outil : **Terraform**
(décision du 2026-10-12) ; OpenTofu, son fork libre compatible, à citer dans l'ADR comme alternative.

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

## Session 1 : prise en main sans frais (≈ 1 h 30)

Option B : le cycle s'apprend sur Docker, gratuitement, avant tout compte Azure. Exercices et notions :
`infrastructure/terraform/apprentissage/docker/README.md`.

1. Installer Terraform (dépôt HashiCorp, Debian 13).
2. Base PostgreSQL + pgvector décrite en Terraform (fournisseur `docker`, noms `tf-apprentissage-*`, la pile de
   développement n'est pas touchée) : `init`, lire le `plan`, `apply`, `plan` sans changement.
3. **Dérive** : conteneur supprimé à la main, `plan` le constate, `apply` répare ; repérer `-/+` (remplacement)
   contre `~` (modification en place).
4. Lire l'état : le mot de passe y est en clair. Puis `destroy`.
5. Ensuite, fournisseur `ovh` : la zone DNS de `limpide-ia.fr`, déjà existante, reprise par **import** (notion
   nouvelle : faire entrer dans Terraform une ressource créée à la main) ; `plan` sans changement attendu.
6. Puis Azure CLI, `az login`, alerte de budget, et le cycle sur un groupe de ressources et un compte de stockage.

**Terminé quand** : tu as fait le cycle complet et observé une dérive.

## Session 2 : l'état distant (≈ 1 h 30) — faite le 2026-10-13

Coffre créé par `infrastructure/terraform/etat/creer-socle.sh` (Azure CLI) ; état de la zone DNS migré ;
verrou vérifié. Détail : `infrastructure/terraform/etat/README.md`.

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
5. **Embeddings en production** : trancher entre un conteneur Ollama dans Azure et une API d'embeddings (voir l'ADR sur l'hébergement des modèles).

**Terminé quand** : la démo tourne sur Azure (environnement recréé à la demande), déployée uniquement par Terraform ;
la démo publique, elle, reste sur le VPS (option B).

## Session 5 : modules et environnements (≈ 2 h) — code fait le 2026-10-13

Modules `identite`, `donnees`, `stockage`, `secrets`, `app`, `budget` ; environnements `preprod` et `prod` (états
séparés), blocs `moved` ; scripts `demo.sh --env`, `detruire.sh <env>`. Test final (2026-10-13) : `demo.sh` sur environnement vide, 11 min 12 s au total (infrastructure 9 min 10 s,
base 28 s), sans intervention manuelle ; destruction complète 13 min 5 s. Pour la S7 : un cycle complet ≈ 25 min,
mais la destruction peut se faire après le verdict de l'évaluation, sans le retarder.
Détail : `infrastructure/terraform/azure/README.md`.

1. Factoriser en modules : `data`, `app`, `network`, `secrets`.
2. Deux environnements, préproduction et production, avec des tailles différentes (la préproduction au minimum).
3. **Alerte de budget** décrite en Terraform, par environnement.
4. Test final : `destroy` de la préproduction, puis `apply`, puis vérification que tout fonctionne.
   **Chronométrer** `apply` et `destroy` (et la restauration des données) : la mesure décide de la préproduction
   éphémère créée par la chaîne de la S7 (option B).

**Terminé quand** : la préproduction se détruit et se recrée sans intervention.

## Session 6 : décisions et documentation (≈ 2 h 30) — faite le 2026-10-13

ADR-011 (Container Apps), ADR-012 (modèles), ADR-013 (Airflow), comparatif de coûts
(`docs/notes/comparatif-couts-hebergement.md`), schéma `docs/architecture-physique.md`. Reste hors session : mise en
œuvre de l'ADR-013 (ingestion en production).

1. **ADR — Container Apps plutôt qu'AKS** : coût, exploitation, compétences ; AKS comme voie de montée en charge.
2. **ADR — hébergement d'Airflow** : service managé, conteneur dédié, ou petite machine.
   C'est le composant le plus lourd du projet : chiffrer chaque option.
3. **ADR — hébergement des modèles : LLM par API ou auto-hébergé sur GPU** : calcul du coût mensuel pour 100, 1 000 et 10 000 questions par jour,
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
- ADR : Container Apps ou AKS, hébergement d'Airflow, hébergement des modèles (API ou GPU)
- Schéma d'architecture physique
- Démo servie depuis Azure

## Ce qui prépare la suite

| Fait cette semaine | Réutilisé en |
|---|---|
| Deux environnements | S7 : déploiement en préproduction avant la production |
| Identités managées | S7 : authentification de GitHub Actions à Azure |
| Journalisation Container Apps | S8 : observabilité |
| Alertes de budget | S8 : FinOps |
| ADR hébergement des modèles (coût GPU) | Soutenance BC02 : dimensionnement CPU/GPU |

---

## Journal de réalisation

Repris tel quel de `CLAUDE.md` le 2026-10-15 (rangement : `CLAUDE.md` ne garde que l'état courant, les règles et les pièges ; le détail des sessions vit ici).

**S6 terminée (2026-10-13)** (`docs/plan/semaine-06.md`) — Terraform et Azure ; schéma `docs/architecture-physique.md` ;
ADR-013 mis en œuvre le 2026-10-13 (Airflow et première ingestion en production). Rappel de la S6 : Alexandre ne connaît pas Terraform : concepts
d'abord. **Option B décidée le 2026-10-12** : la démo publique reste sur le VPS ; Azure = vitrine d'architecture
recréée à la demande (`apply` / `destroy` par session, alerte de budget), apprentissage d'abord sans frais
(fournisseurs `docker`, puis `ovh` pour la zone DNS). Terraform ne demande pas d'abonnement ; seul le fournisseur
`azurerm` en demande un. **Outil retenu : Terraform** (décision du 2026-10-12 : celui des clients et des offres ;
licence BSL depuis 2023) ; OpenTofu (fork compatible, MPL) noté dans l'ADR comme alternative sans réécriture. À faire en S6 :
**comparatif de coûts** Azure / Scaleway / OVHcloud / Hetzner / Clever Cloud / VPS auto-hébergé (permanent et à la
demande), hébergement d'Airflow (le corpus de production n'est mis à jour par aucun pipeline), portabilité souveraine.
Session 1 en cours : Terraform 1.16.5 installé (dépôt HashiCorp) ; exercice Docker
(`infrastructure/terraform/apprentissage/docker/`, base `tf-apprentissage-*` sur 5442, fournisseur `kreuzwerker/docker`
4.6.0) fait par Alexandre le 2026-10-12 : cycle, dérive, `-/+`, mot de passe en clair dans l'état, `destroy`. État et
`.terraform/` ignorés par Git, `.terraform.lock.hcl` versionné. Zone DNS OVH (`infrastructure/terraform/dns/`,
fournisseur `ovh/ovh` 2.22.0, jeton d'API limité à la zone dans `~/.ovh.conf`) : inventaire par sources de données
(12 entrées), import de `@`, `www`, `admin` (plan sans changement), entrée erronée `limpide-ia.fr.limpide-ia.fr`
supprimée par le code (2026-10-12) ; entrées posées par OVH (NS, MX, SPF, ftp, TXT) non gérées. Terraform ajouté à la
vitrine des technologies. État local (reconstructible par les blocs `import`). Azure (2026-10-13) : abonnement
« Azure subscription 1 », budget « Limpide » 10 €/mois (alerte seulement), Azure CLI 2.91 (dépôt `azure-cli` bookworm, clé
`/usr/share/keyrings/microsoft.gpg`) ; exercice `infrastructure/terraform/apprentissage/azure/` (azurerm 5.9.0, random
3.9.1 ; `ARM_SUBSCRIPTION_ID` par l'environnement ; `resource_providers_to_register` car azurerm 5 n'active plus rien :
`Microsoft.Storage` activé) fait par Alexandre : création, dérive d'étiquette (`~`), remplacement en cascade lu,
clé d'accès en clair dans l'état, `destroy` (rien ne reste). Session 2 faite (2026-10-13) : coffre de l'état
`rg-limpide-tfstate` / `stlimpidetf024b1e9f` / conteneur `tfstate`, créé par `infrastructure/terraform/etat/creer-socle.sh`
(hors Terraform ; clés d'accès désactivées, accès par Entra ID, « Storage Blob Data Contributor » sur le conteneur
seul, versions et suppression récupérable 30 jours) ; **seule ressource Azure permanente** (exception à l'option B).
État DNS migré (`dns/backend.tf`, `dns.tfstate`), plan sans changement, verrou vérifié par Alexandre. Les exercices
d'apprentissage gardent un état local. Session 3 faite (2026-10-13) : `infrastructure/terraform/azure/` (14 ressources,
fichiers par rôle, état `azure-preprod.tfstate`) ; PostgreSQL 17 B1ms **sans mot de passe** (Entra ID seul,
Alexandre administrateur, identité managée `id-limpide-preprod` = rôle créé par `pgaadauth_create_principal`),
`initialiser-base.sh` (rôle, migrations, droits, `--avec-corpus`) ; Key Vault RBAC (clé `mistral-api-key` déposée à
la main), stockage `brut` sans clés. Vérifié : 1 377 passages (1 130 servis), clé absente de l'état, aucun mot de
passe. **`apply` ≈ 10 min**, `destroy` ≥ 4 min (données pour la préproduction éphémère de la S7). Premier `destroy`
en échec (administrateur Entra supprimé avant le serveur = `DROP ROLE` refusé) : `detruire.sh` le retire de l'état
d'abord ; toujours détruire par ce script. Session 4 préparée (2026-10-13) : socle renommé `etat/creer-socle.sh`
(+ coffre à secrets **permanent** `kvlimpide024b1e9f` : clé Mistral déposée une fois ; le coffre par environnement de
la session 3 est retiré), `conteneurs.tf` (Log Analytics, Container Apps : `ca-ollama` interne, `ca-web` public),
pare-feu « services Azure » (accès public filtré, Entra ID = vraie barrière), images sur GitHub Container Registry
(`deploy/publier-images.sh`, version = empreinte du commit), `deploy/ollama/Dockerfile` (bge-m3 inclus, sans
bibliothèques GPU : 2,4 Go au lieu de 11,5 ; vecteurs identiques au poste). Code : `DatabaseOptions`
(`Database:Authentication` = `Password` par défaut | `EntraId`, jeton Entra ID par `UsePeriodicPasswordProvider`,
Azure.Identity 1.21.0). Ingestion sur Azure (jobs) reportée : stockage derrière une interface d'abord. **Session 4 faite (2026-10-13)** : Limpide tourne sur
Azure, testé par Alexandre (réponses correctes). Premier `apply` 22 min 16 s ; deux corrections (`logs_destination`
obligatoire en azurerm 5 ; `principal_name` tronqué à 63 caractères par Azure → `ignore_changes`, sinon remplacement
donc DROP ROLE). `azure/demo.sh` monte tout (vérifications, plan confirmé, apply, base, test HTTP, durées) ;
`detruire.sh` utilisable depuis un nouveau terminal. Session 5, code fait (2026-10-13) : `azure/modules/` (identite, donnees,
stockage, secrets, app, budget) et `azure/environnements/{preprod,prod}` (états `azure-preprod.tfstate` /
`azure-prod.tfstate`, tailles et budgets différents : B1ms/10 € contre B2s/20 €), `moved.tf` en préproduction (adresses
passées dans les modules ; plan : 18 ressources par environnement) ; `demo.sh --env`, `detruire.sh <env>`,
`initialiser-base.sh <env>`. **Test final fait (2026-10-13)** : `demo.sh` sur préproduction vide = infrastructure 9 min 10 s, base 28 s,
total 11 min 12 s, sans intervention (critère de la S6 atteint) ; `detruire.sh` complet : 13 min 5 s. Reste :
session 6 : ADR-011 (Container Apps) et comparatif de coûts (`docs/notes/comparatif-couts-hebergement.md`,
2026-10-13 : services gérés 75 à 108 €/mois contre 4 à 6 € pour un VPS ; Ollama ≈ 80 % du coût des conteneurs ; Azure
moins cher que Scaleway pour un service toujours allumé grâce à son tarif au repos ; Scaleway = cible de migration la
plus cohérente, une instance + Compose recréée par Terraform la moins chère) faits ; restent ADR Airflow, ADR modèles,
schéma. Épisode LinkedIn 23 écrit (2026-10-13 : Terraform, option B, deux échecs du cycle réel).

État noté dans les décisions de CLAUDE.md :

- ADR-012 : génération par API Mistral (GPU rentable seulement vers 3 800 questions/jour face à Medium, et avec un
  modèle moins bon) ; vectorisation par **bge-m3 chez OVHcloud AI Endpoints** (0,01 €/million de tokens, France, mêmes
  vecteurs et même évaluation qu'Ollama ; mistral-embed mesuré et écarté : top 5 5/10, MRR 0,31) ; Ollama sur le poste
  et en secours (profil `ollama` du VPS, `deploy/ollama`). **En production le 2026-10-13** : clé `OVH_AI_ENDPOINTS_KEY` dans le
  `.env.prod` du serveur, `web` redéployé, conteneurs Ollama supprimés (volume gardé), mémoire disponible 1 508 → 3 007 Mo ;
  clé `ovh-ai-endpoints-key` dans le coffre permanent Azure.
  Mesures du 2026-10-13 (`docs/notes/observations-modeles-ovh.md`) : rédaction par modèles ouverts chez OVHcloud
  (`Generation:Endpoint`, `Generation:ApiKey` générique) : gpt-oss-120b 11/15, 0,04 centime mais 14 s ; Llama 3.3 tranche
  la situation ; Qwen3.5-397B plus cher que Medium (réflexion). `ModelText.Clean` retire les caractères invisibles
  (U+200B de gpt-oss dans « [P5] »). Vectorisation Qwen3-Embedding-8B (`RequestDimensions`, `QueryInstruction`) :
  top 5 10/10, MRR 0,80 ; latence remesurée 218 ms (3,9 s = ralentissement passager) : candidat à confirmer sur 50
  questions (S7). Relecture de gpt-oss : 4 erreurs + 1 qualification invisibles au contrôle automatique → **Medium
  gardé**. **Tâche notée en S7** : passer la vectorisation à Qwen3-Embedding-8B si le jeu de 50 questions confirme
  (seuil, réglages, bascule sans interruption, ADR-012 révisé) et écrire l'épisode LinkedIn à ce moment (note P).
- ADR-013 (proposé) : Airflow **allégé** (LocalExecutor, `pipelines/airflow/docker-compose.leger.yaml`) sur le VPS de la
  démo, interface par tunnel SSH ; mesuré : 0,70 Go au repos, 0,88 Go au pic (officiel Celery : 1,77 Go) ; VPS : 2,97 Go
  disponibles ; Airflow géré ≈ 300–350 $/mois écarté ; repli : minuteur système. **En production le 2026-10-13** : fichiers du corpus
  copiés dans le volume, `.env` d'Airflow sur le serveur (secrets tirés au hasard ou copiés de `.env.prod`, rien
  affiché), DAG paramétré par `LIMPIDE_*` (`docker-compose.production.yaml`) ; première ingestion réelle réussie :
  17 fausses versions CNIL écartées, rien publié, mémoire disponible ≥ 1,9 Go. Interface : tunnel SSH vers 8080.

Vitrine des technologies (demandée le 2026-10-12) : section « Comment Limpide est construit » de la démo
(`src/Limpide.Web/Components/TechStack.razor`), technologies groupées par rôle, icônes Simple Icons (CC0) servies par
l'application (`wwwroot/tech/`, aucune requête vers un tiers ; Microsoft absent de Simple Icons → icône OpenID).
**À tenir à jour** à chaque technologie adoptée (Terraform ou OpenTofu en S6, GitHub Actions en S7, OpenTelemetry en S8…).
