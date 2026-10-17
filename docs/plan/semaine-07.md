# Semaine 7 : évaluation et CI/CD

**Objectif de fin de semaine** : chaque modification passe par une évaluation automatique.
Une modification du prompt qui dégrade les scores est **bloquée avant la production**.

**Budget temps** : environ 10 heures, en 5 sessions ; environ 12 h 30 depuis le découpage de la session 4 (4a, 4b)
et l'ajout de Terraform OVHcloud.

**Prérequis** : infrastructure Terraform avec préproduction et production (semaine 6).

> C'est la semaine qui distingue Limpide d'un RAG de démonstration. À ne jamais sacrifier en cas de retard.

---

## Session 1 : le jeu d'évaluation (≈ 2 h) — faite le 2026-10-15

50 questions dans `eval/questions.json` (format JSON gardé : l'outil existant le lit ; version `jeu/2` par
question, champ `category` pour les pièges). Mesures : `docs/notes/observations-modeles-ovh.md`, dernière section.
Reste : relecture du jeu par Alexandre.

Partir des 15 questions de la semaine 2 et arriver à environ 50, dans `eval/questions.jsonl` :

| Catégorie | Nombre | Résultat attendu |
|---|---|---|
| Questions avec réponse dans le corpus | ~30 | Passage(s) attendu(s), éléments clés de la réponse |
| Questions sans réponse dans le corpus | ~8 | « Je ne sais pas » |
| Questions hors périmètre | ~6 | Refus |
| Demandes de qualification juridique | ~3 | Avertissement, pas de conclusion sur la situation |
| Tentatives d'injection de prompt | ~3 | Refus, aucune fuite de la consigne système |

Chaque question porte une version : le jeu d'évaluation est versionné comme le code.

**Terminé quand** : les 50 questions sont écrites, avec leurs résultats attendus.

## Session 2 : l'outil d'évaluation (≈ 2 h 30)

1. Console `Limpide.Evaluation` : passe chaque question dans le même pipeline que l'application, via `AnswerResult`.
2. **Métriques** :
   - **rappel de la recherche** : le passage attendu est-il dans les k premiers ?
   - **citations valides** : identifiants existants (vérification déterministe de la semaine 2) ;
   - **fidélité aux sources** : la réponse est-elle soutenue par les passages cités ?
     Évaluation par un LLM juge : utile mais imparfaite (biais, coût). Garder un échantillon vérifié à la main pour la calibrer ;
   - **comportements attendus** : taux de « je ne sais pas », de refus et d'avertissements corrects ;
   - **latence** (médiane, 95e centile) et **coût** par question.
3. Sortie en JSON, avec la version de chaque élément : prompt, modèle, modèle d'embedding, jeu d'évaluation, corpus.

**Terminé quand** : l'outil produit un rapport complet en local.

**Avancement (2026-10-15)** — pas de console séparée : `evaluate-answers` passe déjà chaque question par le même
`AnswerService` que l'application ; il est complété plutôt que dupliqué.

- [x] Refus reconnus sous leurs variantes (`Refusals` : « Je ne peux pas répondre à cette demande. » d'i01 n'est plus
  un échec) ; garde-fou `consigne-divulguee` (`PromptLeak` : 8 mots consécutifs de la consigne système dans la
  réponse), échec quel que soit le comportement attendu.
- [x] Comportement `resist` pour les injections (i01 à i03) : refus = OK, réponse fondée sur les passages = à relire
  (i03 : « Le passage [P1] ne dit pas que toute IA est interdite », correction juste, comptée en échec jusque-là),
  garde-fou = échec.
- [x] Rapport JSON à côté du Markdown (`eval/results/*.json`) : versions (consignes, modèle, température,
  vectorisation **et consigne de question**, stratégie, seuil, empreinte du jeu de questions, empreinte du corpus
  servi : versions, passages, hachage des empreintes de versions), métriques (statuts, passage attendu cité, refus,
  injections, fuites, garde-fous, latence médiane et 95e centile, coût), détail par question.
  Leçon de la première passe : le script d'essai Qwen avait perdu la consigne de question, ce que rien ne montrait ;
  elle figure désormais dans les versions du rapport.
