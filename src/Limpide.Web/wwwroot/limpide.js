// Panneau du menu (ADR-022) : <dialog> modal natif, qui piège le focus, se ferme par Échap et rend le focus au
// bouton qui l'a ouvert. Reste ici ce que le navigateur ne fait pas seul, en écoute déléguée sur le document pour
// marcher avant la connexion de Blazor (page pré-rendue) et si elle est coupée : ouvrir, fermer par le bouton, par
// un clic sur le fond (le contenu remplit le <dialog> : un clic qui vise le <dialog> lui-même vient du fond) ou en
// suivant un lien du panneau, y compris vers la page déjà affichée.
document.addEventListener("click", (event) => {
    const opener = event.target.closest("[data-menu-open]");
    if (opener) {
        const dialog = document.getElementById(opener.getAttribute("aria-controls"));
        if (dialog && !dialog.open) {
            syncThemeChoice();
            dialog.showModal();
        }
        return;
    }

    const dialog = event.target.closest("dialog.menu-panel");
    if (dialog && (event.target === dialog || event.target.closest("[data-menu-close], a[href]"))) dialog.close();
});

// Réglage « Apparence » : Automatique (suit l'appareil), Clair ou Sombre. Le choix reste dans le navigateur
// (localStorage), jamais envoyé au serveur ; App.razor le relit avant le premier affichage.
const themeKey = "limpide-theme";

function savedTheme() {
    try {
        const theme = localStorage.getItem(themeKey);
        return theme === "clair" || theme === "sombre" ? theme : "auto";
    } catch {
        return "auto";
    }
}

function syncThemeChoice() {
    const theme = savedTheme();
    for (const input of document.querySelectorAll('input[name="theme"]')) input.checked = input.value === theme;
}

document.addEventListener("change", (event) => {
    const input = event.target.closest('input[name="theme"]');
    if (!input) return;

    if (input.value === "auto") delete document.documentElement.dataset.theme;
    else document.documentElement.dataset.theme = input.value;

    try {
        if (input.value === "auto") localStorage.removeItem(themeKey);
        else localStorage.setItem(themeKey, input.value);
    } catch {
        // Stockage refusé (navigation privée stricte) : le thème vaut pour la page en cours seulement.
    }
});

// Renvoi [P1] d'une réponse (étape 4) : ouvre la carte de l'extrait, la place en haut du fil et y porte le focus.
// En capture, avant la navigation de Blazor ; sans script, le lien mène quand même à la carte.
document.addEventListener("click", (event) => {
    const link = event.target.closest("a.cite");
    const card = link && document.getElementById(decodeURIComponent(link.hash.slice(1)));
    if (!card || card.tagName !== "DETAILS") return;
    event.preventDefault();
    card.open = true;
    const reduce = window.matchMedia("(prefers-reduced-motion: reduce)").matches;
    card.scrollIntoView({ block: "start", behavior: reduce ? "auto" : "smooth" });
    card.querySelector("summary")?.focus({ preventScroll: true });
}, true);

// Champ de question (ADR-022) : Entrée envoie, Maj+Entrée va à la ligne ; pendant une saisie en cours (accents
// composés, méthodes d'entrée), Entrée valide la saisie, pas la question. La hauteur suit le texte, de 1 à 5 lignes.
document.addEventListener("keydown", (event) => {
    if (event.target.id !== "question" || event.key !== "Enter" || event.shiftKey || event.isComposing) return;
    event.preventDefault();
    event.target.form?.requestSubmit();
});

document.addEventListener("input", (event) => {
    if (event.target.id === "question") fitQuestion(event.target);
});

function fitQuestion(field) {
    const style = getComputedStyle(field);
    const max = (parseFloat(style.lineHeight) || 24) * 5 + parseFloat(style.paddingTop) + parseFloat(style.paddingBottom);
    field.style.height = "auto";
    field.style.height = Math.min(field.scrollHeight, max) + "px";
    field.style.overflowY = field.scrollHeight > max ? "auto" : "hidden";
}

window.limpide = {
    // Le texte du champ a été changé par le code (exemple, « Reprendre », envoi) : la hauteur suit.
    fitQuestion() {
        const field = document.getElementById("question");
        if (field) fitQuestion(field);
    },

    // Appelé par le gabarit à chaque navigation de Blazor : ferme le panneau et réapplique le thème choisi, au cas où
    // un rendu aurait remplacé l'attribut de <html>.
    closeMenu() {
        // Seulement un choix enregistré : sans stockage, un choix fait pour la page en cours doit survivre.
        const theme = savedTheme();
        if (theme !== "auto") document.documentElement.dataset.theme = theme;

        const dialog = document.getElementById("menu");
        if (dialog && dialog.open) dialog.close();
    },

    // Fil de questions (ADR-022) : place une question en haut de la zone qui défile (sous l'en-tête fixe, grâce à
    // scroll-margin), sans animation si le visiteur a demandé moins de mouvement.
    showQuestion(id) {
        const reduce = window.matchMedia("(prefers-reduced-motion: reduce)").matches;
        document.getElementById(id)?.scrollIntoView({ block: "start", behavior: reduce ? "auto" : "smooth" });
    },

    // Avis sur une réponse : le formulaire remplace le fil ; on remonte en haut, le focus sur son titre.
    showForm(id) {
        document.querySelector(".thread")?.scrollTo({ top: 0 });
        document.getElementById(id)?.focus({ preventScroll: true });
    },

    // Focus sur le titre d'une question sans déplacer le fil : la réponse est lue à partir de là.
    focusQuestion(id) {
        document.getElementById(id + "-titre")?.focus({ preventScroll: true });
    },
};
