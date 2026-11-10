# ADR-022 : Interface en fil de questions, sans mémoire de conversation

- **Statut** : accepté
- **Date** : 2026-10-28
- **Précisé le 2026-10-29**, après l'audit de la maquette (`better-interface`, `design-critique`) et avant toute
  implémentation : textes cités repliés en une ligne aussi sur ordinateur (au lieu de deux lignes d'aperçu),
  « chaque question est traitée seule » au-dessus du champ dès la première réponse, ligne de conservation
  reformulée, Back office en bas du panneau, menu mobile modal, focus et défilement précisés ; encarts de l'accueil
  repliés sur leur titre, texte complet au clic, et légende discrète au lieu du titre « Réponse de l'assistant »
  (retours d'Alexandre sur la maquette).
- **Précisé le 2026-11-01**, d'après la maquette finale (direction 2, « la réponse en coupe ») : en-tête réduit au
  nom, à la mention de l'article 50 et au menu ; « Reprendre cette question » à côté de « Donner mon avis » ;
  radiographie en direct ; « Mentions légales, sources et licences » en bas du panneau ; pages secondaires en coupe.
- **Précisé les 2026-11-06 et 2026-11-10**, d'après la démo en ligne (retours d'Alexandre) : liens légaux dans le
  menu seulement, « Vos données » porté par la durée de conservation sous le champ ; encarts de l'accueil retirés,
  exemples en pastilles envoyées au clic ; flèche d'envoi vers le haut.

## Contexte

La démo tient sur une page qui s'allonge : présentation, trois encarts, encart IA, formulaire, réponse, textes cités,
« sous le capot », formulaire d'avis, technologies, pied de page. Le formulaire, l'action principale, n'arrive qu'en
quatrième position, sous la ligne de flottaison sur mobile. Alexandre veut une interface de conversation : en-tête
fixe, panneau latéral, champ de question en bas, réponses au-dessus, et le reste dans des pages accessibles depuis le
panneau, sans rien supprimer.

L'audit d'interface du 2026-10-28 (`better-interface`, `design-critique`, PR #63) a conclu à des retouches, pas à une
refonte, sur l'interface actuelle. La refonte est un choix de présentation, décidé ensuite, à faire **avant le
tournage de la vidéo de démonstration** (S9), qui sinon serait à refaire.

Contraintes qui ne bougent pas :

- **Article 50 de l'AI Act** : la personne sait qu'elle échange avec une IA.
- **ADR-019** : information sur la conservation de la question et case d'opposition au moment de la saisie.
- **Licences du corpus** (`docs/sources.md`) : extraits reproduits à l'identique, source et licence sur chaque extrait.
- **Distinction visuelle** : réponse (sans serif, sur blanc) et textes officiels (fond papier, serif, identifiants
  dorés).
- Limpide traite **chaque question seule** : ni la recherche ni la rédaction ne reçoivent les questions précédentes.

Une interface de conversation laisse croire le contraire : un visiteur posera « et pour les PME ? », et la réponse
portera sur une question privée de son sujet.

## Options envisagées

1. **Garder la page actuelle, retouchée** (PR #63) : aucun risque, mais l'action principale reste noyée et la page
   ne tient pas sur un écran de téléphone.
2. **Fil de conversation avec mémoire** : les questions précédentes sont envoyées à la recherche et au modèle. C'est
   une nouvelle fonctionnalité : consignes à réécrire (reformulation de la question à partir du fil), nouvelles
   questions d'évaluation, coût et latence en hausse, et contexte plus large à protéger contre les injections.
   Hors de l'angle du projet à ce stade.
3. **Fil de questions sans mémoire** : les réponses s'empilent à l'écran, chaque question reste traitée seule, et
   l'interface le dit à côté du champ. Aucun changement de la chaîne de réponse, donc ni consigne ni évaluation à
   refaire.

## Décision

**Option 3, décidée avec Alexandre le 2026-10-28**, d'après une maquette cliquable (ordinateur, mobile, trois
façons d'afficher les textes cités). Détail de la présentation retenue :

- **En-tête fixe** : « Limpide », la mention « IA · Vous échangez avec une IA : elle peut se tromper, ce n'est pas
  un conseil juridique », visible en permanence (article 50), et le bouton « Menu ». Pas de raccourcis ni de
  « Nouvelle question » dans l'en-tête, sur ordinateur comme sur mobile : le panneau suffit.
- **Panneau latéral** (menu sur mobile, en fenêtre modale), icône et libellé : Nouvelle question, Ce que fait
  Limpide, Comment il est construit, Résultats d'évaluation, Votre avis, Vos données, Projet ouvert ; en bas, en
  petit, Mentions légales, sources et licences (une seule page : éditeur et hébergeur, licences du corpus, code et
  crédits) et Back office (réservé à l'administrateur). L'entrée de la page en cours est marquée.
- **Accueil vide** : une colonne centrée, le titre, la phrase d'accroche et trois exemples en pastilles sous un titre
  visible, envoyés au clic ; ils disparaissent à la première question, qui montre les couches. Pas de commande « / » :
  les visiteurs ne sont pas tous techniciens. Jusqu'au 2026-11-10, trois encarts repliés (« ce qu'il fait, ne fait
  pas, pourquoi il montre tout », texte complet au clic, repris dans « Ce que fait Limpide ») occupaient la droite :
  peu ouverts, ils prenaient le pas sur les exemples, et une légende fixe des trois couches a été essayée puis
  écartée. Les exemples étaient recopiés dans le champ : ils passaient pour du texte et le clic semblait sans effet.
- **Champ fixé en bas**, bouton flèche vers le haut nommé « Poser la question » (icône seule, sablier pendant
  l'attente), texte d'aide court (« Posez votre question » ; les exemples sont sur l'accueil), contour de focus visible
  sur le cadre du champ. Sous le champ, visibles sans action avant l'envoi : « pas de donnée personnelle », durée de
  conservation, case « Ne pas conserver ma question » (libellé de l'ADR-019) ; la durée est le lien vers « Vos
  données ». Dès la première réponse, au-dessus : « **Chaque question est traitée seule** : redonnez le contexte. »
  Sur téléphone (2026-11-02, le pied de page prenait plus du tiers d'un écran de 320 px), mêmes informations en
  version courte : « Gardée 30 jours : pas de donnée personnelle. », case « Ne pas garder ma question » sur la ligne
  suivante, « **Question traitée seule** : redonnez le contexte. » Les pages qui renvoient à la case la décrivent sans
  citer son libellé.
- **Chaque réponse est un bloc « en coupe »** à trois couches : en surface la réponse, dessous les textes cités,
  au fond le calcul (« Sous le capot », replié sur une ligne avec les étapes, la durée, le coût et les contrôles).
  Elle porte en tête une légende discrète, « Formulation de l'assistant, à vérifier dans les textes dessous » (pas de
  titre visible, redondant dans un fil), et un titre lu par les lecteurs d'écran seulement (« Réponse à : … ») pour
  passer d'une réponse à l'autre. Sous le bloc, « Reprendre cette question » (recopie la question dans le champ,
  pour la compléter ; rien n'est conservé de plus, et la case « Ne pas conserver ma question » garde son état) et
  « Donner mon avis », qui joint **cette** question et **cette** réponse (aujourd'hui, seule la
  dernière peut l'être). Le fond déplié renvoie aux résultats d'évaluation. Après l'envoi, le fil se place au début
  de la nouvelle question, pas en bas d'une longue réponse.
- **Radiographie en direct** : pendant le traitement, le fond est ouvert et se remplit à chaque vrai point d'étape
  du service (vectorisation, passages trouvés et leur proximité sur une règle avec le seuil, rédaction) ; rien
  n'est simulé. Un seul motif animé, « en cours » (un segment qui balaie l'étape en cours et, en surface, une barre
  sous le texte d'attente), coupé sous `prefers-reduced-motion` ; l'état en cours est aussi écrit.
- **Textes cités** : cartes sous la réponse, **repliées en une ligne** (identifiant, titre, « Lire »), sur ordinateur
  comme sur mobile ; le texte s'ouvre à la demande. Un renvoi `[P1]` ouvre la carte et y porte le focus. Pied de carte
  (affiché carte ouverte) réduit à la source, au lien vers le texte officiel et à la licence ; version et empreinte
  restent dans les détails techniques. Écartés : l'aperçu de deux lignes (trop de texte sous chaque réponse), la
  fenêtre au clic (texte de référence caché, focus à gérer) et le panneau à droite (deux panneaux et le fil sur
  ordinateur, à refaire pour le mobile).
- Compteur de caractères et quota affichés **à l'approche des limites** seulement (à partir de 400 caractères, à
  partir de 3 questions restantes). Les limites restent écrites dans « Ce que fait Limpide », et le message de refus
  quand elles sont atteintes ne change pas.

## Conséquences

- **Information à deux niveaux (ADR-019)** : une ligne sous le champ, le détail dans `/confidentialite`, qui doit
  continuer à tout dire (durée, masquage, destinataires, opposition). Cette page change dans la même PR si la ligne
  courte renvoie à un détail qui n'y figure pas encore.
- **Lien vers `/confidentialite`** : l'ADR-019 le place « en bas de chaque page » ; c'est le seul point de l'ADR-019
  que cette décision modifie. Le pied de page disparaît. Le lien est dans le panneau latéral sur toutes les pages
  (menu sur mobile) et, sous le champ, porté par la durée de conservation (« gardée 30 jours »), sans ouvrir de menu.
  Jusqu'au 2026-11-06, un lien « Vos données » distinct sous le champ et un pied de page de secours doublaient le
  menu ; le pied de page ne subsiste qu'en `<noscript>`, seul accès aux pages légales quand le menu ne s'ouvre pas.
- **Avertissement juridique** : il quitte le bas de chaque réponse pour la pastille de l'en-tête et l'encart « ne juge
  pas votre cas ». Depuis le 2026-11-10, l'encart est retiré de l'accueil : la pastille de l'en-tête suffit, et la
  phrase reste dans « Ce que fait Limpide ». Le garde-fou `qualification-juridique` reste affiché dans la réponse
  concernée.
- **Le fil vit dans la session Blazor** : perdu au rechargement de la page, jamais enregistré en plus de ce que
  `answer_log` et `question_log` gardent déjà. Le quota et le délai de 60 s par question ne changent pas.
- **Pages nouvelles** : Ce que fait Limpide, Votre avis, Comment il est construit (`TechStack.razor`, toujours à
  tenir à jour, avec ses icônes), Mentions légales, sources et licences (LCEN, article 6 : éditeur et hébergeur).
  Aucune ne crée de traitement de données. « Vos données » nomme le même éditeur que les mentions légales comme
  responsable du traitement. Les pages secondaires suivent la même coupe que les réponses (l'essentiel,
  le détail, la méthode) ; le papier et le serif restent réservés aux textes officiels.
- **Revue** : l'agent `relecteur-interface` relit chaque PR qui touche `src/Limpide.Web/` ou `src/Limpide.Admin/`.
- Pas de nouvelle évaluation : la chaîne de réponse ne change pas.

**On reviendrait sur cette décision** si le journal des réponses montre que les visiteurs posent des questions de
suite (« et pour… ? ») qui échouent : taux de « je ne sais pas » nettement plus élevé sur les questions courtes
posées après une première réponse. Une mémoire de conversation serait alors une décision à part, mesurée par
l'évaluation comme le reste.
