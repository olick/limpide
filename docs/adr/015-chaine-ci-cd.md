# ADR-015 : Chaîne CI/CD par GitHub Actions, qui n'orchestre que des scripts lançables en local

- **Statut** : accepté
- **Date** : 2026-10-17

## Contexte

Jusqu'à la S7, tout se faisait à la main depuis le poste :

- tests lancés quand on y pensait ;
- `terraform apply` de la zone DNS depuis le poste, avec un jeton OVH personnel ;
- sur le VPS, `git pull` puis construction des images sur place : l'image en production n'était celle d'aucun
  build vérifié.

L'objectif de la S7 est qu'aucune modification n'atteigne la production sans contrôle automatique, et à terme sans
évaluation (session 4b, ADR-014). Il fallait donc une chaîne, avec ces contraintes :

- **Infrastructure en deux morceaux** (option B, ADR-006 et ADR-011) :
  - OVHcloud est permanent : la zone DNS et le VPS de la démo ;
  - Azure est recréé à la demande : il n'existe presque jamais.
- **Accès** : la chaîne doit lire l'état Terraform (sur Azure), lire et modifier la zone DNS (OVH, qui ne propose
  pas d'OIDC) et déployer sur un VPS joignable seulement en SSH.
- **Un seul auteur**, qui découvre Terraform. Dépôt public : les journaux d'exécution sont visibles de tous.
- **Coût** : nul ou presque.
- **Portabilité** : un client peut imposer GitLab CI ou Azure DevOps. Ce que je montre en entretien doit se
  transposer, pas dépendre d'un outil.

## Options envisagées

**Plateforme**

1. **GitHub Actions**. Le dépôt y est déjà. OIDC vers Azure natif. Registre d'images (ghcr.io) avec le jeton de
   l'exécution. Gratuit pour un dépôt public. Retenu.
2. **GitLab CI** : il faudrait déplacer le dépôt ou le dupliquer. Rien de plus pour ce projet.
3. **Azure Pipelines** : intégration Azure poussée, mais Azure n'est pas permanent ici, et il faudrait un second
   outil (Azure DevOps) à côté du dépôt GitHub.

**Où vit la logique**

1. **Dans le YAML** : actions du catalogue pour tout (connexion Docker, SSH, Terraform). Rapide à écrire, mais
   rien ne se rejoue hors de GitHub, et chaque action tierce a accès aux secrets du job.
2. **Dans des scripts et des commandes du dépôt**, que le YAML ne fait qu'appeler. Retenu : c'est le principe déjà
   appliqué à Airflow (ADR-007).

**Gestion du VPS par Terraform**

