# ADR-018 : Supervision — OpenTelemetry, pile Grafana auto-hébergée sur le VPS, et journal des réponses en base

- **Statut** : accepté, en production depuis le 2026-10-19
- **Date** : 2026-10-19

## Contexte

Objectif de la S8 : **pour n'importe quelle requête de la démo, retrouver sa trace, son coût et ses garde-fous**, et
suivre la santé du système dans le temps (latence, erreurs, « je ne sais pas », mémoire du serveur, DAG).

Aujourd'hui, rien n'est conservé par requête : `AnswerResult` porte les latences par étape, les tokens, le coût et
les garde-fous, que le panneau « sous le capot » affiche, puis tout disparaît. Les messages de l'application ne vont
qu'à la sortie standard des conteneurs (`docker compose logs`, par SSH). Seuls les retours des visiteurs sont en base.
Le DAG n'alerte qu'en cas d'échec ou de quarantaine : une exécution réussie et une exécution absente se ressemblent
(constaté le 2026-10-19).

Le plan de la S8 prévoyait d'exporter vers la supervision d'Azure. Depuis l'ADR-006, la démo tourne sur le VPS
OVHcloud et Azure n'est qu'une vitrine détruite la plupart du temps : il n'y aurait presque jamais rien à superviser.

Contraintes :

- **Mémoire** : VPS-1 (2 vCores, 4 Go) partagé entre la démo (Caddy, web, admin, PostgreSQL : 1,4 Go de plafonds) et
  Airflow allégé (2 Go de plafonds, ADR-013) ; ≈ 1,9 Go disponibles au dernier relevé (S7).
- **Données personnelles** : les questions peuvent en contenir ; ni leur texte ni l'adresse IP ne doivent atteindre
  les traces, les journaux ou la base (décision complète en session 5).
- **Blazor Server** : une question ne passe pas par une requête HTTP, mais par le circuit SignalR ouvert au chargement
  de la page. L'instrumentation des requêtes HTTP ne donne donc pas une trace par question. .NET 10 ouvre une activité
  par événement Blazor (source `Microsoft.AspNetCore.Components`, « Event onsubmit -> Home.AskAsync ») : elle sert de
  racine, et les étapes du RAG s'y rattachent par le code (`ActivitySource`).
- Alexandre découvre ces outils : chaque composant doit être compris, pas seulement installé.

## Options envisagées

1. **A. Service géré, Grafana Cloud (offre gratuite)** — 0 Mo sur le VPS, traces, journaux, métriques et alertes
   inclus. Mais conservation courte, service tiers hors de l'hébergeur, et rien à montrer de l'exploitation d'une
   pile de supervision (BC04).
2. **B. Tout en base et dans le back office** — aucun service de plus : tableaux de bord en SQL dans le back office,
   alertes Discord écrites à la main. Pas de vue des traces en production, alertes et graphiques à coder soi-même.
3. **C. Pile auto-hébergée sur le VPS** : collecteur OpenTelemetry, Tempo (traces), Loki (journaux), Prometheus
   (métriques), Grafana (tableaux de bord, alertes). Standard, souveraine, complète ; ≈ 600 à 850 Mo estimés
   (non mesurés), sur une machine déjà juste.
4. **D. Azure Monitor / Application Insights** — natif pour .NET, mais l'environnement Azure n'existe que pendant
   les séances de vitrine.
5. **E. SigNoz, HyperDX** (tout-en-un sur ClickHouse) — une seule interface, mais ClickHouse demande plusieurs Go de
   mémoire : hors de portée du VPS.

**Aspire Dashboard** (≈ 150 Mo, sans stockage ni alertes) a été écarté comme repli : on garde Tempo et Grafana.

## Décision

**Option C**, complétée par un **journal des réponses en base** :

