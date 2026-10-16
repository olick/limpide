# Semaine 2 : RAG et interface « sous le capot »

**Objectif de fin de semaine** : une question posée dans le navigateur produit une réponse citée,
et le panneau « sous le capot » affiche passages, scores, stratégie, latence, tokens, coût et garde-fous.
Tout tourne encore en local.

**Budget temps** : environ 10 heures, en 5 sessions.

**Prérequis** : semaine 1 terminée, score de référence noté dans le README.

---

## Session 1 : architecture et choix du LLM (≈ 1 h 30)

1. **Séparer l'accès aux données** : créer `src/Limpide.Infrastructure` (classlib) et y déplacer le code Npgsql
   écrit en semaine 1. `Ingestion` et la future application web en dépendent ; `Core` reste sans dépendance d'infrastructure.
   ```
   Limpide.Core            ← domaine, interfaces (IPassageSearch, IAnswerGenerator…), règles
   Limpide.Infrastructure  ← Npgsql, pgvector, implémentations
   Limpide.Ingestion       ← console (fetch, extract, chunk, embed, search)
   Limpide.Web             ← Blazor, créé en session 4
   ```
2. **Choisir le LLM** et rédiger l'ADR-004. Critères : qualité en français, coût par requête,
   hébergement des données (fournisseur européen ou région UE), disponibilité d'une implémentation `IChatClient`.
   - ~~En développement : un petit modèle local via Ollama~~ — écarté (ADR-004) : trop lent sur CPU ;
     on développe avec Mistral Small 4, pour moins d'un centime par série de questions.
   - Pour la démo : une API hébergée. Le changement se fait par configuration.
3. Relever les prix par million de tokens (entrée / sortie) du modèle retenu : ils serviront au calcul du coût.

**Terminé quand** : la solution compile avec le nouveau découpage, l'ADR-004 est rédigé.

## Session 2 : génération avec citations (≈ 2 h 30)

1. **Construire le prompt** :
   - consigne système : répondre uniquement à partir des passages fournis, citer chaque affirmation
     avec l'identifiant du passage (`[P1]`, `[P2]`…), répondre « je ne sais pas » si les passages ne suffisent pas ;
   - les passages, chacun préfixé de son identifiant, de son titre de rattachement et de sa source.
2. **Appeler le modèle** via `IChatClient`, récupérer la réponse et l'usage (`ChatResponse.Usage` : tokens d'entrée et de sortie).
3. **Vérifier les citations de façon déterministe** : extraire les `[P#]` de la réponse, vérifier qu'ils existent
   dans les passages fournis. Une citation inventée est un garde-fou déclenché.
4. **Structurer le résultat** dans un objet unique, sur lequel l'interface s'appuiera :
   ```
   AnswerResult
     ├── Réponse générée (texte de l'assistant)
     ├── Passages cités (texte exact, source, URL, date de collecte, licence, score)
     ├── Stratégie de recherche utilisée
     ├── Durées : embedding de la question, recherche, génération, total
     ├── Tokens entrée / sortie, coût estimé
     └── Garde-fous déclenchés
   ```

**Terminé quand** : la commande console `ask "<question>"` affiche l'`AnswerResult` complet.

## Session 3 : garde-fous v1 et recherche hybride (≈ 2 h)