1. **L'importer** (`resource "ovh_vps"`) : la ressource sert à commander un serveur. Son `destroy` le résilie, et
   changer son `image_id` le réinstalle, disque effacé (`prevent_destroy` n'empêche pas une réinstallation). Elle
   ne permettrait de modifier presque rien d'utile (nom affiché, mode de démarrage). Écarté.
2. **Le lire** (`data "ovh_vps"`) : les entrées DNS prennent l'adresse du serveur lui-même, et rien dans
   Terraform ne peut le modifier. Retenu.

**Construction des images**

1. **Sur le VPS**, comme avant : simple, mais l'image en production n'est pas celle qui a été testée. Et la
   construction consomme la mémoire d'un serveur de 2 Go qui sert la démo.
2. **Une fois dans la chaîne**, publiée sur ghcr.io, tirée par chaque cible (VPS, vitrine Azure). Retenu.

## Décision

**GitHub Actions orchestre ; tout ce qui décide ou transforme se lance aussi à la main.** Le YAML déclenche,
autorise, enchaîne et affiche. Il garde seulement l'installation des outils (`setup-dotnet`, `setup-terraform`) et
le câblage propre à la chaîne (connexion à ghcr.io, clé SSH, résumé dans `GITHUB_STEP_SUMMARY`).

| Workflow | Déclencheur | Ce qu'il lance | Accès |
|---|---|---|---|
| `ci.yml` | chaque PR, push sur `main` | `dotnet build`, `dotnet test` | lecture du dépôt |
| `terraform.yml` | PR et push qui touchent `infrastructure/terraform/` | `fmt -check`, `validate` (DNS, Azure) ; `plan` de la zone OVH en PR | identité Azure de lecture, compte OVH de lecture |
| `terraform-apply.yml` | fusion qui touche `dns/` ou ce workflow | `plan` puis `apply` de la zone | environnement `production` (validation manuelle) |
| `deploiement.yml` | fusion qui touche l'application ou `deploy/` | `deploy/publier-images.sh`, `deploy/analyser-images.sh`, puis SSH → `deploy/deployer.sh <version>` | écriture sur ghcr.io ; environnement `production` |
| `analyse-images.yml` | chaque lundi | `deploy/analyser-images.sh` sur la version courante | aucun secret |

Tous peuvent aussi être lancés à la main (`workflow_dispatch`). L'environnement `production` n'accepte que la
branche `main` (vérifié le 2026-10-17) : un workflow modifié sur une autre branche ne peut pas obtenir ses secrets,
même lancé à la main. C'est nécessaire, car le sujet OIDC `environment:production` ne contient pas la branche.

Les choix qui l'accompagnent :

1. **Identités sans secret quand c'est possible.**
   - **Azure, par OIDC** (`etat/creer-identites-ci.sh`) : deux identités managées, droits limités au conteneur de
     l'état.
     - `id-limpide-ci-lecture` accepte le sujet `pull_request` : `plan` sans verrou.
     - `id-limpide-ci-production` accepte le sujet `environment:production` : `apply`.
     - Le sujet utilise les identifiants immuables du compte et du dépôt : un dépôt recréé sous le même nom
       n'hérite pas de la confiance d'Azure.
     - GitHub ne délivre pas de jeton OIDC aux PR venues d'un fork.
   - **OVHcloud, par deux comptes de service** (OVH n'a pas d'OIDC), dont les identifiants sont stockés en secrets
     GitHub :
     - `limpide-ci-lecture` : lecture de la zone et du VPS, pour le `plan` des PR ;
     - `limpide-ci-ecriture` : écriture de la zone, lecture du VPS, rangé dans l'environnement `production`.

     Aucun des deux ne peut écrire sur le VPS. C'est la seule entorse au « sans secret » : on la limite par les
     droits, faute de pouvoir la supprimer.
   - **Rangement** : les identifiants de lecture sont des secrets du dépôt ; ceux d'écriture, des secrets de
     l'environnement `production`, **sous les mêmes noms** (`AZURE_CLIENT_ID`, `OVH_CLIENT_ID`…). Les workflows
     restent identiques, mais si un secret d'environnement disparaît, le job prend en silence celui de lecture et
     échoue sur un refus de droits.
2. **Infrastructure permanente (OVHcloud) : `plan` en PR, `apply` après fusion, validé à la main.** Un `plan` vide
   est le cas normal. Le VPS est lu, jamais géré : son contenu (Compose, Caddy, application) relève du déploiement.
3. **Azure : `fmt -check` et `validate` seulement, pas d'`apply` après fusion.** Rien n'existe en permanence. Un
   `plan` afficherait « tout créer », et un `apply` créerait un environnement complet (≈ 11 min, payant) que personne
   n'utilise. Azure sera créé par la chaîne pour la préproduction éphémère d'évaluation (session 4b) et pour la
   vitrine, à la demande.
4. **Images construites une fois, identifiées par leur version.** La version est l'empreinte courte du dernier
   commit qui touche ce qui entre dans les images (`deploy/version-images.sh`, règle unique). Un commit de
   documentation ne crée donc pas de nouvelle version, et une version déjà publiée n'est pas reconstruite. Le VPS
   ne construit plus rien.
5. **Déploiement par une clé SSH à commande imposée.** Sur le VPS, la ligne `authorized_keys` de la clé de la
   chaîne est `restrict,command="…/deploy/deployer.sh"` : pas de terminal, pas de redirection, aucune autre
   commande. L'argument transmis n'est jamais exécuté, seulement comparé à un motif hexadécimal. Le script
   vérifie aussi que la version est un commit de `main`. Côté chaîne, l'empreinte du serveur est enregistrée
   (`VPS_KNOWN_HOSTS`) : la chaîne refuse de parler à un autre serveur.
6. **Validation manuelle avant toute écriture en production** : environnement GitHub `production`, relecteur
   requis. Tant que l'évaluation n'est pas dans la chaîne (4b), c'est le seul contrôle de qualité avant la
   production. Elle remplace le déploiement manuel sans rien perdre.
7. **Durcissement de la chaîne elle-même.**
   - Actions tierces épinglées par empreinte de commit (une étiquette peut être déplacée).
   - Jeton de l'exécution en lecture seule par défaut ; écriture accordée job par job (`packages: write`,
     `id-token: write`).
   - `main` protégée : PR obligatoire, contrôle « Build et tests » requis, push forcé interdit.
   - Le jeton `gh` du poste, utilisé par Claude Code, n'a ni l'écriture du contenu ni l'administration : il ne
     peut ni fusionner ni contourner la protection.
