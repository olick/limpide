# ADR-003 : Ingestion par une application console .NET en phase 1, Airflow en phase 2

- **Statut** : accepté
- **Date** : 2026-09-28

## Contexte

Objectif : une démo en ligne en 3 semaines, en parallèle d'une mission. Je ne connais pas encore Airflow.
Le corpus change rarement (quelques mises à jour par mois).

## Options envisagées

1. **Airflow dès le départ** — la cible finale, mais apprentissage et hébergement à assumer avant d'avoir
   la moindre démo.
2. **Application console .NET lancée à la main** — rapide à écrire avec mes compétences actuelles ;
   pas de planification, de relance ni de supervision.

## Décision

Console .NET en phase 1, conçue pour être reprise telle quelle par Airflow en phase 2 :
- chaque étape (collecte, extraction, découpage, embeddings) est une commande séparée ;
- chaque étape est **idempotente** (empreinte SHA-256 du contenu brut) ;
- les étapes échangent via le stockage (fichiers bruts, base), jamais en mémoire.

## Conséquences

- En phase 2, Airflow orchestre les mêmes commandes dans des conteneurs : pas de réécriture.
- Ce passage POC → production sera documenté comme un exemple d'industrialisation (bloc 3 de la RNCP).
- Batch incrémental et non streaming : les sources changent rarement, rien ne justifie un flux temps réel.
