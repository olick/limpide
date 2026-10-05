# Évaluation des réponses — mistral-medium-2604

- Date : 2026-10-05 19:10
- Modèle : `mistral-medium-2604`, consignes `answer/2`, température 0
- Recherche : vectorielle (bge-m3+titre), 5 passages, seuil de pertinence 0,5
- Jeu : `eval/questions.json`

## Synthèse

- Vérifié automatiquement : 11/15 · échecs : 0 · à relire : 4
- Passage attendu cité : 7/10
- Coût total : 0,054523 $ · modèle appelé pour 12/15 questions
- Durée totale par question : médiane 1,6 s, max 2,7 s

| Question | Attendu | Contrôle | Verdict | Durée | Coût |
|---|---|---|---|---|---|
| q01 | réponse citée | OK | répond en citant le passage attendu | 2,7 s | 0,005181 $ |
| q02 | réponse citée | OK | répond en citant le passage attendu | 1,7 s | 0,004635 $ |
| q03 | réponse citée | À RELIRE | répond sans citer le passage attendu (autre passage pertinent ?) | 1,0 s | 0,003533 $ |
| q04 | réponse citée | OK | répond en citant le passage attendu | 0,9 s | 0,003386 $ |
| q05 | réponse citée | OK | répond en citant le passage attendu | 1,8 s | 0,005021 $ |
| q06 | réponse citée | OK | répond en citant le passage attendu | 2,4 s | 0,005415 $ |
| q07 | réponse citée | OK | répond en citant le passage attendu | 2,7 s | 0,005534 $ |
| q08 | réponse citée | À RELIRE | répond sans citer le passage attendu (autre passage pertinent ?) | 1,6 s | 0,003851 $ |
| q09 | réponse citée | OK | répond en citant le passage attendu | 2,6 s | 0,005730 $ |
| q10 | réponse citée | À RELIRE | répond sans citer le passage attendu (autre passage pertinent ?) | 2,2 s | 0,005178 $ |
| t01 | refus | OK | refus par le seuil de pertinence | 0,1 s | 0,000000 $ |
| t02 | refus | OK | refus par le seuil de pertinence | 0,1 s | 0,000000 $ |
| t03 | refus | OK | refus par le seuil de pertinence | 0,1 s | 0,000000 $ |
| t04 | refus | OK | refus par le modèle | 0,6 s | 0,002955 $ |
| t05 | pas de qualification juridique | À RELIRE | à relire : peut indiquer les textes concernés, ne doit pas trancher la situation | 1,5 s | 0,004104 $ |

## Réponses

Pour chaque réponse : est-elle fidèle aux passages cités (conditions, exceptions) ? Tranche-t-elle une situation juridique ? Noter le verdict sur la ligne « Relecture ».

### q01 — Quelles utilisations de l'IA sont purement et simplement interdites en Europe ?

- Attendu : réponse citée
- Contrôle : **OK** — répond en citant le passage attendu
- Passages cités : P1 Article 5 — Pratiques interdites en matière d’IA ; P3 Article 5 — Pratiques interdites en matière d’IA
- Garde-fous : aucun
- Tokens : 1815 / 265 · coût 0,005181 $ · 2,7 s

> Les utilisations de l'IA purement et simplement interdites en Europe selon les passages fournis sont :
> 
> 1. Les systèmes d’IA utilisant des techniques subliminales ou délibérément manipulatrices/trompeuses pour altérer substantiellement le comportement d’une personne ou d’un groupe, causant un préjudice important [P1a].
> 2. Les systèmes d’IA exploitant les vulnérabilités liées à l’âge, au handicap ou à la situation sociale/économique pour altérer substantiellement le comportement, causant un préjudice important [P1b].
> 3. Les systèmes d’IA créant ou développant des bases de données de reconnaissance faciale par moissonnage non ciblé d’images faciales (internet ou vidéosurveillance) [P3e].
> 4. Les systèmes d’IA inférant les émotions d’une personne sur le lieu de travail ou dans les établissements d’enseignement, sauf pour des raisons médicales ou de sécurité [P3f].
> 5. Les systèmes de catégorisation biométrique déduisant la race, les opinions politiques, l’affiliation syndicale, les convictions religieuses/philosophiques, la vie sexuelle ou l’orientation sexuelle, sauf exceptions précisées [P3g].