8. **Analyse des vulnérabilités des images par Grype**, avant chaque déploiement et chaque semaine
   (`deploy/analyser-images.sh`, ajouté le 2026-10-17).
   - **Bloquant** : une vulnérabilité élevée ou critique **dont un correctif existe**. Le reste est affiché sans
     bloquer : une vulnérabilité sans correctif arrêterait la chaîne sans action possible, et on apprendrait à ignorer
     l'alerte (même raisonnement que les seuils de l'ADR-014). Exceptions dans `deploy/grype.yaml`, avec une raison
     (date comprise, par convention : Grype ne la contrôle pas), comptées à part dans le rapport.
   - **Grype plutôt que Trivy**, l'outil le plus courant. Le 19 mars 2026, les étiquettes de `trivy-action` ont été
     réécrites et un binaire Trivy piégé publié : le code volait les secrets de la chaîne avant de lancer l'analyse
     normale (CVE-2026-33634). Un outil de sécurité est une cible de choix, car il tourne partout, souvent avec des
     secrets. D'où trois précautions, qui valent pour tout outil tiers :
     - lancé depuis son image Docker, **épinglée par empreinte**, dans une version publiée depuis au moins deux
       semaines ;
     - **aucune action du catalogue** ;
     - **job sans aucun secret** : les images sont publiques et se lisent sans connexion.
   - **Chaque semaine**, parce qu'une image vieillit : de nouvelles vulnérabilités sont publiées après son
     déploiement.

## Conséquences

