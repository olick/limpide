# Observations sur la génération des réponses

Notes prises au fil de l'eau pour la comparaison Mistral Small / Medium (ADR-004, fin de semaine 2).

## Semaine 2, session 2 — premiers essais (2026-10-05)

Point d'accès `https://api.eu.mistral.ai/v1` (inférence UE), consignes `answer/1` puis `answer/2`,
température 0, 5 passages.

**Modèles disponibles en UE** (`GET /v1/models` sur le point d'accès UE) : `mistral-small-2603`
(alias `mistral-small-latest`) et `mistral-medium-2604` (Medium 3.5, alias `mistral-medium-latest`).
On épingle les versions datées : `mistral-medium-latest` est aussi un alias de `magistral-medium-latest`,
et un alias peut changer de modèle sans prévenir, ce qui fausserait les comparaisons.

**Mesures sur « Quelles utilisations de l'IA sont interdites en Europe ? »**

| | Small (`mistral-small-2603`) | Medium (`mistral-medium-2604`) |
|---|---|---|
| Tokens entrée / sortie | 1 811 / 456 | 1 811 / 446 |
| Coût estimé | 0,060 centime | 0,667 centime |
| Génération | 3,8 s | 3,5 s |
| Garde-fous | aucun | aucun |

L'hypothèse de l'ADR-004 (2 500 tokens en entrée) était pessimiste : environ 1 800 avec 5 passages.

**Constats**

- **Format des citations** : les deux modèles écrivent `[P1a]`, `[P2e]` (lettre du point de l'article),
  même quand les consignes l'interdisent (`answer/2`). Ce n'est pas propre à Small. Le contrôle des citations
  l'accepte comme une citation de P1 ; sans cela, toute réponse sur l'article 5 déclenchait « sans-citation ».
  Bon exemple de l'intérêt d'un contrôle déterministe : l'écart a été vu au premier essai.
- **« Je ne sais pas »** : question hors corpus (coût d'une certification ISO 42001), Small répond la phrase exacte,
  en 19 tokens, sans inventer.
- **Fidélité** (lecture rapide d'une seule réponse, Small) : les exceptions à l'identification biométrique en temps
  réel sont mentionnées ; le considérant 24 (exclusion des usages militaires) est cité à bon escient. À confirmer
  sur le jeu de questions de la session 5, en priorité sur les articles à exceptions.
- **Démarrage à froid d'Ollama** : première vectorisation de question à 4,7 s (modèle déchargé après 5 minutes
  d'inactivité), puis environ 200 ms. Argument pour l'ADR-006 (hébergement du modèle d'embedding).
