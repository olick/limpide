# Environnement Azure de Limpide (vitrine, option B)

S6. La démo publique reste sur le VPS ; cet environnement montre la version « cloud » de Limpide, décrite en code,
**recréée à chaque séance** (`apply` au début, `destroy` à la fin). État : blob `azure-preprod.tfstate` du coffre
(`../etat`). Organisation par rôle, en attendant les modules (session 5) :

| Fichier | Contenu |
|---|---|
| `main.tf` | groupe de ressources, étiquettes, suffixe des noms uniques |
| `identite.tf` | identité managée de l'application (aucun secret : Azure l'authentifie) |
| `donnees.tf` | PostgreSQL 17 managé, pgvector autorisé, base `rag`, **connexion par Entra ID seulement** (aucun mot de passe), pare-feu limité au poste |
| `stockage.tf` | compte de stockage des documents bruts (conteneur `brut`), sans clés d'accès ; l'application y écrit |
| `secrets.tf` | Key Vault (droits Azure) : l'administrateur dépose les secrets, l'application les lit |

## Session 3 : données et secrets

Coût : PostgreSQL B1ms ≈ 2 centimes de l'heure, le reste presque rien. **`destroy` en fin de séance.**

```bash
cd infrastructure/terraform/azure
export ARM_SUBSCRIPTION_ID=$(az account show --query id -o tsv)
export TF_VAR_admin_principal_name="$(az ad signed-in-user show --query userPrincipalName -o tsv)"
export TF_VAR_poste_ipv4="$(curl -s https://api.ipify.org)"
terraform init
terraform plan        # 14 à créer ; aucun mot de passe nulle part
terraform apply       # ≈ 5 à 10 min (PostgreSQL) : noter la durée, elle compte pour la S7 (préproduction éphémère)
```

**Base** : rôle de l'application, migrations, droits, puis le corpus copié depuis la base locale.

```bash
./initialiser-base.sh --avec-corpus
```

**Secret** : la clé Mistral, déposée à la main (une valeur passée à Terraform finirait en clair dans l'état). Le droit
de dépôt peut mettre une à deux minutes à s'appliquer après l'`apply`.

```bash
read -rsp "Clé Mistral : " CLE && echo
az keyvault secret set --vault-name "$(terraform output -raw coffre)" --name mistral-api-key --value "$CLE" --output none && unset CLE
az keyvault secret list --vault-name "$(terraform output -raw coffre)" --query "[].name" -o tsv     # mistral-api-key
```

**Vérifications** (critère de la session : schéma présent, secrets visibles seulement dans le coffre) :

- `terraform state pull | grep -ci mistral` → 0 : la clé n'est pas dans l'état ;
- `terraform state pull | grep -ci password` → seulement des noms d'attributs vides (aucun mot de passe n'existe) ;
- le corpus : la dernière ligne de `initialiser-base.sh` donne le nombre de passages.

**Fin de séance** :

```bash
terraform destroy     # noter la durée ; le coffre est purgé, l'état reste dans ../etat
```

## Pourquoi sans mot de passe

PostgreSQL n'accepte que des jetons Entra ID : l'administrateur (la personne qui applique Terraform) se connecte avec
le jeton de sa session `az login` ; l'application, en session 4, avec celui de son identité managée. Il n'existe donc
aucun mot de passe de base de données : ni dans l'état, ni dans le coffre, ni dans la configuration. Le VPS et le
poste gardent leur mot de passe actuel (connexion inchangée).
