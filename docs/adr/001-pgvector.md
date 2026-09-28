# ADR-001 : PostgreSQL + pgvector plutôt qu'une base vectorielle dédiée

- **Statut** : accepté
- **Date** : 2026-09-28

## Contexte

Le corpus est petit (quelques milliers de passages au plus). Il faut stocker des métadonnées relationnelles
(sources, documents, versions) et des vecteurs, avec une traçabilité forte entre une réponse et la version
exacte d'un document. Je maîtrise PostgreSQL ; je n'ai pas d'expérience d'exploitation d'une base vectorielle dédiée.

## Options envisagées

1. **PostgreSQL + pgvector** — une seule base, transactions et clés étrangères entre versions et passages,
   recherche plein texte native pour la recherche hybride, service managé disponible sur Azure.
   Moins performant qu'une base dédiée à très grande échelle.
2. **Qdrant** — conçu pour la recherche vectorielle, filtres riches, très performant.
   Un composant de plus à apprendre, déployer, sauvegarder et sécuriser ; la cohérence avec les métadonnées
   relationnelles serait à gérer à la main.

## Décision

PostgreSQL + pgvector, avec un index HNSW en distance cosinus.

## Conséquences

- Un seul composant de données à opérer, sauvegarder et provisionner avec Terraform.
- Recherche hybride possible sans outil supplémentaire (index `tsvector` en français).
- **Condition de révision** : si le corpus dépasse quelques millions de passages, ou si la latence de recherche
  au 95e centile dépasse 200 ms, réévaluer une base dédiée.
