# Fiche descriptive du système

Ce qu'est Limpide, sur quoi il repose, à quoi il sert et à quoi il ne sert pas, comment il est mesuré, et ce qu'il
ne sait pas faire. État au **2026-11-20** (production : https://www.limpide-ia.fr). Les valeurs viennent de
`src/appsettings.shared.json` et des fichiers cités : en cas d'écart, ce sont eux qui font foi.

## Identité

| | |
|---|---|
| Nom | Limpide |
| Fournisseur | Alexandre Leclerc - EURL (`classification-ai-act.md`) |
| Nature | assistant de recherche documentaire avec génération augmentée (RAG) sur un corpus fermé |
| Mise en service | 2026-10-06, démo publique et gratuite |
| Classification AI Act | ni pratique interdite ni haut risque ; transparence de l'article 50 (`classification-ai-act.md`) |
| Code | dépôt public, licence Apache 2.0 |

## Fonctionnement

1. **Contrôle de la question**, par règles, sans IA : une tentative d'injection probable est arrêtée ici, sans
   recherche ni appel au modèle (ADR-021).
2. **Vectorisation** de la question, puis **recherche vectorielle** des passages proches dans PostgreSQL avec pgvector
   (ADR-001, ADR-005), en suivant le renvoi de l'article 6 vers l'annexe III (ADR-024).
3. **Seuil de pertinence** : si le meilleur passage est sous le seuil, réponse fixe, sans appel au modèle.
4. **Rédaction** par le modèle de langage, à partir des passages seuls, avec obligation de les citer.
5. **Contrôles après rédaction**, sans IA : citations, fuite des consignes, qualification juridique, troncature. Ils
   sont signalés au visiteur, pas bloquants (`registre-des-risques-ia.md`).
6. **Affichage** : la réponse, distincte des textes ; les passages cités, reproduits à l'identique avec source et
   licence ; « Sous le capot » : scores, stratégie, durées, tokens, coût, garde-fous.

Chaque question est traitée seule, sans mémoire de conversation (ADR-022).

## Modèles et versions

| Rôle | Modèle | Fournisseur, lieu | Réglages |
|---|---|---|---|
| Rédaction | Mistral Medium 3.5 (`mistral-medium-2604`) | Mistral AI, API UE (ADR-004) | température 0 ; 1 500 tokens de sortie au plus ; consignes `answer/8` (`src/prompts/answer/8.txt`, figées par empreinte) |
| Vectorisation | Qwen3-Embedding-8B, réduit à 1 024 dimensions, titre du passage inclus | OVHcloud AI Endpoints, France (ADR-012) | consigne de requête ; un vecteur par modèle (ADR-017) |
| Juge de fidélité (évaluation seulement) | Mistral Medium 3.5 | Mistral AI, espace `limpide-evaluation` | consignes `judge/2`, calibré (`eval/calibration/fidelite.json`) |

**Recherche** : vectorielle seule (l'hybride mesurée ne l'améliore pas, ADR-005) ; 40 candidats, 6 passages fournis
au modèle ; seuil de pertinence **0,60**, propre à Qwen3-Embedding-8B avec titre : tout changement de modèle
d'embedding impose de le recalibrer (ADR-017).

## Données

| Donnée | Origine | Volume | Gouvernance |
|---|---|---|---|
| Corpus | AI Act (EUR-Lex, version consolidée au 27.7.2026, sans considérants) et fiches pratiques IA de la CNIL (17 pages) | 18 versions servies, 925 passages (`/sante`, 2026-11-20) | `registre-des-sources.md`, `docs/sources.md` ; versions et quarantaine (ADR-008) ; découpage structurel (ADR-010) |
| Questions des visiteurs | saisies dans la démo | conservées 30 jours, masquées | `rgpd.md` ; jamais versées automatiquement dans le jeu d'évaluation (ADR-019) |
| Jeu d'évaluation | écrit à la main | 58 questions : 33 réponses citées, 14 refus, 8 injections, 3 sans qualification juridique | `eval/questions.json` ; ADR-014 |

Aucune donnée n'entraîne un modèle : Limpide n'entraîne rien, et l'entraînement par Mistral est refusé (`rgpd.md`).

## Usage prévu

- **Pour qui** : toute personne qui cherche ce que disent l'AI Act et les fiches de la CNIL sur un sujet ; en
  particulier, en rendez-vous ou en entretien, quelqu'un qui veut voir un RAG maîtrisé de près.
- **Pour quoi** : trouver les passages pertinents, en lire une reformulation, et vérifier cette reformulation dans les
  extraits affichés.
- **Comment** : en français, une question à la fois, sur le périmètre du corpus.

## Usages exclus

- **Qualifier une situation** (« mon système est-il à haut risque ? ») : l'assistant indique les textes, il ne tranche
  pas. Ce n'est pas un conseil juridique.
- **Décider à la place d'une personne**, ou évaluer une personne (candidat, salarié, client).
- **Usage par une autorité judiciaire ou lors d'un règlement de litige** : il relèverait de l'annexe III, point 8 a) ;
  autre destination, donc nouvelle classification avant toute mise en service (`classification-ai-act.md`).
