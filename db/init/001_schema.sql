-- Schéma initial du corpus.
-- Exécuté automatiquement au premier démarrage du conteneur PostgreSQL
-- (volume vide). Pour le rejouer : docker compose down -v && docker compose up -d

CREATE EXTENSION IF NOT EXISTS vector;

-- Une source = un producteur de documents (CNIL, EUR-Lex...).
CREATE TABLE sources (
    id              uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    name            text NOT NULL UNIQUE,
    base_url        text NOT NULL,
    reuse_terms     text NOT NULL,          -- conditions de réutilisation, vérifiées à la main
    created_at      timestamptz NOT NULL DEFAULT now()
);

-- Un document = une page ou un fichier identifié par son URL.
CREATE TABLE documents (
    id              uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    source_id       uuid NOT NULL REFERENCES sources(id),
    url             text NOT NULL UNIQUE,
    title           text,
    content_type    text NOT NULL CHECK (content_type IN ('html', 'pdf')),
    created_at      timestamptz NOT NULL DEFAULT now()
);

-- Une version = un contenu précis à un instant donné.
-- L'empreinte (SHA-256 du contenu brut) rend l'ingestion idempotente :
-- même empreinte => rien à refaire.
CREATE TABLE document_versions (
    id              uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    document_id     uuid NOT NULL REFERENCES documents(id),
    content_hash    char(64) NOT NULL,
    raw_path        text NOT NULL,          -- chemin du fichier brut dans data/raw
    fetched_at      timestamptz NOT NULL DEFAULT now(),
    is_current      boolean NOT NULL DEFAULT true,
    UNIQUE (document_id, content_hash)
);

CREATE UNIQUE INDEX one_current_version_per_document
    ON document_versions (document_id) WHERE is_current;

-- Un chunk = un passage indexé, rattaché à une version précise.
-- Chaque réponse pourra ainsi citer la version exacte de sa source.
CREATE TABLE chunks (
    id                  uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    document_version_id uuid NOT NULL REFERENCES document_versions(id) ON DELETE CASCADE,
    ordinal             int  NOT NULL,      -- position dans le document
    heading             text,               -- titre ou article d'origine (ex. « Article 6 »)
    content             text NOT NULL,
    char_count          int  NOT NULL,
    embedding           vector(1024),       -- dimension du modèle bge-m3
    embedding_model     text,
    created_at          timestamptz NOT NULL DEFAULT now(),
    UNIQUE (document_version_id, ordinal)
);

CREATE INDEX chunks_embedding_hnsw
    ON chunks USING hnsw (embedding vector_cosine_ops);

-- Recherche plein texte (servira à la recherche hybride en semaine 2).
ALTER TABLE chunks
    ADD COLUMN content_tsv tsvector
    GENERATED ALWAYS AS (to_tsvector('french', content)) STORED;

CREATE INDEX chunks_content_tsv ON chunks USING gin (content_tsv);
