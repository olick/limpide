# ADR-012 : Hébergement des modèles — génération par API, vectorisation par bge-m3 chez OVHcloud AI Endpoints

- **Statut** : accepté (décision d'Alexandre le 2026-10-13 : OVHcloud pour Azure et pour le VPS)
- **Date** : 2026-10-13

## Contexte

Limpide utilise deux modèles :

- **le modèle qui rédige** la réponse : Mistral Medium 3.5 par l'API de Mistral, inférence en UE (ADR-004) ;
- **le modèle qui vectorise** passages et questions : bge-m3, servi par Ollama sur la machine (ADR-002). En ligne, il
  ne sert qu'à vectoriser la question (≈ 25 tokens) : les passages sont vectorisés une fois, à l'ingestion.

Le comparatif de coûts de la S6 (`docs/notes/comparatif-couts-hebergement.md`) montre qu'Ollama, allumé en
permanence (2 vCPU, 4 Go), représente **environ 80 % du coût des conteneurs** chez les hébergeurs gérés, et occupe
2,3 Go des 4 Go du VPS. Le plan demande aussi de chiffrer un LLM auto-hébergé sur GPU, pour 100, 1 000 et 10 000
questions par jour. Prix relevés le 2026-10-13, hors taxes.

## Génération : API ou GPU

Coût par question avec Medium 3.5 : ≈ 0,5 centime mesuré en production (60 questions par jour plafonnées à ≈ 9 € par
mois ; ADR-004 l'estimait à 0,7 centime) ; avec Small 4 : ≈ 0,05 centime.

| Questions par jour | API Medium 3.5 | API Small 4 | GPU L4 (Scaleway, 0,79 €/h) toujours allumé |
|---|---|---|---|
| 100 | ≈ 15 € / mois | ≈ 1,50 € / mois | ≈ 575 € / mois |
| 1 000 | ≈ 150 € / mois | ≈ 15 € / mois | ≈ 575 € / mois |
| 10 000 | ≈ 1 500 € / mois | ≈ 150 € / mois | ≈ 575 € / mois (capacité d'un seul GPU à mesurer ; 2 GPU pour la disponibilité : ≈ 1 150 €) |

- **Seuil de bascule** : face à l'API Medium, un GPU devient moins cher vers **3 800 questions par jour** ; face à
  l'API Small, vers **38 000**.
- **Mais la comparaison juste est avec Small** : un GPU de 24 Go ne fait tourner qu'un modèle de cette classe en poids
  ouverts ; Medium 3.5 n'est pas publié en poids ouverts. Or la relecture de la S2 a écarté Small : il déforme des
  textes sans que rien ne le détecte (ADR-004). Auto-héberger au niveau de qualité retenu demanderait un modèle bien
  plus gros (GPU de 80 Go), donc un seuil encore plus haut.
- **Ce que le GPU apporterait** : aucune donnée hors de l'infrastructure (exigence possible d'un client aux documents
  confidentiels), coût fixe et prévisible. À cette échelle, ces arguments ne pèsent pas : le corpus est public, et
  l'inférence de Mistral est déjà en UE.

## Vectorisation : trois options mesurées

| | Ollama + bge-m3 (actuel) | API mistral-embed | API bge-m3, OVHcloud AI Endpoints |
|---|---|---|---|
| Recherche (10 questions) : top 1 / top 5 / MRR | 4/10 · 7/10 · 0,52 | 1/10 · 5/10 · 0,31 | **4/10 · 7/10 · 0,52** (mêmes rangs) |
| Vecteurs | — | autre modèle : tout recalculer, seuil à recalibrer | **identiques** (cosinus 0,99998 à 0,99999) : rien à recalculer |
| Coût | un conteneur 2 vCPU / 4 Go permanent (≈ 47 $ par mois sur Azure) ; 2,3 Go sur le VPS | 0,10 $ le million de tokens | **0,01 € le million de tokens** (≈ 0,003 € pour tout le corpus) |
| Latence d'une question | ≈ 65 ms (poste) | — | ≈ 184 ms (médiane, depuis le poste) |
| Lieu | la machine | UE (Mistral) | France (Gravelines) ; requêtes ni conservées ni partagées selon OVHcloud |

## Décision

1. **Génération : API de Mistral** (inchangé, ADR-004). Pas de GPU : il ne devient rentable qu'à plusieurs milliers de
   questions par jour, et seulement avec un modèle de qualité inférieure.
2. **Vectorisation : bge-m3 par l'API d'OVHcloud AI Endpoints**, sur Azure **et** sur le VPS de la démo publique.
   Même modèle, mêmes vecteurs, même évaluation ; un conteneur de moins, la mémoire du VPS libérée, un hébergeur
   français.
3. **Ollama reste** sur le poste (développement, ingestion locale) et **en secours** : profil `ollama` du VPS, image
   `deploy/ollama` pour Azure. Revenir à Ollama ne demande qu'un réglage (`Embedding:Provider=Ollama`) : mêmes
   vecteurs, rien à recalculer.

## Conséquences

- Code : `Embedding:Provider` (`Ollama` ou `OpenAI` : toute API compatible OpenAI), `Embedding:Endpoint`,
  `Embedding:ApiKey`. Clé OVHcloud : user-secrets (poste), `.env.prod` (`OVH_AI_ENDPOINTS_KEY`), coffre permanent
  (`ovh-ai-endpoints-key`, Azure). Une clé de plus à faire tourner.
- Dépendance de plus envers un service extérieur : si l'API d'OVHcloud tombe, la recherche tombe (la génération dépend
  déjà de Mistral). Limite de 400 requêtes par minute par projet, très au-dessus du quota de la démo.
- Coût : vitrine Azure ≈ 0,03 € de l'heure au lieu de ≈ 0,10 € ; sur le VPS, le serveur garde la même offre mais
  retrouve ≈ 2,3 Go de mémoire.
- Le seuil de pertinence `MinScore = 0,50` reste valable (mêmes vecteurs).
- **Conditions de révision** :
  - API d'OVHcloud indisponible, plus chère, ou bge-m3 retiré de son catalogue → retour à Ollama par configuration ;
  - exigence « aucune donnée hors de nos machines » (client, documents confidentiels) → Ollama pour la vectorisation,
    et un LLM auto-hébergé à évaluer sur la qualité avant le coût ;
  - plus de ≈ 3 800 questions par jour au niveau de qualité actuel → réévaluer l'auto-hébergement (GPU de 80 Go,
    modèle ouvert de grande taille, relecture de la qualité comme en S2).
