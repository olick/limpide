# Premier pas sur Azure : groupe de ressources et compte de stockage

Troisième exercice de la S6, le premier qui crée quelque chose chez un fournisseur payant. Même cycle que l'exercice
Docker, avec ce qu'Azure ajoute. Coût : quelques centimes au plus (compte de stockage vide), à condition de finir par
`terraform destroy`.

Prérequis faits le 2026-10-13 : abonnement « Azure subscription 1 », budget « Limpide » de 10 € par mois (il prévient,
il ne bloque pas), Azure CLI 2.91 (dépôt `azure-cli` de Debian 12, clé `microsoft.gpg`), `az login`.

## Ce qu'Azure ajoute

| Notion | Ce que c'est |
|---|---|
| **Abonnement** | Le compte de facturation : toute ressource y appartient. Le fournisseur `azurerm` exige son identifiant (`ARM_SUBSCRIPTION_ID`) |
| **Groupe de ressources** | Un dossier : tout ce que Limpide crée sur Azure ira dans des groupes ; détruire le groupe détruit son contenu |
| **Région** | Où sont les données : `francecentral` (Paris) |
| **Fournisseur de ressources** | Chaque service Azure (stockage, bases, conteneurs) s'active par abonnement. Depuis la version 5, `azurerm` n'active que ce qu'on lui déclare (`resource_providers_to_register`) |
| **Étiquettes** (tags) | Posées sur chaque ressource : `gere_par = terraform` dit qu'on ne la modifie pas dans le portail |
| **Authentification** | La session `az login` du poste : aucun secret dans le code ni dans les variables |

Le fournisseur `random` tire un suffixe au premier `apply` (le nom d'un compte de stockage est unique dans tout Azure)
et le garde dans l'état : les `apply` suivants ne le changent pas.

## Exercices (≈ 1 h)

```bash
cd infrastructure/terraform/apprentissage/azure
export ARM_SUBSCRIPTION_ID=$(az account show --query id -o tsv)
terraform init
```

**1. Créer**

```bash
terraform plan      # 3 à créer : suffixe, groupe, compte de stockage
terraform apply     # ≈ 30 s ; puis voir le groupe « rg-limpide-apprentissage » dans le portail
terraform plan      # « No changes »
```

**2. Dérive en place (`~`)** : dans le portail, ajouter une étiquette au compte de stockage (`essai = portail`).

```bash
terraform plan      # « ~ update in-place » : Terraform veut retirer l'étiquette, sans recréer le compte
terraform apply     # le code fait foi : l'étiquette disparaît
```

**3. Remplacement en cascade (`-/+`)** : changer le nom du groupe, sans rien appliquer.

```bash
terraform plan -var resource_group_name=rg-limpide-essai
```

Le groupe ne peut pas être renommé : il est remplacé, et le compte de stockage qu'il contient avec lui. Sur une base
de données, ce même plan effacerait les données : c'est le `plan` à lire avant tout `apply` sur Azure.

**4. Lire l'état**

```bash
terraform state show azurerm_storage_account.apprentissage | grep access_key       # « (sensitive value) » à l'affichage...
terraform state pull | grep -c '"primary_access_key": "[^"]'                        # ... mais 1 : la clé est en clair dans l'état (sans l'afficher)
```

**5. Détruire** (à ne pas oublier : c'est la règle de l'option B)

```bash
terraform destroy
az group list --query "[?starts_with(name, 'rg-limpide')].name" -o tsv   # rien
```

**Terminé quand** : cycle complet, dérive en place corrigée, remplacement en cascade lu dans un plan, tout détruit.

À noter : la vérification du code (premier `plan`, 2026-10-13) a déjà activé `Microsoft.Storage` sur l'abonnement
(gratuit, sans ressource) : le fournisseur le fait dès sa configuration, avant toute création.
