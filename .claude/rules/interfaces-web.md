---
paths:
  - "src/Limpide.Web/**"
  - "src/Limpide.Admin/**"
---

# Interfaces web (démo et back office)

- **Réponse du modèle = texte non fiable** : Markdown (Markdig) sans HTML, liens ni images (un
  `[x](javascript:...)` passait sinon) ; seuls liens : les citations `[P1]`, ajoutées après coup (`AnswerMarkdown`).
- Réponse (encadré bleu, sans-serif) et textes officiels (fond papier, filet doré, serif) visuellement distincts ;
  encart « Vous échangez avec une IA » obligatoire (AI Act, article 50). Garde-fous traduits par `PlainLanguage.cs`.
- Question limitée à 500 caractères, 60 s maximum par réponse ; quota (`QuestionQuota`) appliqué dans `Home.razor`
  avant l'appel au modèle, IP relevée dans `App.razor`, jamais journalisée.
- **Vitrine des technologies** (`Components/TechStack.razor`, icônes Simple Icons dans `wwwroot/tech/`, aucune
  requête vers un tiers) : à tenir à jour à chaque technologie adoptée (GitHub Actions en S7, OpenTelemetry en S8…).
- **Back office** : seul `AdminAuthentication.cs` connaît OpenID Connect ; accès par identifiant complet
  « émetteur#sujet » (≈ 119 caractères, pas un GUID du portail) ; décisions écrites dans `admin_actions`, dans la
  transaction de la décision. Réglages en lecture seule (modifiés par Git).
- Clés DataProtection persistées dans les volumes `web_keys` / `admin_keys`.
- Tester dans un navigateur (à la main : pas d'outil d'automatisation sur le poste) ; l'image ne se construit pas
  si `blazor.web.js` manque.
