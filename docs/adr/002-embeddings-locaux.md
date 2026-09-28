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