- **Questions hors du corpus** : d'autres textes (RGPD lui-même, droit national, jurisprudence) ne sont pas servis ;
  la réponse attendue est « je ne sais pas ».
- **Citer la réponse comme un texte officiel** : seuls les extraits le sont.

## Mesures de qualité

Évaluation de référence du **2026-11-06 22 h 51** (`eval/results/2026-11-06-2251-mistral-medium-2604.md`), même
configuration qu'en production :

| Mesure | Valeur | Seuil de la barrière |
|---|---|---|
| Questions vérifiées automatiquement | 52/58 (1 échec, 5 à relire) | échecs ≤ 2 |
| Passage attendu cité | 31/33 | ≥ 0,86 |
| Refus attendus obtenus | 14/14 | ≥ 0,94 |
| Injections obéies, consignes divulguées | 0, 0 | 0, 0 |
| Réponses jugées infidèles | 3 sur 36 jugées | ≤ 0,30 |
| Faits attendus absents | 1 (`q33`) | ≤ 1 |
| Durée par question | médiane 1,5 s, 95e centile 5,7 s | 8 s, non bloquant |
| Coût de rédaction | 0,22 € pour 58 questions, modèle appelé 44 fois | ≤ 0,0068 € par question |

La barrière (`gate`) s'applique avant chaque déploiement, dans un environnement jetable (ADR-016). La page
« Résultats d'évaluation » de la démo publie le dernier rapport.

## Limites connues

- **`q33`** : un fait attendu (2 décembre 2026) manque dans la réponse ; échec connu, toléré par la barrière, à
  ramener à zéro.
- **Le juge de fidélité** est le modèle qui rédige, et il varie plus que sa marge : sa mesure n'a de sens qu'agrégée.
- **Garde-fous après rédaction** : ils signalent, ils ne bloquent pas ; la qualification juridique est repérée par
  une heuristique.
- **Contrôle de la question par règles** : une injection paraphrasée ou dans une autre langue peut passer.
- **Corpus** : sans les considérants de l'AI Act (version consolidée) ; images des fiches CNIL exclues (licence) ;
  veille des actes modificatifs manuelle.
- **Fournisseurs sans secours** : une panne d'OVHcloud ou de Mistral rend la démo indisponible ; la vectorisation a
  déjà pris 15 s par question (2026-10-28).
- **Pas de mémoire** : une question qui renvoie à la précédente (« et pour les PME ? ») est traitée seule.
- **Masquage partiel** des données personnelles : noms et adresses postales non détectés.
- **Marquage lisible par machine** du texte généré absent (AI Act, article 50.2 ; `classification-ai-act.md`).
- **Aucune mesure auprès de vrais utilisateurs** : la compréhension de l'interface par des non-techniciens n'est pas
  encore testée.
