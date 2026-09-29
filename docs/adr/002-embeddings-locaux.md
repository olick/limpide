# ADR-002 : Embeddings calculés localement avec bge-m3 (Ollama)

- **Statut** : proposé — à confirmer après les mesures de fin de semaine 1
- **Date** : 2026-09-28

## Contexte

Le corpus est en français. Les embeddings sont recalculés à chaque nouvelle version d'un document.
Il faut un modèle multilingue, un coût maîtrisé et la possibilité d'expliquer où partent les données.

## Options envisagées

1. **bge-m3 via Ollama, en local** — multilingue, 1024 dimensions, gratuit, aucune donnée ne sort.
   Demande du CPU/GPU côté hébergement ; plus lent sans GPU.
2. **API d'embeddings hébergée** (Azure OpenAI ou autre) — rapide, rien à opérer.
   Coût à l'usage, dépendance à un fournisseur, données envoyées à un tiers (acceptable ici : corpus public).

## Décision

bge-m3 en local pour la phase 1. Le code passe par `IEmbeddingGenerator` (Microsoft.Extensions.AI),
donc le changement de fournisseur se limite à la configuration.

## Conséquences

- Coût nul en développement ; mesurer en semaine 1 le temps d'ingestion du corpus complet sur CPU.
- Le modèle d'embedding est enregistré sur chaque passage (`chunks.embedding_model`) :
  changer de modèle impose une réindexation complète, traçable.
- **Condition de révision** : si l'ingestion complète dépasse 30 minutes sur l'hébergement cible,
  ou si l'évaluation de la semaine 5 montre une qualité de recherche insuffisante.

## Mesures (semaine 1, 2026-09-29)

Poste de développement : Intel Core i7-8750H (6 cœurs, 12 threads), 32 Go de RAM. Ollama en conteneur
**sur CPU** : le GPU du poste (GTX 1050) n'est pas exposé au conteneur (pas de NVIDIA Container Toolkit).
Texte vectorisé : le contenu du passage seul. Lots de 16 passages.

| Mesure | Valeur |
|---|---|
| Passages | 691 (634 AI Act, 57 CNIL) |
| Caractères | 683 979 (moyenne 990 par passage) |
| Durée totale | 571 s (9 min 31 s) |
| Premier lot, avec chargement du modèle | 20,2 s |
| Régime établi | 816 ms par passage |

Lecture :
- Le corpus de la phase 1 passe en moins de 10 minutes : la condition de révision (30 minutes) n'est pas atteinte.
  Un corpus cinq fois plus grand (élargissement en semaine 5) la dépasserait sur un CPU comparable.
- Contrôle de cohérence : le plus proche voisin d'un passage de l'article 5 (pratiques interdites) est le
  considérant 29, qui porte précisément sur ces pratiques.
- Qualité de recherche : à compléter en fin de semaine 1 (10 questions de test, session 5).

## Question ouverte pour les ADR-006 et 012

Le calcul hors ligne n'est pas le point dur : c'est un batch rare, dont la durée importe peu.
Le point dur est la **vectorisation des questions** en ligne : l'API a besoin du même modèle, disponible
en permanence. Un conteneur Ollama avec bge-m3 chargé (≈ 1,2 Go) coûte au repos s'il reste allumé,
et impose un démarrage à froid de plusieurs secondes s'il est mis à l'échelle jusqu'à zéro.
Options à chiffrer : Ollama sur CPU toujours allumé, mis à l'échelle jusqu'à zéro, ou API d'embeddings hébergée
(corpus public ; coût négligeable, mais réindexation complète si l'on change de fournisseur).
