# ADR-006 : Démo hébergée sur un VPS français, sous Docker Compose

- **Statut** : accepté (OVHcloud validé par Alexandre ; nom de domaine plus tard, démo d'abord sur l'adresse IP)
- **Date** : 2026-10-05, mis à jour le 2026-10-06 (VPS-2 à VPS-4 en rupture de stock : VPS-1 retenu)

## Contexte

La démo doit être en ligne en fin de semaine 3, utilisable en rendez-vous client : **réponse immédiate**,
HTTPS, nom de domaine, coût maîtrisé. Le déploiement est volontairement manuel cette semaine ; Terraform
le remplacera en semaine 6 sur Azure (point de comparaison du récit POC → production).

Ce qui doit tourner :

| Composant | Ressources mesurées ou estimées | Remarque |
|---|---|---|
| Application web (Blazor, rendu serveur) | ≈ 0,5 vCPU, < 0,5 Go | garde une connexion ouverte (SignalR) pendant la visite |
| PostgreSQL + pgvector | **55 Mo** mesurés (691 passages) | |
| Ollama + bge-m3 (vectorisation des questions) | **1,4 Go** mesurés, modèle chargé | **4,7 s de démarrage à froid** mesuré en S2, ≈ 150 ms ensuite |
| Ingestion (fetch → embed) | ≈ 10 min de calcul par mois | lancée à la main en S3, par Airflow en S4 |
| Génération | API Mistral (UE), ADR-004 | rien à héberger |

**La contrainte qui décide** : Ollama et Blazor Server supportent mal la mise à l'arrêt hors usage (scale to zero).
Un premier visiteur attendrait plusieurs secondes ; en rendez-vous client, c'est rédhibitoire. Il faut donc un
service **toujours allumé**, précisément le cas où la facturation à la seconde coûte le plus cher.

## Options envisagées

Prix relevés le 2026-10-05 (hors taxes ; à vérifier au moment de la commande).

1. **VPS français (OVHcloud VPS-2 : 4 vCores, 8 Go, 75 Go NVMe, ≈ 7,21 € HT/mois) + Docker Compose** :
   PostgreSQL, Ollama et l'application sur la même machine, reverse proxy HTTPS devant.
   - Coût faible et fixe ; aucun démarrage à froid (garder bge-m3 chargé : `OLLAMA_KEEP_ALIVE=-1`) ;
     Docker Compose déjà maîtrisé ; hébergeur français, cohérent avec l'inférence Mistral en UE.
   - Un seul serveur : pas de haute disponibilité, mises à jour et sauvegardes à notre charge ;
     éloigné de la cible Azure (mais Terraform arrive en S6).
   - Alternative équivalente : Hetzner (Allemagne), CX23 2 vCPU / 4 Go à ≈ 4 €/mois, plus juste en mémoire.
2. **Azure Container Apps (consommation) + PostgreSQL managé (Flexible Server B1ms) + Ollama en conteneur** :
   - PostgreSQL B1ms : 0,0167 €/h, ≈ 12 €/mois, plus le stockage ;
   - Container Apps : 0,000024 $ par vCPU-seconde active ; le tarif au repos varie selon les sources
     (0,000003 à 0,000008 $), à confirmer au calculateur Azure. Une application de 0,5 vCPU / 1 Go toujours
     allumée revient à ≈ 34 $/mois ; un conteneur Ollama de 1 vCPU / 3 Go toujours allumé, ≈ 30 à 45 $ ;
   - total ≈ 60 à 90 €/mois, soit environ 10 fois l'option 1, pour le même service rendu à la démo.
   - Proche de la cible (S6), mais le portail Azure est à découvrir cette semaine, et le coût n'apporte rien à la démo.
3. **Azure Container Apps + API d'embeddings Mistral (sans Ollama)** : `mistral-embed`, 0,10 $ par million
   de tokens (le corpus entier : ≈ 0,02 $).
   - Supprime le conteneur le plus gourmand et son démarrage à froid ; l'application peut s'arrêter hors usage ;
     ≈ 16 à 50 €/mois selon qu'elle reste allumée.
   - **Change le modèle d'embedding mesuré en S1** : revectoriser le corpus, refaire `evaluate` (8/10 avec
     bge-m3+titre), recalibrer le seuil de pertinence (0,50, propre à bge-m3). À mesurer, pas à supposer.

## Décision

**Option 1 pour la semaine 3, sur un OVHcloud VPS-1** (2 vCores, 4 Go, 40 Go NVMe, ≈ 3,81 € HT/mois, en France),
avec Docker Compose. Le VPS-2 visé était en rupture de stock (VPS-2 à VPS-4, 2026-10-06).

Le VPS-1 suffit, Ollama compris : ≈ 2,3 Go utilisés sur 4 (Ollama avec bge-m3 1,4 Go et PostgreSQL 55 Mo mesurés ;
application web ≈ 0,3 Go et système ≈ 0,5 Go estimés). Avec 2 vCores, la vectorisation d'une question reste de l'ordre
de quelques centaines de millisecondes (non mesuré sur le VPS), et une revectorisation complète du corpus,
opération rare, de l'ordre de 30 à 40 minutes. Passage à une offre supérieure possible si la mémoire manque.

L'option 3 (`mistral-embed`, sans Ollama) n'est plus nécessaire pour tenir dans le serveur ; elle reste une
simplification à mesurer dans l'ADR sur l'hébergement des modèles (semaine 6), avec le reste de l'infrastructure Azure.

## Conséquences

- Coût de la démo : ≈ 3,81 € HT/mois de serveur, + nom de domaine (≈ 10 €/an, plus tard), + Mistral (plafond 10 €/mois).
- Mémoire comptée : limiter chaque conteneur (`mem_limit`) pour qu'un emballement de l'un ne fasse pas tomber les autres.
- À construire en S3 : images Docker, `docker-compose.prod.yml`, reverse proxy HTTPS (certificat automatique),
  sauvegarde de la base, protection de la démo (limite par IP, plafond Mistral, longueur des questions).
- Les ports PostgreSQL et Ollama restent fermés vers l'extérieur (Ollama n'a pas d'authentification) :
  seul le reverse proxy est exposé.
- Les données des utilisateurs (questions posées) transitent par le serveur et par Mistral : rien n'est conservé
  pour l'instant ; la question des journaux et des données personnelles est traitée en S8.
- **Conditions de révision** : besoin de haute disponibilité ; trafic qui sature le serveur ; passage à Azure
  en S6 (ADR sur Container Apps, l'hébergement d'Airflow et celui des modèles), qui reprendra ce choix avec les coûts réels mesurés ici.

Sources : [OVHcloud, offres VPS](https://www.ovhcloud.com/fr/vps/) ;
[comparaison Container Apps / Hetzner, 2026-09](https://bex.co/blog/2026/09/07/azure-container-apps-vs-hetzner-box) ;
[tarifs Container Apps](https://azure.microsoft.com/en-us/pricing/details/container-apps/) ;
API publique des tarifs Azure (PostgreSQL Flexible Server, France Centre) ;
[tarifs de l'API Mistral](https://mistral.ai/pricing/api).
