---
paths:
  - "eval/**"
  - "src/Limpide.Core/Evaluation/**"
  - "src/Limpide.Ingestion/Search/**"
---

# Évaluation (S2, S7)

- `evaluate` : recherche seule (rang du premier bon passage sur 10 ; top 1, top 5, MRR), questions `answer`
  uniquement ; passage attendu par `anchor` (AI Act) ou morceau de `heading` (CNIL).
- `evaluate-answers` : chaîne complète par le même `AnswerService` que l'application, contrôle automatique
  (OK / ÉCHEC / À RELIRE), rapports `eval/results/AAAA-MM-JJ-HHmm-<modèle>.md` + `.json` (versions de tout ce qui
  influe : consignes, modèle, vectorisation et consigne de question, seuil, empreintes du jeu et du corpus).
  Rapports versionnés, **jamais écrasés** (les réponses varient d'une passe à l'autre).
- Jeu : `eval/questions.json` (version `jeu/2` par question ; `behavior` : `answer`, `decline`,
  `noLegalQualification`, `resist` pour les injections). Questions formulées sans reprendre les mots du titre.
- Juge de fidélité (`FaithfulnessJudge`, `judge/2`, Mistral Medium 3.5) : calibré par `judge-calibrate` sur
  `eval/calibration/` ; sert au tri et à une métrique agrégée, **pas** à bloquer une réponse
  (`docs/notes/observations-evaluation.md`).
- Barrière `gate` (ADR-014) : seuils de la section `EvaluationGate` (appsettings de la console) sur le rapport JSON,
  code 1 si bloqué. Seuils relevés quand une meilleure configuration est adoptée (effet cliquet), élargis seulement
  dans un commit qui dit pourquoi, jamais désactivés.
- Relectures faites par un modèle, pas par un juriste : le dire partout où on cite un résultat.
- L'évaluation tourne sur le poste ou la préproduction, jamais sur la démo publique. Bases locales d'essai :
  `rag_mistral`, `rag_qwen` (comparaison bge-m3 / Qwen3-Embedding).
