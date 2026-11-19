# Données personnelles (RGPD)

Registre des traitements de Limpide, fondements, information des visiteurs, droits, sous-traitants, et écarts
connus. Rédigé le **2026-11-19** à partir du code et de la configuration ; **pas relu par un juriste**. La mention
d'information destinée aux visiteurs est la page « Vos données » (`/confidentialite`). Ce document en est la version
de travail, avec le raisonnement.

## Responsable et périmètre

- **Responsable du traitement** : Alexandre Leclerc - EURL, représentée par son gérant. Contact :
  `contact@limpide-ia.fr`. Pas de délégué à la protection des données : rien ne l'impose (article 37 ; ni autorité
  publique, ni suivi à grande échelle, ni données sensibles).
- **Pourquoi un registre** : l'article 30 l'impose. L'exemption des structures de moins de 250 personnes
  (article 30.5) ne s'applique pas, car les traitements de la démo sont continus, pas occasionnels.
- **Personnes concernées** : les visiteurs de la démo publique, et l'administrateur (Alexandre) pour le back office.
  Aucune donnée personnelle dans le corpus (`registre-des-sources.md`).

## Registre des traitements

| # | Traitement | Finalité | Données | Fondement (article 6.1) | Destinataires | Durée |
|---|---|---|---|---|---|---|
| T1 | Répondre à la question | fournir la réponse demandée | texte de la question, tel qu'écrit ; fil de la visite | f, intérêt légitime : rendre le service que le visiteur demande | OVHcloud AI Endpoints (vectorisation) ; Mistral AI (rédaction), seulement si des passages assez proches existent | le temps de la réponse chez Limpide ; fil : la visite ; chez les fournisseurs : selon leurs conditions (voir « Sous-traitants ») |
| T2 | Conserver la question | comprendre ce que demandent les visiteurs, écrire des questions de test relues par une personne | texte de la question, masqué ; types masqués | f, intérêt légitime, avec opposition (case sous le champ) | aucun | 30 jours, plus 8 jours au plus dans les sauvegardes |
| T3 | Journal des réponses | suivre le coût, la qualité et la dérive | issue, scores, durées, tokens, coût, garde-fous, longueur de la question, identifiant de trace | f, intérêt légitime : maîtriser le coût et la qualité des réponses | aucun | 12 mois |
| T4 | Supervision | faire fonctionner et diagnostiquer le service | traces, journaux, métriques, identifiant de visite tiré au hasard | f, intérêt légitime : faire fonctionner et sécuriser le service (considérant 49) | aucun (hébergée sur le serveur) | 14 jours ; métriques : 30 jours |
| T5 | Limiter le nombre de questions | prévenir les abus et maîtriser le coût | adresse IP, en mémoire | f, intérêt légitime : protéger le service et son budget des abus (considérants 47 et 49) | aucun | la fenêtre du quota ; jamais écrite |
| T6 | Recueillir les avis | améliorer Limpide, constituer des questions de test | réponses au formulaire ; question, réponse, modèle et passages cités si le visiteur coche la case | a, consentement, pour la question jointe ; f pour le reste du formulaire, envoyé volontairement | aucun | 12 mois |
| T7 | Administrer | protéger l'accès au back office et à Grafana ; tracer les décisions | identité Microsoft de l'administrateur (identifiant, nom) ; journal des décisions | f, intérêt légitime | Microsoft (connexion) | session ; journal des décisions : **sans limite** (point ouvert) |

Mesures communes : hébergement en France (serveur OVHcloud, ADR-006) ; sauvegardes de 7 jours, plus 1 jour chez
OVHcloud ; purge automatique à chaque nouvelle écriture (`AnswerLogStore`, `FeedbackStore`) ; aucun cookie de mesure
d'audience ni de publicité ; HTTPS entre le visiteur et le serveur (Caddy) ; comptes de base au moindre privilège (`docs/acces.md`).

### Fondements de T1, T3, T4 et T5

