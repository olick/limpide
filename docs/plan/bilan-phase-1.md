# Bilan de la phase 1 (semaines 1 à 3)

- **Date** : 2026-10-07
- **Résultat** : démo publique en HTTPS, https://www.limpide-ia.fr

## Ce qui existe

| | |
|---|---|
| Corpus | AI Act (version française, via l'API CELLAR) + 2 fiches pratiques IA de la CNIL ; 691 passages |
| Chaîne d'ingestion | `fetch → extract → chunk → embed`, commandes séparées et idempotentes (console .NET) |
| Recherche | vectorielle (bge-m3, titre + texte vectorisés), seuil de pertinence ; top 5 : 8/10 |
| Génération | Mistral Medium 3.5, inférence UE ; consignes `answer/3` versionnées |
| Contrôles | citations vérifiées, réponse sans citation, réponse ambiguë, qualification juridique, hors sujet |
| Interface | une page Blazor : réponse, textes cités à l'identique avec licence, panneau « sous le capot » |
| Évaluation | `evaluate` (recherche, 10 questions), `evaluate-answers` (chaîne complète, 15 questions, rapports relus) |
| Hébergement | VPS OVHcloud (France), Docker Compose, Caddy (HTTPS automatique), sauvegarde quotidienne |
| Protections | 10 questions/h par IP, 60/jour au total, plafond Mistral de 10 €/mois |
| Décisions | ADR-001 à 006 ; observations dans `docs/notes/` |

**Ordres de grandeur mesurés** : une réponse en 1 à 4 s (dont ≈ 0,4 s de vectorisation sur le serveur à 2 vCores),
≈ 0,4 centime par question avec Medium ; vectorisation complète du corpus : 9 min 30 s sur un poste à 6 cœurs.
**Coût de fonctionnement** : ≈ 3,81 € HT/mois de serveur, le nom de domaine, et Mistral (quelques centimes jusqu'ici,
9 €/mois au plus avec le quota).

## Ce que la phase 1 a appris

Les points qui ont changé une décision, ou qui auraient fait échouer la démo s'ils étaient passés inaperçus :

| Constat | Conséquence |
|---|---|
| EUR-Lex bloque les scripts (défi anti-robot) | texte téléchargé par l'API officielle CELLAR ; même texte, même licence |
| Les pages CNIL changent de quelques octets chaque jour (identifiants aléatoires) | fausses nouvelles versions ; à traiter avec le cycle de vie des versions (S5) |
| Vectoriser le titre avec le texte : top 5 de 5/10 à 8/10 | retenu |
| Recherche hybride (plein texte + fusion des rangs) : 6/10 | écartée, malgré l'intuition (ADR-005) |
| Aucun seuil ne sépare les questions proches du sujet mais sans réponse | deux défenses : seuil pour le hors sujet, « je ne sais pas » du modèle pour le reste |
| Les contrôles automatiques ont eux-mêmes été faux (formats de citation, refus mal détecté) | corrigés avant de conclure ; un contrôle se teste aussi |
| Mistral Small déforme des textes sans que rien ne le détecte ; Medium tranche la situation de l'utilisateur | Medium + consignes renforcées + détection (ADR-004) ; **la relecture a renversé le verdict des chiffres** |
| Deux pannes de déploiement vues seulement dans le navigateur (script Blazor absent ; type pgvector recréé par la restauration) | corrigées, verrouillées, documentées ; tester dans un navigateur à chaque mise en ligne |
| Azure facture cher un service toujours allumé, et un démarrage à froid est inacceptable en rendez-vous | VPS à ≈ 4 €/mois (ADR-006) ; Azure en phase 2, chiffré |

## Limites de la V1 : le programme de la phase 2

| Limite | Risque | Réponse prévue | Semaine |
|---|---|---|---|
| Ingestion lancée à la main | corpus qui vieillit sans que personne ne le voie | DAG Airflow qui orchestre les mêmes commandes, conteneurisées | S4 |
| Une version collectée devient courante sans contrôle | une page mal extraite remplace une version saine en production | contrôles qualité, quarantaine, publication explicite, alerte | S5 |
| Fausses nouvelles versions CNIL | revectorisations inutiles, historique faussé | publier seulement si le texte extrait change | S5 |
| Corpus réduit (3 documents) | démo limitée | élargissement, une fois le pipeline fiable | S5 |
| Schéma modifié en recréant la base | impossible en production | outil de migrations SQL | S5 |
| Déploiement manuel, une seule machine | erreurs de manipulation, pas de préproduction | Terraform sur Azure, deux environnements | S6 |
| Secrets dans un fichier sur le serveur | fuite, rotation difficile | Key Vault, identités managées | S6 |
| Hébergement du modèle d'embedding | coût au repos, démarrage à froid | comparer Ollama sur CPU et `mistral-embed` (mesure, recalibrage du seuil) | S6 (ADR-012) |
| Pas d'évaluation automatique avant mise en ligne | une modification du prompt ou du modèle dégrade la qualité sans alerte | jeu de 50 questions, évaluation en CI, déploiement bloqué si le score baisse | S7 |
| Relecture des réponses entièrement manuelle | coûteuse, donc rare | relecture assistée (modèle juge) + échantillon relu par un humain | S7 |
| Pas de supervision ni de traces | une panne ou une dérive passe inaperçue | OpenTelemetry, tableaux de bord, alertes | S8 |
| Coûts suivis à la main | dépassement découvert trop tard | coût par requête et par ingestion, alertes de budget | S8 |
| Anti-robot absent ; quota en mémoire | abus distribué, quota remis à zéro au redémarrage | défi anti-robot, quota partagé | S8 |
| Questions des utilisateurs : aucune politique écrite | données personnelles possibles dans les questions | décision de conservation, information des utilisateurs | S8 |
| Pas de dossier de gouvernance | conformité non démontrée | registre des sources et des risques, classification de Limpide au regard de l'AI Act | S9 |

## Questions ouvertes

- **Considérants contre articles** : les considérants (explicatifs) passent souvent devant l'article qui fait foi.
  Pondérer les articles, ou afficher l'article lié ? À mesurer sur le jeu de 50 questions.
- **Vocabulaire des utilisateurs** (« grands modèles de langage » contre « modèles d'IA à usage général ») :
  reformulation de la question par le modèle, au prix d'un appel de plus ?
- **Fiabilité de la mesure** : 10 à 15 questions écrites par l'auteur ne suffisent pas à départager finement deux
  options ; la S7 doit apporter un jeu plus large, et si possible des questions écrites par d'autres.

## Retours des premiers testeurs

Recueillis par le formulaire en bas de page (trois questions : qu'avez-vous compris de ce que fait Limpide ? une
réponse vous a-t-elle paru fausse ou trompeuse ? le panneau « sous le capot » vous a-t-il servi ?), lus avec la
commande `feedback`. À synthétiser ici après les premiers testeurs (objectif : 2 ou 3 personnes extérieures).

**Premier retour (Alexandre, 2026-10-07, question jointe sur les chatbots)** : « Il donne un point de vue légal » —
malgré l'avertissement, la réponse est perçue comme un avis juridique : risque à suivre pour la gouvernance (S9).
« Je n'ai pas tout compris [du panneau] mais c'est utile » : à rendre lisible pour un non-spécialiste (S8).
