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
   - En développement : un petit modèle local via Ollama (OllamaSharp implémente aussi `IChatClient`), coût nul.
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