- L'application est instrumentée avec **OpenTelemetry** (traces, métriques, journaux) et envoie tout, en OTLP, à
  **une seule adresse : le collecteur**. Le collecteur répartit vers Tempo, Prometheus et Loki, regroupe les envois,
  borne sa mémoire et **supprime les attributs sensibles** (adresse du client, agent utilisateur) en filet de
  sécurité. Changer de destination (Grafana Cloud, Azure Monitor lors de la migration cloud) ne touche que sa
  configuration.
- Une trace par question, ouverte par le code : vectorisation de la question, recherche, seuil, génération,
  contrôles, avec en attributs le nombre de passages, les scores, les tokens, le coût et les garde-fous ; appels HTTP
  sortants, PostgreSQL et LLM (`UseOpenTelemetry()` de Microsoft.Extensions.AI) en étapes filles. **Jamais le texte
  de la question ni celui de la réponse** (option « données sensibles » désactivée).
- **Journal `answer_log`** (migration 0006) : une ligne par question, avec horodatage, `trace_id`, issue (réponse,
  « je ne sais pas », refus, erreur), modèle, version des consignes, scores, latences par étape, tokens, coût et
  garde-fous. Ni texte ni IP. C'est l'enregistrement métier : durable, interrogeable en SQL, couvert par la
  sauvegarde quotidienne. Il sert au coût par jour, à la dérive, à l'audit et au back office. Grafana le lit
  directement (source PostgreSQL, compte en lecture seule, migration 0007) ; le `trace_id` mène à la trace tant que
  Tempo la garde. Pour l'ingestion, Grafana lit de même la base d'Airflow (`dag_run`, `import_error`) : plus fiable
  que les métriques d'Airflow pour savoir si le DAG a tourné (session 2, `supervision/README.md`).

Répartition des rôles :

| Outil | Répond à | Conservation |
|---|---|---|
| Tempo (traces) | Comment s'est déroulée cette requête, étape par étape ? | 14 jours |
| Loki (journaux) | Que s'est-il passé : messages, erreurs, piles d'appels ? | 14 jours |
| Prometheus (métriques) | Comment évoluent latence, volume, erreurs, mémoire du serveur ? | 30 jours |
| `answer_log` (base) | Pour chaque question : issue, coût, garde-fous, modèle, consignes | 12 mois (comme les retours) |

Les journaux racontent les opérations d'une visite (ouverture, pages, question reçue, recherche, rédaction,
garde-fous, réponse, retour, fin), avec un identifiant de visite aléatoire, sans lien avec l'IP ni cookie, qui meurt
avec la page : on suit le parcours d'une visite sans savoir qui elle est.

