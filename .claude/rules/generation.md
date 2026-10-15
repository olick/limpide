---
paths:
  - "src/Limpide.Core/Answering/**"
  - "src/Limpide.Core/Search/**"
  - "src/Limpide.Infrastructure/Search/**"
  - "src/Limpide.Infrastructure/GenerationOptions.cs"
  - "src/appsettings.shared.json"
---

# Recherche et génération (ADR-004, ADR-005, ADR-012)

- **Consignes** versionnées (`AnswerPrompt.Version`, enregistrée avec chaque réponse) : toute modification du prompt
  l'incrémente et garde la consigne qui empêche le modèle de trancher la situation de l'utilisateur.
- Modèles épinglés par version datée ; tarifs dans `Generation:Prices` (majoration UE comprise), tarif absent = coût
  « inconnu », jamais deviné. Température 0, 5 passages. Clé `Mistral:ApiKey` exigée seulement par les commandes qui
  appellent un modèle (`ask`, `evaluate-answers`, `judge-calibrate`) ; `Generation:Endpoint` / `Generation:ApiKey`
  (et `Judge:ApiKey` pour le juge) pour un autre fournisseur compatible OpenAI (essais).
- **Garde-fous** déterministes (`citation-inventee`, `sans-citation`, `reponse-tronquee`, `reponse-vide`,
  `qualification-juridique`, `consigne-divulguee`…) : ils signalent, ils ne réécrivent pas. Citations précises
  acceptées (`[P1a]`, `[P5.1.a]`). Refus reconnus par `Refusals`. Texte du modèle nettoyé par `ModelText.Clean`.
- Avertissement juridique fixe (`AnswerPrompt.Disclaimer`) et réponse hors périmètre fixe (`AnswerPrompt.OutOfScope`),
  jamais générés.
- Seuil `Generation:MinScore = 0,50` (pas d'appel au LLM en dessous), propre à bge-m3+titre. Deux lignes de défense :
  le seuil pour le hors sujet, le « je ne sais pas » du modèle pour les questions proches sans réponse.
- Recherche vectorielle seule ; l'hybride reste en option (`Search:Strategy=Hybrid`), mesurée moins bonne.
- Toute modification de la recherche : `evaluate` avant et après (voir `.claude/rules/evaluation.md`).