1. **Garde-fous** :
   - **seuil de pertinence** : si aucun passage ne dépasse un score minimal, ne pas appeler le LLM,
     répondre « je ne sais pas » (gain de coût, et zéro risque d'invention). Calibrer le seuil avec les 10 questions de la semaine 1 ;
   - **hors périmètre** : une question sans rapport avec la réglementation de l'IA est refusée ;
   - **pas de qualification juridique** : avertissement systématique dans la réponse.
2. **Recherche hybride** : combiner la recherche vectorielle et la recherche plein texte (`content_tsv`)
   par fusion des rangs (Reciprocal Rank Fusion), directement en SQL.
3. **Mesurer** avec les 10 questions : score vectoriel seul contre score hybride.
   Garder l'hybride seulement s'il améliore le résultat. Rédiger l'ADR-005 avec les chiffres.

**Terminé quand** : l'ADR-005 contient la comparaison chiffrée et la décision.

## Session 4 : interface Blazor (≈ 2 h 30)

1. Créer `src/Limpide.Web` (Blazor Web App, rendu interactif côté serveur).
2. Page principale :
   - zone de question ;
   - **réponse de l'assistant**, visuellement distincte des textes cités ;
   - **extraits cités reproduits à l'identique**, avec source, lien, date de collecte et licence (contrainte CC-BY-ND) ;
   - **panneau « sous le capot »** repliable : passages et scores, stratégie, durées par étape, tokens, coût, garde-fous.
3. **Mention de transparence** visible : l'utilisateur échange avec une IA, qui ne fournit pas de conseil juridique.

**Terminé quand** : la page fonctionne de bout en bout en local.

## Session 5 : tests et bilan (≈ 1 h 30)

1. Passer les 10 questions de test dans l'interface, noter les problèmes (réponses, affichage, lenteurs).
2. Ajouter 5 questions « pièges » : hors sujet, sans réponse dans le corpus, demande de qualification juridique.
   Vérifier que les garde-fous se déclenchent.
3. Mettre à jour le README (capture d'écran) et `CLAUDE.md` (avancement).

**Terminé quand** : les 15 questions sont passées et les résultats notés.

---

## Pièges à éviter

- **Laisser le LLM citer librement** : sans vérification déterministe des identifiants, une citation inventée passe inaperçue.
- **Afficher des extraits retouchés** : pas de résumé ni de troncature en milieu de phrase présentés comme le texte de la source.
- **Calibrer le seuil à l'intuition** : il se règle sur les questions de test, et sa valeur est notée dans un ADR ou la configuration.
- **Soigner l'esthétique trop tôt** : une interface sobre et lisible suffit. Le panneau « sous le capot » est ce qui compte.

## Livrables

- Projets `Limpide.Infrastructure` et `Limpide.Web`, commande `ask`
- ADR-004 (choix du LLM), ADR-005 (recherche hybride, avec mesures)
- 15 questions de test (10 + 5 pièges) avec résultats

## Ce qui prépare la suite

| Fait cette semaine | Réutilisé en |
|---|---|
| `AnswerResult` structuré | S7 : évaluation automatique, S8 : traces et coûts |
| Vérification des citations | S7 : métrique de fidélité |
| Seuil de pertinence | S8 : suivi de la dérive (taux de « je ne sais pas ») |
| 15 questions de test | S7 : jeu d'évaluation |

---

## Journal de réalisation

Repris tel quel de `CLAUDE.md` le 2026-10-15 (rangement : `CLAUDE.md` ne garde que l'état courant, les règles et les pièges ; le détail des sessions vit ici).

**S2 terminée** (`docs/plan/semaine-02.md`) — RAG et interface « sous le capot »

- [x] Session 1 : `Limpide.Infrastructure` (code Npgsql/Ollama déplacé, `IPassageSearch` dans Core,
  `evaluate` identique), ADR-004 (Mistral, UE)
- [x] Session 2 : `ask` affiche l'`AnswerResult` complet (réponse, passages cités à l'identique avec source et
  licence, stratégie, durées, tokens, coût, garde-fous). Observations : `docs/notes/observations-generation.md`.
- [x] Session 3 : recherche hybride mesurée et écartée, seuil de pertinence calibré, avertissement fixe (ADR-005)
- [x] Session 4 : interface Blazor, testée de bout en bout par Alexandre le 2026-10-05
  (profil VS Code « Web », ou `dotnet run --project src/Limpide.Web --launch-profile http`, http://localhost:5181)
- [x] Session 5 : 15 questions (10 + 5 pièges) passées par `evaluate-answers`, Small et Medium comparés et relus,
  garde-fou `qualification-juridique`, consignes `answer/3`, démo basculée sur Medium (ADR-004).
  Captures d'écran dans `docs/images/`, intégrées au README.

Choix de la S2, session 2 :

- Modèles épinglés par version datée : `mistral-small-2603` (développement), `mistral-medium-2604` (Medium 3.5,
  démo). Changer de modèle : `--Generation:Model=mistral-medium-2604` ; le coût suit (tarifs par modèle dans
  `Generation:Prices`, majoration UE comprise ; tarif absent = coût « inconnu », jamais deviné).
- Clé `Mistral:ApiKey` (user-secrets), exigée seulement par `ask` : l'ingestion fonctionne sans.
- Consignes versionnées (`AnswerPrompt.Version`, `answer/2`) et enregistrées avec chaque réponse :
  toute modification du prompt incrémente la version.
- Garde-fous déterministes : `citation-inventee`, `sans-citation` (sauf « je ne sais pas »), `reponse-tronquee`,
  `reponse-vide`. Les modèles citent `[P1a]` malgré la consigne : le contrôle l'accepte comme P1.
- Température 0 pour des mesures comparables ; 5 passages ; ≈ 1 800 tokens en entrée par question.

Choix de la S2, session 3 :

- Garde-fous v1 : seuil de pertinence (`hors-perimetre`, réponse fixe `AnswerPrompt.OutOfScope`, LLM non appelé) ;
  avertissement juridique fixe (`AnswerPrompt.Disclaimer`), jamais généré, sur toute réponse.
- Deux lignes de défense : seuil pour le hors sujet ; « je ne sais pas » du LLM pour les questions proches du sujet
  sans réponse dans le corpus (aucun seuil ne les sépare : « règles sur l'IA aux États-Unis » score 0,655).
- Le seuil (0,50) est propre à bge-m3+titre : le recalibrer à tout changement d'embedding.

Choix de la S2, session 4 :

- Réponse du modèle = texte non fiable : rendu Markdown (Markdig) avec HTML brut, liens, images et liens
  automatiques **désactivés** (un `[x](javascript:...)` passait sinon, constaté au test). Seuls liens : les
  citations `[P1]`/`[P1a]`, ajoutées après coup vers l'extrait cité (`AnswerMarkdown`).
- Réponse (encadré bleu, sans-serif) et textes officiels (fond papier, filet doré, serif) visuellement distincts.
- Panneau « sous le capot » ouvert d'office quand un garde-fou s'est déclenché.
- Question limitée à 500 caractères ; délai maximal de 60 s par réponse.
- Pas d'outil d'automatisation de navigateur sur le poste : interaction testée à la main.

Choix de la S2, session 5 :

- `eval/questions.json` : champ `behavior` (`answer` par défaut, `decline`, `noLegalQualification`) ; pièges t01–t05
  distincts des questions de calibrage du seuil. `evaluate` (recherche) ne compte que les questions `answer`.
- `evaluate-answers` : chaîne complète, contrôle automatique (`AnswerChecks` : OK / ÉCHEC / À RELIRE), rapport Markdown
  daté par modèle dans `eval/results/` (versionné : preuve et base de comparaison).
- Citations : renvois précis acceptés (`[P1a]`, `[P5.1.a]`, `[P4.c]`, `[P1, point a]`) ; refus = phrase seule,
  sinon garde-fou `reponse-ambigue`.
- Relecture complète faite par Claude (Alexandre l'a déléguée ; relecture par modèle, pas de juriste) : Small déforme
  2 textes sans que rien ne le détecte (q03, q10) ; Medium tranchait la situation (q03, t05), corrigé par `answer/3`.
- Garde-fou `qualification-juridique` (`LegalQualification`) : phrase en « vous/votre » + verdict, hors conditionnel et
  renvois ; heuristique qui signale sans bloquer ; affichée dans la réponse dans l'interface web.
- Rapports avec le texte des passages cités sous chaque réponse (relecture sans quitter le fichier), nommés
  `AAAA-MM-JJ-HHmm-<modèle>.md` (les réponses varient d'une passe à l'autre : ne pas écraser).
