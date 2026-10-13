# Comparatif des coûts d'hébergement (S6, session 6)

Relevé le **2026-10-13**. Demandé le 2026-10-12 : baisser les coûts au maximum, et préparer l'étape « Migration
cloud » (hébergeur moins cher et plus souverain). Sert aux ADR sur l'hébergement d'Airflow et des modèles, et à la
révision éventuelle de l'ADR-006.

Prix hors taxes, région Paris quand elle existe. Mois = 730 heures. Les montants « permanent » sont des estimations
calculées à partir des tarifs unitaires, pas des factures. À revérifier au moment d'une décision : les grilles
changent (Hetzner a augmenté ses prix deux fois en 2026).

## Ce qu'il faut héberger

| Composant | Taille | Remarque |
|---|---|---|
| Application web (Blazor Server) | 0,5 vCPU, 1 Go | allumée en permanence (connexion ouverte pendant la visite, ADR-006) |
| Ollama + bge-m3 | 2 vCPU, 4 Go (≈ 2,3 Go utilisés) | allumé en permanence : sinon 4,7 s de démarrage à froid (ADR-006) |
| PostgreSQL + pgvector | la plus petite taille | 1 130 passages, quelques dizaines de Mo |
| Stockage des fichiers bruts | < 100 Mo | coût négligeable partout |
| Secrets | 1 (clé Mistral) | |
| Ingestion | ≈ 30 min par semaine | tâche planifiée (Airflow ou équivalent) |
| Génération | API Mistral (ADR-004) | hors comparatif : même coût partout |

## Tarifs unitaires relevés

