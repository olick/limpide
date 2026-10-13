# Modèles ouverts hébergés par OVHcloud : rédaction et vectorisation (S6, 2026-10-13)

Suite du comparatif de coûts (`comparatif-couts-hebergement.md`) et de l'ADR-012. OVHcloud AI Endpoints héberge en
France (Gravelines) des modèles publiés en poids ouverts, par une API compatible OpenAI. Mesures faites sur le poste,
corpus local (1 130 passages servis), mêmes questions que les mesures précédentes. Prix : catalogue public d'OVHcloud,
relevé le 2026-10-13, hors taxes.

## Rédaction des réponses (`evaluate-answers`, 15 questions : 10 normales, 5 pièges)

Réglages : `Generation:Endpoint=https://oai.endpoints.kepler.ai.cloud.ovh.net/v1`, `Generation:ApiKey` (clé OVHcloud),
`Generation:MaxOutputTokens=2000` (8 000 pour Qwen3.5 : voir plus bas) ; consignes `answer/3`, température 0.

| Modèle | Contrôle automatique | Passage attendu cité | Durée (médiane / max) | Tokens (entrée / sortie, moyenne) | Coût par question | Rapport |
|---|---|---|---|---|---|---|
| **Mistral Medium 3.5** (API Mistral, actuel) | 11/15, 0 échec | 7/10 | **1,5 s** / 4,1 s | 1 924 / 232 | ≈ 0,51 centime ($) | `eval/results/2026-10-11-1806-mistral-medium-2604.md` |
| **gpt-oss-120b** (OpenAI, poids ouverts) | 11/15, 0 échec | 7/10 | 13,7 s / 27,1 s | 1 883 / 576 | **≈ 0,04 centime** | `2026-10-13-1918-gpt-oss-120b.md` |
| **Llama 3.3 70B** (Meta) | 11/15, **1 échec** | 7/10 | 5,0 s / 13,8 s | 2 190 / 218 | ≈ 0,16 centime | `2026-10-13-1919-Meta-Llama-3_3-70B-Instruct.md` |
| **Qwen3.5-397B** (Alibaba) | 11/15, 0 échec | 7/10 | 24,6 s / 46,1 s | 1 934 / **3 541** | ≈ 1,39 centime | `2026-10-13-1928-Qwen3.5-397B-A17B.md` |

Constats :

- **gpt-oss-120b, premier passage : 3 échecs « sans-citation » à tort.** Le modèle écrit `[​P5]` avec une espace de
  largeur nulle (U+200B) après le crochet : à l'écran une citation normale, pour le contrôle aucune. Correctif :
  `ModelText.Clean` retire les caractères invisibles de la réponse avant tout contrôle (2 tests). Rapport du premier
  passage gardé : `2026-10-13-1914-gpt-oss-120b.md`.
- **Llama 3.3 70B tranche la situation de l'utilisateur** sur q03 (garde-fou `qualification-juridique`) : le défaut que
  la consigne `answer/3` a corrigé chez Medium.
- **Qwen3.5-397B « réfléchit » avant de répondre** : avec 2 000 tokens de sortie, 9 réponses vides et tronquées (la
  réflexion consomme tout le budget ; rapport `2026-10-13-1922-Qwen3.5-397B-A17B.md`). Avec 8 000 : 11/15, mais
  ≈ 3 500 tokens de sortie par question, d'où un coût **supérieur à Medium** et 25 s d'attente.
- **La vitesse sépare les modèles plus que le contrôle automatique** : Medium répond en 1,5 s, gpt-oss en 14 s.
- **Limite** : le contrôle automatique ne juge pas la fidélité aux textes. Les réponses « à relire » (4 par modèle)
  et toutes les réponses de gpt-oss sont à relire comme en S2 (relecture par un modèle, pas par un juriste) avant
  toute décision : Small 4 passait aussi le contrôle automatique et déformait des textes.

## Vectorisation : Qwen3-Embedding-8B (`evaluate`, 10 questions)

Copie de la base (`rag_qwen`), corpus revectorisé (« titre + texte ») : `Embedding:Provider=OpenAI`,
`Embedding:Model=Qwen3-Embedding-8B`, `Embedding:RequestDimensions=true` (le modèle produit 4 096 dimensions par
défaut et accepte d'en rendre 1 024 : la colonne `vector(1024)` ne change pas). Le modèle recommande une consigne devant
la question (pas devant les passages) : `Embedding:QueryInstruction`.

| | bge-m3 (actuel) | Qwen3-Embedding-8B, question brute | **Qwen3-Embedding-8B, avec consigne** |
|---|---|---|---|
| Bon passage en 1re position | 4/10 | 4/10 | **7/10** |
| Bon passage dans le top 5 | 7/10 | 9/10 | **10/10** |
| MRR | 0,52 | 0,61 | **0,80** |
| Meilleur score, questions légitimes | 0,541 à 0,705 | — | 0,766 à 0,837 |
| Meilleur score, 4 questions hors sujet | 0,291 à 0,467 | — | 0,311 à 0,446 |
| « Règles sur l'IA aux États-Unis » / « certification ISO 42001 » | 0,655 / — | — | 0,684 / 0,550 |
| Vectorisation du corpus | ≈ 41 ms par passage (API OVHcloud, bge-m3) | 506 ms par passage, 9 min 36 s | idem |
| Latence d'une question (6 questions, depuis le poste) | ≈ 184 ms | — | **médiane 3,9 s, max 11,6 s** |
| Prix | 0,01 € le million de tokens | 0,10 € le million de tokens (≈ 0,03 € pour tout le corpus) | idem |

Consigne utilisée : « Instruct: Étant donné une question, retrouver les passages des textes juridiques qui y
répondent\nQuery: ».

Constats :

- **La meilleure recherche mesurée depuis le début du projet** : les articles qui font foi passent enfin devant les
  considérants (q06 Article 99, q07 Article 53, q08 Article 27 en 1re position), la faiblesse notée dès la S1.
- **Le seuil de pertinence deviendrait bien plus sûr** : écart d'environ 0,32 entre le hors sujet (≤ 0,45) et les
  questions légitimes (≥ 0,77), contre 0,07 avec bge-m3. À recalibrer (autour de 0,60).
- **La latence est rédhibitoire en l'état** : ≈ 4 s de plus par question, jusqu'à 11 s. À remesurer (charge du
  service ce soir-là ?) avant de conclure.
- **Limite** : 10 questions écrites par l'auteur ; le jeu de 50 questions de la S7 doit confirmer.

## Pistes

- Rédaction : relire les réponses de gpt-oss-120b (fidélité) ; s'il est fidèle, le coût est divisé par 13, mais
  l'attente est multipliée par 9 : le choix dépend de ce qu'on veut montrer (démo réactive ou coût minimal).
- Vectorisation : remesurer la latence de Qwen3-Embedding-8B à d'autres moments ; si elle reste élevée, chercher un
  modèle de la même famille plus petit, ou l'auto-héberger.
- Les modèles d'origine chinoise ou américaine tournent ici chez OVHcloud, en France : aucune donnée ne part chez leur
  éditeur ; poids ouverts, donc pas de dépendance à l'éditeur (discussion du 2026-10-13, à reprendre dans la révision de l'ADR-004 ou de l'ADR-012).
