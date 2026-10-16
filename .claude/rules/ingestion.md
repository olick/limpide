---
paths:
  - "src/Limpide.Ingestion/**"
  - "src/Limpide.Core/Corpus/**"
  - "src/Limpide.Core/Extraction/**"
  - "src/Limpide.Core/Chunking/**"
  - "src/Limpide.Core/Quality/**"
  - "corpus.json"
---

# Ingestion : console, collecte, extraction, découpage, vectorisation

- **Console** (`src/Limpide.Ingestion/Cli/`) : sans argument, mode interactif ; avec une commande, exécution unique
  et code de sortie (mode d'Airflow, à préserver). Toute commande se déclare dans `CommandCatalog`. Configuration
  reconstruite à chaque commande (`--Section:Cle=valeur` ne vaut que pour elle). Résumé JSON en dernière ligne
  (`CommandSummary`). Quarantaine = code 0, signalée par le résumé.
- **Collecte polie** : `User-Agent` explicite (`Limpide/0.1 (+url du dépôt)`), pause d'1 à 2 s entre requêtes.
  `fetch` exige un `200` non vide et le bon type de contenu : un blocage ne crée jamais de version.
- EUR-Lex bloque les scripts : l'AI Act vient de l'API CELLAR (`fetchUrl` dans `corpus.json`, XHTML Formex, pas la
  page EUR-Lex). Pages CNIL : identifiants aléatoires dans le HTML → fausses nouvelles versions, écartées par
  `TextFingerprint` (ADR-008).
- **Extracteurs et découpeurs versionnés** (`cnil-html/2`, `ai-act/1`…) : changer la version relance l'étape.
  Les extracteurs ne produisent que du texte présent dans la source (seuls les espaces sont normalisés).
- **Découpage structurel** (ADR-010) : un passage par article pour l'AI Act (redécoupé par paragraphe si long), par
  titre de section pour la CNIL, 500 à 1 500 caractères, titre de rattachement dans `heading`.
  `FixedSizeChunker` gardé pour comparer (`--Ingestion:ChunkingStrategy=FixedSize`).
  Modifier `Packer` impose d'incrémenter la version des deux découpeurs. `chunk` remplace tous les passages d'une
  version dans une transaction.
- **Vectorisation** : « titre + texte » (`embedding_model = bge-m3+titre`) ; la recherche ne compare que des vecteurs
  de même libellé ; `embed` recalcule ce qui manque ou vient d'un autre modèle, lot par lot (reprise possible).
- Contrôles qualité (`QualityValidator`) : seuils en configuration (section `Quality`), calibrés dans
  `docs/notes/observations-qualite.md`.
