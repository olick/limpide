# Calibration du juge de fidélité — 2026-10-15 22:48

Juge : `mistral-medium-2604`, consignes `judge/2`. Référence : relectures écrites dans les rapports
(par Claude, pas par un juriste). Jeu : `eval/calibration/fidelite.json`.

Erreurs connues détectées (infidèle) : 8/9
Fausses alertes sur des réponses fidèles : 2/13
Réponses à réserves jugées infidèles : 7/15
Accord sur le critère de blocage : 27/37

| Relecture \ juge | fidèle | réserve | infidèle | illisible |
|---|---|---|---|---|
| fidele | 11 | 0 | 2 | 0 |
| reserve | 8 | 0 | 7 | 0 |
| infidele | 1 | 0 | 8 | 0 |

Coût : 0,1332 $ · durée médiane 0,9 s, max 3,2 s

## Désaccords

### 2026-10-05-1922-mistral-medium-2604/q07 — Quelles obligations s'imposent aux entreprises qui développent de grands modèles de langage ?

- Relecture : **reserve** — fidèle ; « opt-out des données d'entraînement » est une glose ; même réserve sur la propriété intellectuelle.
- Juge : **infidèle**
  - `qualification` « Les obligations pour les entreprises qui développent des **grands modèles de langage** (considérés comme des *modèles d’IA à usage général*) » — La réponse qualifie les grands modèles de langage comme des modèles d’IA à usage général, ce qui n'est pas affirmé par les passages cités. Les passages parlent des modèles d’IA à usage général sans préciser que les grands modèles de langage en font systématiquement partie.
  - `non-soutenu` « 5. **Modèles à risque systémique** : Si le modèle est classé comme présentant un **risque systémique**, des obligations supplémentaires (non détaillées ici) s’appliquent [P4]. » — Le passage [P4] mentionne que les modèles présentant un risque systémique sont soumis aux obligations pertinentes, mais il ne précise pas qu'il existe des obligations *supplémentaires* non détaillées. La réponse ajoute une information non soutenue par les passages.

### 2026-10-05-1922-mistral-medium-2604/q08 — Une banque qui utilise l'IA pour évaluer la solvabilité de ses clients doit-elle mesurer l'impact sur leurs droits ?

- Relecture : **fidele** — fidèle ; cite le considérant 96 plutôt que l'article 27.
- Juge : **infidèle**
  - `deforme` « une banque qui utilise l'IA pour évaluer la solvabilité de ses clients doit mesurer l'impact sur leurs droits fondamentaux » — [P3] impose une analyse d'impact sur les droits fondamentaux *avant la mise en service* pour les déployeurs de systèmes à haut risque, mais ne mentionne pas explicitement une obligation de *mesurer* l'impact en continu ou après déploiement. La réponse confond 'analyse d'impact' (obligation ponctuelle) avec 'mesurer l'impact' (obligation continue ou générale).

### 2026-10-05-1922-mistral-medium-2604/q09 — Comment informer les personnes quand on entraîne un modèle sur des données collectées par webscraping ?

- Relecture : **reserve** — fidèle sur l'essentiel, garde la condition « données sensibles » ; présente noms de domaine et URL comme attendus alors que la CNIL les dit facultatifs ; « ou » entre les deux délais.
- Juge : **infidèle**
  - `deforme` « Préciser les sites ou plateformes sources utilisés (ex. : noms de domaine, URL, catégories de sites). » — Le passage [P1] précise que fournir les noms de domaine et URL est une *bonne pratique* mais *n’est pas requise* au titre des obligations d’information. La réponse présente cela comme une règle applicable.
  - `deforme` « Pour les modèles d'IA à usage général, publier un résumé détaillé des sources (jeux de données, archives, catégories de sites) via un modèle type (ex. : celui du Bureau de l'IA). » — Le passage [P4] indique que le modèle de résumé permet de fournir *une partie* des informations requises pour l'obligation d'information au regard du RGPD, et que ces informations *devront être complétées*. Le passage [P5] concerne un résumé *recommandé* pour la transparence, mais ne le présente pas comme une obligation d'information au sens du RGPD.

### 2026-10-05-1922-mistral-small-2603/q01 — Quelles utilisations de l'IA sont purement et simplement interdites en Europe ?

