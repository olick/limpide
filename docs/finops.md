# FinOps et GreenOps

Ce que coûte Limpide, d'où vient la dépense, ce qui la borne, et un ordre de grandeur de son empreinte énergétique.
Relevés du 2026-10-20 (S8, session 3). Montants en euros, comme les factures ; les coûts estimés par l'application
le sont aussi depuis le 2026-10-20 (ADR-004, révision).

## En bref

- **≈ 5,10 € par mois de coûts fixes** (serveur et nom de domaine), **≈ 7 à 12 € par mois au total** au rythme
  actuel.
- **Le poste variable, c'est Mistral, et surtout l'évaluation en CI** : 93 % de la dépense Mistral depuis le 19
  octobre. La démo coûte ≈ 0,44 centime par question.
- **La dépense est bornée** : quota de la démo, plafonds chez Mistral par espace de travail, deux alertes de budget.
- **Énergie** : ≈ 0,14 Wh par réponse rédigée (estimation EcoLogits). Au trafic actuel, le serveur toujours allumé
  pèse beaucoup plus que les appels au modèle.

## Coût mensuel réel (octobre 2026, du 1er au 20)

| Poste | Fournisseur | Montant | Ramené au mois |
|---|---|---|---|
| Serveur de la démo (VPS-1) | OVHcloud | 55 € pour 12 mois | 4,58 € |
| Nom de domaine `limpide-ia.fr` | OVHcloud | 6 € par an | 0,50 € |
| Vectorisation (AI Endpoints) | OVHcloud | 0,09 € (Qwen3-Embedding-8B, 942 907 tokens) | ≈ 0,10 € |
| Essais de modèles de rédaction (AI Endpoints) | OVHcloud | 0,29 € (gpt-oss-120b, Llama 3.3 70B, Qwen 3.5 397B) | ponctuel |
| Rédaction et juge (API, à l'usage) | Mistral | 5,46 € | ≈ 8 € |
| Vitrine et socle Azure | Microsoft | < 0,01 € (budget « Limpide » : 10 € par mois) | < 0,01 € sans recréation de la vitrine |

Mistral, du 1er au 18 octobre (≈ 4,4 € : relevés du matin du 20, 5,46 € au total moins 1,03 € depuis le 19) : construction de l'évaluation (S7), calibration du juge, comparaisons de
modèles, alors toutes dans l'espace de travail par défaut. Depuis le 19, chaque usage a son espace :

| Espace de travail Mistral (depuis le 19) | Requêtes | Coût | Part |
|---|---|---|---|
| `limpide-evaluation` (CI) | 388 | 1,20 € | 93 % |
| Par défaut (démo et poste) | 21 | 0,09 € | 7 % |

Une évaluation complète coûte **≈ 0,26 € pour 78 requêtes** (rapport `2026-10-18-1925` : 44 rédactions et 34
jugements, 0,305 $ convertis ; ADR-016). Les 388 requêtes de la CI depuis le 19 en font donc ≈ 5 ; les autres
exécutions l'ont sautée (`deploy/evaluation-requise.sh`).

## Coût unitaire

| Opération | Coût | Détail |
|---|---|---|
| Question de la démo, avec réponse | ≈ 0,44 centime | Mistral Medium 3.5 : ≈ 1 800 tokens lus (question, 5 passages, consigne), ≈ 250 écrits, à 1,4025 € et 7,0125 € par million |
| Question hors sujet (sous le seuil) | 0 € chez Mistral | le modèle n'est pas appelé |
| Vectorisation d'une question | ≈ 0,0003 centime | ≈ 30 tokens à ≈ 0,10 € par million |
| Évaluation complète en CI | ≈ 0,26 € | 50 questions : 44 rédactions (6 arrêtées par le seuil) et 34 jugements |
| Ingestion hebdomadaire sans changement | 0 € | même empreinte du contenu brut : rien n'est revectorisé |
| Revectorisation complète du corpus | ≈ 0,03 € | 1 130 passages, ≈ 1,18 million de caractères avec les titres, ≈ 320 000 tokens |

Le coût estimé par l'application se vérifie contre la facture : en production, les 5 réponses du 19 et du 20 octobre
(9 051 tokens lus, 1 272 écrits) donnent exactement le montant enregistré dans `answer_log` ; la page Usage de
Mistral, par espace de travail, en donne le total.

## Ce qui borne la dépense

| Garde-fou | Où | Effet |
|---|---|---|
| Quota | `Protection` (`src/Limpide.Web/appsettings.json`) | 10 questions par heure et par visiteur, 60 par jour au total : la démo dépense au plus ≈ 0,26 € par jour, ≈ 8 € par mois |
| Plafonds Mistral | console Mistral (ADR-016) | organisation 20 €, espace de la démo 10 €, `limpide-evaluation` 10 € par mois : au-delà, Mistral refuse les appels |
| Alerte « dépense anormale sur 24 h » | Grafana, `answer_log` | > 0,50 € en 24 h : le quota ne protège plus (ne doit jamais se déclencher) |
| Alerte « budget à moitié consommé » | Grafana, `answer_log` | > 5 € depuis le début du mois pour la démo |
| Seuil du `gate` | `EvaluationGate` (ADR-014) | coût de rédaction ≤ 0,0068 € par question : une consigne gonflée ou un modèle plus cher bloque la PR |
| Évaluation seulement si nécessaire | `deploy/evaluation-requise.sh` | une PR qui ne touche ni le code, ni les consignes, ni le corpus ne paie pas d'évaluation ; un nouveau push annule l'évaluation en cours |
| Budget Azure | budget « Limpide », 10 € par mois | alerte seulement (50, 80, 100 %) ; la vitrine est détruite quand elle ne sert pas |

**Ce qui n'est pas couvert** : les alertes de Grafana ne voient que la démo. L'évaluation et les essais du poste ne
sont visibles que dans la console Mistral (aucune API publique de lecture de la dépense trouvée) et bornés par leur
plafond. Aucun plafond de dépense n'est configuré chez OVHcloud AI Endpoints ; la vectorisation de la démo y est
bornée par le quota.

## Projection selon le trafic

Hypothèses : coûts unitaires ci-dessus, vectorisation négligeable, une revectorisation complète par mois au plus.

| Scénario | Questions par jour | Évaluations par mois | Mistral (démo + évaluation) | Total par mois |
|---|---|---|---|---|
| Calme (aujourd'hui) | 2 | 5 | 0,26 € + 1,30 € | ≈ 6,7 € |
| Rendez-vous et entretiens | 10 | 20 | 1,32 € + 5,20 € | ≈ 11,6 € |
| Quota atteint chaque jour | 60 | 40 et plus | 7,9 € + 10 € (plafond) | ≈ 23 € |
| Au-delà | — | — | plafonds atteints : la démo ou l'évaluation s'arrête | ≈ 25 € au plus |

Le dernier scénario est le pire cas garanti par les plafonds (5,10 € de fixe + 20 € chez Mistral). Azure n'y
figure pas : < 0,01 € en octobre, la vitrine étant détruite quand elle ne sert pas ; recréée à la demande, ≈ 0,10 €
par recréation (ADR-016).
Au-delà de quelques centaines de questions par jour, il faudrait revoir le quota, le plafond et le modèle : c'est
l'objet de la mise à l'échelle, après le plan.

## Effet du seuil de pertinence

Le seuil (`Generation:MinScore`, 0,60) existe d'abord comme garde-fou : sous ce score, aucun passage ne traite de la
question, et Limpide répond « je ne sais pas » sans appeler le modèle. L'économie est un effet secondaire.

- **Sur le jeu d'évaluation** (rapport `2026-10-18-1925`, 50 questions) : **6 questions arrêtées (12 %)**, toutes
  des questions hors sujet ; 44 appels au modèle. Économie : ≈ 0,38 centime, ≈ 3 s et ≈ 0,14 Wh par question
  arrêtée (coût moyen d'une rédaction de ce rapport, 0,0044 $ convertis ; même ordre que les 0,44 centime d'une
  question de la démo), soit ≈ 12 % du coût de rédaction d'une évaluation.
- **En production** : 1 question sur 6 depuis le 19. Trop peu pour conclure ; le
  tableau de bord « qualité et dérive » suit cette part dans le temps.
- Les 8 autres questions de refus du jeu dépassent le seuil (sujet proche, réponse absente des textes) : c'est le
  modèle qui dit « je ne sais pas », au prix normal.

## Empreinte énergétique (ordre de grandeur)

**Rédaction d'une réponse** (EcoLogits 0.11.3, `mistral-medium-2604`, 250 tokens écrits, 2,5 s) :

| Mix électrique supposé | Énergie | Gaz à effet de serre | dont fabrication du matériel | Eau |
|---|---|---|---|---|
| France | 0,14 Wh | 0,007 g CO₂e | 0,002 g | 0,5 mL |
| Allemagne | 0,14 Wh | 0,05 g CO₂e | 0,002 g | 0,3 mL |
| Moyenne mondiale | 0,14 Wh | 0,07 g CO₂e | 0,002 g | 0,6 mL |

**À comparer avec l'analyse de cycle de vie publiée par Mistral** (juillet 2025, avec Carbone 4 et l'ADEME, relue
par Resilio et Hubblo) : 1,14 g CO₂e et 45 mL d'eau pour une réponse de 400 tokens de Mistral Large 2 dans Le Chat.
L'écart (×17 à ×150) tient au modèle (Large 2, 123 milliards de paramètres, contre une architecture non publiée pour
Medium 3.5), au périmètre (analyse complète de Mistral contre modèle d'EcoLogits limité à l'inférence et au
matériel) et au mix électrique. **L'enseignement est là** : l'ordre de grandeur dépend surtout des hypothèses, et ni
la taille de Medium 3.5 ni la localisation de ses serveurs ne sont publiées.

