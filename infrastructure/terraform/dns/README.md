# Zone DNS `limpide-ia.fr` en Terraform (fournisseur OVHcloud)

Deuxième étape d'apprentissage de la S6, et première infrastructure réelle de Limpide décrite en code. La zone existe
déjà (entrées créées à la main dans l'espace client OVH en S3 et S5) : on ne la recrée pas, on la fait **entrer**
dans Terraform par **import**.

## Deux notions nouvelles

| Notion | Ce que c'est | Ici |
|---|---|---|
| **Source de données** (`data`) | Lit une ressource existante sans la gérer : jamais créée, modifiée ni détruite par Terraform | `inventaire.tf` : liste des entrées de la zone et de leurs identifiants |
| **Import** | Fait entrer dans l'état une ressource créée hors de Terraform ; ensuite, Terraform la gère comme si il l'avait créée | Les entrées `@`, `www`, `admin` de Limpide |

Après un import, le critère de réussite est un `plan` **sans changement** : le code décrit exactement ce qui existe.
Un plan qui propose une modification veut dire que le code ne correspond pas à la réalité ; on corrige le code, pas
la zone.

## 1. Identifiants de l'API OVHcloud (une fois)

Jeton limité à la seule zone `limpide-ia.fr`, créé sur la page OVH (connexion à l'espace client), droits pré-remplis :

https://eu.api.ovh.com/createToken/?GET=/auth/details&GET=/domain/zone/limpide-ia.fr&GET=/domain/zone/limpide-ia.fr/*&POST=/domain/zone/limpide-ia.fr/*&PUT=/domain/zone/limpide-ia.fr/*&DELETE=/domain/zone/limpide-ia.fr/*

- Nom : `limpide-terraform` ; description : `Terraform, zone DNS limpide-ia.fr` ; validité : illimitée (révocable
  dans l'espace client, et le jeton ne touche que cette zone).
- Les trois valeurs obtenues vont dans `~/.ovh.conf`, **hors du dépôt**, jamais dans un fichier `.tf` :

```bash
install -m 600 /dev/null ~/.ovh.conf && nano ~/.ovh.conf
```

```ini
[default]
endpoint=ovh-eu

[ovh-eu]
application_key=...
application_secret=...
consumer_key=...
```

Ne pas mettre de `ovh.conf` dans ce dossier : le fournisseur le lirait aussi, mais il risquerait d'être commité
(`ovh.conf` est dans `.gitignore` par précaution).

## 2. Inventaire

```bash
cd infrastructure/terraform/dns
terraform init
terraform plan      # rien à créer : seules des sources de données sont lues ; la sortie « inventaire » liste la zone
terraform apply     # « yes » : n'écrit que l'inventaire dans l'état (aucune modification de la zone)
terraform output inventaire
```

## 3. Import

L'inventaire du 2026-10-12 : 12 entrées. Gérées par Terraform (`zone.tf`) : `@`, `www`, `admin` (A vers le VPS ;
adresse lue chez OVHcloud depuis la S7, voir « Le VPS, lu et non géré »). Laissées à OVH : 2 NS, 3 MX, SPF, `ftp` (CNAME), `www` TXT `"3|welcome"` (reste de la page
d'accueil OVH). « ttl 0 » = durée par défaut de la zone, aussi la valeur par défaut du fournisseur.

Chaque entrée a un bloc `import` (identifiant `<id>.limpide-ia.fr`) à côté de son bloc `resource` :

```bash
terraform plan      # attendu : « 4 to import, 0 to add, 0 to change, 0 to destroy » ; un « ~ » = code inexact
terraform apply     # les entrées entrent dans l'état ; la zone n'est pas modifiée
terraform plan      # « No changes »
```

(Autre méthode, pour beaucoup d'entrées : blocs `import` seuls, puis `terraform plan -generate-config-out=generated.tf`
écrit le code ; à relire et ranger.)

## 4. Première modification par le code

L'inventaire a montré une erreur de saisie de la S5 : `limpide-ia.fr.limpide-ia.fr` (sous-domaine « limpide-ia.fr »
au lieu de vide). Importée avec les autres, puis son bloc retiré de `zone.tf` : le `plan` a annoncé
`1 to destroy` (« not in configuration »), `apply` l'a supprimée (2026-10-12). Vérifié : `dig` ne la trouve plus,
`@`, `www`, `admin` répondent, les deux sites aussi.

Règle à partir de maintenant : les entrées `@`, `www`, `admin` ne se modifient plus dans l'espace client OVH, sinon
le prochain `plan` signale une dérive.

## État

Distant depuis le 2026-10-13 (S6, session 2) : blob `dns.tfstate` du coffre `stlimpidetf024b1e9f` (`backend.tf`,
`../etat/README.md`), accès par identité Microsoft, verrou pendant chaque opération (vérifié : un second `plan` est
refusé pendant un `apply`). Prérequis sur un nouveau poste : `az login`, le droit sur le conteneur, `terraform init`.
Perdu malgré tout, il se reconstruirait par les blocs `import` (les identifiants y sont) : rien ne serait détruit.

## Le VPS, lu et non géré (S7, session 4a)

`vps.tf` lit le VPS de la démo (`data "ovh_vps"`, nom de service `vps-90f0733b.vps.ovh.net`, variable
`vps_service_name`) : les entrées `@`, `www`, `admin` prennent son adresse IPv4 au lieu d'une valeur recopiée à la
main. Si le serveur change d'adresse, le `plan` le montre.

**Pourquoi ne pas l'importer comme ressource** (`resource "ovh_vps"`, prévu au départ) : cette ressource sert à
commander un serveur. Son `destroy` le **résilie**, et changer son `image_id` le **réinstalle** (disque effacé),
modification en place que `prevent_destroy` n'empêche pas. Elle ne permettrait de modifier presque rien d'utile (nom
affiché, mode de démarrage) ; le contenu du VPS relève du déploiement. Lu seulement, le VPS n'exige qu'un droit de
lecture, pour la CI comme pour le poste.

**Piège constaté** : le fournisseur 2.22.0 avale un refus de l'API (403) sur `GET /vps/<nom>` et renvoie des
attributs vides au lieu d'une erreur. Une `postcondition` sur la source de données transforme ce silence en erreur
explicite.

Droits du jeton du poste (`~/.ovh.conf`), lien à copier depuis ce bloc (un `*` en fin de lien cliquable est perdu) :

```
https://eu.api.ovh.com/createToken/?GET=/vps/vps-90f0733b.vps.ovh.net/*&GET=/vps/vps-90f0733b.vps.ovh.net&GET=/domain/zone/limpide-ia.fr/*&POST=/domain/zone/limpide-ia.fr/*&PUT=/domain/zone/limpide-ia.fr/*&DELETE=/domain/zone/limpide-ia.fr/*&GET=/domain/zone/limpide-ia.fr&GET=/auth/details
```

## Dans la chaîne CI/CD (S7, session 4a)

- **PR** (`.github/workflows/terraform.yml`) : `plan -lock=false` résumé dans l'exécution, avec l'identité Azure de
  lecture (OIDC) et le compte de service OVH `limpide-ci-lecture` (lecture de la zone et du VPS). Un plan vide est le
  cas normal.
- **Après la fusion** (`.github/workflows/terraform-apply.yml`) : `plan` puis `apply`, dans l'environnement GitHub
  `production` (validation manuelle), avec l'identité Azure d'écriture et le compte `limpide-ci-ecriture` (écriture
  de la zone, lecture du VPS).
- OVH n'a pas d'OIDC : les comptes de service (OAuth2, `OVH_CLIENT_ID`, `OVH_CLIENT_SECRET`) sont des secrets GitHub,
  celui d'écriture dans l'environnement `production` seulement. Le fournisseur les lit dans l'environnement ; le
  poste garde `~/.ovh.conf`.