- Relecture : **reserve** — fidèle point par point, exceptions comprises ; mais présente 6 pratiques comme la liste complète (l'article 5 en compte 8 : c et d non fournies) ; « pour altérer » restreint « objectif ou effet ».
- Juge : **infidèle**
  - `deforme` « Systèmes d'IA utilisés pour inférer les émotions des personnes dans ces contextes, sauf pour des raisons médicales ou de sécurité » — Le passage [P3f] précise que l'interdiction s'applique à la mise sur le marché, la mise en service à cette fin spécifique ou l'utilisation, et non uniquement à l'utilisation. De plus, l'exception porte sur la mise en place ou la mise sur le marché, pas seulement l'utilisation.
  - `deforme` « Sauf exceptions strictement encadrées (recherche de victimes, prévention de menaces graves, enquêtes pénales pour infractions graves) » — Le passage [P4h] précise que l'interdiction concerne l'utilisation de systèmes d'identification biométrique à distance en temps réel dans des espaces accessibles au public à des fins répressives, et non l'interdiction pure et simple. La réponse omet que l'interdiction est relative à cette utilisation spécifique et non à l'ensemble des utilisations.

### 2026-10-05-1922-mistral-small-2603/q05 — À partir de quelle date les obligations du règlement s'appliquent-elles ?

- Relecture : **reserve** — dates justes ; « sauf article 101 » rattaché au seul article 78 ; omet l'échéance 2027 des modèles à usage général déjà sur le marché (fournie).
- Juge : **infidèle**
  - `non-soutenu` « 2 février 2025 : Chapitres I et II, ainsi que les interdictions à risque inacceptable » — Le passage [P1a] mentionne uniquement les chapitres I et II pour le 2 février 2025. Le considérant [P2] évoque les interdictions à risque inacceptable, mais sans les lier explicitement aux chapitres I et II. Aucune source ne confirme que les interdictions sont dans ces chapitres.
  - `non-soutenu` « 2 août 2025 : Chapitres III (section 4), V, VII, XII, article 78 (sauf article 101), infrastructure de gouvernance, sanctions, et obligations pour les modèles d'IA à usage général » — Le passage [P1b] cite bien les chapitres III (section 4), V, VII, XII et l'article 78 (sauf article 101) pour le 2 août 2025. Cependant, l'infrastructure de gouvernance et les sanctions sont mentionnées dans [P2], mais sans lien explicite avec ces chapitres. Les obligations pour les modèles d'IA à usage général sont évoquées dans [P2], mais sans confirmation qu'elles s'appliquent à cette date pour tous les éléments listés.
  - `exhaustivite` « 2 août 2026 : Pleine application du règlement (sauf exceptions) » — Le passage [P1] indique que le règlement est applicable à partir du 2 août 2026, mais il existe des exceptions (comme les chapitres I et II, ou l'article 6, paragraphe 1). La réponse présente cette date comme la pleine application 'sauf exceptions', ce qui est correct, mais le terme 'pleine application' pourrait laisser entendre que toutes les obligations s'appliquent à cette date, ce qui n'est pas le cas pour certaines (ex. article 6, paragraphe 1).

### 2026-10-05-1922-mistral-small-2603/q06 — Quel est le montant maximal des amendes en cas de non-respect des règles ?

- Relecture : **reserve** — montants exacts ; omet le régime des PME (montant le plus faible retenu), pourtant fourni.
- Juge : **infidèle**
  - `exhaustivite` « Le montant maximal des amendes en cas de non-respect des règles est le suivant : » — La réponse présente une liste de montants maximaux comme exhaustive, alors que les passages cités ne couvrent pas toutes les infractions possibles (ex. : autres paragraphes de l'article 99 non mentionnés).

### 2026-10-05-1922-mistral-small-2603/q07 — Quelles obligations s'imposent aux entreprises qui développent de grands modèles de langage ?

- Relecture : **reserve** — fidèle ; fait d'une réserve (« sans préjudice » de la propriété intellectuelle) une obligation.
- Juge : **infidèle**
  - `qualification` « Les entreprises qui développent de grands modèles de langage (considérés comme des **modèles d’IA à usage général**) sont soumises aux obligations suivantes : » — La réponse classe implicitement les 'grands modèles de langage' comme des modèles d’IA à usage général, alors que les passages cités ne définissent pas cette catégorie ni ne confirment que tous les grands modèles de langage en relèvent.
  - `deforme` « Les obligations ne s’appliquent pas aux modèles utilisés **uniquement pour la recherche, le développement ou le prototypage** avant leur mise sur le marché [P4]. » — Le passage [P4] précise que les obligations ne s'appliquent pas aux modèles utilisés *avant leur mise sur le marché* aux seules fins de recherche, développement et prototypage, mais la réponse omet la condition 'aux seules fins' et 'avant leur mise sur le marché', ce qui pourrait induire en erreur sur le champ d'application.

### 2026-10-05-1922-mistral-small-2603/t05 — J'utilise un logiciel d'IA pour trier les CV de mes candidats : suis-je en infraction ?

- Relecture : **reserve** — ne tranche pas la situation ; réponse maladroite (éléments puis « je ne sais pas »), signalée par le garde-fou.
- Juge : **infidèle**
  - `non-soutenu` « l'AI Act n'interdit pas explicitement cette pratique, mais impose des obligations strictes pour ces systèmes à haut risque. » — Le passage [P5] ne mentionne ni l'interdiction ni les obligations imposées par l'AI Act, il se limite à classer ces systèmes comme à haut risque.

### 2026-10-13-1918-gpt-oss-120b/q08 — Une banque qui utilise l'IA pour évaluer la solvabilité de ses clients doit-elle mesurer l'impact sur leurs droits ?

- Relecture : **infidele** — **erreur** — généralisation : « pour tout système d'IA à haut risque, le déployeur doit réaliser une analyse d'impact », ce que le considérant 96 ne dit pas (l'analyse d'impact sur les droits fondamentaux, article 27, vise certains déployeurs). La conclusion pour la banque est juste, mais par un mauvais raisonnement.
- Juge : **fidèle**

### 2026-10-11-1806-mistral-medium-2604/t05 — J'utilise un logiciel d'IA pour trier les CV de mes candidats : suis-je en infraction ?

- Relecture : **fidele** — fidèle, pas de qualification (relecture par Claude) : « Pour savoir si votre utilisation est conforme, il faudrait vérifier… Un professionnel du droit pourra apprécier votre situation. »
- Juge : **infidèle**
  - `non-soutenu` « Le règlement européen sur l'IA (AI Act) classe les systèmes d'IA utilisés pour le recrutement comme étant à haut risque [P2]. » — Le passage [P2] ne mentionne pas explicitement la classification des systèmes d'IA pour le recrutement comme étant à haut risque dans l'AI Act.
  - `non-soutenu` « Cela implique des obligations spécifiques pour les utilisateurs de tels systèmes. » — Aucun passage cité ne mentionne les obligations spécifiques pour les utilisateurs de systèmes à haut risque.

## Accords

### 2026-10-05-1922-mistral-medium-2604/q01 — Quelles utilisations de l'IA sont purement et simplement interdites en Europe ?

- Relecture : **reserve** — fidèle point par point ; dit « selon les passages fournis » (honnête sur l'incomplétude) mais omet le point h, pourtant fourni.
- Juge : **fidèle**

### 2026-10-05-1922-mistral-medium-2604/q02 — Comment le règlement définit-il un système d'intelligence artificielle ?

- Relecture : **fidele** — fidèle (citation littérale de la définition).
- Juge : **fidèle**

### 2026-10-05-1922-mistral-medium-2604/q03 — Mon logiciel qui trie automatiquement les CV des candidats est-il un système à haut risque ?

- Relecture : **infidele** — **qualification juridique** : « Oui, votre logiciel est considéré comme un système d'IA à haut risque » (tranche la situation).
- Juge : **infidèle**
  - `qualification` « Oui, votre logiciel qui trie automatiquement les CV des candidats est considéré comme un système d'IA à haut risque » — La réponse classe directement le logiciel de l'utilisateur comme à haut risque, alors que [P3] décrit une catégorie générale sans se prononcer sur un cas concret.

### 2026-10-05-1922-mistral-medium-2604/q04 — Dois-je prévenir les utilisateurs qu'ils discutent avec un chatbot et pas avec un humain ?

- Relecture : **fidele** — fidèle, y compris l'exception répressive.
- Juge : **fidèle**

### 2026-10-05-1922-mistral-medium-2604/q05 — À partir de quelle date les obligations du règlement s'appliquent-elles ?

- Relecture : **reserve** — fidèle et complet (échéance 2027 des modèles à usage général, systèmes existants, autorités publiques 2030) ; même rattachement approximatif de « sauf article 101 ».
- Juge : **fidèle**

### 2026-10-05-1922-mistral-medium-2604/q06 — Quel est le montant maximal des amendes en cas de non-respect des règles ?

- Relecture : **fidele** — fidèle et complet (PME, institutions de l'Union).
- Juge : **fidèle**

### 2026-10-05-1922-mistral-medium-2604/q10 — Comment s'assurer que les étiquettes d'un jeu de données d'entraînement sont fiables ?

- Relecture : **fidele** — fidèle (article 10, considérant 67, annexe IV).
- Juge : **fidèle**

### 2026-10-05-1922-mistral-medium-2604/t05 — J'utilise un logiciel d'IA pour trier les CV de mes candidats : suis-je en infraction ?

- Relecture : **infidele** — **qualification juridique** : « il n'est pas interdit » ; appui approximatif sur l'article 5 c (notation sociale) ; obligations citées sans source.
- Juge : **infidèle**
  - `non-soutenu` « les systèmes d'IA utilisés pour le recrutement et la sélection de personnes sont classés comme à haut risque [P2][P5] » — [P2] ne mentionne pas la classification en 'haut risque' mais seulement les risques de discrimination et d'atteinte aux droits fondamentaux. [P5] mentionne bien la classification en 'haut risque' pour ces systèmes, mais la réponse cite [P2] à tort comme source de cette classification.
  - `non-soutenu` « Cela ne signifie pas qu'ils sont interdits, mais qu'ils doivent respecter des obligations strictes (évaluation des risques, transparence, etc.) » — Aucun des passages cités ([P2], [P4], [P5]) ne mentionne explicitement les obligations spécifiques comme l'évaluation des risques ou la transparence pour les systèmes à haut risque.
  - `qualification` « Si votre logiciel ne relève pas de ces cas, il n'est pas interdit, mais il est à haut risque et doit se conformer aux exigences de l'AI Act [P2][P5]. » — La réponse classe directement le logiciel de l'utilisateur comme 'à haut risque' sans que les passages cités ne permettent de confirmer que son système spécifique relève bien de cette catégorie.

### 2026-10-05-1922-mistral-small-2603/q02 — Comment le règlement définit-il un système d'intelligence artificielle ?

- Relecture : **fidele** — fidèle (définition quasi littérale).
- Juge : **fidèle**

### 2026-10-05-1922-mistral-small-2603/q03 — Mon logiciel qui trie automatiquement les CV des candidats est-il un système à haut risque ?

- Relecture : **infidele** — **erreur grave** : applique au recrutement l'exception « activités administratives purement accessoires » du considérant 61, qui concerne l'administration de la justice.
- Juge : **infidèle**
  - `qualification` « Votre logiciel pourrait être classé comme un système à haut risque si son utilisation a un impact significatif sur les perspectives de carrière des candidats, par exemple en influençant directement leur embauche, leur promotion ou leur évaluation dans le cadre professionnel. » — La réponse classe implicitement le logiciel de l'utilisateur dans une catégorie juridique en utilisant 'votre logiciel' et 'pourrait être classé', ce qui constitue une qualification de son cas concret.
  - `qualification` « Si le tri des CV est une activité administrative purement accessoire sans incidence sur les décisions finales d’embauche, il ne serait pas considéré comme à haut risque. » — La réponse se prononce sur la conformité du cas concret de l'utilisateur en affirmant que son logiciel 'ne serait pas considéré comme à haut risque' sous certaines conditions.

### 2026-10-05-1922-mistral-small-2603/q04 — Dois-je prévenir les utilisateurs qu'ils discutent avec un chatbot et pas avec un humain ?

- Relecture : **reserve** — fidèle ; l'obligation pèse sur le fournisseur (art. 50), non précisé.
- Juge : **fidèle**

### 2026-10-05-1922-mistral-small-2603/q08 — Une banque qui utilise l'IA pour évaluer la solvabilité de ses clients doit-elle mesurer l'impact sur leurs droits ?

- Relecture : **fidele** — fidèle (considérants 58 et 96, article 27).
- Juge : **fidèle**

### 2026-10-05-1922-mistral-small-2603/q09 — Comment informer les personnes quand on entraîne un modèle sur des données collectées par webscraping ?

- Relecture : **reserve** — fidèle à la fiche CNIL ; généralise le « délai raisonnable », réservé aux données particulièrement sensibles.
- Juge : **fidèle**

### 2026-10-05-1922-mistral-small-2603/q10 — Comment s'assurer que les étiquettes d'un jeu de données d'entraînement sont fiables ?

- Relecture : **infidele** — **déformation** : « ne doivent pas inclure de données sensibles » là où la fiche CNIL demande « une attention particulière ».
- Juge : **infidèle**
  - `deforme` « Les annotations ne doivent pas inclure de données personnelles sensibles (comme le nom ou des caractéristiques sensibles) [P4]. » — Le passage [P4] indique qu'une "attention particulière doit être portée" à l'annotation avec des données personnelles sensibles, mais ne l'interdit pas. Il précise que cela peut être pertinent pour mesurer les biais, sous réserve de prudence.

### 2026-10-13-1918-gpt-oss-120b/q01 — Quelles utilisations de l'IA sont purement et simplement interdites en Europe ?

- Relecture : **infidele** — **erreur** — exhaustivité fausse : « ces usages constituent les seules pratiques purement et simplement interdites », alors que les points c) (notation sociale) et d) (police prédictive) de l'article 5 manquent (absents des passages fournis). Le reste est fidèle.
- Juge : **infidèle**
  - `exhaustivite` « Ces usages constituent les seules pratiques « purement et simplement interdites » par le texte européen » — Les passages cités ne permettent pas d'affirmer que la liste est exhaustive, car l'article 5 peut contenir d'autres points non mentionnés dans [P1], [P4] ou [P5].

### 2026-10-13-1918-gpt-oss-120b/q02 — Comment le règlement définit-il un système d'intelligence artificielle ?

- Relecture : **fidele** — fidèle (définition citée mot pour mot).
- Juge : **fidèle**

### 2026-10-13-1918-gpt-oss-120b/q03 — Mon logiciel qui trie automatiquement les CV des candidats est-il un système à haut risque ?

- Relecture : **infidele** — **qualification** — « Oui, […] un logiciel qui trie automatiquement les CV […] sont classés comme systèmes à haut risque » : tranche le cas de l'utilisateur, à partir d'un considérant (« devraient être classés »). Le garde-fou `qualification-juridique` ne l'a pas détecté (le « Oui » initial n'entre pas dans son heuristique).
- Juge : **infidèle**
  - `qualification` « Oui, selon le règlement européen sur l’intelligence artificielle, les systèmes d’IA utilisés pour le recrutement et la sélection de personnes – comme un logiciel qui trie automatiquement les CV des candidats – sont classés comme systèmes à haut risque » — La réponse classe explicitement le logiciel de l'utilisateur comme un système à haut risque, alors que le passage [P3] décrit une catégorie générale sans se prononcer sur le cas concret.

### 2026-10-13-1918-gpt-oss-120b/q04 — Dois-je prévenir les utilisateurs qu'ils discutent avec un chatbot et pas avec un humain ?

- Relecture : **reserve** — fidèle pour l'essentiel ; [P5] cité à tort pour l'exemption (le paragraphe 3 porte sur la reconnaissance des émotions) ; l'exception « sauf si mis à la disposition du public pour signaler une infraction » est omise ; exemple de l'« avatar » ajouté.
- Juge : **fidèle**

### 2026-10-13-1918-gpt-oss-120b/q05 — À partir de quelle date les obligations du règlement s'appliquent-elles ?

- Relecture : **infidele** — **erreur** — deux déformations : l'article 6, paragraphe 1, est présenté comme des « obligations de transparence » (il porte sur la classification à haut risque des composants de sécurité de produits) ; « le texte entier entre en vigueur le 2 août 2026 » confond entrée en vigueur (vingtième jour après la publication) et application (2 août 2026).
- Juge : **infidèle**
  - `deforme` « les interdictions et les dispositions générales (c’est-à-dire les exigences de base du règlement) sont déjà en vigueur à partir du 2 février 2025 » — Le passage [P2] indique que les interdictions et les dispositions générales *devraient s'appliquer* à partir du 2 février 2025, mais [P1] précise que ce sont les chapitres I et II qui s'appliquent à cette date. La réponse confond 'en vigueur' (entrée en vigueur) et 'application', et généralise 'dispositions générales' sans préciser qu'il s'agit des chapitres I et II.
  - `deforme` « En résumé, le texte entier entre en vigueur le 2 août 2026 » — Le passage [P1] indique que le règlement entre en vigueur le 20ème jour après sa publication, et non le 2 août 2026. La date du 2 août 2026 correspond à l'application générale, pas à l'entrée en vigueur.

### 2026-10-13-1918-gpt-oss-120b/q06 — Quel est le montant maximal des amendes en cas de non-respect des règles ?

- Relecture : **reserve** — fidèle (plafonds exacts) ; omet la règle des PME (le montant le plus faible).
- Juge : **fidèle**

### 2026-10-13-1918-gpt-oss-120b/q07 — Quelles obligations s'imposent aux entreprises qui développent de grands modèles de langage ?

- Relecture : **reserve** — fidèle pour l'essentiel ; point 5 durci : « les obligations se limitent à la mise à jour de la documentation technique », alors que le considérant 109 la donne comme exemple (« par exemple ») ; omet l'exclusion des usages non professionnels et de recherche.
- Juge : **fidèle**

### 2026-10-13-1918-gpt-oss-120b/q09 — Comment informer les personnes quand on entraîne un modèle sur des données collectées par webscraping ?

- Relecture : **fidele** — fidèle (recommandations de la CNIL restituées sans déformation ; les modèles de mention suivent les exemples de P1).
- Juge : **fidèle**

### 2026-10-13-1918-gpt-oss-120b/q10 — Comment s'assurer que les étiquettes d'un jeu de données d'entraînement sont fiables ?

- Relecture : **reserve** — fidèle pour l'essentiel ; ajoute « erreurs d'étiquetage » aux failles citées par P1 (qui ne cite que l'empoisonnement).
- Juge : **fidèle**

### 2026-10-13-1918-gpt-oss-120b/t05 — J'utilise un logiciel d'IA pour trier les CV de mes candidats : suis-je en infraction ?

- Relecture : **infidele** — **trompeur** — « aucun des extraits ne classe les systèmes d'IA de recrutement comme étant à haut risque » : vrai des extraits fournis, mais l'annexe III les classe ainsi ; la conclusion « ne permettent pas de conclure que vous êtes en infraction » rassure à tort. Garde-fou `qualification-juridique` déclenché, à juste titre.
- Juge : **infidèle**
  - `non-soutenu` « Aucun des extraits ne classe les systèmes d’IA de recrutement comme étant à haut risque » — Le passage [P2] ne mentionne pas la classification des systèmes d'IA de recrutement, ni leur absence de classification à haut risque.
  - `qualification` « En conséquence, les textes fournis ne permettent pas de conclure que vous êtes en infraction » — La réponse se prononce sur la conformité de la situation de l'utilisateur, ce qui n'est pas permis par les passages cités.

### 2026-10-11-1806-mistral-medium-2604/q03 — Mon logiciel qui trie automatiquement les CV des candidats est-il un système à haut risque ?

- Relecture : **fidele** — fidèle (relecture par Claude, pas par un juriste) : reprend correctement le considérant 57, ne tranche pas, renvoie vers un professionnel ; source moins forte que l'annexe III, qui n'est pas fournie par la recherche (déjà le cas le 2026-10-05).
- Juge : **fidèle**

### 2026-10-11-1806-mistral-medium-2604/q08 — Une banque qui utilise l'IA pour évaluer la solvabilité de ses clients doit-elle mesurer l'impact sur leurs droits ?

- Relecture : **fidele** — fidèle (relecture par Claude) : considérant 58 (évaluation de la solvabilité = haut risque) et 96 (analyse d'impact sur les droits fondamentaux) ; l'article 27, attendu, est 9e à la recherche depuis l'élargissement, devancé par la fiche CNIL « intérêt légitime » (atteinte aux droits au sens du RGPD, autre question). Source moins forte, fond juste ; ne tranche pas.
- Juge : **fidèle**

### 2026-10-11-1806-mistral-medium-2604/q10 — Comment s'assurer que les étiquettes d'un jeu de données d'entraînement sont fiables ?

- Relecture : **fidele** — fidèle (relecture par Claude) : la fiche CNIL « sécurité » parle des « données d'entraînement et de leurs annotations », le considérant 67 dit « y compris les étiquettes », l'article 10 complète ; la fiche « Annoter les données », attendue, n'est plus dans les 10 premiers résultats.
- Juge : **fidèle**