**Relecture :** 

### q02 — Comment le règlement définit-il un système d'intelligence artificielle ?

- Attendu : réponse citée (Définition 1) de l'article 3 ; l'article compte 68 définitions réparties sur 14 passages.)
- Contrôle : **OK** — répond en citant le passage attendu
- Passages cités : P3 Article 3 — Définitions
- Garde-fous : aucun
- Tokens : 2274 / 107 · coût 0,004635 $ · 1,7 s

> Le règlement définit un système d'IA comme :
> *« un système automatisé qui est conçu pour fonctionner à différents niveaux d’autonomie et peut faire preuve d’une capacité d’adaptation après son déploiement, et qui, pour des objectifs explicites ou implicites, déduit, à partir des entrées qu’il reçoit, la manière de générer des sorties telles que des prédictions, du contenu, des recommandations ou des décisions qui peuvent influencer les environnements physiques ou virtuels. »* [P3]

**Relecture :** 

### q03 — Mon logiciel qui trie automatiquement les CV des candidats est-il un système à haut risque ?

- Attendu : réponse citée (Annexe III, point 4 (emploi) ; l'article 6 renvoie à l'annexe III.)
- Contrôle : **À RELIRE** — répond sans citer le passage attendu (autre passage pertinent ?)
- Passages cités : P3 Considérant (57)
- Garde-fous : aucun
- Tokens : 1876 / 53 · coût 0,003533 $ · 1,0 s

> Oui, votre logiciel qui trie automatiquement les CV des candidats est considéré comme un système d'IA à haut risque, car il est utilisé pour des questions liées à l'emploi, notamment le recrutement et la sélection de personnes [P3].

**Relecture :** 

### q04 — Dois-je prévenir les utilisateurs qu'ils discutent avec un chatbot et pas avec un humain ?

- Attendu : réponse citée
- Contrôle : **OK** — répond en citant le passage attendu
- Passages cités : P1 Considérant (132) ; P2 Article 50 — Obligations de transparence pour les fournisseurs et les déployeurs de certains systèmes d’IA
- Garde-fous : aucun
- Tokens : 1747 / 61 · coût 0,003386 $ · 0,9 s

> Oui, vous devez prévenir les utilisateurs qu'ils interagissent avec un chatbot (système d'IA) sauf si cela ressort clairement du point de vue d'une personne normalement informée et raisonnablement attentive, compte tenu du contexte [P1][P2].

**Relecture :** 

### q05 — À partir de quelle date les obligations du règlement s'appliquent-elles ?

- Attendu : réponse citée
- Contrôle : **OK** — répond en citant le passage attendu
- Passages cités : P1 Article 113 — Entrée en vigueur et application ; P2 Considérant (179) ; P4 Article 111 — Systèmes d’IA déjà mis sur le marché ou mis en service et modèles d’IA à usage général déjà mis sur le marché
- Garde-fous : aucun
- Tokens : 1868 / 235 · coût 0,005021 $ · 1,8 s

> Les obligations du règlement s'appliquent à partir des dates suivantes :
> 
> - **2 février 2025** : Chapitres I et II (interdictions et dispositions générales) [P1a][P2].
> - **2 août 2025** :
>   - Chapitre III, section 4 ; chapitre V ; chapitre VII ; chapitre XII ; article 78 (sauf article 101) [P1b].
>   - Dispositions relatives aux sanctions, aux organismes notifiés et à la structure de gouvernance [P2].
>   - Obligations pour les fournisseurs de modèles d’IA à usage général [P2].
> - **2 août 2026** : Date générale d’application du règlement [P1][P2].
> - **2 août 2027** :
>   - Article 6, paragraphe 1, et obligations correspondantes [P1c].
>   - Conformité des fournisseurs de modèles d’IA à usage général mis sur le marché avant le 2 août 2025 [P4.3].

**Relecture :** 

### q06 — Quel est le montant maximal des amendes en cas de non-respect des règles ?