La page « Vos données » donne le fondement de T2 et de T6. Ceux de T1, T3, T4 et T5 manquaient : point ouvert depuis
la refonte (journal de la S9). Proposé le 2026-11-19 : l'intérêt légitime (article 6.1.f), pour les quatre, avec
l'intérêt nommé pour chacun (article 13.1.d) dans le registre ci-dessus. À confirmer par Alexandre, puis à écrire sur
la page.

- **Pourquoi pas le contrat (6.1.b)** pour T1 : défendable (le visiteur demande un service, et un contrat peut être
  gratuit), mais la démo n'a ni compte ni conditions d'utilisation. Rien ne matérialise ce contrat, et deux bases
  différentes pour une même question seraient plus difficiles à expliquer sur la page.
- **Pourquoi pas le consentement** : il devrait être demandé avant la première question, et un refus rendrait le
  service impossible (T1, T5) ou aveugle (T3, T4). Le consentement serait contraint, donc pas libre.
- **Mise en balance** :
  - T1 : le visiteur écrit sa question pour obtenir une réponse. L'envoi aux deux fournisseurs est attendu, et dit
    avant l'envoi (ligne sous le champ, page « Vos données »).
  - T3 et T4 : pas de texte, pas d'adresse IP, un identifiant de visite qui ne suit pas d'une visite à l'autre.
    Rattachés à une question 30 jours au plus, à une visite 14 jours au plus (`classification-des-donnees.md`).
  - T5 : l'adresse ne quitte pas la mémoire de l'application. Sans limite, une seule personne pourrait épuiser le
    budget de la démo en quelques minutes (ADR-020).

### Droit d'opposition (article 21)

L'intérêt légitime ouvre au visiteur un droit d'opposition, que la page doit mentionner explicitement (article 21.4).
Réponse prévue, traitement par traitement :

| Traitement | S'opposer, concrètement | Réponse |
|---|---|---|
| T1 Répondre | ne pas poser de question | la seule possible : la réponse suppose l'envoi |
| T2 Conserver la question | case « Ne pas conserver ma question » | immédiate, pour les questions suivantes |
| T3 Journal des réponses | courriel à `contact@limpide-ia.fr` | au cas par cas, et seulement si la question a été conservée (30 jours au plus) : c'est elle qui relie une ligne à un texte ; sinon, la ligne n'est pas retrouvable (article 11) |
| T4 Supervision | courriel | motifs légitimes impérieux à invoquer (article 21.1), à notre analyse : sécurité et diagnostic, 14 jours seulement |
| T5 Limite de questions | courriel | motifs légitimes impérieux à invoquer, à notre analyse : sans limite, le service et son budget sont exposés à tout abus |

### Analyse d'impact (AIPD)

**Non requise à notre analyse.** Les lignes directrices du CEPD la demandent quand deux critères sur neuf sont
réunis. Ici, un seul l'est peut-être : l'usage d'une technologie nouvelle (un modèle de langage). Les autres ne le sont
pas : ni évaluation ou notation des personnes, ni décision automatisée produisant un effet, ni surveillance
systématique, ni données sensibles, ni grande échelle, ni croisement de fichiers, ni personnes vulnérables ciblées, ni
obstacle à l'exercice d'un droit ou d'un service. À reprendre si un critère apparaît. Exemples : des questions
réelles versées automatiquement dans un jeu d'évaluation, ou un historique de conversation renvoyé au modèle
(ADR-022).

## Information des visiteurs

Deux niveaux, comme le recommande la CNIL :

1. **Au moment de la collecte** : une ligne sous le champ dit la durée de conservation, porte la case d'opposition et
   renvoie à la page « Vos données » (ADR-019, ADR-022).
2. **La page « Vos données »** (`src/Limpide.Web/Components/Pages/Confidentialite.razor`) : trajet de la question,
   donnée par donnée (durée, usage, fondement), droits, responsable, protections.

**Règle** : chaque durée affichée vient du code ou de la configuration (`KeptQuestion`, `AnswerLogEntry`,
`VisitorFeedback`, `supervision/`). Un nouveau traitement, une durée ou un destinataire qui change entraîne la mise à jour
de la page dans la même PR (`.claude/rules/interfaces-web.md`).

