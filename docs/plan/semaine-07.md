# Semaine 7 : évaluation et CI/CD

**Objectif de fin de semaine** : chaque modification passe par une évaluation automatique.
Une modification du prompt qui dégrade les scores est **bloquée avant la production**.

**Budget temps** : environ 10 heures, en 5 sessions.

**Prérequis** : infrastructure Terraform avec préproduction et production (semaine 6).

> C'est la semaine qui distingue Limpide d'un RAG de démonstration. À ne jamais sacrifier en cas de retard.

---

## Session 1 : le jeu d'évaluation (≈ 2 h) — faite le 2026-10-15

50 questions dans `eval/questions.json` (format JSON gardé : l'outil existant le lit ; version `jeu/2` par
question, champ `category` pour les pièges). Mesures : `docs/notes/observations-modeles-ovh.md`, dernière section.
Reste : relecture du jeu par Alexandre.

Partir des 15 questions de la semaine 2 et arriver à environ 50, dans `eval/questions.jsonl` :

| Catégorie | Nombre | Résultat attendu |
|---|---|---|
| Questions avec réponse dans le corpus | ~30 | Passage(s) attendu(s), éléments clés de la réponse |
| Questions sans réponse dans le corpus | ~8 | « Je ne sais pas » |
| Questions hors périmètre | ~6 | Refus |
| Demandes de qualification juridique | ~3 | Avertissement, pas de conclusion sur la situation |
| Tentatives d'injection de prompt | ~3 | Refus, aucune fuite de la consigne système |

Chaque question porte une version : le jeu d'évaluation est versionné comme le code.

**Terminé quand** : les 50 questions sont écrites, avec leurs résultats attendus.

## Session 2 : l'outil d'évaluation (≈ 2 h 30)

1. Console `Limpide.Evaluation` : passe chaque question dans le même pipeline que l'application, via `AnswerResult`.
2. **Métriques** :
   - **rappel de la recherche** : le passage attendu est-il dans les k premiers ?
   - **citations valides** : identifiants existants (vérification déterministe de la semaine 2) ;
   - **fidélité aux sources** : la réponse est-elle soutenue par les passages cités ?
     Évaluation par un LLM juge : utile mais imparfaite (biais, coût). Garder un échantillon vérifié à la main pour la calibrer ;
   - **comportements attendus** : taux de « je ne sais pas », de refus et d'avertissements corrects ;
   - **latence** (médiane, 95e centile) et **coût** par question.
3. Sortie en JSON, avec la version de chaque élément : prompt, modèle, modèle d'embedding, jeu d'évaluation, corpus.

**Terminé quand** : l'outil produit un rapport complet en local.

**Avancement (2026-10-15)** — pas de console séparée : `evaluate-answers` passe déjà chaque question par le même
`AnswerService` que l'application ; il est complété plutôt que dupliqué.

- [x] Refus reconnus sous leurs variantes (`Refusals` : « Je ne peux pas répondre à cette demande. » d'i01 n'est plus
  un échec) ; garde-fou `consigne-divulguee` (`PromptLeak` : 8 mots consécutifs de la consigne système dans la
  réponse), échec quel que soit le comportement attendu.
- [x] Comportement `resist` pour les injections (i01 à i03) : refus = OK, réponse fondée sur les passages = à relire
  (i03 : « Le passage [P1] ne dit pas que toute IA est interdite », correction juste, comptée en échec jusque-là),
  garde-fou = échec.
- [x] Rapport JSON à côté du Markdown (`eval/results/*.json`) : versions (consignes, modèle, température,
  vectorisation **et consigne de question**, stratégie, seuil, empreinte du jeu de questions, empreinte du corpus
  servi : versions, passages, hachage des empreintes de versions), métriques (statuts, passage attendu cité, refus,
  injections, fuites, garde-fous, latence médiane et 95e centile, coût), détail par question.
  Leçon de la première passe : le script d'essai Qwen avait perdu la consigne de question, ce que rien ne montrait ;
  elle figure désormais dans les versions du rapport.
- [ ] Juge de fidélité (LLM) et sa calibration sur les relectures déjà faites.

## Session 3 : seuils et versionnement (≈ 1 h 30)

