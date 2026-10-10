# Pipeline d'ingestion

Airflow planifie et surveille ; le travail est fait par la console .NET, une commande par conteneur (ADR-007).
Mise en place : `pipelines/airflow/README.md`.

```mermaid
flowchart LR
    subgraph Airflow["Airflow (DAG ingestion, lundi 4 h UTC)"]
        direction LR
        F[fetch] --> E[extract] --> C[chunk] --> M[embed] --> P[publish] --> B{bilan}
    end

    subgraph Conteneurs["Image limpide-ingestion (une par tâche)"]
        direction TB
        CF["fetch : télécharge, empreinte SHA-256,<br/>nouvelle candidate si l'empreinte change"]
        CE["extract : texte structuré<br/>(extracteur versionné)"]
        CC["chunk : passages<br/>(découpeur versionné)"]
        CM["embed : vecteurs bge-m3<br/>par lots, reprise possible"]
        CP["publish : bascule atomique<br/>candidate → version publiée"]
    end

    Sources[(EUR-Lex CELLAR<br/>CNIL)] --> CF
    CF --> Raw[(data/raw)]
    Raw --> CE --> Ext[(data/extracted)]
    Ext --> CC --> DB[(PostgreSQL<br/>pgvector)]
    DB --> CM --> DB
    CP --> DB
    Ollama[Ollama bge-m3] --- CM
    CF --> DB

    F -.lance.-> CF
    E -.lance.-> CE
    C -.lance.-> CC
    M -.lance.-> CM
    P -.lance.-> CP
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

## Cycle de vie d'une version (ADR-014)

```mermaid
stateDiagram-v2
    [*] --> collected : fetch (nouveau contenu)
    collected --> extracted : extract
    extracted --> discarded : texte identique à la version publiée
    extracted --> chunked : chunk
    chunked --> embedded : embed (S5 session 2 : chunked → validated → embedded)
    embedded --> published : publish (transaction)
    published --> archived : publication d'une version plus récente
    collected --> discarded : collecte plus récente
```

**La recherche ne lit que la version publiée** : une candidate peut échouer à n'importe quelle étape, la version
publiée reste en service. La bascule (`publish`) est atomique : le document n'est jamais absent de la recherche.
Avant (S4) : la version devenait courante dès `fetch`, et le document disparaissait de la recherche jusqu'à la fin
d'`embed` (≈ 17 min pendant la panne d'Ollama du 2026-10-08).

**Fausses nouvelles versions** : une candidate dont le texte extrait est identique à celui de la version publiée
(pages CNIL dont seul le HTML change) est écartée dès `extract`, sans découpage ni vectorisation.
