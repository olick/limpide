# ADR-004 : Génération des réponses par Mistral, hébergé en UE

- **Statut** : accepté — choix du modèle (Small ou Medium) à confirmer par mesure en fin de semaine 2
- **Date** : 2026-10-05

## Contexte

Le LLM rédige la réponse à partir des 5 passages trouvés par la recherche. Il ne cherche rien lui-même :
il doit répondre en français à partir des passages seuls, citer chaque affirmation (`[P1]`, `[P2]`…),
dire « je ne sais pas » quand les passages ne suffisent pas, ne pas qualifier la situation juridique
de l'utilisateur, et **ne pas dénaturer le texte** (condition de réutilisation EUR-Lex, voir `docs/sources.md`).

Contraintes :

- démo publique présentée à des clients français, sur un sujet de régulation européenne :
  le lieu de traitement des questions est un argument visible ;
- les questions des utilisateurs peuvent contenir des données personnelles, même si le corpus est public ;
- budget d'une démo personnelle ; le risque financier principal est l'abus (traité en S3 par un plafond) ;
- code .NET derrière `IChatClient` (Microsoft.Extensions.AI) : le fournisseur doit pouvoir changer par configuration.

Hypothèse de coût d'une question, à remesurer en semaine 2 : environ 2 500 tokens en entrée
(consignes, 5 passages, question) et 400 en sortie.

## Options envisagées

Prix relevés le 2026-10-05, par million de tokens (entrée / sortie).

| Option | Prix | Coût d'une question | Traitement des données | Intégration .NET |
|---|---|---|---|---|
| Mistral Small 4 | 0,15 $ / 0,60 $, +10 % en UE | ≈ 0,07 centime | UE (Mistral, France) | API compatible OpenAI (à vérifier) |
| Mistral Medium 3.5 | 1,50 $ / 7,50 $, +10 % en UE | ≈ 0,7 centime | UE (Mistral, France) | idem |
| Claude Haiku 4.5 | 1 $ / 5 $ | ≈ 0,45 centime | API Anthropic : États-Unis ou mondial ; UE seulement via Bedrock, Vertex ou Azure, tarifs propres | SDK officiel, `IChatClient` |
| Claude Sonnet 5.5 | 2 $ / 10 $ | ≈ 1 centime, plus le raisonnement interne (non désactivable) | idem | idem |
| Modèle ouvert auto-hébergé (Ollama sur GPU) | instance L4 Scaleway 0,79 €/h, ≈ 577 €/mois en continu | fixe | UE, maîtrise complète | OllamaSharp, `IChatClient` |

1. **API Mistral hébergée en UE** — traitement en Europe par un fournisseur européen, réglage simple ;
   coût négligeable à l'échelle d'une démo. Qualité sur des textes juridiques non mesurée.
2. **API Anthropic (Claude)** — intégration .NET officielle ; mais pas de traitement en UE par l'API directe :
   il faudrait un compte et une configuration sur une plateforme cloud, à d'autres tarifs.
3. **Modèle ouvert auto-hébergé** — rentable seulement au-delà d'environ 80 000 questions par mois face à
   Mistral Medium ; démarrage à froid de plusieurs dizaines de secondes s'il s'éteint au repos ; un serveur
   de plus à sécuriser (Ollama n'a pas d'authentification). Comparaison chiffrée prévue dans l'ADR-012 (S6).
4. **Petit modèle local via Ollama pour le développement** (prévu par le plan) — sur le CPU du poste,
   génération estimée à environ une minute par réponse (non mesuré) : le temps perdu coûte plus que l'API.

## Décision

Mistral via son API, **inférence UE**, derrière `IChatClient` :

- **démo : Mistral Medium 3.5**, par prudence tant que la qualité n'est pas mesurée ;
- **développement : Mistral Small 4** (pas de modèle local) ;
- **en fin de semaine 2 (session 5)**, passer les mêmes questions aux deux modèles et garder le plus petit qui
  tient la qualité.

Critères de la mesure : citations inventées (vérification automatique), « je ne sais pas » à bon escient
(questions sans réponse dans le corpus), **fidélité au texte** (relecture humaine, en priorité sur les articles
à exceptions comme l'article 5), latence et coût par question.

## Conséquences

- Le nom du modèle, l'adresse de l'API et la région sont en configuration ; la clé d'API dans
  `dotnet user-secrets` en local, puis dans Key Vault (S6). Aucune clé dans le dépôt.
- Coût attendu : quelques euros par mois pour la démo, hors abus ; le plafond de dépenses chez Mistral
  est à poser en S3.
- Le panneau « sous le capot » affiche le modèle utilisé, les tokens et le coût de chaque question.
- Fournisseur unique pour la génération : si l'API Mistral est indisponible, la démo ne répond plus.
  Acceptable pour une démo ; à reconsidérer avec l'observabilité (S8).
- Vérifié le 2026-10-05 (S2, session 2) : `IChatClient` de Microsoft.Extensions.AI (implémentation OpenAI)
  fonctionne avec `https://api.eu.mistral.ai/v1`, l'usage (tokens d'entrée et de sortie) remonte, et les deux
  modèles sont servis en UE sous les versions `mistral-small-2603` et `mistral-medium-2604`.
  Coût mesuré sur une question : 0,06 centime (Small), 0,67 centime (Medium), pour ≈ 1 800 tokens en entrée.
- **Conditions de révision** : Medium 3.5 ne tient pas la fidélité au texte (essayer Large 3, ou Claude via
  une plateforme UE) ; l'intégration `IChatClient` pose problème ; le volume dépasse quelques dizaines de milliers
  de questions par mois (ADR-012) ; ou un client impose un hébergement maîtrisé de bout en bout.

Sources : [tarifs de l'API Mistral](https://mistral.ai/pricing/api),
[tarifs Anthropic](https://platform.claude.com/docs/en/about-claude/pricing.md),
[tarifs GPU Scaleway](https://www.scaleway.com/en/pricing/gpu/), relevés le 2026-10-05.
