# ADR-013 : Hébergement d'Airflow — une version allégée sur le serveur de la démo

- **Statut** : proposé (mise en œuvre à faire : ingestion en production)
- **Date** : 2026-10-13

## Contexte

Le pipeline d'ingestion (collecte, extraction, découpage, contrôles qualité, vectorisation, publication, alerte ;
ADR-003, ADR-007, ADR-008) est orchestré par Airflow **sur le poste de développement**. En production, **aucune
ingestion n'a jamais tourné** : le corpus de la démo vient d'une sauvegarde de base copiée à la main (S3, S5), et le
dossier des fichiers bruts du serveur est vide. Le pipeline n'existe donc pas pour les visiteurs.

Le besoin est modeste : **une exécution par semaine** (ADR-009), quelques minutes, 18 documents. Le plan qualifie
pourtant Airflow de « composant le plus lourd du projet » : il faut chiffrer chaque option. Il faut aussi le dire :
Airflow a été choisi en S4 pour apprendre l'outil et pour le bloc BC03 de la certification, pas parce qu'une tâche
hebdomadaire l'exigeait.

Le serveur de la démo (OVHcloud VPS-1 : 2 vCores, 4 Go) a **2,97 Go de mémoire disponible** depuis que la
vectorisation passe par OVHcloud (ADR-012 ; 1,5 Go avant).

## Mesures (2026-10-13, poste, Airflow 3.3.1)

| Configuration | Conteneurs | Mémoire au repos | Pic pendant une exécution |
|---|---|---|---|
| Officielle (CeleryExecutor : interface, ordonnanceur, analyse des DAG, triggerer, worker, Redis, base) | 7 | **1,77 Go** | — |
| **Allégée** (LocalExecutor : les tâches tournent dans l'ordonnanceur ; ni Redis, ni worker, ni triggerer ; `pipelines/airflow/docker-compose.leger.yaml`) | 4 | **0,70 Go** | **0,88 Go** |

Exécution complète du DAG `ingestion` en version allégée : 7 tâches réussies, 69 secondes (aucun texte changé).
Les conteneurs d'ingestion lancés par les tâches s'y ajoutent : 512 Mo au plus chacun (limite du compose de
production), un à la fois.

## Options envisagées

1. **A. Airflow allégé sur le serveur de la démo** — 0 € de plus.
   - Pic estimé ≈ 1,4 Go (Airflow + une tâche d'ingestion) sur 2,97 Go disponibles : il reste ≈ 1,5 Go de marge.
   - Le pipeline tourne là où est la démo : mêmes images, même base, même volume de fichiers bruts.
   - Limites : un seul serveur pour tout (une ingestion lourde peut ralentir la démo) ; le socket Docker donne à
     Airflow le contrôle de la machine (ADR-007) : interface jamais exposée (127.0.0.1, accès par tunnel SSH).
2. **B. Petite machine dédiée** (Hetzner CX23 ≈ 6 €/mois, OVHcloud VPS-1 ≈ 4 €/mois) : isole la démo, mais
   l'ingestion doit atteindre la base du VPS (tunnel ou réseau privé à monter et sécuriser) : plus de pièces pour
   une tâche hebdomadaire.
3. **C. Airflow géré** : Amazon MWAA ≈ 350 $/mois pour le plus petit environnement facturé à l'heure (une offre
   « serverless » facturée au temps d'exécution existe depuis fin 2025, non étudiée : AWS) ; Google Cloud Composer
   ≈ 300 $/mois ; Astronomer selon contrat. Hors de proportion : une tâche par semaine, une démo à 4 €.
4. **D. Pas d'Airflow en production** : un minuteur système (systemd) lance la même chaîne de commandes .NET, avec la
   même alerte par un script. 0 €, le plus léger. Perd l'interface, l'historique et les relances d'Airflow ;
   Airflow resterait sur le poste comme démonstration, ce qui laisse la production sans le pipeline montré.
5. **E. Tâches planifiées Azure** (Container Apps Jobs) : coût faible, mais l'environnement Azure est détruit à
   chaque séance (option B de la S6) : il ne peut pas porter la production. Reste une piste pour la vitrine.

## Décision

**Option A : Airflow allégé (LocalExecutor) sur le serveur de la démo**, interface accessible seulement par tunnel
SSH. C'est sans surcoût, la mémoire le permet (mesuré), et c'est le pipeline réel qui met à jour la démo réelle.
Repli si la marge ne tient pas en production : option D, avec les mêmes commandes.

## Conséquences

- Mise en œuvre (à faire) : compose allégé sur le serveur, DAG adapté au réseau et aux volumes de production
  (`limpide-prod`), vectorisation par OVHcloud dans les tâches (`Embedding__*`), webhook Discord, clé Fernet ;
  limites de mémoire par conteneur ; puis **première ingestion réelle en production** (contrôler que les fausses
  versions de la CNIL sont écartées sur le serveur, que la quarantaine et l'alerte fonctionnent).
- Le dossier des fichiers bruts du serveur se remplit enfin : la trace `trace` et les contrôles qualité y
  retrouvent leurs fichiers.
- Surveillance de la mémoire du serveur (S8) : la marge est mesurée sur le poste, à confirmer sur le VPS.
- **Conditions de révision** :
  - mémoire disponible sous ≈ 500 Mo pendant une ingestion, ou démo ralentie → option D (minuteur système), ou B ;
  - plus de sources ou des mises à jour plus fréquentes (ADR-009) → machine dédiée (B) ;
  - étape « Migration cloud » : l'orchestration suit l'hébergeur retenu (tâches planifiées du fournisseur, ou
    Airflow sur la même machine).