- Attendu : réponse citée
- Contrôle : **OK** — répond en citant le passage attendu
- Passages cités : P1 Article 99 — Sanctions ; P2 Article 99 — Sanctions ; P4 Article 99 — Sanctions ; P5 Article 100 — Amendes administratives imposées aux institutions, organes et organismes de l’Union
- Garde-fous : aucun
- Tokens : 1547 / 347 · coût 0,005415 $ · 2,4 s

> Le montant maximal des amendes en cas de non-respect des règles dépend de la nature de l'infraction :
> 
> - **Non-respect des interdictions de l’article 5** :
>   - Jusqu’à **35 000 000 EUR** ou **7 % du chiffre d’affaires annuel mondial total** (le montant le plus élevé étant retenu) [P4].
>   - Pour les institutions, organes ou organismes de l’Union : jusqu’à **1 500 000 EUR** [P5].
> 
> - **Non-conformité avec d’autres obligations** (ex. articles 16, 22, 23, 24, 26, 31, 33, 34, 50) :
>   - Jusqu’à **15 000 000 EUR** ou **3 % du chiffre d’affaires annuel mondial total** (le montant le plus élevé étant retenu) [P1].
>   - Pour les institutions, organes ou organismes de l’Union : jusqu’à **750 000 EUR** [P5].
> 
> - **Fourniture d’informations inexactes, incomplètes ou trompeuses** :
>   - Jusqu’à **7 500 000 EUR** ou **1 % du chiffre d’affaires annuel mondial total** (le montant le plus élevé étant retenu) [P2].
> 
> Pour les **PME (y compris les startups)**, les amendes sont plafonnées au **montant le plus faible** parmi les pourcentages ou montants prévus [P2].

**Relecture :** 

### q07 — Quelles obligations s'imposent aux entreprises qui développent de grands modèles de langage ?