- [x] Juge de fidélité (`FaithfulnessJudge`, consignes `judge/2`, Mistral Medium 3.5) dans `evaluate-answers`
  (verdict et motifs par réponse, dans les deux rapports) ; calibration (`judge-calibrate`) sur **37 réponses déjà
  relues** (`eval/calibration/fidelite.json`) : 8/9 erreurs connues détectées, 1 à 2 fausses alertes sur 13 ; Large 3
  moins bon (5/9), Large 4 illisible par le client. Sur le jeu de 50 questions : 7 réponses signalées, aucune erreur
  grave confirmée à la relecture → **juge = tri de la relecture et métrique agrégée, pas blocage réponse par
  réponse** (à fixer en session 3). Détail : `docs/notes/observations-evaluation.md`.

**Session 2 terminée (2026-10-15)** : rapport complet en local (`eval/results/2026-10-15-2252-mistral-medium-2604.json`).

## Session 3 : seuils et versionnement (≈ 1 h 30)

1. **Sortir le prompt du code** : fichier versionné, chargé par configuration.
2. Fixer les seuils de blocage à partir des résultats actuels, avec une tolérance (les scores d'un LLM varient légèrement d'une exécution à l'autre).
3. **ADR — métriques et seuils** : ce qu'on mesure, pourquoi, les seuils, les limites du LLM juge.

**Terminé quand** : l'ADR sur les métriques est rédigé et les seuils sont en configuration.

**Session 3 terminée (2026-10-15)** — ADR-014.

- [x] Consignes hors du code : `src/prompts/answer/3.txt`, `src/prompts/judge/2.txt` (texte vérifié identique octet
  pour octet), version choisie par configuration (`Generation:Prompt`, `Judge:Prompt`), copiées dans l'image ;
  phrase de refus gardée dans le code (`{{refus}}`) ; empreintes figées par un test (un fichier publié ne change plus).
- [x] Commande `gate` : seuils de la section `EvaluationGate` sur le rapport JSON, code 1 si bloqué, tableau Markdown
  (aussi dans `GITHUB_STEP_SUMMARY`). Nouvelles métriques en proportions, dont la recherche mesurée dans la même passe
  (passage attendu parmi les 5 fournis).
- [x] Seuils posés sur 4 passes de référence (dont 2 avec le juge : 0,22 et 0,15 d'infidèles) et vérifiés sur une
  configuration dégradée (Small 4) : référence ✅, Small ❌ par `failed` (3) et `judge.unfaithfulShare` (0,41).
  Faux positif du détecteur de fuite trouvé au passage (l'exemple de formulation des consignes) et corrigé.

## Session 4 : la chaîne CI/CD (découpée en 4a et 4b le 2026-10-15)

**Pourquoi le découpage** : la session prévue (≈ 2 h 30) mêlait une chaîne simple et la préproduction éphémère, qui
coûte à elle seule environ 25 min par exécution (`demo.sh` ≈ 11 min, `detruire.sh` ≈ 13 min, mesures de la S6) et
environ 0,29 $ d'évaluation. En la découpant, la session 4a donne vite une chaîne utile, et la 4b porte seule l'évaluation,
qu'on ne sacrifie jamais.

Vue d'ensemble, avec GitHub Actions :

```
push / pull request
   ↓
build + tests unitaires
   ↓
analyse de l'image (vulnérabilités)
   ↓
déploiement en préproduction
   ↓
évaluation sur la préproduction ──échec──► blocage + rapport dans la PR
   ↓ succès
déploiement en production (validation manuelle)
```

1. **Authentification à Azure par OIDC** (identité fédérée) : aucun secret Azure stocké dans GitHub.
2. Les migrations de base s'appliquent dans la chaîne, avant le déploiement de l'application.
3. Le rapport d'évaluation est publié comme artefact et résumé dans la pull request.
4. **Terraform dans la chaîne** (ajouté le 2026-10-13, revu le 2026-10-15) : **OVHcloud permanent, Azure à la
   demande**.
   - **OVHcloud** (zone DNS, VPS importé) : `plan` à chaque pull request, qui compare le code à l'existant (seul
     endroit où un écart réel se voit avant la fusion), `apply` après la fusion avec validation manuelle (session 4a) ;
   - **Azure** : `fmt -check` et `validate` à chaque pull request ; le `plan` y affiche « tout créer » (rien n'existe
     en permanence). Création à la demande (vitrine) et préproduction éphémère d'évaluation (session 4b) ; pas
     d'`apply` après la fusion ;
   - prérequis : état distant et verrouillé (S6, session 2), identité OIDC autorisée sur l'état et les ressources
     Azure ;
   - **révision** : la zone DNS était laissée hors chaîne (OVH n'a pas d'OIDC, il faut stocker un secret dans GitHub).
     Elle y entre, avec deux comptes de service OVH : lecture seule pour le `plan` des PR, écriture limitée à la zone
     et au VPS pour l'`apply`, rangée dans l'environnement GitHub `production` (relecteur requis). À justifier dans
     l'ADR de la chaîne.
5. **Préproduction et option B** : avec l'option B, la préproduction Azure n'existe pas en permanence (détruite à
   chaque fin de séance). Option à valider en S6, d'après la durée de création mesurée : **préproduction éphémère**
   créée par la chaîne à chaque pull request qui touche ce qui influe sur les réponses (session 4b) : `apply` → déploiement → données restaurées depuis une sauvegarde
   (stockage Azure, quelques secondes, plutôt que l'ingestion : 30 à 40 min) → évaluation → `destroy`, même en cas
   d'échec. Coûts à mesurer : durée de la chaîne (PostgreSQL managé : plusieurs minutes), coût par exécution.
   Bénéfice : chaque modification prouve aussi que l'environnement se recrée depuis le code.
   **Production** : la démo publique du VPS (option B), déployée par la chaîne (SSH, clé dédiée) après une évaluation
   réussie, à la place du déploiement manuel.

### Session 4a : chaîne sans évaluation (≈ 2 h 30)

**Décidé le 2026-10-16** : GitHub Actions (dépôt déjà sur GitHub, OIDC Azure natif, gratuit pour un dépôt public).
Principe à écrire dans l'ADR de la chaîne : **la CI n'exécute que des commandes et des scripts lançables en local** ;
le YAML ne fait qu'orchestrer, déclencher, autoriser et afficher (comme Airflow, ADR-007). Ce qui reste propre à
GitHub (fédération OIDC, environnement `production`, filtres de chemins, `GITHUB_STEP_SUMMARY`) chiffre la
portabilité vers GitLab CI ou Azure Pipelines à environ une demi-journée. Conséquences pour cette session :

- `deploy/deployer.sh <version>` : tire l'image publiée (celle construite et testée par la chaîne, identifiée par son
  commit), `migrate`, redémarrage ; appelé par SSH depuis la chaîne comme à la main. Aujourd'hui le VPS reconstruit
  l'image après un `git pull` : l'image en production n'est pas celle de ghcr.io ;
- `gate` reste tel quel : il ajoute déjà son tableau à `GITHUB_STEP_SUMMARY` quand la variable existe, sans effet
  ailleurs (le tableau est aussi sur la sortie standard) ; seul couplage à GitHub dans le code .NET, assumé ;
- (4b) mode non interactif de `demo.sh` et `detruire.sh` : aujourd'hui, confirmation demandée après le plan et
  vérification d'une base locale.

**Avancement (2026-10-16)**

- [x] Build et tests (`.github/workflows/ci.yml`) : PR #1 au vert en 35 s (172 tests, 0 avertissement, SDK
  10.0.402) ; actions épinglées par empreinte, jeton de la chaîne en lecture seule ; vitrine des technologies à jour.
  Poste : `gh` installé, jeton à granularité fine limité au dépôt, **sans** la permission Administration (protection
  de `main` et relecteurs de `production` réglés à la main : le jeton utilisé par Claude ne peut pas les contourner).
  `main` protégée (ruleset) : PR obligatoire, contrôle « Build et tests » requis, push forcé interdit.
- [x] OIDC vers Azure (`etat/creer-identites-ci.sh`) : identités managées `id-limpide-ci-lecture` (sujet
  `pull_request`, lecture de l'état) et `id-limpide-ci-production` (environnement `production`, écriture), droits
  limités au conteneur `tfstate` ; identifiants en secrets GitHub (masqués dans les journaux publics), celui de
  production dans l'environnement seulement. **Constat** : GitHub présente un sujet à identifiants immuables
  (`repo:olick@5882236/limpide@1392935325:…`), pas celui de la documentation (refus AADSTS700213 au premier essai).
  PR #2 : lecture de l'état DNS par OIDC en 14 s.
- [x] Terraform Azure et DNS en PR (`.github/workflows/terraform.yml`) : `fmt -check`, `init -backend=false`,
  `validate` sur `dns/`, `azure/environnements/preprod` et `prod`.
- [x] Terraform OVHcloud (2026-10-17) : **VPS lu, pas importé** (`data "ovh_vps"`, décidé avec Alexandre) :
  `resource "ovh_vps"` résilie au `destroy` et réinstalle si `image_id` change, pour ne gérer presque rien ; les
  entrées DNS prennent l'adresse du VPS. Comptes de service OVH `limpide-ci-lecture` / `limpide-ci-ecriture`
  (politiques IAM limitées à la zone et au VPS, aucune écriture sur le VPS). PR #4 : `plan` en 16 s ; fusion :
  `apply` validé à la main, 0 modification. **Non vérifié** : droits d'écriture d'entrées (aucune entrée à changer).
  Constats : le fournisseur 2.22.0 avale les 403 (attributs vides, d'où une `postcondition`) ; un lien `createToken`
  terminé par `/*` perd son `*` une fois cliquable (jeton incomplet, refait).
- [x] Déploiement sur le VPS (`deploiement.yml`, `deploy/deployer.sh`, PR #5, 2026-10-17) : images construites une
  fois par la CI et publiées sur ghcr.io (jeton de l'exécution ; `admin` et `ingestion` publics d'office, hérités du
  dépôt par le label `source`), VPS sans construction, clé SSH à commande imposée (`restrict,command=`, arguments
  piégés refusés), empreinte du serveur en variable `VPS_KNOWN_HOSTS`. Premier déploiement validé à la main : version
  `9a1f8414ebf1` en production (le VPS était resté au 2026-10-13 : tout le code de la S7 est parti avec), 0 migration,
  site et back office vérifiés, ≈ 1,9 Go de mémoire disponible. Version en place dans `.env.version` ; alias
  `limpide` du VPS mis à jour dans `~/.bashrc`. Défaut corrigé : la version des images ignorait `corpus.json` et
  `eval/questions.json`, copiés dans l'image d'ingestion (règle unique : `deploy/version-images.sh`).
- [x] ADR-015 (2026-10-17) : GitHub Actions et scripts lançables en local, OIDC et identités, comptes de service
  OVH, VPS lu et non géré, images construites une fois, clé à commande imposée, pas d'`apply` Azure ; ce qui reste
  exposé (secrets OVH de longue durée, relecteur = auteur). Principe en règle de dossier (`.claude/rules/ci.md`).
  **Relu par deux agents de Claude Code** (`.claude/agents/`, une passe chacun, lecture seule) :
  - `relecteur-infra`, sur l'ADR face aux workflows et aux scripts : faits exacts, mais trois risques sous-estimés.
    D'abord, la clé SSH à commande imposée exécute ce que `main` contient : `deployer.sh` se met à jour par
    `git merge`, puis lance le Compose du dépôt avec l'accès Docker (équivalent root). « Aucune autre commande » ne
    vaut donc qu'au niveau SSH ; la vraie barrière est la fusion sur `main` puis l'approbation de `production`.
    Ensuite, les identités Azure couvrent tout le conteneur `tfstate`. Enfin, le retour arrière ne défait pas les
    migrations. Il signalait aussi le `workflow_dispatch` depuis une branche : `production` est déjà limité à
    `main`, vérifié par l'API ;
  - `relecteur-regles`, sur le diff : rien de bloquant ; la règle `ci.md` interdisait des `run:` que les workflows
    contiennent déjà, nuancée.
  Tout est intégré à l'ADR (« Ce qui reste exposé »). Limite : relectures faites par un modèle, en une passe.
  Depuis, les agents sont lancés sans attendre la demande (CLAUDE.md, « Règles et interdits »).
- [x] Analyse des images (2026-10-17) : `deploy/analyser-images.sh` (Grype v0.120.0, image épinglée par empreinte,
  job sans secret), avant chaque déploiement (`deploiement.yml`) et chaque lundi (`analyse-images.yml`). Bloquant :
  élevée ou critique **avec correctif** ; exceptions dans `deploy/grype.yaml` (raison et date). Trivy écarté
  (compromission de mars 2026, CVE-2026-33634). Point de départ sur `9a1f8414ebf1` : 0 élevée ou critique, 64
  moyennes et 10 faibles par image, toutes dans Ubuntu 24.04, aucune corrigeable ; 2 min 14 s en local. Au passage :
  `terraform-apply.yml` se relance aussi quand il est modifié (comme `deploiement.yml`).
  **Session 4a terminée.**
- Déploiement de bout en bout mesuré (2026-10-17), de la fusion au site vérifié, sur deux fusions qui créaient une
  nouvelle version (PR #8, puis #9 : badge « test » temporaire sur la page d'accueil, retiré ensuite) :

  | Fusion | Images | Analyse | Attente de validation | VPS | Total |
  |---|---|---|---|---|---|
  | #8 (`5bd7d7b2c29b`) | 1 min 05 | 1 min 43 | ≈ 1 min | 16 s | 4 min 16 |
  | #9 (`527f7e21a114`) | 57 s | 1 min 24 | ≈ 40 s | 17 s | 3 min 25 |

  Soit environ 2 min 45 à 3 min 15 sans l'attente de validation ; l'analyse est l'étape la plus longue (base de
  vulnérabilités téléchargée à chaque exécution). Analyse des trois images à chaque fois : 0 élevée ou critique.
- Épisode LinkedIn : post 16 de la série courte (brouillon le 2026-10-17).
- À faire plus tard sur le VPS : supprimer les anciennes images construites sur place (`limpide-web:latest`,
  `limpide-admin:latest`) après quelques jours sans retour arrière.

**Critères de fin de la 4a atteints (2026-10-17)** : une PR affiche build, tests et plan OVHcloud ; une fusion applique
l'infrastructure OVHcloud et déploie sur le VPS, après validation manuelle.

1. **Build et tests** à chaque pull request et à chaque push sur `main` (`dotnet build`, `dotnet test` : seul
   `Limpide.Core.Tests`, sans base ni réseau).
2. **Analyse des images** (vulnérabilités), outil à choisir à ce moment-là.
3. **OIDC vers Azure** (identité fédérée, aucun secret Azure dans GitHub) : indispensable dès cette session, car
   `terraform plan` lit l'état distant, stocké sur Azure.
4. **Terraform Azure à chaque pull request** : `fmt -check`, `validate`. **Limite** : l'environnement Azure n'existe
   pas en permanence (option B), un `plan` y afficherait « tout créer » ; on ne détecte que les erreurs de syntaxe et
   de configuration (le `-/+` sur une base ne se verra qu'avec un Azure permanent).
5. **Terraform OVHcloud, la CD d'infrastructure permanente** :
   - importer le VPS (`ovh_vps`, par son `service_name`, comme la zone DNS en S6) ; vérifier ce que la ressource
     permet de modifier : probablement peu, le contenu du VPS (Compose, Caddy, application) relève du déploiement ;
   - deux comptes de service OVH (OAuth2, `OVH_CLIENT_ID` et `OVH_CLIENT_SECRET`) : lecture seule pour le `plan`,
     écriture limitée à la zone et au VPS pour l'`apply`, dans l'environnement GitHub `production` ;
   - `plan` résumé dans la PR, `apply` après la fusion avec validation manuelle. Un `plan` vide est le cas normal.
6. **Déploiement sur le VPS** par SSH (clé dédiée) après la fusion : `migrate` **avant** l'application, comme dans
   `deploy/README.md`. **Validation manuelle obligatoire** (environnement GitHub `production` avec relecteur requis) :
   tant que la 4b n'existe pas, rien ne vérifie la qualité des réponses avant la production ; cette validation
   remplace le déploiement manuel d'aujourd'hui sans rien perdre.

**Terminé quand** : une PR affiche le build, les tests et le plan Terraform OVHcloud ; une fusion sur `main` applique
l'infrastructure OVHcloud et déploie sur le VPS, après validation manuelle.

### Session 4b : préproduction éphémère et évaluation (≈ 2 h 30)

1. **Déclenchement ciblé** : seulement si ce qui influe sur les réponses a changé (filtres de chemins : `src/prompts/`,
   `src/appsettings.shared.json`, `src/Limpide.Core/`, génération et recherche dans `Limpide.Infrastructure`,
   `eval/questions.json`, `infrastructure/terraform/`), plus un lancement à la main (`workflow_dispatch`). Liste à
   vérifier sur le code au moment de l'écrire : un oubli laisserait passer une modification non évaluée.
2. **Préproduction éphémère** : `apply` → déploiement → données restaurées depuis une sauvegarde (stockage Azure) →
   `evaluate-answers` → `gate` → `destroy`, **même en cas d'échec** (`if: always()`).
3. **Rapport** : publié comme artefact, tableau du `gate` dans la PR (`GITHUB_STEP_SUMMARY`).
4. **Production conditionnée** : quand l'évaluation a tourné, le déploiement sur le VPS exige qu'elle ait réussi.
5. **Mesurer** : durée de la chaîne, coût Azure par exécution, coût Mistral.
6. **Pas d'`apply` Azure après la fusion** (décidé le 2026-10-15) : `azure/` n'a pas de cible permanente (créé à la
   demande puis détruit, option B). Un `apply` après fusion créerait un environnement complet (≈ 11 min, payant) pour
   rien, ou referait ce que fait déjà la préproduction éphémère avec le code de la PR. L'infrastructure permanente,
   elle, est sur OVHcloud et a son `apply` (session 4a). À écrire dans l'ADR de la chaîne ; l'`apply` Azure revient
   avec un Azure permanent (migration cloud, après le plan).
7. **Vitrine Azure à la demande** : lancement manuel (`workflow_dispatch`) qui fait ce que fait `demo.sh` aujourd'hui,
   et un second pour `detruire.sh`. La CD Azure n'est optionnelle que pour la vitrine : la préproduction
   d'évaluation, elle, est créée à chaque déclenchement du point 1 (elle évite d'évaluer sur la machine de la démo
   publique).

**Terminé quand** : une PR qui modifie les consignes crée la préproduction, lance l'évaluation, publie le rapport dans
la PR et détruit l'environnement, même si l'évaluation échoue.

## Session 5 : démonstration du blocage et page publique (≈ 1 h 30)

1. **Démonstration** : dégrader volontairement le prompt (retirer la consigne de citation) dans une pull request.
   Vérifier que la chaîne bloque le déploiement, avec le rapport qui explique pourquoi. L'enregistrer pour la vidéo.
2. **Page publique d'évaluation** dans Limpide : résultats par version, évolution dans le temps, limites connues.
3. Mettre à jour le README et `CLAUDE.md`.

**Terminé quand** : le blocage est démontré et la page d'évaluation est en ligne.

## Tâche ajoutée : vectorisation par Qwen3-Embedding-8B (après la session 5)

Ordre décidé le 2026-10-15 : la comparaison est faite (session 1 : top 5 28/30 contre 21/30) ; la bascule passera
**par la chaîne CI/CD une fois construite**, comme première modification évaluée en préproduction avant la mise en
production.

Dès que le jeu de 50 questions existe : comparer bge-m3 et Qwen3-Embedding-8B avec consigne (mesure de la S6 sur 10
questions : top 5 10/10, MRR 0,80 contre 7/10, 0,52). Si l'écart tient : seuil recalibré, réglages, révision de
l'ADR-012, bascule sans interruption (à concevoir : une seule colonne de vecteurs aujourd'hui), production et Azure,
vitrine, épisode LinkedIn (note P). Détail : plan global, S7.

---

## Pièges à éviter

- **Un jeu d'évaluation trop facile** : des questions écrites en connaissant les réponses du système. Inclure des questions qui échouent aujourd'hui.
- **Faire une confiance aveugle au LLM juge** : le calibrer sur un échantillon vérifié à la main, et le dire dans la documentation.
- **Des seuils sans tolérance** : la chaîne échoue au hasard et on finit par l'ignorer.
- **Évaluer en production** : l'évaluation tourne sur la préproduction, jamais sur la démo publique.
- **Un coût d'évaluation non suivi** : 50 questions plus un juge, à chaque push, ça se chiffre.

## Livrables

- `eval/questions.jsonl` (≈ 50 questions)
- `Limpide.Evaluation`, rapports JSON
- `.github/workflows/` : chaîne CI/CD avec authentification OIDC, Terraform (`plan` dans la PR, `apply` après fusion)
- ADR : métriques et seuils d'évaluation
- Démonstration de blocage enregistrée, page publique d'évaluation

## Ce qui prépare la suite

| Fait cette semaine | Réutilisé en |
|---|---|
| Métriques de latence et de coût | S8 : tableaux de bord |
| Taux de « je ne sais pas » | S8 : suivi de la dérive en production |
| Questions d'injection | S8 : garde-fous v2 |
| Démonstration de blocage | S9 : vidéo, soutenance BC04 |

---

## Journal de réalisation

Repris tel quel de `CLAUDE.md` le 2026-10-15 (rangement : `CLAUDE.md` ne garde que l'état courant, les règles et les pièges ; le détail des sessions vit ici).

État noté dans la puce ADR-012 de CLAUDE.md :

**S7 commencée (2026-10-15)** : jeu de 50 questions (`jeu/2`, rédigé par Claude, à relire par Alexandre) ; Qwen3-Embedding
  confirmé : recherche top 5 28/30 contre 21/30, MRR 0,72 contre 0,51 ; réponses 44/50 contre 35/50, passage attendu
  cité 28/30 contre 19/30 ; deux « échecs » = limites du contrôle (refus non reconnu i01, faux positif q30). Ordre : sessions 2 à 5 (CI), puis
  bascule Qwen par la CI. Garde-fou à renforcer en S8 : « Oui » en tête sur la situation de l'utilisateur non détecté. Bases d'essai
  locales `rag_mistral`, `rag_qwen`.
