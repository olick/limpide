-- Coût du journal des réponses en euros (S8, session 3) : les factures (Mistral, OVHcloud) sont en euros, le coût
-- estimé doit pouvoir s'y confronter sans taux de change.
ALTER TABLE answer_log RENAME COLUMN cost_usd TO cost_eur;

-- Lignes déjà écrites : recalculées à partir des tokens, aux tarifs en euros par million (relevés le 2026-10-20,
-- majoration UE de 10 % comprise, comme Generation:Prices). Un modèle sans tarif en euros : coût inconnu, plutôt
-- qu'un montant en dollars sous une colonne en euros.
UPDATE answer_log
SET cost_eur = CASE model
        WHEN 'mistral-medium-2604' THEN round((input_tokens * 1.4025 + output_tokens * 7.0125) / 1000000, 6)
        WHEN 'mistral-small-2603' THEN round((input_tokens * 0.14025 + output_tokens * 0.561) / 1000000, 6)
    END
WHERE model IS NOT NULL;
