# Registre des risques IA

Ce qui peut mal tourner dans le comportement de Limpide, ce qui le limite, et où le vérifier. Rédigé le
**2026-11-20**. Les risques sur les données personnelles sont traités dans `rgpd.md`, ceux de l'infrastructure dans
`deploy/README.md` et ADR-018 ; ils ne sont repris ici que lorsqu'ils touchent la réponse.

**Échelle** (risque **résiduel**, mesures en place) :

- **Probabilité** : faible (pas observé, ou bloqué par un contrôle automatique) ; moyenne (observé en évaluation, ou
  possible sans contrôle qui l'arrête) ; élevée (observé en production).
- **Impact** : faible (gêne, coût minime) ; moyen (réponse fausse ou trompeuse, vérifiable dans les passages
  affichés) ; élevé (atteinte à une personne, à une licence, ou réponse fausse difficile à déceler).

**Limite de lecture** : un garde-fou appliqué **après** la rédaction (consigne divulguée, qualification juridique,
citation inventée, absence de citation) **signale** la réponse au visiteur, il ne la bloque pas
(`src/Limpide.Core/Answering/AnswerService.cs`, méthode `Guardrails`). Seuls deux contrôles empêchent l'appel au
modèle : le seuil de pertinence et le contrôle de la question.

## Registre

| # | Risque | Prob. | Impact | Mesures en place | Preuve | Limite connue |
|---|---|---|---|---|---|---|
| R1 | **Réponse inventée** (hallucination) | moyenne | moyen | seuil de pertinence 0,60 : sous ce score, réponse fixe sans appel au modèle (ADR-005) ; consignes `answer/8` (citer chaque affirmation, dire « je ne sais pas ») ; température 0 ; citations vérifiées sans IA (`Citations.cs`) ; passages cités affichés à l'identique sous la réponse | évaluation de référence du 2026-11-06 22 h 51 : 52/58 vérifiées, 3 réponses infidèles sur 36 jugées (`eval/results/2026-11-06-2251-mistral-medium-2604.md`) ; barrière `gate` en CI : part infidèle ≤ 0,30, citations attendues ≥ 0,86 (ADR-014, `appsettings.shared.json`) | le juge de fidélité est un modèle, le même que celui qui rédige, et varie plus que sa marge (journal de la S9, 2026-11-06) : mesure agrégée, jamais par réponse |
| R2 | **Conseil juridique implicite** : la réponse tranche la situation du visiteur | moyenne | élevé | règle 5 des consignes (ne jamais qualifier) ; bandeau permanent « ce n'est pas un conseil juridique » (article 50) ; repérage heuristique `qualification-juridique` (`LegalQualification.cs`), signalé au visiteur | 3 questions du jeu (`t05`, `j01`, `j02`), à relire à chaque évaluation : « peut indiquer les textes concernés, ne doit pas trancher » | heuristique phrase par phrase, et signalement seulement ; les trois questions sont relues à la main, pas jugées automatiquement |
| R3 | **Texte source dénaturé** (licences : CC-BY-ND pour la CNIL, sens non dénaturé pour EUR-Lex) | faible | élevé | extraits reproduits à l'identique, avec source et licence ; réponse visuellement distincte, présentée comme une formulation de l'assistant | `registre-des-sources.md` ; `.claude/rules/interfaces-web.md` ; `ThreadAnswer.razor` | la réponse elle-même peut mal résumer un passage : c'est R1, vérifiable dans l'extrait affiché |
| R4 | **Corpus périmé** : la réponse s'appuie sur un texte qui n'est plus en vigueur | élevée | élevé | collecte hebdomadaire et versions (ADR-008, ADR-009) ; AI Act servi en version consolidée (ADR-023) ; faits attendus (dates, montants) contrôlés par la barrière | **survenu** : jusqu'au 2026-11-06, la démo annonçait les obligations du haut risque au 2 août 2026 au lieu du 2 décembre 2027 (journal de la S9, « AI Act consolidé ») ; règle `expectedFactsMissing` ≤ 1 | la veille des actes modificatifs est manuelle : un nouveau règlement modificatif ne déclenche rien tant qu'EUR-Lex n'a pas republié la consolidation |
| R5 | **Version défectueuse d'une source** (page tronquée, format changé) | faible | moyen | contrôles qualité déterministes avant publication, quarantaine : la version publiée reste servie (`QualityValidator.cs`, ADR-008) | sources de panne simulées (`pipelines/airflow/demo/quarantaine.sh`, démonstration de la S5) ; quarantaine réelle du 2026-11-06 (écart de 32 %, seuil 30 %), due à un changement voulu (considérants retirés) et levée après examen : le contrôle a joué, aucune version défectueuse n'a été servie | contrôles de structure et de volume : un texte modifié sur le fond mais bien formé passe |
| R6 | **Injection de prompt** (la question s'adresse aux consignes) | moyenne | moyen | contrôle de la question avant la recherche, par règles : ni recherche ni appel au modèle (`PromptInjection.cs`, ADR-021) ; fuite des consignes repérée dans la réponse (`PromptLeak.cs`) | 8 questions d'injection dans le jeu ; barrière : `injectionsFailed` = 0 et `promptLeaks` = 0 ; production : 2 questions bloquées sur 83, du 2026-10-19 au 2026-11-19 (`answer_log`, relevé en lecture seule le 2026-11-19) | des règles se contournent (paraphrase, autre langue) ; la fuite est signalée, pas bloquée |
| R7 | **Régression à une mise à jour** (consignes, modèle, recherche, corpus) | faible | moyen | consignes versionnées, figées par empreinte (`PromptFilesTests.cs`) ; évaluation complète avant la production dans un environnement jetable, barrière bloquante (ADR-016) ; déploiement approuvé à la main (ADR-015) | `.github/workflows/` ; rapports d'évaluation de chaque déploiement | le jeu compte 58 questions : une régression hors de ce qu'il couvre passe |
| R8 | **Dépendance au fournisseur du modèle** (prix, conditions, retrait d'un modèle) | moyenne | moyen | abstraction `IChatClient` (ADR-004) ; changement de modèle mesuré par la même évaluation (Small 4 essayé) ; modèle daté (`mistral-medium-2604`), pas un alias mouvant | ADR-004 ; `eval/results/` (Medium et Small) | aucun secours automatique ; vectorisation chez OVHcloud sans secours (ADR-012), et changer de modèle d'embedding impose de recalibrer le seuil (ADR-017) |
| R9 | **Indisponibilité ou lenteur d'un fournisseur** | élevée | faible | 60 s au plus par réponse, puis un message d'erreur ; alertes « Questions en erreur » et « Réponses anormalement lentes » (Discord) | `supervision/grafana/provisioning/alerting/alertes.yaml` ; **observé** : 15 s par question chez OVHcloud le 2026-10-28 (points ouverts de la S8) | pas de file d'attente ni de secours : la démo est indisponible tant que le fournisseur l'est |
| R10 | **Dérive des coûts** (abus, boucle, changement de tarif) | faible | faible | quota de 10 questions par heure et par adresse IP, 60 par jour ; plafonds chez Mistral par espace ; deux alertes de budget ; coût par question dans la barrière | `docs/finops.md` (pire cas ≈ 25 € par mois) ; `QuestionQuota.cs` ; ADR-020 | aucun plafond chez OVHcloud AI Endpoints ; le quota par adresse IP se contourne en changeant d'adresse |
| R11 | **Question personnelle exposée** : le visiteur écrit une donnée personnelle, envoyée aux fournisseurs | moyenne | moyen | avertissement sous le champ ; masquage avant conservation ; conservation de 30 jours, avec opposition ; entraînement refusé chez Mistral | `rgpd.md` (T1, T2, écarts) ; `PersonalDataMask.cs` | noms et adresses postales non détectés ; la question part non masquée chez les deux fournisseurs (nécessaire à la réponse) |
| R12 | **Confiance excessive** : la réponse est prise pour le texte ou pour un avis | moyenne | moyen | réponse distincte des textes (couleur, police, libellé) ; passages cités affichés en entier ; « Sous le capot » : score, garde-fous, coût ; « je ne sais pas » préféré à une réponse faible | ADR-022 ; `ThreadAnswer.razor` ; page « Résultats d'évaluation » | aucune mesure de la compréhension réelle par des visiteurs : validation par des non-techniciens reportée à la fin du plan |
| R13 | **Texte généré non marqué pour les machines** (AI Act, article 50.2) | élevée (écart constaté) | faible | marquage pour les personnes seulement (article 50.1) | `classification-ai-act.md` | **écart** : aucun marquage lisible par machine ; mesure proposée, à décider |

## Écarts et décisions attendues

| Écart | Risque | Proposition | Qui |
|---|---|---|---|
| Garde-fous après rédaction : signalement, pas blocage | R1, R2, R6 | garder ce choix (une réponse signalée reste vérifiable, et le blocage sur heuristique produirait des faux positifs) ; le réexaminer si le journal des réponses montre des déclenchements en production | Alexandre |
| Marquage lisible par machine absent | R13 | marquer le bloc de réponse dans le HTML (attribut et métadonnées) ; suivre le code de bonne pratique de l'article 50.7 | Alexandre |
| Veille des actes modificatifs manuelle | R4 | rappel mensuel de vérifier la page des consolidations EUR-Lex, tant qu'aucune alerte automatique n'existe | Alexandre |

## Revue

À chaque nouvelle ADR qui touche la réponse, à chaque incident (`procedure-incident.md`, retour d'expérience), et à
chaque changement de modèle. Un risque passe de « moyenne » à « élevée » dès qu'il est observé en production.