## Cookies et stockage local

Régime à part : la directive ePrivacy (article 82 de la loi Informatique et Libertés), pas l'article 6 du RGPD. Lire ou
écrire sur l'appareil du visiteur demande son consentement, sauf si c'est strictement nécessaire au service qu'il
demande. L'intérêt légitime n'y est pas une base possible.

Sur la démo (visiteurs), à notre analyse :

| Ce qui est écrit sur l'appareil | Régime |
|---|---|
| Cookie de protection des formulaires (anti-falsification, `UseAntiforgery`) | exempté : strictement nécessaire à la sécurité des formulaires que le visiteur envoie |
| Choix d'apparence (stockage local, `limpide.js`) | exempté : personnalisation de l'interface choisie par le visiteur, cas cité par les lignes directrices de la CNIL (délibération 2020-091 du 17 septembre 2020) ; jamais envoyé au serveur |
| Mesure d'audience, publicité | aucun |

D'où l'absence de bandeau de consentement. Les polices sont servies par le site (`wwwroot/fonts/`), sans requête vers
un tiers. Un outil de mesure d'audience, Turnstile (défi anti-robot d'un tiers, non retenu, ADR-020) ou une police
servie par un tiers changeraient ce tableau. Le back office et Grafana posent leurs propres cookies de connexion : ils
concernent l'administrateur seul (T7), pas les visiteurs.

## Exercice des droits

- **Canal** : `contact@limpide-ia.fr`. **Délai** : un mois (article 12.3).
- **Identification** : Limpide n'identifie pas les visiteurs (article 11). Pour retrouver une question conservée, le
  visiteur donne son texte et le jour. L'administrateur la cherche dans le back office.
- **Effacement** : suppression dans le back office, notée dans `admin_actions` **sans le texte**. La ligne du
  journal des réponses reste, sans texte, jusqu'à sa propre purge. Les sauvegardes s'effacent d'elles-mêmes en 8 jours au plus.
