# État distant : le coffre de l'état Terraform sur Azure

S6, session 2. Jusqu'ici, l'état de chaque dossier Terraform est un fichier sur le poste : perdu, Terraform ne sait
plus ce qui existe ; il contient des secrets en clair ; la CI de la S7 n'y a pas accès. Il est désormais rangé sur
Azure, dans un coffre dédié.

## Le coffre

`creer-socle.sh` (Azure CLI, relançable sans risque) crée :

| Élément | Réglage | Pourquoi |
|---|---|---|
| Groupe `rg-limpide-tfstate` | `francecentral`, étiquette `permanent=oui` | Séparé de tout ce que Terraform crée et détruit |
| Compte `stlimpidetf<8 caractères>` | nom dérivé de l'abonnement (empreinte, sans le révéler) | Unique dans Azure, stable |
| | HTTPS, TLS 1.2, aucun accès public, **clés d'accès désactivées** | Accès par identité Microsoft seulement : aucune clé à voler ni à faire tourner |
| | versions conservées, suppression récupérable 30 jours | Un état abîmé ou effacé se restaure |
| Conteneur `tfstate` | un fichier (blob) par dossier Terraform : `dns.tfstate`... | |
| Droit « Storage Blob Data Contributor » | sur ce conteneur seulement, pour la personne connectée | Être propriétaire de l'abonnement ne donne pas accès aux données d'un compte de stockage |

**Coffre à secrets permanent** (ajouté en session 4) : `kvlimpide<8 caractères>` (`kvlimpide024b1e9f`), même groupe,
droits Azure (RBAC), suppression réversible 90 jours et protection contre la purge ; « Key Vault Secrets Officer »
pour la personne connectée. Les secrets y sont déposés **une fois** et survivent aux environnements recréés à chaque
séance, qui les lisent par leur identité managée (sans lui, la clé Mistral serait à redéposer après chaque `apply`,
une intervention manuelle contraire au critère de la S6) :

```bash
read -rsp "Clé Mistral : " CLE && echo     # saisie sans affichage
az keyvault secret set --vault-name kvlimpide024b1e9f --name mistral-api-key --value "$CLE" --output none && unset CLE
# Clé OVHcloud AI Endpoints (bge-m3, ADR-012), déjà rangée dans les user-secrets du poste :
CLE="$(dotnet user-secrets list --project ../../../src/Limpide.Ingestion | sed -n 's/^Embedding:ApiKey = //p')"
az keyvault secret set --vault-name kvlimpide024b1e9f --name ovh-ai-endpoints-key --value "$CLE" --output none && unset CLE
```

Pourquoi un script et pas Terraform : Terraform ne peut pas ranger son état dans une ressource qu'il crée lui-même
(l'œuf et la poule). Le script est court, versionné et relançable.

**Exception à l'option B** : ce socle (état et secrets) ne se détruit jamais (seules ressources Azure permanentes du
projet) ; coût : quelques centimes par mois (fichiers de quelques Ko, quelques lectures de secrets).

## Mise en place (une fois)

```bash
cd infrastructure/terraform/etat
./creer-socle.sh
```

Le nom du compte affiché à la fin doit être celui de `../dns/backend.tf` (`stlimpidetf024b1e9f`).

## Migrer l'état de la zone DNS

`dns/backend.tf` déclare le backend `azurerm`. Terraform le remarque et demande une réinitialisation :

```bash
cd ../dns
terraform init -migrate-state      # « Do you want to copy existing state to the new backend? » → yes
terraform plan                     # « No changes » : l'état distant est le même
```

Si `init` répond 403 (« not authorized to perform this operation ») : le droit sur le conteneur n'est pas encore
appliqué ; attendre quelques minutes et relancer.

Ensuite, l'état local ne sert plus : vérifier qu'il est bien sur Azure, puis supprimer les copies locales (elles
contiennent l'ancien état, avec ses valeurs en clair).

```bash
az storage blob list --account-name stlimpidetf024b1e9f --container-name tfstate --auth-mode login --query "[].name" -o tsv   # dns.tfstate
rm terraform.tfstate terraform.tfstate.backup
terraform plan                     # toujours « No changes »
```

## Vérifier le verrou

Deux terminaux, dans `dns/` :

1. Terminal 1 : `terraform apply`, **sans répondre** à la question (Terraform tient le verrou).
2. Terminal 2 : `terraform plan` → `Error acquiring the state lock`, avec qui tient le verrou et depuis quand.
3. Terminal 1 : répondre `no`. Le verrou est libéré ; le `plan` du terminal 2 passe.

Un verrou resté bloqué (session coupée en plein `apply`) se libère par `terraform force-unlock <ID>`, l'identifiant
étant affiché dans le message d'erreur : seulement après avoir vérifié que personne d'autre ne travaille.

**Terminé quand** : l'état DNS est sur Azure, `plan` sans changement, le verrou a refusé une seconde opération.

Les exercices d'apprentissage (`apprentissage/`) gardent un état local : ils sont détruits à chaque fois.
