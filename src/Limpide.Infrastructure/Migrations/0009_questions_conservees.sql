-- Texte des questions posées à la démo (S8, session 5, ADR-019) : pour comprendre ce que demandent les visiteurs et en
-- tirer des questions de test, relues par une personne.
-- Données personnelles : la question seule (ni la réponse, ni l'adresse IP, ni identifiant de visiteur), après
-- masquage des adresses électroniques, IBAN, numéros de sécurité sociale et de téléphone (PersonalDataMask).
-- Rien n'est écrit si le visiteur s'y oppose (case « Ne pas conserver ma question »), ni pour une question refusée par
-- le quota. Conservation : 30 jours, purge à chaque nouvelle question (AnswerLogStore) ; la ligne d'answer_log, sans
-- texte, reste 12 mois.
-- Accès : le back office seulement. Aucun droit pour lecture_supervision (migration 0007) : Grafana ne la lit pas.

CREATE TABLE question_log (
    answer_log_id  bigint PRIMARY KEY REFERENCES answer_log (id) ON DELETE CASCADE,
    at             timestamptz NOT NULL DEFAULT now(),
    question       text NOT NULL,     -- après masquage
    masked         text[] NOT NULL    -- types masqués (telephone, adresse-electronique...) : mesure du filtre
);

CREATE INDEX question_log_at ON question_log (at);