| Hébergeur | Conteneurs gérés | PostgreSQL géré (plus petit) | Autres |
|---|---|---|---|
| **Azure** | Container Apps : vCPU 0,000024 $/s actif, **0,000003 $/s au repos** ; mémoire 0,000003 $/Go-s ; gratuit : 180 000 vCPU-s, 360 000 Go-s par mois | B1ms (1 vCore, 2 Go) 0,0167 €/h + stockage 0,117 €/Go/mois | AKS : plan de contrôle gratuit (palier Free) |
| **Scaleway** | Serverless Containers : vCPU 0,00001 €/s, mémoire 0,000002 €/Go-s affiché (la FAQ calcule avec 0,000001), **pas de tarif réduit au repos** ; gratuit : 200 000 vCPU-s, 400 000 Go-s | DB-DEV-S (2 vCPU, 2 Go) 0,0156 €/h + stockage 0,0993 €/Go/mois, sauvegardes 0,03 €/Go/mois ; pgvector annoncé | Kapsule : plan de contrôle mutualisé gratuit ; Secret Manager 0,04 €/version/mois ; Object Storage 0,016 €/Go/mois ; registre public gratuit jusqu'à 75 Go |
| **OVHcloud** | pas de conteneurs sans serveur ; Kubernetes géré : plan de contrôle gratuit, nœuds au prix des instances (d2-4 : 0,0206 €/h ; d2-8 : 0,0372 €/h) | Essential db1-4 0,0746 €/h ; Business db1-4 0,0949 €/h ; pgvector annoncé (offre précise à vérifier) | Object Storage standard ≈ 0,007 €/Go/mois ; VPS-1 (2 vCores, 4 Go) 3,81 €/mois (ADR-006, relevé le 2026-10-05) |
| **Hetzner** | aucun service géré de conteneurs ni de base | aucun | CX23 (2 vCPU, 4 Go, 40 Go) 5,49 €/mois + IPv4 0,50 € (relevé tiers d'août 2026 ; page officielle illisible automatiquement) |
| **Clever Cloud** | instances facturées à la seconde : XS (1 vCPU, 1,1 Go) 0,0222 €/h ; S (2 vCPU, 2 Go) 0,0444 €/h ; M (4 vCPU, 4 Go) 0,1056 €/h | XS_TNY 0,0208 €/h ; pgvector **sur demande au support** (annonce de 2024) | Cellar (stockage objet compatible S3) : non relevé |

## Coût estimé pour Limpide

| Option | Permanent (par mois) | À la demande (par heure de séance) | Exploitation |
|---|---|---|---|
| **VPS OVHcloud + Docker Compose** (aujourd'hui) | **≈ 4 €** | sans objet (forfait mensuel) | tout à la main : système, sauvegardes, mises à jour |
| **Hetzner CX23 + Compose**, recréé par Terraform (fournisseur `hcloud`) | ≈ 6 € | **≈ 0,01 €** | idem, mais recréable ; mémoire aussi juste que le VPS |
| **Azure** : Container Apps + PostgreSQL B1ms (actuel) | ≈ 75 € (conteneurs ≈ 58 $, base ≈ 16 €) | ≈ 0,10 € | aucune machine ; identités managées, coffre, journaux |
| **Scaleway** : Serverless Containers + DB-DEV-S | ≈ 90 à 100 € (conteneurs ≈ 75 à 90 €, base ≈ 12 €) | ≈ 0,12 à 0,14 € | aucune machine ; clés d'API (pas d'identité managée) |
| **OVHcloud** : Kubernetes géré (1 nœud d2-8) + PostgreSQL Essential | ≈ 82 € (nœud ≈ 27 €, base ≈ 55 €) | ≈ 0,11 € | Kubernetes à exploiter (entrée HTTPS, certificats) |
| **OVHcloud** : Kubernetes géré, PostgreSQL dans le cluster | ≈ 27 € | ≈ 0,04 € | base à exploiter soi-même (sauvegardes) |
| **Clever Cloud** : XS + M + PostgreSQL XS | ≈ 108 € | ≈ 0,15 € | aucune machine ; pgvector sur demande |

## Ce que le comparatif montre

1. **À cette taille, les services gérés coûtent 12 à 27 fois le VPS** : 75 à 108 € par mois contre 4 à 6 €. L'ADR-006
   (démo sur un VPS) reste juste, et l'option B (Azure en vitrine à la demande) aussi.
2. **Ollama coûte plus que tout le reste** : 2 vCPU et 4 Go allumés en permanence pèsent environ 80 % de la facture des
   conteneurs, partout. Une API d'embeddings changerait l'équation : c'est la question de l'ADR sur l'hébergement des
   modèles.
3. **Azure n'est pas le plus cher pour un service toujours allumé**, contrairement à l'intuition : son tarif réduit au
   repos (0,000003 $ par vCPU-seconde, huit fois moins qu'en activité) n'existe pas chez Scaleway, où une instance
   minimale se paie au prix plein. Scaleway redevient intéressant si les conteneurs peuvent s'arrêter (mise à zéro).
4. **PostgreSQL géré avec pgvector** : Scaleway (≈ 12 €) et Azure (≈ 16 € avec le stockage) sont les moins chers ;
   OVHcloud Essential est près de cinq fois plus cher (≈ 55 €) ; Clever Cloud ne l'active que sur demande.
5. **À la demande**, Hetzner (ou une instance OVHcloud) recréée par Terraform avec Docker Compose coûte dix fois moins
   que les services gérés (≈ 0,01 €/h), au prix de l'exploitation manuelle d'une machine et sans identités managées.
6. **Souveraineté** : Scaleway, OVHcloud et Clever Cloud sont des sociétés françaises ; Hetzner est allemand ; Azure
   relève du droit américain. La qualification SecNumCloud se vérifie sur la liste de l'ANSSI (étude de la S10).
7. **Ce qui se perd hors d'Azure** : les identités managées (clés d'API ou mots de passe à stocker et à faire tourner),
   et la connexion à PostgreSQL sans mot de passe.

## Alternative à Ollama : l'API d'embeddings de Mistral

Relevé le 2026-10-13 (documentation de Mistral, page du modèle) : **mistral-embed** (version 23.12), **0,10 $ par
million de tokens** en entrée (≈ 0,11 $ avec la majoration UE de 10 % déjà appliquée à la génération, ADR-004),
8 000 tokens au plus par texte. Dimension des vecteurs à confirmer par un appel (1 024 attendu, comme bge-m3).

| Usage | Volume estimé | Coût |
|---|---|---|
| Vectoriser tout le corpus (1 130 passages servis, ≈ 1,1 million de caractères avec les titres) | ≈ 300 000 tokens | ≈ 0,03 $ par passage complet |
| Questions des visiteurs (≈ 25 tokens chacune), 100 par jour | ≈ 75 000 tokens par mois | < 0,01 $ par mois |
| Questions, 10 000 par jour | ≈ 7,5 millions de tokens par mois | ≈ 0,80 $ par mois |

À comparer à Ollama allumé en permanence : ≈ 47 $ par mois sur Azure (2 vCPU, 4 Go au repos), ≈ 60 € chez Scaleway ;
sur le VPS, 2,3 Go des 4 Go de mémoire. **L'API retire environ 80 % du coût des conteneurs** et le démarrage à froid
du modèle.

Ce qu'elle coûte autrement :

- **changer de modèle** : tous les vecteurs à recalculer (quelques centimes), le seuil de pertinence `MinScore = 0,50`
  à recalibrer (propre à bge-m3, ADR-005), la recherche à remesurer (top 5, MRR) : la qualité de mistral-embed sur le
  vocabulaire juridique français est inconnue, et le modèle date de décembre 2023 ;
- **une dépendance de plus envers Mistral** : si l'API est indisponible, la recherche l'est aussi (la génération
  l'est déjà) ; les questions partent déjà chez Mistral pour la réponse : pas de nouvelle exposition des données ;
- **le code** : `IEmbeddingGenerator` (ADR-002) permet le changement par configuration et quelques lignes
  d'enregistrement ; le libellé `embedding_model` change, donc `embed` recalcule tout de lui-même.

**Mesure du 2026-10-13** (copie de la base locale `rag_mistral`, mêmes 1 130 passages servis, même texte vectorisé
« titre + texte », mêmes 10 questions ; `Embedding:Provider=Mistral`, `Embedding:Model=mistral-embed`) :

| | bge-m3 (Ollama, CPU) | mistral-embed (API) |
|---|---|---|
| Bon passage en 1re position | **4/10** | 1/10 |
| Bon passage dans le top 5 | **7/10** | 5/10 |
| MRR | **0,52** | 0,31 |
| Vectorisation du corpus | ≈ 816 ms par passage (S1, CPU du poste) | **41 ms par passage, 47 s en tout** |
| Meilleur score, questions légitimes | 0,541 à 0,705 | 0,807 à 0,887 |
| Meilleur score, 4 questions hors sujet (VPN, météo, football, recette) | 0,291 à 0,467 | 0,715 à 0,739 |
| « Règles sur l'IA aux États-Unis » (dans le domaine, sans réponse) | 0,655 | 0,772 |

- **La qualité baisse nettement** : mistral-embed place les considérants devant les articles qui font foi (q02, q04,
  q05, q06) et perd trois bons passages du top 5. Sur 10 questions, l'écart est trop grand pour être du bruit.
- Le seuil resterait calibrable (écart d'environ 0,07 entre hors sujet et légitime, comme avec bge-m3), mais autour de
  0,78 : les scores de mistral-embed sont tassés vers le haut.
- Coût réel de l'expérience : ≈ 300 000 tokens, ≈ 0,03 $.
- **Conclusion** : pas de passage à mistral-embed ; bge-m3 reste, pour la qualité. Le gain de coût (≈ 80 % des
  conteneurs) reste à chercher autrement : bge-m3 servi par une API (aucun hébergeur trouvé pour l'instant ; Scaleway
  propose bge-multilingual-gemma2 à 0,10 € le million de tokens, mais en 3 584 dimensions, au-delà de la colonne
  `vector(1024)` et de la limite d'index de pgvector pour ce type ; possible en demi-précision, `halfvec`, au prix d'une
  migration et d'une nouvelle mesure), ou Ollama mis à zéro hors usage.
- Décision formelle : ADR sur l'hébergement des modèles.

## Pistes pour l'étape « Migration cloud »

- **Cible la plus cohérente** : Scaleway (conteneurs sans serveur, PostgreSQL géré avec pgvector, Secret Manager,
  stockage S3, registre, Kapsule gratuit), au même prix qu'Azure tant qu'Ollama reste allumé, moins cher sinon.
- **Cible la moins chère** : une instance (Hetzner, OVHcloud ou Scaleway) recréée par Terraform avec Docker Compose :
  l'architecture du VPS, rendue reproductible.
- À vérifier avant de choisir : pgvector sur l'offre PostgreSQL OVHcloud visée ; prix officiels Hetzner ; tarif mémoire
  exact de Scaleway (0,000001 ou 0,000002 €/Go-s) ; Cellar de Clever Cloud ; gestionnaire de secrets chez OVHcloud.

## Sources

- Azure : API publique des tarifs, `https://prices.azure.com/api/retail/prices` (France Centre ; tarifs à la seconde
  de Container Apps lus en dollars, l'API arrondissant les montants en euros) ; page de tarifs de Container Apps
  (franchise gratuite, compteur « Management »).
- Scaleway : `scaleway.com/fr/tarifs/serverless/`, `/managed-databases/`, `/containers/`, `/security-and-account/`,
  `/storage/` ; FAQ Serverless Containers (exemple de calcul).
- OVHcloud : catalogue public de l'API, `https://eu.api.ovh.com/v1/order/catalog/public/cloud?ovhSubsidiary=FR` ; page
  PostgreSQL (pgvector) ; ADR-006 (VPS).
- Hetzner : comparatif tiers daté d'août 2026, `kimmo.suominen.com/stuff/cpc-2026-08.txt` (la page officielle charge
  ses prix dynamiquement).
- Clever Cloud : API publique, `https://api.clever-cloud.com/v4/billing/price-system?zone_id=par` ; changelog pgvector
  (février 2024).
