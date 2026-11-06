# Semaine 9 : gouvernance, documentation, démonstration

**Objectif de fin de semaine** : quelqu'un qui ne connaît pas le projet comprend en 5 minutes ce que fait Limpide,
comment, et pourquoi ces choix. Le dossier de gouvernance est complet.

**Budget temps** : environ 10 heures, en 5 sessions, plus 8 à 10 heures pour la refonte de l'interface.

**Prérequis** : semaines 1 à 8 terminées (ou arbitrées : ce qui manque est listé comme limite).

---

## Refonte de l'interface, avant le script de la vidéo (≈ 8 à 10 h, ajoutée le 2026-10-28)

Décision : ADR-022 (fil de questions sans mémoire), d'après l'audit d'interface du 2026-10-28 (PR #63, à fusionner
d'abord) et une maquette cliquable validée avec Alexandre. Une PR par étape, chacune relue par `relecteur-interface`
puis vérifiée à la main dans un navigateur (320 px, thème sombre, clavier), et par `/interface-review`.

1. **Squelette** (≈ 2 h) : en-tête fixe (titre, phrase courte, pastille IA), panneau latéral (menu sur mobile en
   `<dialog>` modal, page en cours marquée par `aria-current`), zone du fil, champ fixé en bas (hauteur en `100dvh`
   pour le clavier mobile). Le contenu actuel de la page y est replacé tel quel.
