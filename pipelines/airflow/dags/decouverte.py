"""DAG d'apprentissage (S4, session 1) : lancer un DAG, lire ses logs, observer un échec, ses relances, puis relancer.

À supprimer une fois le DAG d'ingestion en place.

1. Déclencher le DAG depuis l'interface : la tâche « echec_volontaire » échoue, est relancée 2 fois (10 s d'écart),
   puis passe en échec ; les tâches précédentes restent en succès.
2. Lire les logs de chaque tentative.
3. Relancer seulement la tâche en échec (« Clear ») après avoir « réparé » : déclencher à nouveau avec le paramètre
   reussir = true, ou modifier ce fichier.
"""

import datetime

import pendulum
from airflow.providers.standard.operators.bash import BashOperator
from airflow.sdk import DAG, Param

with DAG(
    dag_id="decouverte",
    description="Prise en main : deux tâches qui affichent un message, une qui échoue volontairement",
    schedule=None,  # pas de planification : déclenchement à la main uniquement
    start_date=pendulum.datetime(2026, 10, 1, tz="UTC"),
    catchup=False,
    params={"reussir": Param(False, type="boolean", description="Faire réussir la tâche echec_volontaire")},
    default_args={"retries": 2, "retry_delay": datetime.timedelta(seconds=10)},
    tags=["apprentissage"],
) as dag:
    bonjour = BashOperator(
        task_id="dire_bonjour",
        bash_command='echo "Bonjour depuis Airflow : exécution {{ run_id }}"',
    )

    date = BashOperator(
        task_id="afficher_la_date",
        bash_command='echo "Date logique de l\'exécution : {{ logical_date }}"; date',
    )

    echec = BashOperator(
        task_id="echec_volontaire",
        bash_command=(
            'if [ "{{ params.reussir }}" = "True" ]; then echo "Réparé : la tâche réussit."; '
            'else echo "Échec volontaire (tentative {{ ti.try_number }})." >&2; exit 1; fi'
        ),
    )

    # L'ordre des tâches : c'est tout ce que décrit un DAG.
    bonjour >> date >> echec