- **Mesures** (2026-10-16 et 17) :
  - build et tests : 35 s (172 tests) ;
  - `plan` OVH en PR : 16 s ;
  - premier déploiement par la chaîne : version `9a1f8414ebf1`, 0 migration, site et back office vérifiés.
  - Coût : nul (minutes gratuites d'un dépôt public, ghcr.io gratuit pour des images publiques).
- **Rejouable hors de GitHub** : en cas de panne de la chaîne, `deploy/publier-images.sh` depuis le poste puis
  `deploy/deployer.sh <version>` sur le VPS font exactement la même chose.
- **Retour arrière, avec deux réserves** : on relance `deployer.sh` avec la version précédente, qu'il affiche au début.
  - Les migrations ne sont pas défaites : une ancienne version tourne sur le schéma de la nouvelle. Le retour
    arrière n'est sûr que si aucune migration n'est passée entre-temps, ou si elles sont compatibles en arrière.
  - Le fichier Compose, le Caddyfile et le script restent ceux de la tête de `main` : seules les images reviennent
    en arrière.
- **Un commit qui ne touche que `deploy/`** (même un README) redéploie la version en place : validation demandée,
  et coupure de quelques secondes si on l'accepte. On peut refuser la validation.
- **Couplage à GitHub, assumé et chiffré** :
  - fédération OIDC (sujets propres à GitHub) ;
  - environnement `production` et ses relecteurs ;
  - filtres de chemins ;
  - `GITHUB_STEP_SUMMARY`, seul couplage dans le code .NET (`gate` y ajoute son tableau quand la variable existe).

  Porter la chaîne vers GitLab CI ou Azure Pipelines : environ une demi-journée, sans toucher aux scripts.
- **Ce qui reste exposé** :
  - les deux secrets OVH sont de longue durée, contrairement aux jetons Azure (environ une heure) : à renouveler à
    la main, et à révoquer en premier en cas de doute ;
  - **la clé SSH exécute ce que `main` contient**. La commande imposée est un fichier du dépôt, que `deployer.sh`
    met lui-même à jour (`git merge`) avant de lancer `docker compose` avec le Compose du dépôt, sous un compte qui
    a accès à Docker, donc l'équivalent de root. « Aucune autre commande » vaut donc au niveau SSH seulement. La
    vraie barrière, c'est que seul le code fusionné sur `main` puis approuvé dans `production` s'exécute sur le VPS.
    Qui peut fusionner et approuver peut agir sur le serveur ;
  - la clé peut aussi redéployer une ancienne version de `main` : c'est voulu (retour arrière) ;
  - **les identités Azure couvrent tout le conteneur `tfstate`**, pas seulement l'état DNS. L'identité de
    production peut écraser les états Azure, et celle des PR peut les lire. Un conteneur par état réduirait ce
    périmètre ; à faire si les états Azure deviennent permanents ou contiennent des valeurs sensibles ;
  - **les images sont publiées avant la validation** (le job `images` n'a pas d'environnement) et elles sont
    publiques, comme le dépôt. Elles ne contiennent rien qui n'y soit déjà : code, consignes, `corpus.json`,
    `eval/questions.json`. Les secrets sont dans `.env.prod` et le coffre Azure, jamais dans une image ;
  - le relecteur requis est aussi l'auteur : la validation manuelle protège d'une fusion ou d'un déploiement
    involontaire, pas d'une erreur de jugement. Elle prendra son sens avec l'évaluation (4b) ou un second relecteur.
    Les secrets de lecture sont accessibles aux PR venues d'une branche du dépôt, que seul l'auteur peut pousser :
    à revoir si un collaborateur rejoint le projet.
- **Analyse des images, point de départ** (2026-10-17, version `9a1f8414ebf1`) : aucune vulnérabilité élevée ni
  critique. Par image : 64 moyennes et 10 faibles ou négligeables, toutes dans les paquets d'Ubuntu 24.04 (image de
  base .NET), aucune avec correctif disponible, aucune dans les paquets NuGet. Durée en local : 2 min 14 s, dont le
  premier téléchargement de la base de vulnérabilités.
- **Un correctif de l'image de base n'arrive qu'avec une nouvelle version** : les images de base sont désignées par
  une étiquette mouvante (`aspnet:10.0`), mais une version déjà publiée n'est jamais reconstruite. Corriger une
  vulnérabilité bloquante de la base demande donc un commit qui touche les sources des images. Épingler les images de
  base par empreinte, avec une mise à jour automatique proposée en PR, rendrait ces correctifs explicites : à
  envisager en S8. Tant qu'une version est bloquée, un commit qui ne touche que `deploy/` (correctif de
  `deployer.sh`, par exemple) garde cette version et reste bloqué : en sortir par un commit qui reconstruit les images
  ou, en urgence, par une exception datée.
- **L'analyse hebdomadaire est un workflow planifié** : GitHub le désactive après 60 jours sans activité sur le dépôt,
  sans alerte. Sans effet tant que le projet vit ; à surveiller s'il se met en sommeil.
- **Évaluation avant la production** (session 4b) : dans un environnement jetable sur le runner plutôt qu'en
  préproduction Azure éphémère, et production conditionnée par un contrôle requis de `main` (ADR-016).
- **Conditions de révision** :
  - **Azure devient permanent** (migration cloud, après le plan) : `plan` Azure en PR, `apply` après fusion,
    déploiement vers Container Apps à la place du VPS. Les principes restent : identités OIDC, images construites une
    fois, validation manuelle.
  - **OVHcloud propose une fédération d'identité** utilisable par Terraform : remplacer les deux comptes de service.
  - **Un client impose une autre plateforme** : porter le YAML, garder les scripts.
  - **Le VPS doit être recréé souvent** (changement d'offre, autre région) : reconsidérer sa gestion par Terraform,
    avec une protection explicite contre la réinstallation.
