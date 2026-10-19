# Supervision de Limpide (S8, ADR-018)

Pour n'importe quelle question posée à la démo : retrouver ce qui s'est passé, étape par étape, combien elle a coûté
et quels garde-fous se sont déclenchés ; et suivre la santé du système dans le temps.

## Les pièces, et à quoi chacune répond

```
Limpide.Web ──OTLP──▶ collecteur ──▶ Tempo       traces       « comment s'est déroulée cette question ? »   14 j
                         │       ──▶ Loki        journaux     « que s'est-il passé, quelle erreur ? »        14 j
                         │       ──▶ Prometheus  métriques    « comment évoluent latence, erreurs ? »        30 j
node-exporter ◀──────────────────── Prometheus   machine      mémoire disponible, swap, disque
PostgreSQL : answer_log           journal métier « issue, coût, garde-fous de chaque question »      12 mois
                                     ▲
Grafana : affiche et relie le tout (d'une trace à ses messages, d'un message à sa trace), envoie les alertes
```

- **OpenTelemetry** : le standard ouvert de mesure. L'application décrit ce qu'elle fait (activités, métriques,
  messages) sans savoir où ça part ; elle envoie tout, en OTLP, à **une seule adresse**.
- **Le collecteur** : reçoit, borne sa mémoire, **retire l'adresse du visiteur, l'agent utilisateur, le message et
  la pile d'appels des erreurs** (un message d'exception peut citer la question ; le type de l'erreur reste), regroupe
  et répartit. Changer de destination ne touche que `collecteur.yaml`, jamais le code.
- **Une trace** : l'arbre des étapes d'une question, avec leur durée. Exemple réel (poste, 2026-10-19) :

  ```
  Event onsubmit -> Home.AskAsync                         3 789 ms   (activité Blazor, .NET 10)
    Réponse à une question                                3 658 ms   issue, scores, tokens, coût, garde-fous
      Recherche des passages                                597 ms
        embeddings Qwen3-Embedding-8B                       371 ms   OVHcloud AI Endpoints
        postgresql (SELECT … embedding <=> @query)          213 ms
      chat mistral-medium-2604                            3 029 ms   2 052 tokens en entrée, 340 en sortie
    postgresql (INSERT INTO answer_log …)                     4 ms
  ```
- **Le journal `answer_log`** (base, pas la pile) : une ligne par question, avec son `trace_id`. Il est durable,
  sauvegardé avec la base, interrogeable en SQL ; la trace, elle, disparaît au bout de 14 jours.

- **Les journaux** racontent les opérations d'une visite (`Visit.cs`) : ouverture, pages, question reçue
  (longueur), recherche, rédaction, garde-fous, réponse, retour envoyé, fin. Chaque message porte un identifiant de
  **visite** tiré au hasard à l'ouverture de la page (ni IP, ni cookie, il meurt avec elle) et, pour une question,
  le `trace_id`. Dans Grafana : `{service_name="limpide-web"} | visite="7f3a1c2e"` pour suivre une visite.

**Jamais le texte des questions ni des réponses** : ni dans les traces (contenu des appels aux modèles désactivé dans
le code), ni dans les messages, ni dans la base. Seulement des mesures et des codes.

## Démarrer en local

```bash
cp supervision/.env.example supervision/.env      # puis choisir GRAFANA_ADMIN_PASSWORD (sans $)
docker compose -f supervision/docker-compose.yml up -d --wait
OTEL_EXPORTER_OTLP_ENDPOINT=http://localhost:4317 dotnet run --project src/Limpide.Web --launch-profile http
```

Grafana : http://localhost:3000 (`admin`). Explore → Tempo (Search) pour les traces, Loki
(`{service_name="limpide-web"}`) pour les messages, Prometheus pour les métriques. Sans
`OTEL_EXPORTER_OTLP_ENDPOINT`, l'application ne mesure ni n'exporte rien (et `answer_log.trace_id` reste vide).

Seul Grafana publie un port (127.0.0.1:3000) ; Tempo, Loki et Prometheus s'interrogent par Grafana.

## Mémoire (poste, 2026-10-19)

Tous ces outils sont écrits en Go, dont le ramasse-miettes ignore le plafond du conteneur : sans `GOMEMLIMIT`, Tempo
est monté à 250 Mo (plafond 256 Mo) après une seule question ; avec, 120 Mo sous charge.

| Conteneur | Au repos | Sous charge (6 questions, Grafana consulté) | Plafond |
|---|---|---|---|
| Grafana 13.2 | 167 Mo | 353 Mo (pic 512 Mo, cache disque compris) | 512 Mo |
| Tempo 3.1 | 36 Mo | 120 Mo | 384 Mo |
| Loki 3.7 | 45 Mo | 72 Mo | 256 Mo |
| Collecteur 0.162 | 40 Mo | 50 Mo | 128 Mo |
| Prometheus 3.15 | 45 Mo | 39 Mo | 256 Mo |
| node-exporter 1.12 | 13 Mo | 12 Mo | 32 Mo |
| **Total** | **≈ 350 Mo** | **≈ 650 Mo** | **≈ 1,55 Go** |

Mémoire « réelle » = `anon` de `memory.stat` (cgroup) : le cache disque, récupérable, est à part (même piège que le
graphique d'OVH, `deploy/README.md`). Aucun arrêt par manque de mémoire pendant la mesure.

## Pièges

- Le collecteur a renommé ses exporteurs : `otlp_grpc`, `otlp_http` (`otlp`, `otlphttp` : alias dépréciés).
- Tempo 3 n'a plus d'ingesters ni de compacteur ; en un seul processus, il tourne sans Kafka. Ne pas copier les
  blocs `ingest`, `block_builder` des exemples « microservices ».
- Les métriques de l'application partent toutes les 60 s : rien dans Prometheus pendant la première minute.
- Fichier de configuration modifié : `up -d --force-recreate <service>` (fichiers montés un par un ; `up -d` seul
  garde l'ancien contenu).
- Au redémarrage, Tempo écrit « failed to replay block. removing. » pour un bloc récent inachevé : sans perte
  constatée (10 traces sur 10 retrouvées, 2026-10-19).
- Le mot de passe de Grafana n'est lu qu'au premier démarrage (puis il vit dans le volume `grafana`).
