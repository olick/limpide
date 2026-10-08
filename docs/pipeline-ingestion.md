# Pipeline d'ingestion

Airflow planifie et surveille ; le travail est fait par la console .NET, une commande par conteneur (ADR-007).
Mise en place : `pipelines/airflow/README.md`.

```mermaid
flowchart LR
    subgraph Airflow["Airflow (DAG ingestion, lundi 4 h UTC)"]
        direction LR
        F[fetch] --> E[extract] --> C[chunk] --> M[embed] --> B{bilan}
    end

    subgraph Conteneurs["Image limpide-ingestion (une par tâche)"]
        direction TB
        CF["fetch : télécharge, empreinte SHA-256,<br/>nouvelle version si l'empreinte change"]
        CE["extract : texte structuré<br/>(extracteur versionné)"]
        CC["chunk : passages<br/>(découpeur versionné)"]
        CM["embed : vecteurs bge-m3<br/>par lots, reprise possible"]
    end

    Sources[(EUR-Lex CELLAR<br/>CNIL)] --> CF
    CF --> Raw[(data/raw)]
    Raw --> CE --> Ext[(data/extracted)]
    Ext --> CC --> DB[(PostgreSQL<br/>pgvector)]
    DB --> CM --> DB
    Ollama[Ollama bge-m3] --- CM
    CF --> DB

    F -.lance.-> CF
    E -.lance.-> CE
    C -.lance.-> CC
    M -.lance.-> CM
    B -- "une tâche en échec" --> Discord[Alerte Discord]
```

| Élément | Rôle |
|---|---|
| Flèches pleines | données : fichiers (`data/`) et base ; jamais par Airflow |
| Flèches pointillées | Airflow lance le conteneur et lit son code de sortie et son résumé JSON (XCom) |
| `bilan` | tourne toujours ; si une tâche a échoué : une alerte, et l'exécution est marquée en échec |

**Relances** : 2 par tâche, à 5 min d'écart. Sans risque parce que chaque commande est idempotente : même
empreinte, même version d'extracteur ou de découpeur, passages déjà vectorisés = rien à refaire.

**Échec d'une étape** : les suivantes tournent quand même (`all_done`) et ne traitent que les versions cohérentes ;
un document en échec plus haut est signalé, les autres avancent.

**Limite connue (S5)** : une nouvelle version devient courante dès `fetch` ; jusqu'à la fin d'`embed`, le document
est absent de la recherche.
