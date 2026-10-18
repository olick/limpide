# Environnement Azure de Limpide (vitrine, option B)

S6. La démo publique reste sur le VPS ; cet environnement montre la version « cloud » de Limpide, décrite en code,
**recréée à chaque séance** (`./demo.sh` au début, `./detruire.sh` à la fin).

Depuis la session 5 : des **modules** (blocs réutilisables, un par rôle) et **deux environnements** qui les appellent
avec des tailles différentes, chacun avec son propre état (`azure-preprod.tfstate`, `azure-prod.tfstate`, dans le
socle `../etat`).

| Module (`modules/`) | Contenu |
|---|---|
| `identite` | identité managée de l'application (aucun secret : Azure l'authentifie) |
| `donnees` | PostgreSQL 17 managé, pgvector autorisé, base `rag`, **connexion par Entra ID seulement** ; pare-feu : poste de l'administrateur et services Azure |
| `stockage` | compte de stockage des documents bruts (conteneur `brut`), sans clés d'accès ; l'application y écrit |
| `secrets` | droit de l'application de lire les secrets du **coffre permanent du socle** |
| `app` | journaux (Log Analytics), environnement Container Apps, application web (publique) ; vectorisation par Qwen3-Embedding-8B chez OVHcloud AI Endpoints (ADR-012) |
| `budget` | alerte de budget de l'environnement (50, 80, 100 % ; propriétaires de l'abonnement) |

| Environnement (`environnements/`) | PostgreSQL | Application | Budget |
|---|---|---|---|
| `preprod` | B1ms (1 vCore partagé, 2 Go), sauvegardes 7 j | 0,5 vCPU / 1 Go, 1 instance | 10 € / mois |
| `prod` | B2s (2 vCores, 4 Go), sauvegardes 14 j | 1 vCPU / 2 Go, 1 à 3 instances | 20 € / mois |

`environnements/preprod/moved.tf` : les ressources ont changé d'adresse en passant dans des modules
(`azurerm_container_app.web` → `module.app.azurerm_container_app.web`). Sans bloc `moved`, Terraform croirait qu'elles
ont disparu et que d'autres sont apparues : il détruirait puis recréerait tout, base comprise. Le bloc dit « même
ressource, nouvelle adresse ».

Rien de secret dans le code, dans l'état ni dans la configuration des conteneurs : PostgreSQL par jeton Entra ID,
clés Mistral et OVHcloud lues dans le coffre par la plateforme au démarrage, grâce à l'identité de l'application.

## Une fois pour toutes

1. **Socle** (état et secrets) : `../etat/creer-socle.sh`, puis déposer les clés Mistral et OVHcloud AI Endpoints dans le coffre permanent
   (commande dans `../etat/README.md`). Elle sert à toutes les séances suivantes.
2. **Registre d'images** : GitHub Container Registry (gratuit pour un dépôt public, survit aux `destroy`).
   Jeton GitHub (Settings → Developer settings → Personal access tokens → Tokens (classic)) avec le droit
   `write:packages`, puis `docker login ghcr.io -u olick` (coller le jeton à l'invite).
3. Après la première publication : rendre public le paquet `limpide-web` (et `limpide-ollama`, image de secours, s'il est publié) (GitHub → profil →
   Packages → le paquet → Package settings → Change visibility → Public). Azure les télécharge alors sans identifiant.

## Une séance

Coût : ≈ 0,03 € de l'heure depuis l'ADR-012 (≈ 0,10 € avec Ollama : ADR-011 ; tarifs relevés le 2026-10-13 :
PostgreSQL B1ms 0,0167 €/h, application au repos ≈ 0,016 $/h, vectorisation ≈ 0,01 € le million de tokens) ;
**`./detruire.sh` à la fin.**

En une commande (vérifications, plan à confirmer, `apply`, base restaurée depuis le socle, test de fumée, durées) :

```bash
cd infrastructure/terraform/azure
./demo.sh              # préproduction, images du commit courant, déjà publiées
./demo.sh --env prod   # la production
./demo.sh --publier    # après une modification de src/ ou du Dockerfile : publie d'abord l'image
./detruire.sh          # fin de séance : préproduction (./detruire.sh prod pour la production)
```

Le détail, étape par étape (ce que fait `demo.sh`) :

```bash
deploy/publier-images.sh                     # depuis la racine du dépôt, seulement si le code a changé ; affiche la version
cd infrastructure/terraform/azure/environnements/preprod
export ARM_SUBSCRIPTION_ID=$(az account show --query id -o tsv)
export TF_VAR_admin_principal_name="$(az ad signed-in-user show --query userPrincipalName -o tsv)"
export TF_VAR_poste_ipv4="$(curl -s https://api.ipify.org)"
export TF_VAR_image_tag=<version affichée par publier-images.sh>
terraform init
terraform apply                              # ≈ 10 min ou plus (PostgreSQL, puis les conteneurs)
../../initialiser-base.sh preprod --avec-corpus   # rôle de l'application, corpus du socle, migrations, droits
../../verifier.sh preprod                    # accueil et /sante (l'application lit-elle la base ?)
terraform output url                         # ouvrir dans le navigateur, poser une question
```

**Vérifications** :

- une question reçoit une réponse avec ses extraits : base (jeton Entra ID), vectorisation (OVHcloud) et clé Mistral fonctionnent ;
- `terraform state pull | grep -ci mistral` → 0 (dans `environnements/<env>`) ; aucun mot de passe dans l'état ;
- journaux : portail → `ca-web` → Monitoring → Log stream (ou `az containerapp logs show -n ca-web -g rg-limpide-preprod`).

**Fin de séance** :

```bash
./detruire.sh         # affiche ce qui sera détruit, demande « oui », détruit, donne la durée
```

Pas de `terraform destroy` direct : Terraform supprimerait l'administrateur Entra ID de PostgreSQL avant le serveur,
ce qui revient à un `DROP ROLE` d'un rôle propriétaire des tables, refusé par PostgreSQL (échec du premier `destroy`,
2026-10-13). Le script le retire d'abord de l'état ; la suppression du serveur emporte tout. Au passage : le nom de ce
rôle est tronqué à 63 caractères (limite de PostgreSQL), l'UPN d'un compte Microsoft personnel invité étant plus
long ; sans effet sur la connexion.