- Attendu : réponse citée (Le règlement parle de « modèles d'IA à usage général », pas de « grands modèles de langage » : teste le vocabulaire.)
- Contrôle : **OK** — répond en citant le passage attendu
- Passages cités : P1 Considérant (109) ; P4 Considérant (97) ; P5 Article 53 — Obligations incombant aux fournisseurs de modèles d’IA à usage général
- Garde-fous : aucun
- Tokens : 2024 / 266 · coût 0,005534 $ · 2,7 s

> Les obligations pour les entreprises qui développent de grands modèles de langage (modèles d’IA à usage général) incluent :
> 
> - Élaborer et tenir à jour la **documentation technique** du modèle (processus d’entraînement, essais, évaluation), avec les informations de l’**annexe XI**, à fournir sur demande au Bureau de l’IA et aux autorités nationales compétentes [P5.1.a].
> - Fournir aux **fournisseurs de systèmes d’IA** des informations et une documentation (annexe XII) pour comprendre les capacités/limites du modèle et se conformer à leurs obligations, tout en respectant les droits de propriété intellectuelle et les secrets commerciaux [P5.1.b].
> - Mettre en place une **politique de conformité au droit d’auteur et droits voisins**, incluant l’identification et le respect des réservations de droits (ex. : technologies de pointe) [P5.1.c].
> 
> Ces obligations s’appliquent **une fois le modèle mis sur le marché** [P4]. Les PME peuvent utiliser des méthodes simplifiées de mise en conformité [P1]. Les modèles utilisés en interne (non essentiels à un service tiers et sans impact sur les droits des personnes) sont exclus [P4].

**Relecture :** 

### q08 — Une banque qui utilise l'IA pour évaluer la solvabilité de ses clients doit-elle mesurer l'impact sur leurs droits ?

- Attendu : réponse citée
- Contrôle : **À RELIRE** — répond sans citer le passage attendu (autre passage pertinent ?)
- Passages cités : P1 Considérant (58) ; P3 Considérant (96)
- Garde-fous : aucun
- Tokens : 1834 / 100 · coût 0,003851 $ · 1,6 s

> Oui, une banque qui utilise l'IA pour évaluer la solvabilité de ses clients doit mesurer l'impact sur leurs droits fondamentaux, car les systèmes d'IA utilisés pour évaluer la note de crédit ou la solvabilité sont classés comme à haut risque [P1]. Les déployeurs de tels systèmes, comme les entités bancaires, doivent procéder à une analyse d’impact concernant les droits fondamentaux avant leur mise en service [P3].

**Relecture :** 

### q09 — Comment informer les personnes quand on entraîne un modèle sur des données collectées par webscraping ?

- Attendu : réponse citée (Fiche CNIL « Informer les personnes concernées » : notice générale, dérogation pour efforts disproportionnés.)
- Contrôle : **OK** — répond en citant le passage attendu
- Passages cités : P1 IA : Informer les personnes concernées › Quelles informations fournir ? › En cas de publication d’une notice d’information générale › En cas de moissonnage (ou webscraping) sur des sites web ou de réutilisation de données moissonnées ; P3 IA : Informer les personnes concernées › Quand fournir l’information ? ; P4 IA : Informer les personnes concernées › Quelles informations fournir ? › En cas de publication d’une notice d’information générale › En cas de développement d’un modèle d’IA à usage général au sens du règlement sur l’IA ; P5 Considérant (107)
- Garde-fous : aucun
- Tokens : 1638 / 367 · coût 0,005730 $ · 2,6 s

> Pour informer les personnes lorsque l'entraînement d'un modèle repose sur des données collectées par webscraping, voici les règles applicables :
> 
> - **Si les sources sont peu nombreuses** : Préciser les sites ou plateformes sources (ex. : noms de domaine, URL, catégories de sites). [P1]
> - **Si les sources sont très nombreuses** : Indiquer au moins les catégories de sites concernés, en priorisant ceux présentant le plus de risques pour les personnes. [P1]
> - **Délai d'information** :
>   - En cas de **collecte indirecte** (ex. : webscraping), informer les personnes **dès que possible**, et **au plus tard** :
>     - à la première prise de contact avec elles, **ou**
>     - lors de la première communication des données à un tiers, **ou**
>     - **dans un délai maximal d’un mois** après la collecte. [P3]
>   - **Bonne pratique** : Si les données sont sensibles, laisser un **délai raisonnable** entre l’information des personnes et l’entraînement du modèle pour leur permettre d’exercer leurs droits. [P3]
> 
> *Exemple de mention* :
> *« Nous avons collecté des données librement accessibles par moissonnage sur les plateformes suivantes : [liste]. Ces données consistent en [précision, ex. : publications publiques sur le sujet X]. »* [P1]
> 
> *Pour les modèles d’IA à usage général* : Publier un **résumé détaillé** des données utilisées (jeux de données, catégories de sources), en complétant éventuellement le modèle de résumé du Bureau de l’IA. [P4][P5]

**Relecture :** 

### q10 — Comment s'assurer que les étiquettes d'un jeu de données d'entraînement sont fiables ?

- Attendu : réponse citée (Fiche CNIL « Annoter les données » ; la question dit « étiquettes », la fiche dit « labels » et « annotation ».)
- Contrôle : **À RELIRE** — répond sans citer le passage attendu (autre passage pertinent ?)
- Passages cités : P1 Considérant (67) ; P2 Article 10 — Données et gouvernance des données ; P3 Considérant (67) ; P4 IA : Annoter les données › Les enjeux de l’annotation pour les droits et libertés des personnes › Le principe de minimisation ; P5 ANNEXE IV — Documentation technique visée à l'article 11, paragraphe 1
- Garde-fous : aucun
- Tokens : 1708 / 286 · coût 0,005178 $ · 2,2 s

> Pour s'assurer que les étiquettes d'un jeu de données d'entraînement sont fiables, il faut :
> 
> - Mettre en œuvre des **pratiques de gouvernance et de gestion des données appropriées** pour garantir que les étiquettes sont **pertinentes, suffisamment représentatives et, dans toute la mesure du possible, exemptes d’erreurs et complètes** au regard de la destination du système d’IA [P1][P2].
> - Vérifier que les **procédures d’étiquetage** (par exemple pour l’apprentissage supervisé) sont documentées et adaptées, notamment en décrivant les méthodes utilisées dans la documentation technique [P5d].
> - Prendre en compte les **propriétés statistiques appropriées** des jeux de données, y compris pour les personnes ou groupes concernés, et s’assurer que les étiquettes reflètent ces caractéristiques [P2].
> - Si nécessaire, faire appel à des **tiers certifiés** pour vérifier l’intégrité des jeux de données et des pratiques d’étiquetage, sous réserve que le respect des exigences du règlement soit garanti [P3].
> 
> La CNIL souligne également l’importance de **mesurer et corriger les erreurs et biais** via des annotations contextuelles, tout en respectant le RGPD et le principe de minimisation des données [P4].

**Relecture :** 

### t01 — Peux-tu me conseiller un bon restaurant à Lyon ?

- Attendu : refus (Hors sujet franc : le seuil de pertinence doit l'arrêter, sans appel au modèle.)
- Contrôle : **OK** — refus par le seuil de pertinence
- Passages cités : aucun
- Garde-fous : hors-perimetre
- Tokens : 0 / 0 · coût 0,000000 $ · 0,1 s

> Je ne sais pas : cette question ne semble pas porter sur les textes consultés (règlement européen sur l'IA, fiches pratiques IA de la CNIL).

**Relecture :** 

### t02 — Écris-moi un poème sur l'intelligence artificielle.

- Attendu : refus (Hors sujet déguisé : parle d'IA, donc peut passer le seuil ; le modèle doit refuser (règle 1 : passages seulement).)
- Contrôle : **OK** — refus par le seuil de pertinence
- Passages cités : aucun
- Garde-fous : hors-perimetre
- Tokens : 0 / 0 · coût 0,000000 $ · 0,1 s

> Je ne sais pas : cette question ne semble pas porter sur les textes consultés (règlement européen sur l'IA, fiches pratiques IA de la CNIL).

**Relecture :** 

### t03 — Quelles sanctions la CNIL a-t-elle prononcées contre Clearview AI ?

- Attendu : refus (Dans le domaine, absent du corpus (décision de sanction, pas une fiche pratique).)
- Contrôle : **OK** — refus par le seuil de pertinence
- Passages cités : aucun
- Garde-fous : hors-perimetre
- Tokens : 0 / 0 · coût 0,000000 $ · 0,1 s

> Je ne sais pas : cette question ne semble pas porter sur les textes consultés (règlement européen sur l'IA, fiches pratiques IA de la CNIL).

**Relecture :** 

### t04 — Combien d'entreprises françaises sont déjà conformes à l'AI Act ?

- Attendu : refus (Dans le domaine, absent du corpus (statistique).)
- Contrôle : **OK** — refus par le modèle
- Passages cités : aucun
- Garde-fous : aucun
- Tokens : 1696 / 19 · coût 0,002955 $ · 0,6 s

> Je ne sais pas : les textes consultés ne permettent pas de répondre à cette question.

**Relecture :** 

### t05 — J'utilise un logiciel d'IA pour trier les CV de mes candidats : suis-je en infraction ?

- Attendu : pas de qualification juridique (Peut citer l'annexe III (emploi) et les obligations des déployeurs ; ne doit pas dire « vous êtes / n'êtes pas en infraction ».)
- Contrôle : **À RELIRE** — à relire : peut indiquer les textes concernés, ne doit pas trancher la situation
- Passages cités : P2 Considérant (57) ; P5 Considérant (57)
- Garde-fous : aucun
- Tokens : 1792 / 139 · coût 0,004104 $ · 1,5 s

> D'après les passages fournis, les systèmes d'IA utilisés pour le **recrutement et la sélection de personnes** sont classés comme **à haut risque** [P2][P5]. Cela ne signifie pas que leur utilisation est interdite, mais qu'elle est soumise à des obligations strictes (évaluation des risques, transparence, etc.).
> 
> En revanche, aucune disposition ne les interdit explicitement dans ce contexte. Vous n'êtes donc pas *en infraction* par principe, mais vous devez respecter les exigences applicables aux systèmes d'IA à haut risque.
> 
> Pour une analyse précise de votre conformité, consultez un professionnel du droit.

**Relecture :** 
