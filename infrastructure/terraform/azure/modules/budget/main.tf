# Alerte de budget de l'environnement, décrite en code (le budget de l'abonnement, posé à la main avant toute
# ressource, reste le garde-fou général). Une alerte prévient, elle ne bloque rien : la vraie protection reste
# `detruire.sh` en fin de séance. Destinataires : les propriétaires de l'abonnement (aucune adresse dans le dépôt).

variable "nom" {
  type = string
}

variable "resource_group_id" {
  type = string
}

variable "montant" {
  description = "Montant mensuel, dans la devise de facturation de l'abonnement."
  type        = number
}

variable "seuils" {
  description = "Pourcentages du montant qui déclenchent une alerte (dépense réelle)."
  type        = list(number)
  default     = [50, 80, 100]
}

resource "azurerm_consumption_budget_resource_group" "env" {
  name              = "budget-${var.nom}"
  resource_group_id = var.resource_group_id
  amount            = var.montant
  time_grain        = "Monthly"

  time_period {
    # Azure exige le premier jour d'un mois ; celui du premier `apply`.
    start_date = formatdate("YYYY-MM-01'T'00:00:00Z", plantimestamp())
  }

  dynamic "notification" {
    for_each = var.seuils
    content {
      enabled        = true
      operator       = "GreaterThanOrEqualTo"
      threshold      = notification.value
      threshold_type = "Actual"
      contact_roles  = ["Owner"]
    }
  }

  # La date se recalculerait à chaque plan (plantimestamp) : on garde celle de la création.
  lifecycle {
    ignore_changes = [time_period]
  }
}