Accès à Grafana : d'abord par tunnel SSH seulement, comme Airflow (ADR-013). **Révision du 2026-10-19** (demande
d'Alexandre : montrer les tableaux de bord en rendez-vous) : Grafana est servi par Caddy sur son propre
sous-domaine, https://grafana.limpide-ia.fr/, lien « Supervision » du back office. **Sous-domaine et non chemin du
back office** (`/grafana/`, essayé puis écarté à la relecture) : le navigateur isole les sites par domaine, pas par
chemin ; sous la même adresse, une faille de Grafana exécutant du JavaScript agirait dans le back office avec la
session de l'administrateur. Grafana gère lui-même la connexion Microsoft (même inscription d'application, même
personne autorisée que le back office, rôle Editor, toute autre refusée ; formulaire « admin » désactivé ; le mot de
passe reste pour l'API, par tunnel seulement : Caddy refuse l'authentification par mot de passe venue d'Internet ;
métriques internes coupées, cookie « Secure », HSTS). Écartés : le back office comme porte d'entrée (Grafana ferait
confiance à un en-tête transmis par Caddy : du code maison dans le chemin de sécurité) et le tunnel seul (rien à
montrer en rendez-vous). Prix : Grafana devient joignable depuis Internet ; Grype y trouve 19 failles élevées ou
critiques corrigeables (13.2.4), surtout dans des greffons inutilisés ou en HTTP/2 (Caddy lui parle en HTTP/1.1) ;
dans Grafana même, surtout des plantages. Rayon d'action si Grafana était compromis : plus large qu'un déni de
service, il connaît les mots de passe de lecture (`answer_log`, quelques colonnes d'Airflow), le webhook Discord et le
secret client Microsoft du back office, et rejoint les réseaux de la démo et d'Airflow. Suivi : analyse hebdomadaire
des images de la supervision (S8, session 5), montée de version à chaque correctif ; secours : vider
`GRAFANA_ADDRESS` ferme Grafana au public. Les alertes partent
vers Discord, comme celles du DAG.

## Mesures (poste, 2026-10-19)

Pile complète, 6 questions, Grafana consulté : **≈ 650 Mo réels** (Grafana 353, Tempo 120, Loki 72, collecteur 50,
Prometheus 39, node-exporter 12), 350 Mo au repos ; plafonds ≈ 1,55 Go. Grafana 13 et Tempo 3 pèsent deux à trois
fois l'estimation ; Tempo n'est contenu que par `GOMEMLIMIT` (250 Mo sans, 120 Mo avec). Sur le serveur (≈ 1,9 Go
disponibles), il en restait ≈ 1,2 Go selon cette mesure ; mesure sur place ci-dessous. Détail :
`supervision/README.md`.

## Mesures (serveur, 2026-10-19)

| | Avant la pile | Pile + 3 questions (10 h 28) | Au repos, après une ingestion (15 h 49) |
|---|---|---|---|
| Mémoire disponible | 1 977 Mo | 1 541 Mo | 1 354 Mo |
| Swap utilisé | 15 Mo | 30 Mo | 140 Mo |
| Pile (mémoire réelle) | — | 394 Mo | 505 Mo |

Sur le serveur, la pile pèse moins que sur le poste (Grafana 223 à 285 Mo, Tempo 52 à 65, Loki 50 à 62, collecteur
38 à 43, Prometheus 24 à 37, node-exporter 7 à 13). Aucun arrêt par manque de mémoire ; 1,35 Go disponibles, soit
près de trois fois le seuil de révision (500 Mo). Pour comparaison, Airflow au repos : ≈ 800 Mo. Les trois questions
ont chacune leur `trace_id` dans `answer_log` et leur trace dans Tempo.

## Conséquences

- Mémoire mesurée en local, puis sur le serveur (voir « Mesures ») : les plafonds de production en découlent ;
  `supervision/memoire.sh` refait la mesure.
- Un projet Compose séparé pour la supervision : elle se met à jour et s'arrête sans toucher la démo.
- Le code : une `ActivitySource` dans `Limpide.Core` (bibliothèque standard, aucun paquet ajouté ; les métriques
  viennent des instrumentations, les indicateurs métier du journal),
  l'export et l'instrumentation automatique dans `Limpide.Web` ; la configuration de l'exporteur par les variables
  `OTEL_*`. Sans collecteur joignable, l'application fonctionne normalement (les envois sont abandonnés).
- Les messages de l'application deviennent stockés et consultables : **aucun message ne doit contenir la question**
  (corrigé dans `Home.razor`, qui l'écrivait dans le message d'erreur). Le message d'une exception peut aussi la
  citer : le code ne trace que le type, et le collecteur vide le message et la pile d'appels des traces et des
  journaux (vérifié par un envoi forgé). Ils restent dans la sortie du conteneur, non conservée par la supervision.
- Mêmes mesures à trois endroits (panneau, traces, journal) : une seule source dans le code, `AnswerResult`.
- Les tableaux de bord prévus dans le back office (v3) se font dans Grafana ; le back office peut afficher le
  journal d'une question.
- **Conditions de révision** :
  - mémoire disponible du serveur sous ≈ 500 Mo au repos, ou démo ralentie : réduire les conservations et les
    plafonds, puis serveur plus grand ou supervision sur une machine à part (le collecteur rend ce déplacement simple) ;
  - migration cloud : le collecteur exporte vers le service de supervision de l'hébergeur retenu.