**Le reste** :

- vectorisation : ≈ 30 tokens par question sur un modèle de 8 milliards de paramètres, non modélisée par
  EcoLogits ; négligeable devant la rédaction ;
- évaluation : 78 requêtes, soit ≈ 11 Wh au plus par évaluation (les jugements écrivent moins que 250 tokens) ;
- **serveur** : allumé en permanence, quel que soit le trafic. Sa part d'une machine partagée n'est pas publiée ;
  avec 5 à 10 W, il consommerait 3,6 à 7,2 kWh par mois, l'équivalent de 26 000 à 52 000 réponses rédigées. À
  relever dans le calculateur d'empreinte carbone de l'espace client OVHcloud.

Conséquence : au trafic d'une démo, réduire l'empreinte passe par le serveur (dimensionnement, mutualisation) bien
plus que par les appels au modèle ; le seuil et le choix du modèle comptent quand le trafic grandit.

## Leviers d'optimisation

| Levier | État | Gain | Contrepartie |
|---|---|---|---|
| Évaluer seulement les PR qui changent la qualité | fait (S7) | 9 exécutions sur 15 sans évaluation les 19 et 20 octobre | une PR mal classée passerait sans évaluation : liste des fichiers en cause tenue à jour |
| Seuil de pertinence | fait (ADR-005, recalibré par l'ADR-017) | 12 % des appels sur le jeu d'évaluation | à recalibrer à chaque changement de modèle d'embedding |
| Plafonds par espace de travail | fait (S7) | l'évaluation ne peut plus consommer le budget de la démo | exposition maximale portée de 10 € à 20 € |
| Mise en cache de l'entrée chez Mistral | à mesurer | entrée en cache 10 fois moins chère (0,14 € contre 1,40 € par million) ; la consigne est un préfixe fixe | vérifier que l'API l'applique et le rapporte dans l'usage ; adapter le calcul du coût |
| Modèle moins cher (Small 4, ≈ 10 fois moins cher) | écarté | ≈ 0,04 centime par question | fidélité jugée insuffisante (0,41 de réponses infidèles, ADR-014) |
| Moins de passages fournis (5 → 3) | non essayé | deux passages sur cinq en moins dans les tokens lus | qualité à mesurer avant tout changement (ADR-005, ADR-010) |
| Réponses en cache pour les questions d'exemple | non essayé | les questions cliquées le plus souvent ne coûteraient plus rien | réponse figée jusqu'à la prochaine version du corpus ; complexité ajoutée pour un gain faible au trafic actuel |

## Limites

- Coûts unitaires mesurés sur une question type (≈ 1 800 tokens lus) : une question qui ramène de longs passages
  coûte plus.
- La part de la démo dans l'espace par défaut de Mistral inclut les essais du poste ; seul `answer_log` isole la
  production.
- Énergie : estimations sans mesure possible (serveurs de Mistral et d'OVHcloud non instrumentés) ; à lire comme
  des ordres de grandeur.
- Mois de facturation de Mistral supposé en UTC (alerte mensuelle), à confirmer sur la facture.

Sources : factures et consoles OVHcloud et Mistral (relevés d'Alexandre, 2026-10-20) ;
[tarifs de l'API Mistral](https://docs.mistral.ai/inference/pricing) ;
[EcoLogits](https://ecologits.ai/) (méthode : [inférence des LLM](https://ecologits.ai/latest/methodology/llm_inference/)) ;
[analyse de cycle de vie de Mistral](https://mistral.ai/news/our-contribution-to-a-global-environmental-standard-for-ai).
