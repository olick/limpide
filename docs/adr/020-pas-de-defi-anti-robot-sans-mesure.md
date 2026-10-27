# ADR-020 : Pas de défi anti-robot tant qu'aucun abus n'est mesuré ; alerte quand le quota du jour est atteint

- **Statut** : accepté
- **Date** : 2026-10-27

## Contexte

Le plan de la S3 prévoyait « un défi invisible (type Turnstile) avant l'envoi d'une question », reporté en S7 puis en
S8 (session 5). La démo publique est déjà protégée (S3, session 4) :

- **10 questions par heure et par adresse IP**, **60 par jour** au total (`QuestionQuota`, en mémoire) ;
- **plafond de 10 € par mois** chez Mistral pour l'espace de la démo (ADR-016) ;
- deux **alertes de budget** sur `answer_log` (S8, session 3).

Le coût est donc borné trois fois. Ce qu'un robot peut encore faire : épuiser les 60 questions du jour, et priver les
vrais visiteurs de la démo jusqu'à minuit UTC. C'est un risque de **disponibilité**, pas de coût.

Avec Blazor Server, une question part par la connexion WebSocket que le JavaScript de la page ouvre : un simple
`curl` ne peut pas en poser, il faut un navigateur, éventuellement piloté par un programme.

Rien ne mesurait jusqu'ici si ce risque se réalise : une journée saturée était muette.

## Options envisagées

1. **Mesurer d'abord** : pas de défi ; une alerte quand le quota du jour est atteint, et la décision reprise si elle
   montre un abus.
2. **ALTCHA**, preuve de travail auto-hébergée (logiciel libre) : vérifiée par l'application, sans tiers ni cookie.
   Freine peu un robot déterminé : environ une seconde de calcul par question, rien pour 60 questions.
3. **Cloudflare Turnstile** : le plus efficace contre les navigateurs pilotés, mais un tiers (américain) voit chaque
   visiteur ; compte, mention d'information et registre des traitements à reprendre.

## Décision

**Option 1, décidée avec Alexandre le 2026-10-27.** Alerte Grafana « Quota du jour de la démo atteint » : au moins une
question refusée par le quota global (`outcome = 'quota-journalier'` dans `answer_log`) depuis minuit UTC ; un message
Discord par journée saturée, qui dit comment distinguer un robot d'un vrai succès (visites dans Loki).

Si l'alerte montre un abus : **ALTCHA** d'abord (sans tiers, cohérent avec la mention d'information et l'angle du
projet) ; Turnstile seulement si un robot passe la preuve de travail.

## Mesures (2026-10-27, poste)

- Quota global ramené à 1 (`--Protection:GlobalPerDay=1`) : deuxième question refusée, message « La démo a atteint
  son nombre maximal de questions pour aujourd'hui… », ligne `quota-journalier` dans `answer_log`, alerte en
  « Firing » à l'évaluation suivante.
- Quota par visiteur ramené à 1 : message « Limite atteinte : 1 questions par heure et par visiteur… » (point reporté
  depuis la S3 : « test du message à la 11e question »).

## Conséquences

- Aucune dépendance ni donnée envoyée à un tiers de plus ; la mention d'information ne change pas.
- La démo reste exposée à un robot qui l'épuise chaque jour ; l'alerte le dit le jour même, pas avant.
- Le quota est en mémoire : un redémarrage de `web` (chaque déploiement) le remet à zéro. Un robot n'y gagne rien de
  plus que quelques questions ; à revoir si l'alerte se déclenche souvent. L'alerte décrit la journée, pas l'instant :
  après un redémarrage, le quota repart de zéro mais les refus déjà journalisés la gardent active jusqu'à minuit UTC
  (deux messages au plus par jour, `repeat_interval` de 12 h).
- Le message par visiteur accorde mal « 1 questions » : sans effet en production (10).
