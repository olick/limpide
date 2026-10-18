# ADR-012 : Hébergement des modèles — génération par API, vectorisation chez OVHcloud AI Endpoints

- **Statut** : accepté (décision d'Alexandre le 2026-10-13 : OVHcloud pour Azure et pour le VPS)
- **Date** : 2026-10-13
- **Révision** : 2026-10-18, bge-m3 remplacé par Qwen3-Embedding-8B, OVHcloud partout, sans secours (section
  « Révision du 2026-10-18 », qui remplace le point 3 de la décision)

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

## Révision du 2026-10-18 : Qwen3-Embedding-8B

**Pourquoi** : sur le jeu de 50 questions (`jeu/2`), Qwen3-Embedding-8B, servi par la même API d'OVHcloud, trouve
nettement mieux les passages. Mesures avec Mistral Medium 3.5 ; Qwen : `answer/5`, sauvegarde de la production
(rapport `eval/results/2026-10-18-1925`), confirmées par la session 1 de la S7 ; bge-m3 : passes de référence des
2026-10-15 à 2026-10-18 (consignes `answer/3` et `answer/5`) :

| | bge-m3+titre | Qwen3-Embedding-8B+titre |
|---|---|---|
| Recherche : bon passage en 1re position / top 5 / MRR | 10/30 · 21/30 · 0,51 | **17/30 · 28/30 · 0,72** |
| Passage attendu fourni / cité dans la réponse | 21/30 · 19/30 | **28/30 · 28/30** |
| Refus corrects, injections obéies, fuites | 14/14 · 0 · 0 | 14/14 · 0 · 0 |
| Réponses jugées infidèles | 3 à 7 sur 32 à 34 (4 passes Medium) | 7 sur 34 |
| Meilleur score, questions légitimes / hors sujet | ≥ 0,541 / ≤ 0,478 | **≥ 0,742 / ≤ 0,587** |
| Prix (OVHcloud) | 0,01 € le million de tokens | 0,10 € le million de tokens ; corpus revectorisé en 93 s |

**Décision (Alexandre, 2026-10-18)** : vectorisation par **Qwen3-Embedding-8B chez OVHcloud AI Endpoints, partout**
(poste de développement compris), réduit à 1 024 dimensions (`Embedding:RequestDimensions`), avec une consigne devant
la question (`Embedding:QueryInstruction`). Bascule faite sans interruption (ADR-017) et évaluée par la chaîne.

- **Seuil de pertinence `Generation:MinScore = 0,60`** (était 0,50) : 0,14 sous la question légitime la plus basse
  (q22), 0,013 au-dessus du hors sujet le plus haut (t02, « poème sur l'IA ») ; les cinq autres hors sujet sont sous
  0,40. Marge laissée du côté des questions légitimes : un refus à tort se voit, un hors sujet qui passe est encore
  refusé par la consigne du modèle (≈ 0,004 $).
- **Plus de secours par Ollama.** Le point 3 ci-dessus reposait sur « mêmes vecteurs » : il ne tient plus. Qwen3-
  Embedding-8B ne tient pas sur le VPS (3,8 Go de mémoire, 2 vCPU). Si l'API d'OVHcloud tombe, la recherche tombe,
  comme la génération sans Mistral. Recours : revenir à bge-m3 (Ollama, profil `ollama` du VPS) par la configuration
  (modèle, fournisseur, dimensions, consigne de question, seuil 0,50), **puis** `embed` pour les passages publiés
  depuis la bascule (ADR-017) : des minutes, pas un simple réglage. Non outillé, par choix.
- **Poste de développement** : vectorise aussi par OVHcloud (clé `Embedding:ApiKey` des user-secrets). Ollama et
  bge-m3 restent pour travailler hors ligne, en surcharge explicite (`appsettings.shared.json`).
- Coût : 10 fois celui de bge-m3 par token, mais une question fait ≈ 25 tokens et le corpus se revectorise pour
  ≈ 0,03 € : négligeable devant la génération.
- Poids ouverts (Alibaba), servis en France par OVHcloud : aucune donnée ne part chez l'éditeur.
- **Conditions de révision** : Qwen3-Embedding-8B retiré du catalogue d'OVHcloud ou plus cher ; pannes répétées de
  l'API (journaux de la S8) → outiller le retour à bge-m3 ; exigence « aucune donnée hors de nos machines » → modèle
  local, avec recalibrage du seuil et nouvelle évaluation.

