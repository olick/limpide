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
cp supervision/.env.example supervision/.env      # GRAFANA_ADMIN_PASSWORD (sans $), DISCORD_WEBHOOK_URL
supervision/compte-lecture.sh                     # comptes de lecture (base de la démo, base d'Airflow) : une fois
docker compose -f supervision/docker-compose.yml up -d --wait
OTEL_EXPORTER_OTLP_ENDPOINT=http://localhost:4317 dotnet run --project src/Limpide.Web --launch-profile http
```

Prérequis : la démo (`docker compose up -d`) et Airflow (`pipelines/airflow`) ont démarré au moins une fois, Grafana
rejoint leurs réseaux ; migration 0007 appliquée (`migrate`).

Grafana : http://localhost:3000 (`admin`). Explore → Tempo (Search) pour les traces, Loki
(`{service_name="limpide-web"}`) pour les messages, Prometheus pour les métriques. Sans
`OTEL_EXPORTER_OTLP_ENDPOINT`, l'application ne mesure ni n'exporte rien (et `answer_log.trace_id` reste vide).

Seul Grafana publie un port (127.0.0.1:3000) ; Tempo, Loki et Prometheus s'interrogent par Grafana.

## Accès (production)

https://grafana.limpide-ia.fr/ (sous-domaine à part : une faille de Grafana ne peut pas agir dans le back office),
lien « Supervision » du back office. Grafana gère lui-même la connexion Microsoft, avec sa propre inscription
d'application (« Limpide Grafana », `connexion-microsoft.sh`) : Grafana compromis n'expose pas le secret du back
office. Une seule personne entre, avec le rôle Editor ; toute autre est refusée. Le mot de passe `admin` reste
valable pour l'API et l'administration, **par tunnel SSH seulement** : Caddy refuse l'authentification par mot de
passe venue d'Internet. Procédure : `deploy/README.md`,
« Supervision ».

## Tableaux de bord et alertes (session 2)

Versionnés dans le dépôt, chargés au démarrage de Grafana (dossier « Limpide ») : une modification faite dans
l'interface n'est pas gardée (l'exporter en JSON et remplacer le fichier, `grafana/dashboards/`).

- **Limpide — technique** : questions par issue, latence par étape (médiane, 95e centile), erreurs, coût par jour,
  réponses HTTP 5xx, mémoire disponible et swap, disque, et les **dernières questions** (sans texte) : un clic sur
  `trace` ouvre la trace.
- **Limpide — qualité et dérive** : part de « je ne sais pas » et de hors périmètre, meilleur score de la recherche
  face au seuil (0,60), garde-fous par jour et par code, passages cités par réponse.

Sources de Grafana : Prometheus, Tempo, Loki, et deux bases PostgreSQL **en lecture seule** (compte
`grafana_lecture`, `compte-lecture.sh`) : `answer_log` dans la base de la démo (rôle `lecture_supervision`, migration
0007), `dag_run` et `import_error` dans celle d'Airflow.

Alertes (`grafana/provisioning/alerting/alertes.yaml`), sur Discord, avec l'environnement (poste ou serveur) et
« que faire » dans le message :

| Alerte | Seuil | Source |
|---|---|---|
| Mémoire disponible basse | < 300 Mo pendant 5 min | Prometheus (node-exporter) |
| Swap très utilisé | > 1 Go pendant 15 min | Prometheus |
| Disque presque plein | > 85 % pendant 15 min | Prometheus |
| La démo n'envoie plus de mesures | aucune métrique de `limpide-web` depuis 10 min | Prometheus |
| Questions en erreur | ≥ 3 erreurs ou délais dépassés en 15 min | `answer_log` |
| Réponses anormalement lentes | médiane > 15 s sur 30 min (au moins 3 questions) | `answer_log` |
| Dépense de la démo anormale sur 24 h | > 0,50 € (le quota en permet ≈ 0,26 €) | `answer_log` |
| Budget Mistral de la démo à moitié consommé | > 5 € depuis le début du mois (UTC) | `answer_log` |
| Quota du jour de la démo atteint | au moins une question refusée par le quota global depuis minuit UTC (ADR-020) | `answer_log` |
| Le DAG n'a pas tourné | aucune exécution réussie depuis 8 jours | base d'Airflow |
| Un DAG ne s'importe plus | au moins une erreur d'import pendant 5 min | base d'Airflow |
| Dépense de la démo anormale sur 24 h (session 3) | > 0,50 € sur 24 h glissantes : le quota (60 questions par jour, ≈ 0,26 €) ne tient plus | `answer_log` |
| Budget Mistral de la démo à moitié consommé (session 3) | > 5 € depuis le début du mois civil (UTC, à confirmer sur la facture de Mistral), moitié du plafond de 10 € de l'espace | `answer_log` |

L'échec et la quarantaine d'une ingestion restent signalés par le DAG lui-même (tâche `bilan`). Les alertes de budget
ne voient que la démo : l'évaluation (espace `limpide-evaluation`) et le poste ne sont bornés que par leur plafond chez
Mistral (aucune API publique de lecture de la dépense trouvée le 2026-10-20).

**Pourquoi la base d'Airflow et pas ses métriques** (essayé le 2026-10-19) : Airflow sait envoyer ses métriques en
OpenTelemetry, mais un compteur n'existe qu'à partir de sa première mesure et repart de zéro au redémarrage de
l'ordonnanceur. Après un redémarrage sans exécution, l'alerte se déclencherait à tort ; DAG en pause sans
redémarrage, la dernière valeur continuerait d'être envoyée et l'alerte ne partirait jamais. La table `dag_run`
donne directement la date de la dernière réussite.

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
- Connexion Microsoft : « Error retrieving access token payload … not in JWT format » est attendu (comptes
  personnels : jeton d'accès opaque) ; Grafana lit le jeton d'identité.
