# ADR-016 : Évaluation avant la production dans un environnement jetable sur le runner, recréation d'Azure à part

- **Statut** : accepté
- **Date** : 2026-10-17

## Contexte

L'objectif de la S7 : une modification qui dégrade les réponses est **bloquée avant la production**. La barrière
existe (`gate`, seuils de l'ADR-014) ; il restait à la placer dans la chaîne (ADR-015).

Le plan prévoyait une **préproduction Azure éphémère** à chaque PR qui touche aux réponses : `apply`, déploiement,
données restaurées, `evaluate-answers`, `gate`, `destroy`. En écrivant la session 4b, trois constats :

1. **`evaluate-answers` n'utilise pas l'application déployée.** Il appelle `AnswerService` dans son propre
   processus. Sur Azure, l'évaluation tournerait donc sur le runner, branchée sur la base Azure. La Container App
   créée à chaque PR ne servirait qu'à un test de fumée.
2. **La production, c'est le VPS** (ADR-006) : PostgreSQL et pgvector dans Docker (`pgvector/pgvector:pg17`),
   connexion par mot de passe. Un conteneur de la même image sur le runner lui ressemble davantage qu'un PostgreSQL
   managé d'Azure (Entra ID, extensions autorisées une à une).
3. **Le coût par PR** :
   - durée : environ 25 à 30 min (`apply` ≈ 11 min, `destroy` ≈ 13 min, mesures de la S6) ;
   - droits : une identité capable de créer des groupes de ressources et d'attribuer des rôles, donc proche de
     propriétaire de l'abonnement, utilisable depuis n'importe quelle branche du dépôt ;
   - risque : un `destroy` raté laisse un environnement qui continue de facturer.

## Options envisagées

1. **Préproduction Azure éphémère à chaque PR concernée** (le plan initial). Elle prouve à chaque fois que
   l'infrastructure se recrée depuis le code. En contrepartie, chaque PR coûte environ 25 à 30 min et l'identité
   est très large. Elle n'évalue pas mieux : même code, même processus, autre base.
2. **Environnement jetable sur le runner, seul.** Rapide et gratuit côté Azure. Mais plus rien ne vérifie
   régulièrement qu'Azure se recrée depuis le code.
3. **Hybride** : l'option 2 sert de barrière ; la recréation complète d'Azure (`apply`, base, test de fumée,
   `destroy`) devient un workflow lancé à la main et validé, hors du chemin critique. Il sert aussi la
   vitrine à la demande. **Retenu**, décidé avec Alexandre le 2026-10-17.

## Décision

**La barrière avant la production est `deploy/evaluer.sh`, lancé sur le runner à chaque PR qui peut changer les
réponses.** Le même script se lance depuis le poste.

| Étape | Ce qui se passe |
|---|---|
| Déclenchement | `evaluation.yml` à chaque PR. `deploy/evaluation-requise.sh` compare la PR à sa base : consignes, `appsettings.shared.json`, `Limpide.Core`, `Limpide.Infrastructure`, `Limpide.Ingestion` (dont les seuils de `gate`), jeu de questions, versions des paquets, l'évaluation elle-même |
| Clés d'API | lues dans le coffre du socle par l'identité `id-limpide-ci-evaluation`, masquées dans les journaux |
| Données | `corpus.dump`, conteneur `corpus` du socle : `pg_dump` du corpus sans donnée de visiteur, journal des migrations compris |
| Base | conteneur `pgvector/pgvector:pg17` jetable ; sauvegarde restaurée, puis `migrate` du code de la PR, comme un déploiement |
| Évaluation | `evaluate-answers` (jeu complet, juge de fidélité), configuration du dépôt seulement (ni user-secrets ni réglages du poste), questions vectorisées par OVHcloud AI Endpoints comme en production |
| Décision | `gate` : code 1 si un seuil bloquant est franchi ; tableau dans le résumé de l'exécution, rapports en artefact (90 jours) |

**Le blocage passe par la fusion, pas par un lien entre workflows.** Le job « Évaluation » est un contrôle requis
de `main` (ruleset). Une PR qui échoue à la barrière ne peut pas être fusionnée, et `deploiement.yml` ne part que
d'une fusion. Un job sauté par sa condition compte comme réussi : une PR sans effet sur les réponses n'est ni bloquée
ni facturée. Si la détection elle-même échoue, l'évaluation tourne (dans le doute, on évalue).

**Identité dédiée** (`etat/creer-identites-ci.sh`) : `id-limpide-ci-evaluation`, sujet OIDC
`environment:evaluation` (environnement GitHub sans relecteur). Elle peut lire le conteneur `corpus` et deux secrets
nommés, **secret par secret** : une clé ajoutée plus tard au coffre ne lui sera pas lisible. Elle n'a aucun droit
sur l'abonnement.

## Conséquences

- **Mesures** (2026-10-17, première passe depuis le poste, rapport `eval/results/2026-10-17-2050-…`) :
  - durée totale : 1 min 48 s, dont 3 s pour restaurer la sauvegarde (6,9 Mo, 1 130 passages servis) ;
  - coût : 0,28 $ (rédaction 0,185 $, juge 0,094 $) ;
  - barrière passée, avec des valeurs dans la fourchette des passes de référence de l'ADR-014 : 1 échec, 21/30
    passages attendus fournis, 19/30 cités, 0,15 de réponses jugées infidèles.
  - Contre 25 à 30 min pour la préproduction Azure, et environ 0,10 € d'Azure en plus par exécution.
- **Ce qu'on ne teste plus à chaque PR** :
  - l'image publiée : on évalue le code compilé du même commit, pas l'image qui partira en production ;
  - Azure : l'authentification Entra ID, Container Apps et la recréation de l'infrastructure. C'est le rôle du
    workflow de recréation (à construire).
- **Le corpus évalué est une sauvegarde**, pas la base de la production. Elle se périme si la production publie une
  nouvelle version et qu'on oublie `deploy/exporter-corpus.sh`. Son empreinte (versions, passages, hachage) figure
  dans chaque rapport : la comparer à celle de la production. Les changements du corpus (ingestion) ne passent pas
  par cette barrière ; ils ont leurs propres contrôles : `validate`, quarantaine, `publish` (ADR-008).
- **Bascule vers Qwen3-Embedding (après la session 5)** : la PR qui change de modèle d'embedding ne pourra être
  évaluée que si la sauvegarde contient déjà les vecteurs Qwen. La recherche ne compare que des vecteurs de même
  libellé : avec ceux de bge-m3 seuls, elle ne trouverait rien. Il faudra donc calculer les vecteurs Qwen en
  production (deuxième colonne, à concevoir avec la bascule sans interruption), puis exporter, puis ouvrir la PR.
- **La sauvegarde contient le texte des documents** (CNIL en CC-BY-ND, EUR-Lex) dans un conteneur privé : copie de
  travail, jamais redistribuée. Elle ne contient ni les retours des visiteurs ni le journal du back office.
- **Coût Mistral** : environ 0,29 $ à chaque push sur une PR concernée (un nouveau push annule l'exécution en
  cours). Le plafond de 10 € par mois chez Mistral est **partagé avec la démo**, dont le quota peut à lui seul
  consommer environ 9 € : des évaluations nombreuses pourraient couper la démo en fin de mois. À trancher : clé
  séparée avec son propre plafond, ou plafond relevé.
- **Résultats variables d'une passe à l'autre** : les seuils ont une tolérance (ADR-014). Un échec isolé reste
  possible : relancer le job. Si cela devient fréquent, revoir les seuils dans un commit qui dit pourquoi, jamais
  les désactiver.
- **PR venue d'un fork** : pas de jeton OIDC, donc l'évaluation échoue sans rien exposer, et la PR n'est pas
  fusionnable telle quelle. Acceptable avec un seul auteur.
- **L'environnement `evaluation` n'est pas une barrière** (relecture du 2026-10-17) : sans relecteur ni restriction
  de branche, une branche du dépôt peut modifier le workflow ou `evaluer.sh` et lire les deux clés d'API. La vraie
  barrière est le droit de pousser sur le dépôt, que l'auteur est seul à avoir. Un deuxième contributeur changerait
  l'analyse : clés propres à l'évaluation, plafonnées, ou relecteur sur l'environnement.
- **La liste des fichiers qui déclenchent l'évaluation se tient à la main.** Un oubli laisserait passer une
  modification non évaluée ; elle est donc large exprès (tout `Limpide.Ingestion`, par exemple, même la collecte).
- **La configuration de production n'est pas évaluée** : `docker-compose.prod.yml` fixe la vectorisation du VPS
  (fournisseur, adresse) ; `evaluer.sh` en recopie l'adresse. Une modification de ce fichier part en production sans
  évaluation, et le lancer ne prouverait rien puisque l'évaluation ne le lit pas. Garde-fou : la règle de l'ADR-012
  (changer de modèle d'embedding impose de recalibrer le seuil et de réévaluer), et la relecture de la PR.
- **Recréation d'Azure (4b-2)** : workflow `azure.yml`, lancé à la main et validé (environnement `azure`, limité à
  `main`, relecteur requis), sans planification (décidé avec Alexandre le 2026-10-17 : l'identité qu'il utilise ne
  sert jamais sans quelqu'un derrière). Actions : `verifier` (préproduction créée, base restaurée depuis le socle, test
  de fumée sur l'accueil et `/sante`, puis détruite, même en cas d'échec, et seulement si elle n'existait pas avant),
  `vitrine`, `detruire-vitrine`. Identité `id-limpide-ci-azure`, administratrice PostgreSQL des environnements qu'elle
  crée :
  - Contributeur de l'abonnement (Terraform crée les groupes de ressources), plus le droit d'attribuer deux rôles de
    données à des identités de service (condition ABAC) ;
  - **ce qui reste exposé** : la condition borne les rôles, pas leur portée. L'identité peut s'attribuer la lecture du
    coffre (clés Mistral et OVH) ou l'écriture du conteneur `corpus`. Contributeur atteint aussi le socle : réactiver
    les clés d'accès du compte de stockage, changer le mode d'accès du coffre, ajouter une relation de confiance aux
    autres identités. Un verrou « suppression impossible » protège le compte de l'état et du corpus (Contributeur ne
    peut pas le retirer) ; le coffre a sa suppression réversible et sa purge interdite. Isolation complète : un
    abonnement séparé pour le socle, à envisager avec la migration cloud ;
  - un `apply` tué brutalement laisse l'état verrouillé, et la destruction échoue : environnement payant jusqu'à
    `terraform force-unlock` puis `detruire.sh` (README d'`azure/`). L'étape d'`apply` a son propre délai (30 min)
    pour laisser du temps à la destruction.
- **Conditions de révision** :
  - **Azure devient permanent** (migration cloud, après le plan) : évaluer sur la préproduction permanente, par
    l'application déployée.
  - **L'évaluation passe par l'API de l'application** (HTTP) : l'environnement déployé redevient utile, et
    l'option 1 se discute à nouveau.
  - **Le coût Mistral devient gênant** : sous-ensemble de questions dans les PR, jeu complet avant la fusion.
