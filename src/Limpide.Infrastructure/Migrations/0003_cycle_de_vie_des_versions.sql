-- Cycle de vie d'une version (S5, session 1). Jusqu'ici, fetch rendait une nouvelle version courante dès son
-- téléchargement : pendant extract → chunk → embed, le document n'avait plus de passages vectorisés et disparaissait
-- de la recherche (≈ 17 min pendant la panne d'Ollama du 2026-10-08). Désormais, une version collectée est une
-- CANDIDATE ; elle ne devient courante qu'à la publication, qui bascule en une transaction.
--
--   collected → extracted → chunked → (validated) → embedded → published → archived
--                   └→ discarded : texte identique à la version publiée (fausses nouvelles versions CNIL),
--                                  ou candidate remplacée par une collecte plus récente
--                                  (quarantined : contrôles qualité en échec, S5 session 3)
--
-- is_current garde son sens (la version en service, lue par la recherche) : la contrainte
-- document_versions_current_is_published garantit is_current = (status = 'published').

ALTER TABLE document_versions
    ADD COLUMN status          text,
    ADD COLUMN status_reason   text,           -- pourquoi une version a été écartée ou mise en quarantaine
    ADD COLUMN text_hash       char(64),       -- empreinte du texte extrait : deux versions au même texte sont identiques
    ADD COLUMN published_at    timestamptz,
    ADD COLUMN quality_report  jsonb;          -- résultat des contrôles qualité (S5, session 2)

-- Versions existantes : avant ce cycle de vie, chaque version devenait courante dès sa collecte.
UPDATE document_versions
SET status = CASE WHEN is_current THEN 'published' ELSE 'archived' END,
    published_at = fetched_at;

ALTER TABLE document_versions
    ALTER COLUMN status SET NOT NULL,
    ALTER COLUMN status SET DEFAULT 'collected',
    ALTER COLUMN is_current SET DEFAULT false,
    ADD CONSTRAINT document_versions_status_check CHECK (status IN (
        'collected', 'extracted', 'chunked', 'validated', 'embedded',
        'published', 'archived', 'discarded', 'quarantined')),
    ADD CONSTRAINT document_versions_current_is_published CHECK (is_current = (status = 'published'));

-- Une seule candidate en cours par document : une collecte plus récente écarte la précédente.
CREATE UNIQUE INDEX one_candidate_version_per_document
    ON document_versions (document_id)
    WHERE status IN ('collected', 'extracted', 'chunked', 'validated', 'embedded');