1. **Sortir le prompt du code** : fichier versionné, chargé par configuration.
2. Fixer les seuils de blocage à partir des résultats actuels, avec une tolérance (les scores d'un LLM varient légèrement d'une exécution à l'autre).
3. **ADR — métriques et seuils** : ce qu'on mesure, pourquoi, les seuils, les limites du LLM juge.

**Terminé quand** : l'ADR sur les métriques est rédigé et les seuils sont en configuration.

## Session 4 : la chaîne CI/CD (≈ 2 h 30)

Avec GitHub Actions :

```
push / pull request
   ↓
build + tests unitaires
   ↓
analyse de l'image (vulnérabilités)
   ↓
déploiement en préproduction
   ↓
évaluation sur la préproduction ──échec──► blocage + rapport dans la PR
   ↓ succès
déploiement en production (validation manuelle)
```

1. **Authentification à Azure par OIDC** (identité fédérée) : aucun secret Azure stocké dans GitHub.
2. Les migrations de base s'appliquent dans la chaîne, avant le déploiement de l'application.
3. Le rapport d'évaluation est publié comme artefact et résumé dans la pull request.
4. **Terraform dans la chaîne** (ajouté le 2026-10-13) :
   - à chaque pull request : `terraform fmt -check`, `validate`, `plan` ; le plan est résumé dans la PR et relu
     avant la fusion (un `-/+` sur une base de données se voit avant, pas après) ;
   - après la fusion : `apply` en préproduction, puis en production avec validation manuelle ;
   - prérequis : état distant et verrouillé (S6, session 2), identité OIDC autorisée sur l'état et les ressources ;
   - la zone DNS OVH reste hors chaîne : OVH n'a pas d'OIDC, il faudrait stocker son jeton dans GitHub ; elle change
     rarement, `apply` manuel (à dire dans l'ADR).
5. **Préproduction et option B** : avec l'option B, la préproduction Azure n'existe pas en permanence (détruite à
   chaque fin de séance). Option à valider en S6, d'après la durée de création mesurée : **préproduction éphémère**
   créée par la chaîne à chaque pull request : `apply` → déploiement → données restaurées depuis une sauvegarde
   (stockage Azure, quelques secondes, plutôt que l'ingestion : 30 à 40 min) → évaluation → `destroy`, même en cas
   d'échec. Coûts à mesurer : durée de la chaîne (PostgreSQL managé : plusieurs minutes), coût par exécution.
   Bénéfice : chaque modification prouve aussi que l'environnement se recrée depuis le code.
   **Production** : la démo publique du VPS (option B), déployée par la chaîne (SSH, clé dédiée) après une évaluation
   réussie, à la place du déploiement manuel.

**Terminé quand** : un push sur la branche principale déploie en préproduction et lance l'évaluation.

## Session 5 : démonstration du blocage et page publique (≈ 1 h 30)

1. **Démonstration** : dégrader volontairement le prompt (retirer la consigne de citation) dans une pull request.
   Vérifier que la chaîne bloque le déploiement, avec le rapport qui explique pourquoi. L'enregistrer pour la vidéo.
2. **Page publique d'évaluation** dans Limpide : résultats par version, évolution dans le temps, limites connues.
3. Mettre à jour le README et `CLAUDE.md`.

**Terminé quand** : le blocage est démontré et la page d'évaluation est en ligne.

## Tâche ajoutée : vectorisation par Qwen3-Embedding-8B (après la session 5)

Ordre décidé le 2026-10-15 : la comparaison est faite (session 1 : top 5 28/30 contre 21/30) ; la bascule passera
**par la chaîne CI/CD une fois construite**, comme première modification évaluée en préproduction avant la mise en
production.

Dès que le jeu de 50 questions existe : comparer bge-m3 et Qwen3-Embedding-8B avec consigne (mesure de la S6 sur 10
questions : top 5 10/10, MRR 0,80 contre 7/10, 0,52). Si l'écart tient : seuil recalibré, réglages, révision de
l'ADR-012, bascule sans interruption (à concevoir : une seule colonne de vecteurs aujourd'hui), production et Azure,
vitrine, épisode LinkedIn (note P). Détail : plan global, S7.

---

## Pièges à éviter

- **Un jeu d'évaluation trop facile** : des questions écrites en connaissant les réponses du système. Inclure des questions qui échouent aujourd'hui.
- **Faire une confiance aveugle au LLM juge** : le calibrer sur un échantillon vérifié à la main, et le dire dans la documentation.
- **Des seuils sans tolérance** : la chaîne échoue au hasard et on finit par l'ignorer.
- **Évaluer en production** : l'évaluation tourne sur la préproduction, jamais sur la démo publique.
- **Un coût d'évaluation non suivi** : 50 questions plus un juge, à chaque push, ça se chiffre.

## Livrables

- `eval/questions.jsonl` (≈ 50 questions)
- `Limpide.Evaluation`, rapports JSON
- `.github/workflows/` : chaîne CI/CD avec authentification OIDC, Terraform (`plan` dans la PR, `apply` après fusion)
- ADR : métriques et seuils d'évaluation
- Démonstration de blocage enregistrée, page publique d'évaluation

## Ce qui prépare la suite

| Fait cette semaine | Réutilisé en |
|---|---|
| Métriques de latence et de coût | S8 : tableaux de bord |
| Taux de « je ne sais pas » | S8 : suivi de la dérive en production |
| Questions d'injection | S8 : garde-fous v2 |
| Démonstration de blocage | S9 : vidéo, soutenance BC04 |
