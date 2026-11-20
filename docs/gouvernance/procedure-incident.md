# Procédure d'incident

Que faire quand Limpide se comporte mal, fuit des données ou tombe : détecter, qualifier, corriger, communiquer,
retenir. Rédigée le **2026-11-20**. Une seule personne tient tous les rôles (`raci.md`) : la procédure sert surtout à ne
rien oublier sous pression, et à laisser une trace.

## Ce qui compte comme incident

| Type | Exemples | Rôle qui décide (`raci.md`) |
|---|---|---|
| **Comportement** | réponse fausse sur un point vérifiable, qualification juridique, citation inventée, injection obéie, corpus périmé | responsable du système IA |
| **Données** | donnée personnelle copiée ou exposée hors de ce que dit `rgpd.md`, fournisseur qui en fait un usage non annoncé | propriétaire des données |
| **Sécurité** | secret exposé, accès non prévu au serveur, à la base ou au back office | sécurité |
| **Disponibilité et coût** | démo indisponible, fournisseur en panne, dépense anormale | sécurité |

Un défaut connu, inscrit au registre des risques et toléré par la barrière (par exemple `q33`), n'est pas un
incident ; il le devient s'il s'aggrave.

## 1. Détecter

| Source | Ce qu'elle voit | Où |
|---|---|---|
| Alertes Grafana sur Discord | démo muette, questions en erreur, réponses lentes, dépense anormale, budget à moitié, mémoire, disque | `supervision/grafana/provisioning/alerting/alertes.yaml` |
| Bilan du DAG d'ingestion | échec, quarantaine, version publiée | Discord, ADR-007 et ADR-008 |
| Barrière d'évaluation | régression avant la production | CI, ADR-016 |
| Journal des réponses | garde-fous déclenchés en production | `answer_log`, tableau de bord Grafana |
| Avis des visiteurs | réponse jugée fausse ou gênante | back office, page « Retours » |
| Courriel | demande d'un visiteur, signalement | `contact@limpide-ia.fr` |
| Revue | relecture d'un document, d'une configuration ou d'un fournisseur | ce dossier |

## 2. Qualifier (dans l'heure qui suit la détection, si possible)

Répondre par écrit, dans l'entrée de journal de l'incident (étape 5) :

1. **Quoi, depuis quand, qui est touché** : visiteurs, données, budget.
2. **Gravité** :
   - **critique** : donnée personnelle ou secret exposé à un tiers ; réponse qui pourrait causer un préjudice et
     qu'un visiteur ne peut pas vérifier ; dépense hors des plafonds ;
   - **majeur** : réponse fausse sur un point vérifiable mais servi à plusieurs visiteurs (corpus périmé) ; démo
     indisponible plus d'une heure ; écart entre ce que dit la page « Vos données » et la réalité ;
   - **mineur** : le reste.
3. **Données personnelles** : y a-t-il une **violation** au sens du RGPD (article 4, point 12 : destruction, perte,
   altération, divulgation ou accès non autorisés, accidentels ou illicites) ? Si oui, le risque pour les personnes
   est-il nul, réel ou élevé ? Voir l'étape 4.

## 3. Corriger

