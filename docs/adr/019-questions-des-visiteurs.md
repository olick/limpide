# ADR-019 : Questions des visiteurs — texte conservé 30 jours dans une table à part, masqué, sauf opposition

- **Statut** : accepté
- **Date** : 2026-10-21

## Contexte

Depuis la S8 (ADR-018), chaque question laisse une ligne dans `answer_log` (12 mois) et des traces et messages dans la
supervision (14 jours), **sans son texte** : seulement des mesures. Les questions peuvent contenir des données
personnelles (un visiteur décrit sa situation, laisse son adresse).

Ce qui manque sans le texte (constaté le 2026-10-19) : les journaux disent qu'une question a été hors périmètre ou
lente, pas ce qu'elle demandait. On ne sait pas ce que cherchent les visiteurs, ni si le corpus y répond, et le jeu
d'évaluation n'a que des questions écrites par l'auteur (S10 : questions réelles comme cas de test).

Deux constats de la session 5 :

- la démo n'avait **aucune mention d'information** : seule la phrase du formulaire de retours informait le visiteur,
  alors que `answer_log`, les journaux des visites et l'envoi de la question à OVHcloud et Mistral existaient déjà ;
- les journaux bruts des conteneurs ont pu garder le texte de questions (message d'erreur, avant la PR #24) : purge et
  plafond traités à part (PR #42, appliqués sur le serveur après sa fusion).

## Options envisagées

1. **Aucun texte** (statu quo) : la minimisation la plus simple ; le formulaire de retours (case cochée) reste le
   seul moyen de voir une question. Avec ≈ 6 questions en deux jours, peu de perte, mais aucune matière pour la S10.
2. **Question seule, table à part liée à `answer_log`**, durée courte, masquage, lisible dans le back office.
3. **Question dans Loki**, avec les messages de la visite (14 jours) : le plus rapide, mais le texte se mêle aux
   journaux techniques, Grafana (exposé sur Internet) y donne accès, et Loki n'a pas de suppression ciblée.

Pour la base légale : le **consentement** (case à cocher, comme les retours) laisse la table presque vide ;
l'**intérêt légitime avec opposition** (case « Ne pas conserver ») est le schéma admis pour l'amélioration d'un
service, à condition d'informer au moment de la collecte et de rendre l'opposition simple.

## Décision

**Option 2**, décidée avec Alexandre le 2026-10-21 :

- **Table `question_log`** (migration 0009) : une ligne par question conservée, clé = la ligne d'`answer_log`
  (suppression en cascade), date, texte, types de données masquées. Ni la réponse, ni l'IP, ni identifiant de visite.
- **30 jours**, purge à chaque nouvelle question, conservée ou non (`AnswerLogStore`) ; la ligne d'`answer_log`, sans
  texte, reste 12 mois. Les sauvegardes de la base (7 jours, plus la copie quotidienne d'OVHcloud, gardée 1 jour)
  prolongent la conservation de 8 jours au plus : dit dans la mention.
- **Masquage avant l'écriture** (`PersonalDataMask`, Core) : adresses électroniques, IBAN, numéros de sécurité
  sociale, numéros de téléphone, remplacés par leur type. Par motifs : ni les noms ni les adresses postales ; la
  mention demande de ne pas en écrire, et le back office permet de supprimer une question.
- **Opposition** : case « Ne pas conserver ma question » sous le champ, décochée par défaut, avec l'information juste
  au-dessus et un lien vers la page « Vos données » (`/confidentialite`). Une question refusée par le quota n'est
  jamais conservée.
- **Accès** : le back office seulement (page « Questions », suppression notée dans `admin_actions` sans le texte).
  Aucun droit pour `lecture_supervision` : Grafana ne la lit pas.
- **Jamais dans la supervision** : la règle d'ADR-018 (ni texte dans les traces, les messages, `answer_log`) reste
  entière ; le texte n'existe qu'en base, dans cette table.
- **Mention d'information** (`/confidentialite`, lien en bas de chaque page ; sous le champ et dans le panneau
  latéral depuis l'ADR-022) : responsable, finalités, destinataires
  (OVHcloud AI Endpoints, Mistral AI), durées de chaque traitement, droits, contact (`contact@limpide-ia.fr`),
  réclamation à la CNIL.

## Conséquences

- Les questions réelles deviennent lisibles pour l'auteur : matière pour le corpus (questions sans réponse) et pour le
  jeu d'évaluation, toujours par Git et relecture (S10) ; une question n'y entre jamais automatiquement.
- Un traitement de plus, à inscrire au registre des traitements (S9) avec les autres (`answer_log`, journaux des
  visites, retours).
- Le masquage est un filet, pas une garantie : la relecture des questions dans le back office est le second ; le
  nombre de masquages par type (`masked`) dit s'il sert.
- Chaque durée affichée par la page « Vos données » vient du code ou de la configuration (`KeptQuestion`,
  `AnswerLogEntry`, `VisitorFeedback`, `supervision/`) : en changer une, c'est changer la page.
- **Conditions de révision** : une donnée personnelle trouvée dans la table malgré le masquage (élargir les motifs,
  ou passer au consentement) ; trafic réel qui rend la relecture impossible (réduire la durée) ; nouvelle finalité
  (par exemple un cas de test tiré automatiquement d'une question) : la mention d'information d'abord.
