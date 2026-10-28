# ADR-022 : Interface en fil de questions, sans mémoire de conversation

- **Statut** : accepté
- **Date** : 2026-10-28

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
- **Distinction visuelle** : réponse (encadré bleu, sans serif) et textes officiels (fond papier, filet doré, serif).
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

- **En-tête fixe** : « Limpide », une phrase courte, et une pastille « IA · Vous échangez avec une IA : elle peut se
  tromper, ce n'est pas un conseil juridique », visible en permanence (article 50).
- **Panneau latéral** (menu sur mobile), icône et libellé : Nouvelle question, Ce que fait Limpide, Comment il est
  construit, Résultats d'évaluation, Votre avis, Vos données, Projet ouvert, Back office, Sources et licences.
- **Accueil vide** : les trois encarts (« ce qu'il fait, ne fait pas, pourquoi il montre tout »), raccourcis, et les
  exemples en boutons. Ils disparaissent à la première question ; le texte complet passe dans « Ce que fait Limpide ».
  Pas de commande « / » : les visiteurs ne sont pas tous techniciens.
- **Champ fixé en bas**, bouton flèche nommé « Poser la question », texte d'aide « complète et sans donnée
  personnelle ». Sous le champ, visible sans action avant l'envoi : case « Ne pas conserver ma question », durée,
  lien « Vos données ».
- **Chaque réponse** garde ses textes cités, « Sous le capot » (replié, avec la durée et le coût dans son bouton) et un
  bouton « Mon avis » qui joint **cette** question et **cette** réponse (aujourd'hui, seule la dernière peut l'être).
- **Textes cités** : cartes sous la réponse, deux lignes d'aperçu sur ordinateur, repliées en une ligne sur mobile ;
  un renvoi `[P1]` ouvre la carte. Pied de carte réduit à la source, au lien vers le texte officiel et à la licence ;
  version et empreinte restent dans les détails techniques. Écartées : la fenêtre au clic (texte de référence caché,
  focus à gérer) et le panneau à droite (deux panneaux et le fil sur ordinateur, à refaire pour le mobile).
- Compteur de caractères et quota affichés **à l'approche des limites** seulement (à partir de 400 caractères, à
  partir de 3 questions restantes). Les limites restent écrites dans « Ce que fait Limpide », et le message de refus
  quand elles sont atteintes ne change pas.

## Conséquences

- **Information à deux niveaux (ADR-019)** : une ligne sous le champ, le détail dans `/confidentialite`, qui doit
  continuer à tout dire (durée, masquage, destinataires, opposition). Cette page change dans la même PR si la ligne
  courte renvoie à un détail qui n'y figure pas encore.
- **Lien vers `/confidentialite`** : l'ADR-019 le place « en bas de chaque page ». Le pied de page disparaît ; le lien
  est sous le champ (page des questions, sans ouvrir de menu) et dans le panneau latéral sur toutes les pages (menu
  sur mobile). C'est le seul point de l'ADR-019 que cette décision modifie.
- **Avertissement juridique** : il quitte le bas de chaque réponse pour la pastille de l'en-tête et l'encart « ne
  juge pas votre cas ». Le garde-fou `qualification-juridique` reste affiché dans la réponse concernée.
- **Le fil vit dans la session Blazor** : perdu au rechargement de la page, jamais enregistré en plus de ce que
  `answer_log` et `question_log` gardent déjà. Le quota et le délai de 60 s par question ne changent pas.
- **Pages nouvelles** : Ce que fait Limpide, Votre avis, Comment il est construit (`TechStack.razor`, toujours à
  tenir à jour), Sources et licences. Aucune ne crée de traitement de données.
- **Revue** : l'agent `relecteur-interface` relit chaque PR qui touche `src/Limpide.Web/` ou `src/Limpide.Admin/`.
- Pas de nouvelle évaluation : la chaîne de réponse ne change pas.

**On reviendrait sur cette décision** si le journal des réponses montre que les visiteurs posent des questions de
suite (« et pour… ? ») qui échouent : taux de « je ne sais pas » nettement plus élevé sur les questions courtes
posées après une première réponse. Une mémoire de conversation serait alors une décision à part, mesurée par
l'évaluation comme le reste.
