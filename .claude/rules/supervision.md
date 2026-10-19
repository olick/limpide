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
  mesures et des codes. `EnableSensitiveData = false` reste fixé dans le code ; une description de garde-fou (qui
  peut citer la réponse) ne va pas dans les traces. Le collecteur retire en plus `client.address`,
  `user_agent.original`.
- `answer_log` : une ligne par question (issue, `trace_id`, coût, garde-fous), 12 mois ; c'est l'enregistrement
  métier, la pile n'est que l'exploitation (14 à 30 jours).
- Mémoire : `GOMEMLIMIT` (≈ 80 % du plafond) sur chaque conteneur Go, sinon Tempo atteint son plafond ; mesurer
  `anon` dans `memory.stat`, pas `docker stats` seul (cache disque compris).
- Ports liés à 127.0.0.1 ; Grafana par tunnel SSH en production, comme Airflow.
- Collecteur : exporteurs `otlp_grpc` / `otlp_http`. Tempo 3 en un seul processus : pas de Kafka, pas de blocs
  `ingest` / `block_builder`.
