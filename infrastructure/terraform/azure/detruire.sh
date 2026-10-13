#!/usr/bin/env bash
# Détruit l'environnement Azure en fin de séance (option B), en contournant un ordre de destruction impossible.
#
# Terraform supprimerait l'administrateur Entra ID de PostgreSQL avant le serveur. Pour Azure, c'est un DROP ROLE ;
# or ce rôle possède les tables (il a lancé les migrations) : PostgreSQL refuse (« cannot be dropped because some
# objects depend on it », vu le 2026-10-13). Inutile de toute façon : supprimer le serveur supprime tout ce qu'il
# contient. On retire donc l'administrateur de l'état (Terraform l'oublie, sans rien supprimer), puis on détruit.
#
# Usage, depuis infrastructure/terraform/azure : ./detruire.sh
set -euo pipefail
cd "$(dirname "$0")"
ADMIN="azurerm_postgresql_flexible_server_active_directory_administrator.admin"

# Variables exigées par la configuration, sans effet sur une destruction : utilisable depuis un nouveau terminal.
export ARM_SUBSCRIPTION_ID="${ARM_SUBSCRIPTION_ID:-$(az account show --query id -o tsv)}"
export TF_VAR_admin_principal_name="${TF_VAR_admin_principal_name:-$(az ad signed-in-user show --query userPrincipalName -o tsv)}"
export TF_VAR_poste_ipv4="${TF_VAR_poste_ipv4:-0.0.0.0}"
export TF_VAR_image_tag="${TF_VAR_image_tag:-sans-objet}"

terraform plan -destroy -input=false | grep -E "will be destroyed|^Plan:" || true
read -rp "Détruire l'environnement ci-dessus ? (oui/non) " reponse
[[ "$reponse" == "oui" ]] || { echo "Rien n'est détruit."; exit 1; }

# Après la confirmation seulement : un « non » laisse l'état intact.
if terraform state list | grep -qx "$ADMIN"; then
  terraform state rm "$ADMIN" >/dev/null
  echo "Administrateur Entra ID retiré de l'état (supprimé avec le serveur)."
fi

debut=$SECONDS
terraform destroy -auto-approve -input=false
echo "Durée du destroy : $(( (SECONDS - debut) / 60 )) min $(( (SECONDS - debut) % 60 )) s"
