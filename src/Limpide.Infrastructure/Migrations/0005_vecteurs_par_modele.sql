-- Vecteurs par modèle (S7, session 6, ADR-017). Jusqu'ici, chaque passage n'avait qu'un vecteur (chunks.embedding) :
-- changer de modèle d'embedding obligeait à revectoriser en place, et les passages sortaient de la recherche le
-- temps du calcul (filtre sur le modèle). Désormais, les vecteurs de plusieurs modèles coexistent : embed remplit
-- celui de la configuration sans toucher les autres, la recherche lit celui de la configuration. Changer de modèle
-- devient un changement de configuration, une fois ses vecteurs calculés.
--
-- Pas d'index vectoriel : à ≈ 1 100 passages par modèle, la recherche exacte prend quelques millisecondes, et c'est
-- déjà elle que sert la démo (le planificateur n'utilisait pas chunks_embedding_hnsw, mesuré le 2026-10-18). Un index
-- HNSW commun aux modèles filtrerait sur le modèle après la recherche approchée, et pourrait rendre moins de passages
-- que demandé : à revoir à la mise à l'échelle (index partiel par modèle, ou parcours itératif de pgvector 0.8).

CREATE TABLE chunk_embeddings (
    chunk_id        uuid NOT NULL REFERENCES chunks(id) ON DELETE CASCADE,   -- redécouper retire les vecteurs
    embedding_model text NOT NULL,          -- ce qui a produit le vecteur : modèle et texte vectorisé (bge-m3+titre)
    embedding       vector(1024) NOT NULL,  -- même dimension pour tous les modèles (Qwen3-Embedding réduit à 1 024)
    PRIMARY KEY (chunk_id, embedding_model)
);

INSERT INTO chunk_embeddings (chunk_id, embedding_model, embedding)
SELECT id, embedding_model, embedding
FROM chunks
WHERE embedding IS NOT NULL AND embedding_model IS NOT NULL;

-- chunks.embedding et chunks.embedding_model ne sont plus ni lus ni écrits. Gardés pour pouvoir redéployer l'image
-- précédente (valable tant qu'aucun passage n'a été vectorisé depuis) ; retirés par une migration après la bascule.
COMMENT ON COLUMN chunks.embedding IS 'obsolète depuis 0005 : voir chunk_embeddings';
COMMENT ON COLUMN chunks.embedding_model IS 'obsolète depuis 0005 : voir chunk_embeddings';
