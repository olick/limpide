-- Retours des visiteurs (formulaire en bas de page, S3). Rejouable sans risque (IF NOT EXISTS) :
-- exécuté seul sur une base neuve ; à appliquer à la main sur une base existante (deploy/README.md),
-- en attendant l'outil de migrations (S5).
--
-- Données personnelles : aucune adresse IP ni identifiant. La question et la réponse ne sont jointes qu'avec
-- l'accord explicite du visiteur (case décochée par défaut). Conservation : 12 mois, purge à chaque nouvel envoi.

CREATE TABLE IF NOT EXISTS feedback (
    id              uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    created_at      timestamptz NOT NULL DEFAULT now(),
    understood      text,          -- « Qu'avez-vous compris de ce que fait Limpide ? »
    misleading      text,          -- « Une réponse vous a-t-elle paru fausse ou trompeuse ? »
    under_the_hood  text,          -- « Le panneau "sous le capot" vous a-t-il servi à quelque chose ? »
    question        text,          -- jointes seulement avec l'accord du visiteur :
    answer          text,          --   dernière question posée et réponse obtenue,
    model           text,          --   modèle et version des consignes qui l'ont produite,
    prompt_version  text,
    cited           text[],        --   passages cités (titres de rattachement)
    CHECK (num_nonnulls(understood, misleading, under_the_hood) > 0)
);

CREATE INDEX IF NOT EXISTS feedback_created_at ON feedback (created_at);
