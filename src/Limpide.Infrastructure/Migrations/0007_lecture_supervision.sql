-- Lecture du journal des réponses par Grafana (S8, session 2, ADR-018) : coût par jour, dérive, latences.
-- Un rôle sans connexion, qui ne peut que lire answer_log. Le compte qui s'en sert (grafana_lecture, avec son mot de
-- passe, un secret) est créé à part, sur chaque machine, par supervision/compte-lecture.sh : jamais de mot de passe
-- dans une migration.
-- Rôles PostgreSQL : communs à toute l'instance, d'où le test d'existence (base recréée, plusieurs bases).

DO $$
BEGIN
    IF NOT EXISTS (SELECT FROM pg_roles WHERE rolname = 'lecture_supervision') THEN
        CREATE ROLE lecture_supervision NOLOGIN;
    END IF;
END
$$;

GRANT USAGE ON SCHEMA public TO lecture_supervision;
GRANT SELECT ON answer_log TO lecture_supervision;
