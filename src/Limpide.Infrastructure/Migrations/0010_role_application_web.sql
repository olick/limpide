-- Droits de l'application web publique (S8, session 5, revue des accès) : jusqu'ici, elle se connectait avec le compte
-- créé par l'image PostgreSQL, superutilisateur (toute la base, et des commandes dans le conteneur par COPY … PROGRAM).
-- Un rôle sans connexion, limité à ce que fait la démo ; le compte qui s'en sert (limpide_web, avec son mot de passe,
-- un secret) est créé à part, sur chaque machine, par deploy/compte-web.sh : jamais de mot de passe dans une migration.
-- Une nouvelle table que la démo lit ou écrit reçoit ses droits dans sa propre migration.
-- Rôles PostgreSQL : communs à toute l'instance, d'où le test d'existence (base recréée, plusieurs bases ; le script
-- peut aussi l'avoir créé, s'il a tourné avant cette migration).

DO $$
BEGIN
    IF NOT EXISTS (SELECT FROM pg_roles WHERE rolname = 'application_web') THEN
        CREATE ROLE application_web NOLOGIN;
    END IF;
END
$$;

GRANT USAGE ON SCHEMA public TO application_web;

-- Corpus : recherche, lignée des passages cités, empreinte de /sante.
GRANT SELECT ON sources, documents, document_versions, chunks, chunk_embeddings TO application_web;

-- Journaux : écriture, et purge à chaque écriture (durées de conservation). La lecture se limite aux colonnes que
-- demandent RETURNING et les conditions de purge : la démo ne relit ni les questions conservées ni les retours.
GRANT INSERT, DELETE ON answer_log, question_log, feedback TO application_web;
GRANT SELECT (id, at) ON answer_log TO application_web;
GRANT SELECT (at) ON question_log TO application_web;
GRANT SELECT (created_at) ON feedback TO application_web;
