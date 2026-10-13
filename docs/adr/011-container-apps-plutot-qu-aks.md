# ADR-011 : Azure Container Apps plutôt qu'AKS pour exécuter Limpide sur Azure

- **Statut** : accepté
- **Date** : 2026-10-13

## Contexte

Sur Azure (S6, option B : vitrine recréée à chaque séance, la démo publique restant sur le VPS, ADR-006), Limpide
fait tourner deux conteneurs permanents pendant une séance : l'application web (Blazor, 0,5 vCPU / 1 Go) et Ollama +
bge-m3 (2 vCPU / 4 Go, interne). S'y ajouteront des tâches ponctuelles (ingestion). Une seule personne exploite tout.

Contraintes : coût minimal (budget de 10 € par mois sur l'abonnement), environnement **recréé de zéro** à chaque
séance (`demo.sh` / `detruire.sh`), aucun secret (identité managée, Entra ID), observabilité de base. L'auteur connaît
Kubernetes : la compétence n'est pas le critère, la charge d'exploitation et le coût le sont.

Prix relevés le 2026-10-13 dans l'API publique des tarifs Azure (`prices.azure.com`), région France Centre, hors
taxes. Les tarifs à la seconde de Container Apps n'y sont exploitables qu'en dollars (l'API arrondit les montants en
euros à zéro) ; 1 $ ≈ 1 € pour les ordres de grandeur ci-dessous.

## Options envisagées

1. **Azure Container Apps, plan Consommation** (conteneurs gérés, sans serveur à administrer)
   - Tarif : vCPU actif 0,000024 $/s, au repos 0,000003 $/s ; mémoire 0,000003 $ par Go-seconde ; requêtes 0,40 $ le
     million. Gratuit chaque mois : 180 000 vCPU-secondes, 360 000 Go-secondes, 2 millions de requêtes. Pas de frais
     d'environnement : le compteur « Management » (0,11 €/h) ne vise que le plan dédié, les points de terminaison
     privés et la maintenance planifiée, non utilisés ici.
   - Coût de Limpide : ≈ 0,08 $ par heure de séance pour les deux conteneurs au repos (avant franchise gratuite),
     ≈ 55 $ par mois s'ils restaient allumés en permanence ; mise à l'échelle jusqu'à zéro possible.
   - Exploitation : aucune machine, aucun système, aucun Kubernetes à mettre à jour ; HTTPS, journaux (Log
     Analytics), identité managée et lecture des secrets du coffre intégrés ; ~30 lignes de Terraform par application.
     Mesuré : environnement complet recréé en 9 min 10 s, détruit en 13 min 5 s (PostgreSQL compris).
   - Limites : moins de contrôle (pas d'opérateurs, de politiques réseau fines, de maillage de services) ; produit
     propre à Azure (l'équivalent ailleurs : Serverless Containers chez Scaleway, à comparer en S6) ; 2 vCPU / 4 Go
     au plus par conteneur sur ce plan ; GPU seulement en plan dédié.

2. **AKS** (Kubernetes géré par Azure)
   - Tarif : plan de contrôle gratuit (palier « Free », sans engagement de disponibilité) ou 0,088 €/h en palier
     Standard (≈ 64 € par mois) ; nœuds facturés comme des machines virtuelles, en continu tant que le cluster existe :
     un nœud B2ms (2 vCPU, 8 Go, assez pour Ollama + l'application + les composants système) à 0,0831 €/h, ≈ 61 € par
     mois ; plus l'adresse publique et l'équilibreur de charge.
   - Coût de Limpide : ≈ 0,09 €/h en séance (palier Free, un nœud), ≈ 65 € par mois en permanence, ≈ 130 € en palier
     Standard ; pas de mise à zéro des nœuds système.
   - Exploitation : mises à jour de Kubernetes et des nœuds, contrôleur d'entrée HTTPS, certificats, identité des
     pods (workload identity), supervision ; création d'un cluster plus longue et plus de Terraform.
   - Atouts : tout l'écosystème Kubernetes (opérateurs, Helm, politiques réseau, pools de nœuds GPU) ; **portabilité**
     de l'exécution d'un cloud à l'autre (Kubernetes géré existe chez Scaleway et OVHcloud).

3. **Une machine virtuelle sous Docker Compose** (comme le VPS) : ≈ 61 € par mois pour un B2ms sur Azure, contre
   moins de 4 € pour le VPS. Écartée : sur Azure, elle cumule le prix du cloud et l'exploitation manuelle, sans rien
   montrer de plus que la démo actuelle.

## Décision

**Azure Container Apps, plan Consommation.** À cette échelle (deux conteneurs, une personne), il coûte autant ou moins
qu'AKS, supprime toute la couche Kubernetes à exploiter, et se recrée en quelques minutes : c'est ce que l'option B
demande. AKS reste la voie de montée en charge.

## Conséquences

- Configuration propre à Azure (`modules/app`) : la migration vers un autre hébergeur réécrit ce module ; les images
  Docker, elles, sont portables telles quelles.
- Démarrage à froid si `min_replicas = 0` : Ollama et Blazor Server s'y prêtent mal (ADR-006) ; en séance, une
  instance reste allumée (coût de repos ci-dessus).
- Pas de GPU sur ce plan : un LLM auto-hébergé (ADR sur l'hébergement des modèles) demanderait le plan dédié ou AKS.
- **Conditions de révision** :
  - plus d'une dizaine de services, ou besoin de l'écosystème Kubernetes (opérateurs, politiques réseau, maillage,
    pools GPU pour un modèle auto-hébergé) ;
  - charge soutenue et prévisible, où des nœuds réservés reviennent moins cher que la facturation à la seconde ;
  - étape « Migration cloud » : si l'hébergeur retenu n'a pas d'équivalent à Container Apps mais propose un Kubernetes
    géré peu coûteux, Kubernetes devient le format d'exécution commun, et donc la voie de sortie.
