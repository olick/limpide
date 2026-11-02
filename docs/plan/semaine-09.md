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
- [ ] **Passages CNIL** : lien vers le texte de la licence CC-BY-ND 4.0 FR sous chaque extrait.
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
  défaut** (consentement ; cochée dans la maquette).
- **Placement** : la nouvelle question en haut de la zone qui défile (sans animation si mouvement réduit), le focus
  sur son titre à l'arrivée de la réponse ; au retour de l'avis, focus sur la question concernée.
- **« Sous le capot » devient « Au fond »** partout (accueil, « Ce que fait Limpide », schéma de « Comment il est
  construit ») ; la troisième question de l'avis aussi, par choix d'Alexandre : « La couche « Au fond » (comment la
  réponse a été produite) vous a-t-elle servi à quelque chose ? ». Les avis d'avant le 2026-11-02 répondaient à
  l'ancienne formulation (libellé du back office).
- **Pas fait** : la radiographie en direct (O2, O3 : étapes qui s'allument pendant l'attente) demande que
  `AnswerService` publie son avancement ; l'attente affiche une phrase fixe. À décider.
- Vérifié avec de vraies questions, Firefox piloté par WebDriver BiDi (script Node sans dépendance, hors dépôt) :
  1440 px clair, 390 px clair et sombre, 320 px ; débordements corrigés (`minmax(0, 1fr)` sur les couches et le
  détail du fond).