- **Contenir d'abord** : arrêter ce qui aggrave (retirer un fichier, révoquer une clé, désactiver un réglage,
  remettre la version précédente). Moyens déjà prêts :
  - revenir à la version précédente de l'application : `deploy/deployer.sh <version>`, la version précédente étant
    affichée au début de chaque déploiement (`deploy/README.md`, « Mettre à jour l'application ») ;
  - revenir à l'état d'avant une publication du corpus : restaurer la sauvegarde quotidienne de la base
    (`deploy/README.md`, « Sauvegarde quotidienne de la base »), puis redémarrer `web` ; il n'existe pas de commande
    pour republier la version précédente d'un seul document ;
  - changer une clé : « Rotation de la clé Mistral » (`deploy/README.md`) ;
  - couper la démo : arrêter le conteneur `web` (le site renvoie une erreur, rien n'est servi de faux).
- **Corriger ensuite, par une PR** comme toute modification ; ajouter le cas au jeu d'évaluation quand c'est un
  comportement, pour que la barrière le voie la prochaine fois.
- **Vérifier en production** avant de déclarer l'incident clos (`/sante`, la question qui posait problème).

## 4. Communiquer

| À qui | Quand | Comment |
|---|---|---|
| **CNIL** | violation de données **susceptible d'engendrer un risque**, dans les **72 heures** après en avoir pris connaissance (RGPD, article 33.1) ; sinon, aucune notification | téléservice de notification de la CNIL ; ce qui manque peut être envoyé ensuite (article 33.4) |
| **Personnes concernées** | risque **élevé** pour leurs droits (article 34) | Limpide n'identifie pas ses visiteurs (article 11) : communication publique sur la démo (article 34.3, c) |
| **Visiteurs** | réponse fausse servie (gravité majeure ou critique) | note sur la page « Résultats d'évaluation » ou « Vos données », selon le sujet |
| **Fournisseur concerné** | incident chez lui ou causé par lui | son support |
| **Toutes les violations** | toujours, notifiées ou non | documentées dans ce dossier (article 33.5) : faits, effets, mesures |

## 5. Retenir

Une entrée par incident, dans le journal de la semaine en cours (`docs/plan/semaine-NN.md`) et, pour les données, dans
les écarts de `rgpd.md` :

- chronologie (détection, qualification, contention, correction, clôture) ;
- cause, y compris ce qui a permis qu'elle passe inaperçue ;
- ce qui change : test, contrôle, alerte, règle, document ; mise à jour du registre des risques
  (`registre-des-risques-ia.md`).

## Incidents déjà survenus, qualifiés après coup

| Date | Incident | Type, gravité | Violation de données ? | Communication | Ce qui a changé |
|---|---|---|---|---|---|
| jusqu'au 2026-11-06 | **AI Act périmé** : la démo servait le texte de 2024 et annonçait les obligations du haut risque au 2 août 2026 au lieu du 2 décembre 2027 | comportement, **majeur** (vérifiable, mais servi à tous) | non | aucune | version consolidée (ADR-023) ; faits attendus dans la barrière ; risque R4 |
| 2026-11-06 au 2026-11-19 | **Questions des visiteurs dans la sauvegarde du corpus** (Azure, puis machines de GitHub à chaque évaluation) | données, **majeur** (écart avec la page « Vos données ») | **oui, à notre analyse** : copie non prévue chez deux sous-traitants. Violation **non susceptible d'engendrer un risque** pour les droits et libertés des personnes : textes masqués, sans identifiant ni adresse IP ; conteneur Azure privé, lu par l'administrateur et l'identité de la chaîne seulement ; sauvegarde restaurée sur des machines de GitHub éphémères, le temps d'une évaluation (`deploy/README.md`, « Sauvegarde du corpus » ; ADR-016) | **pas de notification** à la CNIL (article 33.1) ; documentée ici et dans `rgpd.md` (article 33.5) | export par liste blanche, vérifié sur la sauvegarde (PR #109) ; anciennes versions supprimées le 2026-11-19 |
| jusqu'au 2026-11-19 | **Entraînement autorisé chez Mistral** : le réglage de l'API était actif ; 66 questions envoyées depuis le 2026-10-19 | données, **majeur** (destinataire et finalité non annoncés) | **non, à notre analyse**, choix d'interprétation : les questions sont parties là où elles devaient partir, pour la réponse, sous des conditions acceptées par Limpide ; l'écart tient à un réglage de ces conditions et à ce que la page n'en disait rien, donc un défaut d'information (article 13) plutôt qu'une divulgation non autorisée (article 4, point 12). Lecture inverse possible : à reprendre si une autorité la retient | pas de notification ; page « Vos données » corrigée ; documenté dans `rgpd.md` | réglage désactivé dans les deux espaces ; conditions de chaque sous-traitant relevées dans `rgpd.md` |

Chaque qualification est celle de l'auteur, pas relue par un juriste. Elle est à reprendre si un fait nouveau apparaît :
par exemple, un accès d'un tiers au conteneur Azure ou la preuve que Mistral a réellement utilisé ces questions.
