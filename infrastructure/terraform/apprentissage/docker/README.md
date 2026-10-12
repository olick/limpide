# Apprendre Terraform sans frais : la base PostgreSQL en local

Premier exercice de la S6 (option B : Terraform d'abord sur des ressources gratuites, Azure ensuite). La
configuration crée une base PostgreSQL + pgvector dans Docker, comme le service `postgres` de `docker-compose.yml`,
mais sous d'autres noms (`tf-apprentissage-*`, port 5442) : la pile de développement n'est jamais touchée.

## Les notions, rapportées à ce que tu connais

| Terraform | Ce que c'est | Équivalent connu |
|---|---|---|
| **Fournisseur** (provider) | Le connecteur vers une plateforme : ici le démon Docker ; plus tard `ovh`, `azurerm` | Un client d'API, comme Npgsql pour PostgreSQL |
| **Ressource** | Un élément d'infrastructure décrit en code (`docker_container`, `docker_volume`...) | Un service de `docker-compose.yml` |
| **Référence** | `docker_volume.pgdata.name` : une ressource en utilise une autre ; Terraform en déduit l'ordre de création | `depends_on` de Compose, mais déduit tout seul |
| **Variable / sortie** | Paramètres d'entrée (`variables.tf`), valeurs exposées (`outputs.tf`) | Les `${VAR}` de `.env` ; les valeurs de retour |
| **État** (state) | `terraform.tfstate` : ce que Terraform a créé, avec les identifiants réels | Aucun dans Compose : c'est la grande différence |
| **`plan`** | Compare code, état et réalité ; affiche ce qui serait créé (`+`), modifié (`~`), remplacé (`-/+`), détruit (`-`) | Un `git diff` de l'infrastructure |
| **`apply`** | Exécute le plan, après confirmation | `docker compose up` |
| **`destroy`** | Détruit tout ce que l'état connaît | `docker compose down -v` |

Ce que Terraform apporte par rapport à Compose : l'**état**. Compose relance ce que décrit le fichier ; Terraform sait
ce qu'il a créé, montre l'écart avant d'agir, et le même langage décrit une base Docker, une zone DNS OVH ou un
PostgreSQL managé Azure. Le prix : l'état devient un fichier critique (perdu, Terraform ne sait plus ce qui existe ;
il contient les secrets en clair, l'exercice 4 le montre).

## Installation (une fois, sur le poste)

Dépôt officiel HashiCorp (Debian 13 « trixie » est pris en charge) :

```bash
wget -O - https://apt.releases.hashicorp.com/gpg | sudo gpg --dearmor -o /usr/share/keyrings/hashicorp-archive-keyring.gpg
echo "deb [arch=$(dpkg --print-architecture) signed-by=/usr/share/keyrings/hashicorp-archive-keyring.gpg] https://apt.releases.hashicorp.com trixie main" | sudo tee /etc/apt/sources.list.d/hashicorp.list
sudo apt update && sudo apt install terraform
terraform version        # 1.16.x
```

Extension VS Code conseillée : « HashiCorp Terraform » (coloration, complétion, `terraform fmt` à l'enregistrement).

## Exercices (≈ 1 h 30)

Dans ce dossier :

```bash
cd infrastructure/terraform/apprentissage/docker
read -rsp "Mot de passe : " TF_VAR_postgres_password && export TF_VAR_postgres_password   # saisi sans écho
```

Le mot de passe est **à inventer** : la base n'existe pas encore, PostgreSQL crée l'utilisateur `rag` avec ce mot de
passe au premier démarrage. Pas un vrai mot de passe (il finit en clair dans l'état, exercice 4). Il ne vaut que pour
ce terminal : retaper le même dans un nouveau terminal (un autre ferait remplacer le conteneur, sans changer le mot de
passe dans la base, fixé à la création du volume).

**1. Initialiser, prévoir, appliquer**

```bash
terraform init      # télécharge le fournisseur docker 4.6.0, crée .terraform/ et .terraform.lock.hcl
terraform plan      # lire : 4 ressources à créer ; le mot de passe apparaît en « (sensitive value) »
terraform apply     # répond « yes » ; rend la main quand la base est prête (wait = true)
terraform output    # la chaîne de connexion
docker exec tf-apprentissage-postgres psql -U rag -d rag -c "select version();"
```

Observer l'ordre de création dans la sortie d'`apply` : image, réseau et volume d'abord (en parallèle), conteneur
ensuite. Aucun `depends_on` dans le code : les références suffisent.

**2. Idempotence**

```bash
terraform plan      # « No changes » : code, état et réalité concordent
```

**3. Dérive** (une modification faite hors de Terraform)

```bash
docker rm -f tf-apprentissage-postgres
terraform plan      # le conteneur sera recréé : Terraform a constaté qu'il manque
terraform apply     # réparé ; les données sont intactes (elles sont dans le volume)
```

Puis une modification du code : passer `postgres_port` à 5443 (`terraform apply -var postgres_port=5443`).
Le plan affiche `-/+` (« forces replacement ») : un conteneur Docker ne change pas de port en place, il est
remplacé. Distinguer `~` (modification en place) et `-/+` (remplacement) est le réflexe à prendre avant tout
`apply` : sur Azure, un remplacement de base de données efface les données.

**4. Ce que contient l'état**

```bash
terraform state list                                  # les 4 ressources connues
terraform state show docker_container.postgres        # tous les attributs réels
grep -cF "$TF_VAR_postgres_password" terraform.tfstate # > 0 : le mot de passe est en clair dans l'état
```

D'où deux règles de la S6 : l'état n'est jamais commité (`.gitignore`), et sur Azure il sera distant, chiffré et
verrouillé ; les secrets seront créés dans le Key Vault et lus par identité managée, pas passés en variables.

**5. Détruire**

```bash
terraform destroy   # conteneur, réseau et volume supprimés ; l'image reste (keep_locally = true)
docker volume ls | grep tf-apprentissage   # rien
```

**Terminé quand** : cycle complet fait, dérive observée et réparée, `-/+` repéré dans un plan, mot de passe trouvé
dans l'état.
