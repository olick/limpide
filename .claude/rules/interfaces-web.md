---
paths:
  - "src/Limpide.Web/**"
  - "src/Limpide.Admin/**"
  - "src/Limpide.Core/Protection/**"
---

# Interfaces web (démo et back office)

- **Réponse du modèle = texte non fiable** : Markdown (Markdig) sans HTML, liens ni images (un
  `[x](javascript:...)` passait sinon) ; seuls liens : les citations `[P1]`, ajoutées après coup (`AnswerMarkdown`).
- Réponse (encadré bleu, sans-serif) et textes officiels (fond papier, filet doré, serif) visuellement distincts ;
  encart « Vous échangez avec une IA » obligatoire (AI Act, article 50). Garde-fous traduits par `PlainLanguage.cs`.
- Question limitée à 500 caractères, 60 s maximum par réponse ; quota (`QuestionQuota`) appliqué dans `Home.razor`
  avant l'appel au modèle, IP relevée dans `App.razor`, jamais journalisée.
- **Page « Vos données »** (`/confidentialite`, ADR-019) : mention d'information de tout ce que la démo traite. Un
  nouveau traitement, une durée ou un destinataire qui change (fournisseur de modèle, supervision) = cette page à
  mettre à jour dans la même PR. Texte de la question conservé 30 jours (`question_log`), case d'opposition sous le
  champ.
- **Vitrine des technologies** (`Components/TechStack.razor`, icônes Simple Icons dans `wwwroot/tech/`, aucune
  requête vers un tiers) : à tenir à jour à chaque technologie adoptée (par exemple GitHub Actions, OpenTelemetry).
- **Back office** : seul `AdminAuthentication.cs` connaît OpenID Connect ; accès par identifiant complet
  « émetteur#sujet » (≈ 119 caractères, pas un GUID du portail) ; décisions écrites dans `admin_actions`, dans la
  transaction de la décision. Réglages en lecture seule (modifiés par Git).
- Clés DataProtection persistées dans les volumes `web_keys` / `admin_keys`.
- Tester dans un navigateur (à la main : pas d'outil d'automatisation sur le poste) ; l'image ne se construit pas
  si `blazor.web.js` manque.
