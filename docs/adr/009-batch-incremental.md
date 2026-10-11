# ADR-009 : Mise à jour du corpus en batch incrémental hebdomadaire, pas en flux continu

- **Statut** : accepté
- **Date** : 2026-10-11

## Contexte

Le corpus est fait de textes officiels publiés : l'AI Act (règlement, version consolidée sur EUR-Lex) et la série
« Les fiches pratiques IA » de la CNIL (17 pages). Il faut le tenir à jour sans intervention, et savoir qu'il l'est.

Mesuré du 2026-09-29 au 2026-10-11 (13 jours, collecte manuelle puis DAG Airflow) :

| | AI Act | CNIL |
|---|---|---|
| Versions brutes collectées | 1 | 39 |
| Dont textes réellement différents | 0 | 0 (*) |
| Fausses nouvelles versions (HTML changé, texte identique) | 0 | au moins 10 explicitement écartées, jusqu'à 3 par jour |

(*) Les seuls changements de texte observés viennent de nos propres changements de code (extracteur `cnil-html/2`),
pas des sources.

Durée d'une exécution du DAG (poste de développement, CPU) : **11 à 19 s** quand rien ne change ; 404 s pour ajouter
15 pages ; 555 s pour revectoriser toute la CNIL après un changement d'extracteur.

Rythme des sources : un règlement européen évolue par actes modificatifs, publiés et applicables à des dates connues
des mois à l'avance ; la CNIL a publié ses fiches IA par séries (2024, puis finalisation en juillet 2025).

## Options envisagées

1. **Flux continu** (*streaming* : Kafka ou équivalent, chaque changement traité à son arrivée) — fraîcheur à la
   minute ; mais aucune source ne publie d'événements : il faudrait interroger les sites en continu (contraire à la
   collecte polie, `User-Agent` explicite et pause entre requêtes), et exploiter un composant de plus (courtier de
   messages, consommateurs, reprise sur erreur) pour des changements qui arrivent quelques fois par an.
2. **Batch complet** (tout retraiter à chaque passage) — simple, mais 10 minutes de vectorisation au moins à chaque
   fois, et chaque passage remplace la version publiée sans raison.
3. **Batch incrémental planifié** — un passage régulier ne retraite que ce qui a changé : empreinte du fichier brut
   (`fetch`), empreinte du texte extrait (`extract`, fausses versions écartées), versions d'extracteur et de découpeur
   (`chunk`), modèle d'embedding (`embed`).

## Décision

Option 3 : **batch incrémental, chaque lundi à 4 h UTC** (DAG `ingestion`, ADR-007), chaque étape idempotente
(ADR-003), nouvelle version publiée seulement si elle est validée et prête (ADR-008).

## Conséquences

- **Fraîcheur garantie : une semaine au plus.** Suffisant pour des textes dont les changements sont annoncés des mois
  à l'avance ; un changement urgent se traite en déclenchant le DAG à la main (une commande ou un clic).
- **Coût négligeable** : 11 à 19 s de calcul par semaine sans changement ; pas de composant à exploiter en continu.
- **Sobriété et politesse** : 18 requêtes par semaine vers les sites sources, avec une pause entre chacune.
- **Rien de perdu à attendre** : une version collectée n'est publiée qu'après contrôles ; un passage plus fréquent
  n'apporterait que davantage de fausses versions CNIL à écarter.
- **Conditions de révision** : une source qui change plusieurs fois par jour **en texte** (jurisprudence,
  actualités) ; une source qui publie des événements (flux, abonnement) ; un engagement de fraîcheur inférieur à un
  jour. Dans les deux premiers cas, rapprocher d'abord la planification (quotidienne) avant d'envisager un flux.
- Limite actuelle, indépendante de ce choix : en production, le corpus n'est mis à jour par aucun pipeline (Airflow
  tourne sur le poste de développement) ; c'est l'ADR sur l'hébergement d'Airflow (S6).
