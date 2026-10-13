# Environnement Azure de Limpide (vitrine, option B)

S6. La démo publique reste sur le VPS ; cet environnement montre la version « cloud » de Limpide, décrite en code,
**recréée à chaque séance** (`apply` au début, `./detruire.sh` à la fin). État : blob `azure-preprod.tfstate` du
socle (`../etat`). Organisation par rôle, en attendant les modules (session 5) :

| Fichier | Contenu |
|---|---|
| `main.tf` | groupe de ressources, étiquettes, suffixe des noms uniques |
| `identite.tf` | identité managée de l'application (aucun secret : Azure l'authentifie) |
| `donnees.tf` | PostgreSQL 17 managé, pgvector autorisé, base `rag`, **connexion par Entra ID seulement** (aucun mot de passe) ; pare-feu : poste de l'administrateur et services Azure |
| `stockage.tf` | compte de stockage des documents bruts (conteneur `brut`), sans clés d'accès ; l'application y écrit |
| `secrets.tf` | droit de l'application de lire les secrets du **coffre permanent du socle** |
| `conteneurs.tf` | journaux (Log Analytics), environnement Container Apps, Ollama + bge-m3 (interne), application web (publique) |

Rien de secret dans le code, dans l'état ni dans la configuration des conteneurs : PostgreSQL par jeton Entra ID,
clé Mistral lue dans le coffre par la plateforme au démarrage, grâce à l'identité de l'application.

## Une fois pour toutes

1. **Socle** (état et secrets) : `../etat/creer-socle.sh`, puis déposer la clé Mistral dans le coffre permanent
   (commande dans `../etat/README.md`). Elle sert à toutes les séances suivantes.
2. **Registre d'images** : GitHub Container Registry (gratuit pour un dépôt public, survit aux `destroy`).
   Jeton GitHub (Settings → Developer settings → Personal access tokens → Tokens (classic)) avec le droit
   `write:packages`, puis `docker login ghcr.io -u olick` (coller le jeton à l'invite).
3. Après la première publication : rendre publics les paquets `limpide-web` et `limpide-ollama` (GitHub → profil →
   Packages → le paquet → Package settings → Change visibility → Public). Azure les télécharge alors sans identifiant.

## Une séance

Coût : ≈ 0,25 € de l'heure (PostgreSQL ≈ 2 centimes, Ollama 2 vCPU / 4 Go et l'application 0,5 vCPU / 1 Go
toujours allumés pendant la séance) ; **`./detruire.sh` à la fin.**

En une commande (vérifications, plan à confirmer, `apply`, base, test de l'adresse, durées) :

```bash
cd infrastructure/terraform/azure
./demo.sh              # images du commit courant, déjà publiées
./demo.sh --publier    # après une modification du code : publie d'abord les images du commit courant
./detruire.sh          # fin de séance (utilisable depuis un nouveau terminal)
```

Le détail, étape par étape (ce que fait `demo.sh`) :

```bash
deploy/publier-images.sh                     # depuis la racine du dépôt, seulement si le code a changé ; affiche la version
cd infrastructure/terraform/azure
export ARM_SUBSCRIPTION_ID=$(az account show --query id -o tsv)
export TF_VAR_admin_principal_name="$(az ad signed-in-user show --query userPrincipalName -o tsv)"
export TF_VAR_poste_ipv4="$(curl -s https://api.ipify.org)"
export TF_VAR_image_tag=<version affichée par publier-images.sh>
terraform init
terraform apply                              # ≈ 10 min ou plus (PostgreSQL, puis les conteneurs)
./initialiser-base.sh --avec-corpus          # rôle de l'application, migrations, droits, corpus
terraform output url                         # ouvrir dans le navigateur, poser une question
```

**Vérifications** :

- une question reçoit une réponse avec ses extraits : base (jeton Entra ID), Ollama et clé Mistral fonctionnent ;
- `terraform state pull | grep -ci mistral` → 0 ; aucun mot de passe dans l'état ;
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

## Choix de la session 4

- **Registre** : GitHub Container Registry (gratuit, images conservées, CI de la S7 sur GitHub) ; Azure Container
  Registry (≈ 5 € par mois, détruit à chaque séance) écarté ; à l'étape « Migration cloud », le registre suivra
  l'hébergeur cible (Scaleway, OVHcloud).
- **Embeddings** : Ollama + bge-m3 dans l'environnement, image avec le modèle inclus (`deploy/ollama/Dockerfile`,
  2,4 Go sans les bibliothèques GPU) ; vecteurs identiques à ceux du poste (écart 0,0, vérifié) : ni recalcul du
  corpus, ni recalibrage du seuil. Alternatives (API d'embeddings ; Ollama du VPS mutualisé) : ADR sur l'hébergement
  des modèles.
- **Réseau** : accès public filtré (règle « services Azure » + poste), l'authentification Entra ID étant la vraie
  barrière ; réseau privé (VNet) : voie de production, dans l'ADR.
- **Secrets** : coffre permanent du socle (la clé Mistral déposée une fois) au lieu d'un coffre par environnement
  (session 3), qui obligeait à la redéposer après chaque `apply`.
- **Pas encore** : tâches d'ingestion (Container Apps Jobs) : les commandes écrivent dans `data/` ; il faut d'abord
  le stockage derrière une interface (portabilité, plan global). Clés DataProtection non persistées sur Azure : une
  déconnexion des visiteurs à chaque redémarrage, sans effet pour une vitrine.

## Mesures

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