1 bis. **Système visuel** (ajouté le 2026-11-01, ≈ 1 h) : palette claire de la direction 2 et palette sombre
   validée par Alexandre (contrastes mesurés, tous AA), réglage « Apparence » en bas du panneau (Automatique, Clair,
   Sombre ; choix gardé dans le navigateur et appliqué avant l'affichage), polices Public Sans et Literata servies
   par le site (Fontsource 5.3.0, sous-ensemble latin, SIL OFL), mention du stockage local dans `/confidentialite`.
2. **Pages secondaires** (≈ 1 h 30) : Ce que fait Limpide (texte complet des trois encarts), Votre avis (formulaire
   général), Comment il est construit (`TechStack.razor`, avec ses icônes),
   Mentions légales, sources et licences (mentions du pied de page actuel, éditeur et hébergeur).
   Liens du panneau vers ces pages, l'évaluation, « Vos données », le dépôt et le back office.
3. **Fil de questions** (≈ 2 h 30) : réponses empilées (session Blazor), accueil vide avec encarts repliés (texte complet au clic) et
   exemples en boutons, « Sous le capot » et « Donner mon avis » par réponse (l'avis joint cette question et cette
   réponse), le fil placé au début de la nouvelle question et le focus sur sa réponse.
4. **Textes cités** (≈ 1 h 30) : cartes repliées en une ligne sous la réponse, sur ordinateur comme sur mobile ; un
   renvoi `[P1]` ouvre la carte et y porte le focus ; pied de carte réduit (source, texte officiel, licence), version
   et empreinte dans les détails techniques.
5. **Champ et finitions** (≈ 1 h 30) : bouton flèche, Entrée pour envoyer et Maj+Entrée pour aller à la ligne, ligne
   de conservation (ADR-019) avec sa case, sous le champ ; compteur à partir de 400 caractères et quota à partir de 3
   questions restantes, limites écrites dans « Ce que fait Limpide », message de refus inchangé ; « chaque question
   est traitée seule » au-dessus du champ dès la première réponse ; contour de focus sur le cadre du champ ;
   relecture de `/confidentialite` (information à deux niveaux).

**Terminé quand** : la démo en production suit l'ADR-022, l'encart IA et la case d'opposition sont visibles sans
action, et les vérifications manuelles sont cochées dans chaque PR. Le script de la vidéo (session 4) s'écrit sur
cette interface.

---

## Session 1 : dossier de gouvernance, partie données (≈ 2 h)

Dans `docs/gouvernance/` :

1. **Classification des données** : corpus public, métadonnées techniques, questions des utilisateurs,
   traces, secrets. Pour chaque catégorie : sensibilité, lieu de stockage, durée de conservation, accès.
2. **RACI** : même seul sur le projet, définir les rôles (propriétaire des données, responsable du système IA,
   sécurité, développement) et qui est responsable, décide, est consulté, est informé pour chaque activité
   (ajout d'une source, changement de modèle, levée de quarantaine, incident).
3. **Registre des sources** : reprendre `docs/sources.md` avec les licences et la règle de citation à l'identique.
4. **RGPD** : traitement des questions des utilisateurs (décision de la semaine 8), base légale, information, durées.

**Terminé quand** : les quatre documents sont rédigés.

## Session 2 : dossier de gouvernance, partie IA (≈ 2 h)

1. **Classification de Limpide au regard de l'AI Act** : quelle catégorie de risque, quelles obligations
   (en particulier la transparence envers les utilisateurs). À vérifier sur le texte, avec les articles cités.
   C'est un test de crédibilité : un projet sur l'AI Act doit savoir se situer lui-même.
2. **Registre des risques IA** : pour chaque risque, probabilité, impact, mesure en place, preuve.
   | Risque | Mesure | Preuve |
   |---|---|---|
   | Réponse inventée | Seuil de pertinence, citations vérifiées, « je ne sais pas » | Évaluation S7 |
   | Texte source dénaturé | Extraits reproduits à l'identique, réponse distincte | Interface S2 |
   | Version défectueuse d'une source | Contrôles qualité, quarantaine | Démonstration S5 |
   | Injection de prompt | Garde-fous v1 et v2 | Questions d'injection S7-S8 |
   | Dérive des coûts | Plafonds, alertes de budget | FinOps S8 |
   | Dépendance au fournisseur du LLM | Abstraction `IChatClient` | ADR-004 |
   | Conseil juridique implicite | Avertissement, refus de qualification | Évaluation S7 |
3. **Fiche descriptive du système** : modèles et versions, données, usage prévu et usages exclus,
   métriques d'évaluation, limites connues.
4. **Procédure d'incident** : détection, qualification, correction, communication, retour d'expérience.

**Terminé quand** : les quatre documents sont rédigés, chaque mesure pointe vers une preuve dans le dépôt.

## Session 3 : ADR et README (≈ 2 h)

1. **Relire les 13 ADR** : mettre à jour les statuts, ajouter les mesures obtenues depuis leur rédaction
   (l'ADR-002 sur les embeddings en particulier).
2. **README final** :
   - en une phrase : ce que fait Limpide et ce qui le distingue ;
   - lien vers la démo et la page d'évaluation ;
   - schéma d'architecture ;
   - le récit POC → production, avec le bilan de la phase 1 ;
   - résultats d'évaluation, coût par requête ;
   - limites connues et suites possibles ;
   - index de la documentation (ADR, gouvernance, FinOps).

**Terminé quand** : une personne extérieure relit le README et sait dire ce que fait le projet.

## Session 4 : script de la vidéo de démonstration (≈ 45 min)

Le tournage est reporté à la fin du plan, avec tous les enregistrements de démonstration (2026-10-18). Ici,
le script de deux à trois minutes :

1. Le problème en une phrase, et ce que Limpide fait différemment.
2. Une question : la réponse, les extraits cités, le panneau « sous le capot ».
3. Une question hors sujet et une question sans réponse : les garde-fous.
4. Une source corrompue bloquée par la quarantaine (démonstration de la semaine 5, à tourner).
5. Une modification de prompt bloquée par l'évaluation (PR de démonstration de la semaine 7, à tourner).
6. Une trace et le coût d'une requête.
7. Conclusion : les choix d'architecture en trois points.

**Terminé quand** : le script est écrit, scène par scène ; tournage, publication et lien depuis le README à la fin
du plan.

## Session 5 : diffusion (≈ 2 h)

1. Mettre à jour les profils (Malt, LinkedIn) : lien vers la démo, la vidéo et le dépôt.
2. Rédiger un court article ou billet sur le récit POC → production, ou sur un apprentissage marquant
   (la contrainte CC-BY-ND, la quarantaine, le blocage par l'évaluation).
3. Préparer une version « rendez-vous client » de la démonstration (5 minutes) :
   ce que Limpide montrerait sur les documents du prospect.

**Terminé quand** : le projet est visible publiquement, avec une démonstration prête pour un rendez-vous.

---

## Pièges à éviter

- **Une gouvernance théorique** : chaque règle doit pointer vers une preuve dans le dépôt ou la démo.
- **Cacher les limites** : un projet qui dit ce qu'il ne sait pas faire est plus crédible, et c'est précisément le propos de Limpide.
- **Une vidéo trop longue** : au-delà de 3 minutes, elle ne sera pas regardée en entier.
- **Un README écrit pour soi** : il s'adresse à un DSI, un recruteur ou un jury, pas à un développeur du projet.

## Livrables

- `docs/gouvernance/` : classification, RACI, sources, RGPD, classification AI Act, registre des risques, fiche système, procédure d'incident
- ADR relus et à jour
- README final
- Script de la vidéo de démonstration (tournage à la fin du plan)
- Profils mis à jour, version « rendez-vous client »

---

## Journal de réalisation

### Maquette finale de la refonte (2026-10-30 et 31)

Toile Design (claude.ai, privée), rangées « Maquette finale : direction 2 » (écrans O1 à O10, M1 à M6 ; prototypes
`F2-ordinateur` et `F2-mobile`) et « Maquette finale : les autres pages » (Résultats d'évaluation, Ce que fait
Limpide, Vos données). Données réelles uniquement ; ce qui manque est entre crochets. **Font foi : les écrans
`F2-*`, page « Maquette finale »** ; les pistes écartées (directions 1 à 5, texte annoté, premières versions) sont
rangées dans la page « Explorations », pour mémoire seulement.

**Décisions prises avec Alexandre, reportées dans l'ADR-022 le 2026-11-01** :

- bandeau ordinateur : « Limpide », mention article 50 et « Menu » seulement (ni raccourcis, ni « Nouvelle
  question ») ; pastille « IA » à 8 px du nom, texte de la mention à 18 px de la pastille ;
- « Reprendre cette question » sous la réponse, à gauche de « Donner mon avis » ;
- lien vers l'évaluation au fond de la réponse dépliée, sous les garde-fous ;
- menu : « Mentions légales, sources et licences » et « Back office » en bas ; « Sources et licences » seul retiré ;
- pages secondaires en coupe (surface, dessous, fond) ; papier et Literata réservés aux textes officiels (sauf le
  titre « Les textes officiels », comme sur l'accueil) ; titre de la page en `h1`.

**Thème sombre** (2026-11-01) : palette sombre déduite de la direction 2, montrée sur la toile (planche « Système
visuel ») et validée par Alexandre ; réglage à trois choix plutôt qu'un interrupteur, pour garder « suit
l'appareil » par défaut.

**Points ouverts** :

- [ ] **Bases légales de « Vos données »** (session 1, point 4) : la page actuelle n'en donne pas pour l'envoi de la
  question à OVHcloud et Mistral, ni pour le journal des réponses, les traces et l'adresse IP (article 13 du RGPD).
  Proposition à trancher : intérêt légitime (6.1.f) pour ces quatre finalités (pas de contrat faute de conditions
  d'utilisation, consentement trop lourd), avec une mise en balance écrite dans `docs/gouvernance/`.
- [ ] **Purge à l'écriture seulement** (`AnswerLogStore`, `FeedbackStore`) : sans nouvelle question, une question
  peut rester au-delà des 30 jours annoncés. Dit tel quel dans « Vos données » depuis l'étape 2b (la maquette le
  présentait comme un avantage) ; une purge planifiée reste à décider.
- [x] **Mentions légales (LCEN, article 6)** : page `/mentions-legales` (étape 2a), éditeur complet (informations
  d'Alexandre, confirmées au registre des entreprises le 2026-11-01). **Sans téléphone, par choix d'Alexandre** :
  son seul numéro est personnel, l'adresse électronique sert de contact ; l'article 6, III, 1, b demande un
  téléphone pour une société, écart signalé et accepté. Celui d'OVHcloud manque aussi (absent de ses mentions
  légales) : lien vers son site.
- [x] **Responsable du traitement** (étape 2a) : `Confidentialite.razor` nomme Alexandre Leclerc à titre personnel ; si Limpide
  est publié par l'EURL, c'est elle le responsable (représentée par son gérant). À aligner sur les mentions légales.
- [x] **Passages CNIL** : lien vers le texte de la licence CC-BY-ND 4.0 FR sous chaque extrait (étape 4, « Lire la
  licence », reconnue au début des conditions de `corpus.json` ; décision 2011/833/UE pour EUR-Lex).
- [ ] **Rapports d'évaluation** : ils ne versionnent pas le code des garde-fous. Les passes du 2026-10-28 à 19 h 02
  (sans détecteur) et 19 h 05 (avec) déclarent les mêmes versions ; `Evaluation.razor` présente donc à tort la
  première comme « même configuration qu'en ligne ». Ajouter le commit ou une version des garde-fous. Étape 2b : la
  page ne marque plus que la dernière passe de la configuration servie (« version en ligne ») et annote la passe de
  19 h 02 (« avant le contrôle de la question ») ; le versionnement des garde-fous reste à faire.
- [x] **`Evaluation.razor` périmé** (étape 2b, chiffres lus dans les rapports) : « 50 questions » (55), « 3 tentatives » (8), « 0,30 $ » (≈ 0,25 € par passe).
- [x] **`Confidentialite.razor`** (étape 2b) : « scores des passages » (seul le meilleur est gardé) ; « formulaire en bas de
  la page d'accueil » (disparaît avec la refonte).
- [ ] **Cas hors sujet (O10)** : `answer_log` ne garde ni la question ni les 5 scores ; seul le refus n° 13 du poste
  (2026-10-21, meilleur 0,481) sert d'exemple. Les vrais scores hors sujet (0,34 à 0,48) sont sous l'échelle
  0,55–0,85 de la règle : échelle élargie à 0,30 pour un refus, à confirmer.
- [ ] Formulation de l'alerte de quota (« Il vous reste 3 questions pour cette heure. »), à valider.
- [ ] **Icônes de « Comment il est construit »** (souhait d'Alexandre) : garder celles de `TechStack.razor`
  (`wwwroot/tech`, Simple Icons, CC0, servies par le site, colorées par masque CSS), l'initiale encadrée pour les
  briques sans logo, et la mention sur les marques. Ajoutées à la maquette le 2026-11-01.
- [ ] **Script en ligne d'`App.razor`** (thème appliqué avant l'affichage) : si une Content-Security-Policy
  `script-src` est ajoutée en production, lui donner une empreinte ou un nonce, sinon le thème clignote.
- [ ] Textes nouveaux de « Ce que fait Limpide » à relire (Une question à la fois, Ce qu'il ne connaît pas, les
  5 étapes, le coût, Où part votre question).


### Refonte, étape 2b : Résultats d'évaluation et Vos données en coupe (2026-11-01)

- **Résultats d'évaluation** : résumé, quatre chiffres, « Mesure par mesure » (ce que chaque chiffre ne dit pas), la
  barrière, trois moments, l'historique. Tout est lu dans les rapports versionnés (détail par question ajouté à
  `EvaluationReports` : comportement, verdict, garde-fous, juge ; coût d'une passe, 95ᵉ centile) ; seuls restent
  écrits à la main la calibration du juge et la lenteur mesurée en ligne (15 s).
- **Seuils de la barrière** déplacés de l'appsettings de la console vers `src/appsettings.shared.json`
  (`EvaluationGateOptions` dans `Limpide.Core`) : la page affiche les 11 seuils réels, traduits par
  `PlainLanguage.GateRule`, au lieu des 6 recopiés de la maquette. `gate` vérifié sur le rapport servi (11 seuils lus,
  code 0) ; l'évaluation tourne sur la PR (fichier partagé modifié).
- **Vos données** : l'essentiel en chiffres, droits et responsable, tableau donnée par donnée, protections. Fondements
  laissés tels quels (question conservée, avis) : les quatre manquants restent un point ouvert. Effacement décrit tel
  qu'il est (à l'écriture).
- Tableaux empilés sous 40rem (en-têtes de colonne en libellés), comme la maquette mobile.
- **Page d'évaluation vérifiée contre ses sources** (2026-11-02, à la demande d'Alexandre, commit 821f74a) : les
  chiffres « 50 questions », « 3 tentatives », « 0,30 $ » étaient écrits en dur dans l'ancien `Evaluation.razor`.
  Six formulations corrigées : le contrôle met *toujours* les questions de qualification en relecture ; « aucun
  juriste n'a relu ces 3 réponses » au lieu d'une relecture « en attente » non établie ; « tentatives écrites par un
  modèle » au lieu de « techniques connues » ; les 15 s, un épisode ponctuel en ligne (rien n'établissait que la passe
  avait tourné sur le poste) ; « seuil bloquant » (le 95ᵉ centile ne bloque pas) ; l'historique contient une passe de
  la chaîne (consigne bloquée du 18 octobre). Ajouté : sur une passe précédente, aucune erreur grave dans les 7
  alertes du juge (ADR-014).
- Corrections de la relecture de la PR #69 : espaces insécables avant « ? : ; » et dans les guillemets, jeton
  `--paper-rule` supprimé, séparateur du pied de page masqué aux lecteurs d'écran. Styles des anciennes pages retirés.

### Refonte, étape 3 : fil de questions (2026-11-02)

- **Fil** : questions empilées dans un service propre au circuit (`QuestionThread`, « scoped ») : il survit au passage
  par les autres pages, disparaît avec la visite ; « Nouvelle question » (`/?nouvelle=1`) le vide. Rien n'est envoyé
  au modèle avec la question suivante (ADR-022) ; « Chaque question est traitée seule » au-dessus du champ dès la
  première question. « Vos données » le dit (bloc « Le fil de vos questions »).
- **Accueil, fil vide** (O1) : promesse, exemples, coupe des trois couches repliées (texte complet au clic). La
  couche « Les textes officiels » de la coupe sans papier ni Literata, comme en 2a (texte de Limpide).
- **Chaque question en coupe** (O4 à O6) : la question en titre ; en surface la réponse (avertissement fixe
  conservé : `AnswerPrompt.Disclaimer` doit accompagner toute réponse) ; dessous les textes cités (tels quels, l'étape
  4 les repliera) ; au fond une ligne de résumé (étapes, durée, coût, contrôles) et le détail au clic : 5 étapes, règle
  de proximité (nouvelle, `ProximityRuler`), temps, coût, passages fournis, garde-fous, lien vers l'évaluation. Détail
  ouvert d'emblée si un garde-fou s'est déclenché. Ancres des extraits préfixées par question (`q2-P1`).
- **« Reprendre cette question »** et **« Donner mon avis sur cette réponse »** sous chaque réponse ; l'avis remplace
  le fil (« Revenir à vos questions »), la case « Joindre ma question et la réponse obtenue (« … ») » **décochée par
  défaut** (consentement ; cochée dans la maquette, écart vu en codant l'étape, pas par une relecture).
- **Placement** : la nouvelle question en haut de la zone qui défile (sans animation si mouvement réduit), le focus
  sur son titre à l'arrivée de la réponse ; au retour de l'avis, focus sur la question concernée.
- **Relectures** (`relecteur-interface`, `/interface-review`) : focus perdu au dépli du fond (le bouton activé était
  remplacé : le focus suit maintenant le nouveau bouton, vérifié avec la touche Entrée), contour de focus rogné par le
  conteneur arrondi, `role="alert"` perdu sur l'alerte de qualification, `aria-controls` vers un élément absent,
  garde-fous en code brut ; tous corrigés.
- **« Sous le capot » devient « Au fond »** partout (accueil, « Ce que fait Limpide », schéma de « Comment il est
  construit ») ; la troisième question de l'avis aussi, par choix d'Alexandre : « La couche « Au fond » (comment la
  réponse a été produite) vous a-t-elle servi à quelque chose ? ». Les avis d'avant le 2026-11-02 répondaient à
  l'ancienne formulation (libellé du back office).
- **Pas fait** : la radiographie en direct (O2, O3 : étapes qui s'allument pendant l'attente) demande que
  `AnswerService` publie son avancement ; l'attente affiche une phrase fixe. **Décidé le 2026-11-02** : les étapes en
  direct juste après la refonte, avant le script de la vidéo (prévues par l'ADR-022) ; la réponse écrite en continu
  (« suivi en direct » du plan global, S8) reportée en S10. Faites le jour même : voir « Radiographie en direct ».
- Vérifié avec de vraies questions, Firefox piloté par WebDriver BiDi (script Node sans dépendance, hors dépôt) :
  1440 px clair, 390 px clair et sombre, 320 px ; débordements corrigés (`minmax(0, 1fr)` sur les couches et le
  détail du fond).

### Refonte, étape 5 : champ et finitions (2026-11-02, avant l'étape 4)

Avancée avant l'étape 4 : le champ se voit sur tous les écrans, et l'étape 3 ayant refait `Home.razor`, les deux
étapes ne touchent plus les mêmes fichiers.

- **Bouton dans le cadre** (« Poser la question », « Poser » sur mobile, flèche) ; contour de focus sur le cadre ;
  **Entrée envoie, Maj+Entrée va à la ligne** (`limpide.js`, sauf pendant une saisie composée) ; hauteur qui suit le
  texte, de 1 à 5 lignes. Le champ reste modifiable pendant l'attente ; le bouton passe en `aria-disabled`.
- **Ligne de conservation** sur une ligne : la phrase, la case « Ne pas conserver ma question » et « Vos données »
  (ADR-019) ; limites (quota à 3 questions restantes, compteur à 400 caractères) sur leur propre ligne, à leur approche.
- Relectures : limite de 500 caractères aussi tenue côté serveur (avant, `maxlength` seul) ; champ et bouton inactifs
  tant que la page n'est pas interactive (`RendererInfo.IsInteractive` : une saisie aurait été perdue, Entrée aurait
  rechargé la page) ; compteur lu avec son unité ; case d'opposition à 44 px sur mobile ; un échec n'écrase plus une
  question préparée pendant l'attente. Sur téléphone, Entrée envoie (pas de Maj+Entrée sur un clavier virtuel) :
  choix assumé pour des questions courtes.
- Le reste de l'étape était déjà fait : limites écrites dans « Ce que fait Limpide » (2a), « Chaque question est
  traitée seule » (3), « Vos données » relue en 2b ; message de refus inchangé.
- Vérifié au pilote : Entrée, Maj+Entrée, hauteur (1 puis 3 lignes, retour à 1 après l'envoi), 1440 px, 390 px en
  sombre, 320 × 568 (champ entier, 247 px).

### Refonte, étape 4 : textes cités (2026-11-02)

- **Cartes repliées** (`<details>` natif) dans la couche « Dessous », seule couche sur papier : identifiant, titre en
  Literata, « Lire » / « Replier ». Ouverte : le texte à l'identique (filet doré, serif), puis source, « Texte
  officiel », conditions de réutilisation et **« Lire la licence »** (CC-BY-ND 4.0 FR ou décision 2011/833/UE). Version
  et date de collecte passent dans le tableau du fond (colonne et infobulle « Version »).
- **Un renvoi [P1] ouvre sa carte**, la place en haut du fil et y porte le focus (`limpide.js`, en capture avant la
  navigation de Blazor ; sans script, le lien mène quand même à la carte) ; nom accessible « Ouvrir le texte cité P1 ».
- Relectures : « Licence : » devant les conditions ; date de collecte en clair dans le pied de carte (la version reste
  au fond) ; « Lire » / « Replier » dans le nom du résumé (commande vocale) ; **jamais d'extrait sans lien de
  licence** : licence non reconnue, lien vers « Sources et licences » des mentions légales (plutôt qu'un test, qui
  aurait mis le code dans `Limpide.Core` et relancé l'évaluation).
- Réponses bien plus courtes à l'écran : la réponse, trois lignes de textes, la ligne du fond.
- Corrigés en passant : bande vide entre les textes et le fond (marge des anciennes sections) ; relecture de la PR
  #73 (opacité du bouton en attente retirée, message `<noscript>` sous le champ).
- Vérifié au pilote avec de vraies questions : cartes repliées, [P2] ouvre P2 avec le focus sur son titre ; 1440 px,
  390 px en sombre ; aucun défilement horizontal.

### Radiographie en direct (2026-11-02, après la refonte)

Prévue par l'ADR-022, décidée le jour même (avant le script de la vidéo).

- **Un vrai point d'étape** : `AnswerService.AskAsync` accepte un `IProgress<AnswerProgress>` facultatif et publie la
  fin de la recherche (passages, durées de vectorisation et de requête, seuil atteint ou non) avant la rédaction.
  Rien n'est simulé : ce sont les valeurs que la réponse finale reprend. Console et évaluation inchangées (paramètre
  facultatif). 3 tests (publié avant l'appel au modèle, seuil non atteint, rien pour une injection).
- **Pendant l'attente**, le fond est ouvert (« En ce moment ») : la vectorisation en cours et les autres étapes en
  attente ; puis, la recherche finie, étapes 1 à 3 faites avec leurs vraies durées, rédaction en cours, passages sur
  la règle de proximité, et leur liste dessous avec leur proximité (écrans O2, O3). Annonce « 5 passages trouvés.
  Rédaction de la réponse en cours. »
- **Animation d'attente** (renforcée après essai d'Alexandre : le point qui pulse ne se voyait pas, et la liste des
  passages repousse le fond sous le champ dès la recherche finie) : un segment qui balaie le filet de l'étape en
  cours et une barre sous le texte d'attente de la surface, là où le regard se pose ; « en cours… » écrit sous
  l'étape. Rien ne bouge sous `prefers-reduced-motion` (barre de la surface masquée, textes suffisants).
- La réponse arrivée, le fond se replie sur sa ligne de résumé, comme avant.
- Relectures : avant la fin de la recherche, « contrôle de la question, puis vectorisation » (une question arrêtée
  par le contrôle d'injection ne publie aucun point d'étape : rien n'affirme une vectorisation qui n'a pas lieu) ;
  relais protégé si la page se ferme pendant l'attente ; accords au singulier (« 1 passage trouvé ») ; classe des
  étapes en attente distincte de celle du texte d'attente. `/interface-review` : « en cours » écrit, pas seulement
  animé ; textes d'attente qui n'annoncent ni une recherche ni une rédaction que le chemin suivi ne prendra pas
  (« Préparation de la réponse », puis « le modèle ne sera pas appelé » sous le seuil).
- Relecture de la PR #74 corrigée en passant : à 320 px, titre des cartes sur toute la largeur et action dessous
  (les titres longs des fiches de la CNIL se coupaient au milieu des mots).
- Vérifié au pilote (captures pendant l'attente) : 1440 px, 320 px en sombre.

### AI Act consolidé : Omnibus numérique et rectificatif (2026-11-05, hors plan)

Constat du 2026-11-04 : le corpus servait le texte d'origine de l'AI Act (JO du 12.7.2024) ; l'Omnibus numérique
(règlement 2026/1744, en vigueur le 27.7.2026) en a modifié ou ajouté au moins 41 articles et annexes, dont l'article 113 : la démo
annonçait les obligations du haut risque au 2 août 2026 au lieu du 2 décembre 2027. Décisions et mesures : ADR-023.

- **Nouvel extracteur** `eur-lex-consolide/1` (format HTML d'une consolidation, sans rapport avec celui du JO),
  choisi selon le format du fichier ; découpeur `ai-act/1` (accepte « art_4a ») sans incrément de version : ses 634
  passages du JO redécoupés sont identiques au caractère près à ceux de la base (exception validée par Alexandre). 13 tests sur un extrait
  réel (`Fixtures/ai-act-consolide-extraits.html`), 1 test du découpeur, 2 tests du chargeur de corpus.
- **Renommage d'un document** par `previousUrls` (`corpus.json`) : la version consolidée devient une nouvelle version
  du même document, avec le lien de la version consolidée datée. Vérifié dans la copie : un document, deux
  versions, nouvelle collecte « inchangée ».
- **Mesure dans une copie de la base** (`rag_consolide`) : `fetch` → candidate → **quarantaine** (32 % de passages en
  moins : les 250 passages de considérants, absents d'une consolidation) → `release` → `embed` (0,012 €) → `publish`. Recherche :
  17 → 21/30 en 1re position, MRR 0,72 → 0,80 ; réponses : 49/55 vérifiées, 1 échec (`q06`, passage des 35 M€ devancé),
  `gate` passée. Considérants abandonnés (choix d'Alexandre).
- **Interface** : avertissement de la version consolidée sous chaque extrait (conditions de réutilisation) et dans
  les mentions légales ; le lien de la carte garde le libellé « Texte officiel », avec un nom accessible propre à
  chaque document.
- **En production (2026-11-06)** : PR #77 fusionnée et déployée. DAG `ingestion` lancé à la main, deux passages :
  - premier passage : quarantaine (écart de 0,323, seuil 0,3), candidate `248c47ff5dd6`, la même empreinte que dans la
    copie ;
  - `release`, puis second passage (1 min 38 s) : 18 documents vérifiés, 1 version publiée, 429 passages vectorisés,
    120 950 tokens, ≈ 0,0121 € ; 2 versions collectées écartées (texte identique) ;
  - `/sante` : 925 passages (1 130 avant), `missingEmbeddings` 0, empreinte `d1ab6824e180` ; la question sur la date
    du haut risque, posée à la démo, donne les nouvelles dates.
  Puis `deploy/exporter-corpus.sh --vps` : sauvegarde relue, 925 passages, même empreinte.
- **Points ouverts** : `q06` (passage des amendes hors des 5 fournis) ; veille des actes modificatifs, manuelle.

### Évaluation : faits attendus et chiffres sourcés (2026-11-06, hors plan)

Suite de l'AI Act consolidé : l'évaluation contrôlait l'article cité, jamais ce que la réponse en dit. Mesure sur
`rag_consolide`, rapport `2026-11-06-0816`. Décision : ADR-014, « Faits attendus ».

- **Deux contrôles** : `expectedFacts` dans `questions.json` (fait absent = échec, seuil bloquant) et
  `AnswerFigures` (date, montant, pourcentage ou durée absents des passages cités = à relire, seuil signalé).
- **Vérification sur les rapports** : le contrôle des faits aurait échoué sur `q05` du 2026-10-28 ; celui des chiffres
  relève les 35 M€ et les 7 % de `q06`. Une fausse alerte sur trois passes : « 20 jours », écrit « vingtième jour »
  dans le texte (`q05`, passe de la chaîne de la PR #79).
- **Jeu `jeu/4`** : `q31` (date du haut risque), `q32` (article 4 réécrit), `q33` (date de la nouvelle interdiction de
  l'article 5) ; faits ajoutés à `q05` et `q06`. **Tout est à relire par Alexandre** (rédigé par Claude).
- **Mesure** : 51/58 vérifiées, 1 échec, 6 à relire, passage attendu cité 31/33, 8/36 jugées infidèles, `gate` passée.
- **`q33` : vraie erreur trouvée.** La réponse dit l'interdiction applicable « sans délai supplémentaire », alors
  qu'elle s'applique à partir du 2 décembre 2026 : l'article 113 n'est pas parmi les 5 passages fournis. Seuil à 1
  pour elle, à ramener à 0.
- **Correction le même jour** : 6 passages (35 M€ sourcés sur `q06`) et `answer/7` (ne plus conclure « sans délai »),
  mesurés configuration par configuration (ADR-014) ; `answer/6`, trop large, écartée (`q16` refusée, `q03` ambiguë).
  Rapport `2026-11-06-0844` : 51/58 vérifiées, 2 échecs (`q30`, `q33`), aucun chiffre sans passage cité, 8/36 infidèles.
- **Points ouverts, à traiter avant la vidéo de la S9** : `q33` reste fausse à moitié (« s'applique dès que ces
  conditions sont remplies », sans la date : la règle et sa date sont dans deux articles), `expectedFactsMissing` à
  ramener à 0.
- **`q30` : faux positif corrigé.** `qualification-juridique` réagissait à « base légale » (terme du RGPD) dans une
  phrase avec « votre » ou « vous » : ce n'était ni « vous pouvez… à condition », ni un effet des 6 passages. Passe de
  la chaîne de la PR #81 : `q30` passe, mais `q01` est tronquée (`reponse-tronquee`), donc toujours 2 échecs : à mesurer,
  `MaxOutputTokens` à relever si ça se confirme avec 6 passages.

### Déploiement bloqué par une faille d'apt (2026-11-06, hors plan)

Les déploiements des PR #79 et #80 ont échoué à l'analyse des images : CVE-2026-109518 (apt, élevée, correctif publié le
2026-11-04), présente dans l'image Microsoft du 2026-11-05 (apt 2.8.3). L'attaque vise l'installation d'un paquet par
apt ; aucune image ne lance apt, et elles tournent sans droits root. Exception datée dans `deploy/grype.yaml`, vérifiée
par `deploy/analyser-images.sh` (2 exceptions, ✅). Rien ne rappelle l'échéance : **retirer l'exception au plus tard le
2026-11-30**, dès qu'une image de base embarque apt 2.8.4ubuntu0.1.

### Barrière : le juge de fidélité varie plus que sa marge (2026-11-06, hors plan)

La chaîne de la PR #84 (`MaxOutputTokens` à 1 500) a bloqué deux fois sur `judge.unfaithfulShare`, puis est passée, sur
le **même commit** : 11/36, 11/36, 7/36. Le seuil de 0,30 tolère 10 réponses sur 36. Passes du jour avant elle : 7, 9, 7,
7, 9.

- **La PR n'est pas en cause.** Seule `q01` a dépassé 1 000 tokens (1 122, essai 1 ; 834 et 692 ensuite) : le relèvement
  a évité une troncature, et il ne peut pas avoir changé plus d'un verdict sur 36.
- **Un noyau stable et une frange** : `j02`, `q01`, `q03`, `q07`, `q16`, `q21` sont infidèles aux trois essais ; `q06`,
  `q08`, `q13`, `q15`, `q25`, `q33`, `t05` vont et viennent. Verdicts d'un modèle, à relire.
- **Le vert du troisième essai ne prouve rien** : il tient au tirage, comme les deux blocages. La fusion se justifie par
  le premier point, pas par ce vert.
- **Non tranché** : le niveau tourne autour de 0,25, au-dessus des références de l'ADR-014 (0,15 et 0,22). Vrai recul,
  ou effet du corpus consolidé et des 6 passages ? Mesure de la dispersion en S10 (plan global).
