-- Journal des réponses (S8, ADR-018) : une ligne par question posée à la démo, pour retrouver le coût, les
-- garde-fous et la trace de n'importe quelle requête, et suivre la dérive dans le temps.
-- Données personnelles : ni le texte de la question, ni celui de la réponse, ni l'adresse IP, ni identifiant de
-- visiteur. Conservation : 12 mois, purge à chaque nouvelle ligne (AnswerLogStore), comme les retours.

CREATE TABLE answer_log (
    id                bigint GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    at                timestamptz NOT NULL DEFAULT now(),
    outcome           text NOT NULL,     -- reponse, je-ne-sais-pas, hors-perimetre, quota-*, delai-depasse, erreur
    trace_id          text,              -- trace OpenTelemetry (32 caractères hexadécimaux), si la supervision est active
    question_length   int NOT NULL,      -- en caractères : seule mesure gardée de la question
    search_strategy   text,
    embedding_model   text,              -- vecteurs lus par la recherche (EmbeddingOptions.Label) : les scores en dépendent
    model             text,              -- null si le modèle n'a pas été appelé
    prompt_version    text,
    passages          int NOT NULL,      -- passages fournis au modèle (ou trouvés, sous le seuil)
    cited             int NOT NULL,      -- passages cités par la réponse
    best_score        double precision,  -- similarité du meilleur passage
    embedding_ms      int,
    search_ms         int,
    generation_ms     int,
    total_ms          int,
    input_tokens      bigint,
    output_tokens     bigint,
    cost_usd          numeric(12, 6),    -- d'après le tarif configuré ; null si inconnu
    guardrails        text[] NOT NULL,   -- codes des garde-fous déclenchés (citation-inventee...)
    error             text               -- type de l'exception, jamais son message
);

CREATE INDEX answer_log_at ON answer_log (at);