## Par la chaîne (S7, session 4b-2, ADR-016)

Workflow « Azure » (`.github/workflows/azure.yml`), lancé à la main depuis l'onglet Actions, puis validé
(environnement GitHub `azure`, limité à `main`, relecteur requis) :

| Action | Ce qu'il fait |
|---|---|
| `verifier` | préproduction créée, base, test de fumée, puis détruite même en cas d'échec ; refuse si une préproduction existe déjà (`est-vide.sh`) |
| `vitrine` | production créée ou mise à jour, laissée en place (≈ 0,25 € de l'heure) |
| `detruire-vitrine` | production détruite |

Mêmes scripts qu'à la main, avec `--oui` (sans confirmation), `--neuf` et `--sans-base` (la chaîne se reconnecte à
Azure entre l'`apply` et la base : le jeton OIDC de GitHub ne vit que quelques minutes). Administrateur PostgreSQL :
l'identité de la chaîne, `id-limpide-ci-azure` (`TF_VAR_admin_principal_type=ServicePrincipal`).

**Verrou d'état** : un `apply` ou un `destroy` tué brutalement (délai dépassé) laisse le bail du blob d'état ;
la destruction suivante échoue sur « state locked » et l'environnement continue de coûter. Depuis le poste :

```bash
cd environnements/preprod && terraform init
terraform force-unlock <identifiant du verrou affiché dans l'erreur>
cd ../.. && ./detruire.sh preprod
```

## Choix de la session 4

- **Registre** : GitHub Container Registry (gratuit, images conservées, CI de la S7 sur GitHub) ; Azure Container
  Registry (≈ 5 € par mois, détruit à chaque séance) écarté ; à l'étape « Migration cloud », le registre suivra
  l'hébergeur cible (Scaleway, OVHcloud).
- **Embeddings** : d'abord Ollama + bge-m3 dans l'environnement (image avec le modèle inclus, `deploy/ollama/`,
  2,4 Go) ; **remplacé le 2026-10-13 (ADR-012) par bge-m3 chez OVHcloud AI Endpoints** : mêmes vecteurs (cosinus
  0,99998), même évaluation (7/10, MRR 0,52), ≈ 80 % du coût des conteneurs en moins, un conteneur de moins.
  L'image Ollama reste en secours (`deploy/publier-images.sh --avec-ollama`).
- **Réseau** : accès public filtré (règle « services Azure » + poste), l'authentification Entra ID étant la vraie
  barrière ; réseau privé (VNet) : voie de production, dans l'ADR.
- **Secrets** : coffre permanent du socle (la clé Mistral déposée une fois) au lieu d'un coffre par environnement
  (session 3), qui obligeait à la redéposer après chaque `apply`.
- **Pas encore** : tâches d'ingestion (Container Apps Jobs) : les commandes écrivent dans `data/` ; il faut d'abord
  le stockage derrière une interface (portabilité, plan global). Clés DataProtection non persistées sur Azure : une
  déconnexion des visiteurs à chaque redémarrage, sans effet pour une vitrine.

## Mesures

- **Test final de la S6** (2026-10-13, session 5, préproduction vide, `./demo.sh`) : infrastructure **9 min 10 s**
  (18 ressources), base **28 s** (rôle, migrations, droits, corpus), total **11 min 12 s** vérifications et
  confirmation comprises ; aucune intervention manuelle. **Destruction complète d'un trait (`./detruire.sh`) :
  13 min 5 s**, plus longue que la création (suppression du serveur PostgreSQL et de l'environnement Container Apps). Vérifié : réponse 200, budget `budget-limpide-preprod`
  (10 €) créé, bge-m3 chargé en 2,5 s au démarrage de l'application. Avertissement attendu : clés DataProtection non
  persistées.

- Session 4 (2026-10-13) : premier `apply` complet 22 min 16 s, arrêté par deux erreurs de configuration (destination
  des journaux ; nom d'administrateur tronqué par Azure qui forçait son remplacement), corrigées ; les 3 ressources
  restantes créées ensuite. Application testée par Alexandre : réponses correctes (PostgreSQL par jeton Entra ID,
  Ollama interne, clé Mistral lue dans le coffre).
- Session 3 (2026-10-13, première création) : `apply` ≈ 10 min (PostgreSQL l'essentiel) ; `destroy` en deux temps
  (échec ci-dessus) : 1 min 55 s puis 1 min 56 s, soit au moins 4 min. Base vérifiée : 4 migrations, pgvector 0.8.2,
  1 377 passages dont 1 130 servis ; aucun secret dans l'état.

## Pourquoi sans mot de passe

PostgreSQL n'accepte que des jetons Entra ID : l'administrateur (la personne qui applique Terraform) se connecte avec
le jeton de sa session `az login` ; l'application avec celui de son identité managée (`Database:Authentication =
EntraId`, `Database:ManagedIdentityClientId` : réglages lus par `AddLimpideInfrastructure`, jeton renouvelé toutes
les 30 minutes). Il n'existe donc aucun mot de passe de base de données : ni dans l'état, ni dans un coffre, ni dans
la configuration. Le VPS et le poste gardent leur mot de passe (`Database:Authentication = Password`, par défaut).
