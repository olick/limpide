---
paths:
  - "supervision/**"
  - "src/Limpide.Core/Answering/AnswerTelemetry.cs"
  - "src/Limpide.Core/Answering/AnswerLogEntry.cs"
---

# Supervision (S8, ADR-018)

Alexandre découvre OpenTelemetry, Grafana, Tempo, Loki et Prometheus avec ce projet : expliquer les concepts.
Détail et mesures : `supervision/README.md`.

- L'application envoie tout en OTLP au **collecteur**, seule adresse connue d'elle (`OTEL_EXPORTER_OTLP_ENDPOINT`,
  rien n'est exporté sans) ; le collecteur répartit vers Tempo, Loki, Prometheus.
- **Jamais le texte des questions ni des réponses** dans les traces, les messages ou `answer_log` : seulement des
  mesures et des codes. Le texte de la question n'existe qu'en base, dans `question_log` (ADR-019, 30 jours,
  masqué, back office seulement, aucun droit pour Grafana). `EnableSensitiveData = false` reste fixé dans le code ;
  une description de garde-fou (qui peut citer la réponse) ne va pas dans les traces. Le collecteur retire en plus `client.address`,
  `user_agent.original`.
- `answer_log` : une ligne par question (issue, `trace_id`, coût, garde-fous), 12 mois ; c'est l'enregistrement
  métier, la pile n'est que l'exploitation (14 à 30 jours).
- Mémoire : `GOMEMLIMIT` (≈ 80 % du plafond) sur chaque conteneur Go, sinon Tempo atteint son plafond ; mesurer
  `anon` dans `memory.stat`, pas `docker stats` seul (cache disque compris).
- Ports liés à 127.0.0.1. Grafana en production : https://grafana.limpide-ia.fr/ (sous-domaine à part du back office :
  isolation par le navigateur ; Caddy, connexion Microsoft gérée par Grafana, inscription d'application propre
  « Limpide Grafana » (`connexion-microsoft.sh`, secret par l'entrée standard ; sujet propre à l'inscription, pas
  celui du back office), une seule personne, rôle Editor) ; mot de passe `admin` par tunnel SSH seulement (Caddy le
  refuse). Exposé sur Internet : suivre ses failles (Grype) et monter sa version.
- Collecteur : exporteurs `otlp_grpc` / `otlp_http`. Tempo 3 en un seul processus : pas de Kafka, pas de blocs
  `ingest` / `block_builder`.
- Tableaux de bord (`grafana/dashboards/*.json`) et alertes (`grafana/provisioning/alerting/alertes.yaml`) versionnés,
  chargés au démarrage : ne pas les modifier dans l'interface. Peu d'indicateurs, chacun relié à une décision ; chaque
  alerte dit « que faire » et porte `environnement` (poste, serveur ; même salon Discord).
- Grafana lit deux bases en lecture seule (`grafana_lecture`, `compte-lecture.sh`) : `answer_log` (rôle
  `lecture_supervision`, migration 0007) et la base d'Airflow (`dag_run`, `import_error`). Hôtes désignés par le
  nom du conteneur : Grafana est sur deux réseaux qui ont chacun un service `postgres`.
- « Le DAG n'a pas tourné » se lit dans `dag_run`, pas dans les métriques d'Airflow (compteurs remis à zéro au
  redémarrage, figés quand le DAG est en pause).
