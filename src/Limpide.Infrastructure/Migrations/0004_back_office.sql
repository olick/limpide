-- Back office v1 (S5) : retours traités, et journal des décisions humaines.

-- Un retour lu et pris en compte est marqué « traité » (date et auteur), sans être supprimé : la conservation reste
-- de 12 mois (purge à chaque nouvel envoi, FeedbackStore).
ALTER TABLE feedback
    ADD COLUMN handled_at timestamptz,
    ADD COLUMN handled_by text;

-- Journal des décisions : qui a fait quoi, quand, et pourquoi. Écrit dans la même transaction que la décision
-- elle-même : une décision sans trace est impossible. Jamais modifié ni purgé par l'application.
CREATE TABLE admin_actions (
    id          bigint GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    at          timestamptz NOT NULL DEFAULT now(),
    actor       text NOT NULL,       -- identifiant de la personne (back office) ou « console »
    actor_name  text,                -- nom affiché, pour la lecture
    action      text NOT NULL,       -- release, retry, reject, feedback-handled...
    target      text NOT NULL,       -- empreinte de version, identifiant de retour...
    detail      text                 -- motif, document concerné
);

CREATE INDEX admin_actions_at ON admin_actions (at DESC);