- **Opposition** : la case « Ne pas conserver ma question » vaut pour les questions suivantes ; pour les autres
  traitements, par courriel (« Droit d'opposition » ci-dessus).
- **Réclamation** : CNIL, lien sur la page.

## Sous-traitants et destinataires

| Destinataire | Rôle | Ce qu'il reçoit | Où | Ce qu'on sait de ses conditions |
|---|---|---|---|---|
| OVHcloud (VPS) | sous-traitant, hébergeur | tout ce qui est stocké sur le serveur ; copie quotidienne du disque | France (ADR-006) | conditions de l'hébergeur |
| OVHcloud AI Endpoints | sous-traitant | texte de la question, pour la vectoriser | France (ADR-012) | **non établi** : ni accord de traitement propre au service, ni durée de conservation, ni usage pour l'entraînement trouvés dans les pages publiques (relevé du 2026-11-19, pages en partie illisibles). À demander à OVHcloud |
| Mistral AI (La Plateforme) | sous-traitant pour la rédaction ; **responsable de traitement** pour ses propres finalités (entraînement, modération des abus, statistiques), d'après son accord de traitement (§ 2.3) | question et passages proches, si le seuil est franchi | Espace économique européen par défaut | accord de traitement en vigueur au 27.7.2026, incorporé aux conditions commerciales ; conservation : « la durée de l'accord », sans précision pour les entrées et sorties ; conservation nulle sur demande seulement, accordée à sa discrétion ; entraînement : opposition par un réglage de la console (« Data usage for improving our services »), distinct pour l'API. **État de ce réglage à vérifier** |
| Microsoft (Entra ID) | connexion de l'administrateur | identité de l'administrateur | — | hors visiteurs |
| Microsoft (Azure, conteneur `corpus`) et GitHub (machines de la chaîne) | ne devraient rien recevoir des visiteurs | **voir l'écart ci-dessous** | Azure : France Centre ; machines de GitHub : région non garantie | — |
| Discord | alertes | bilans d'exécution, alertes de quota ; aucune donnée de visiteur | — | — |

Sources du relevé Mistral (lues le 2026-11-19) : accord de traitement,
<https://legal.mistral.ai/terms/data-processing-addendum> (« Effective: July 27, 2026 », § 2.1, § 2.3, annexe 1) ;
conditions commerciales, <https://legal.mistral.ai/terms/commercial-terms-of-service> (« Effective: September 25,
2026 », § 4.2, § 12.3) ; centre d'aide, articles 347612 (conservation nulle), 455207 (opposition à l'entraînement) et
347629 (lieu d'hébergement), <https://help.mistral.ai>. Pour OVHcloud : page du produit AI Endpoints, page des contrats
et pages de protection des données de <https://www.ovhcloud.com/fr/>, sans clause trouvée sur ces points.

**Transferts hors de l'UE** : aucun prévu. Mistral sert depuis l'Espace économique européen par défaut, et Limpide
n'utilise pas son point d'accès américain. L'écart ci-dessous en crée un, probable, à corriger.

## Écarts et points ouverts

| Point | Pourquoi il compte | Correction proposée | Décide |
|---|---|---|---|
| **Questions des visiteurs dans la sauvegarde du corpus** : `deploy/exporter-corpus.sh` n'exclut que `feedback` et `admin_actions`. Le fichier déposé dans Azure le 2026-11-06 vient de la production : il contient `question_log` (texte masqué des questions conservées) et `answer_log` | destinataires et transfert non annoncés (Azure ; machines de GitHub, où chaque évaluation le restaure) ; conservation prolongée au-delà de 30 jours : le versionnage du compte garde chaque ancienne version **sans limite de durée** (`creer-socle.sh`), et une version supprimée reste récupérable 30 jours (suppression réversible) | export par liste blanche (PR #109) ; **réexport fait le 2026-11-19 à 12 h 46**, sans donnée de visiteur (vérifié sur la table des matières de la sauvegarde) ; reste à supprimer les anciennes versions du fichier, dont les deux du 2026-11-06 ; la session 2 dira s'il s'agit d'une violation à notifier (article 33 : probablement non, risque faible, mais à écrire) | Alexandre |
| Fondements de T1, T3, T4, T5 et droit d'opposition absents de la page « Vos données » | information incomplète (articles 13.1.c, 13.1.d, 21.4) | intérêt légitime proposé le 2026-11-19 ; une fois confirmé, écrire sur la page l'intérêt de chaque traitement, la phrase sur l'opposition, et l'identifiant de trace dans la description du journal des réponses, qui ne le cite pas (PR d'interface) | Alexandre |
| Journal des réponses : `trace_id` gardé 12 mois, alors que la trace disparaît en 14 jours | tant qu'il existe, la ligne reste rattachable, en principe, à une visite ; passé 14 jours, il ne relie plus à rien de conservé, mais le garder entretient le doute | effacer `trace_id` après 14 jours (à la purge) : le journal pourrait sortir, à notre analyse, du champ des données personnelles passé ce délai, sauf pour les lignes encore reliées à une question conservée (30 jours) ; à confronter à l'usage réel du `trace_id` ancien dans Grafana | Alexandre |
| Mistral : réglage d'opposition à l'entraînement par l'API non vérifié ; conservation des entrées et sorties non précisée | la page dit « le temps de la réponse » ; ce n'est vrai chez Mistral que si rien n'est gardé pour la modération | vérifier le réglage dans la console, dans les deux espaces (démo, évaluation) ; demander la conservation nulle ou corriger la page | Alexandre |
| OVHcloud AI Endpoints : conditions de traitement non trouvées | sous-traitant sans contrat identifié | demander l'accord de traitement, la durée de conservation et l'usage pour l'entraînement | Alexandre |
| `admin_actions` sans durée de conservation | toute donnée personnelle a une durée (article 5.1.e) ; ici, celle de l'administrateur lui-même | fixer une durée (par exemple la durée du projet), ou l'assumer par écrit | Alexandre |
