// Panneau du menu (ADR-022) : <dialog> modal natif, qui piège le focus, se ferme par Échap et rend le focus au
// bouton qui l'a ouvert. Reste ici ce que le navigateur ne fait pas seul, en écoute déléguée sur le document pour
// marcher avant la connexion de Blazor (page pré-rendue) et si elle est coupée : ouvrir, fermer par le bouton, par
// un clic sur le fond (le contenu remplit le <dialog> : un clic qui vise le <dialog> lui-même vient du fond) ou en
// suivant un lien du panneau, y compris vers la page déjà affichée.
document.addEventListener("click", (event) => {
    const opener = event.target.closest("[data-menu-open]");
    if (opener) {
        const dialog = document.getElementById(opener.getAttribute("aria-controls"));
        if (dialog && !dialog.open) dialog.showModal();
        return;
    }

    const dialog = event.target.closest("dialog.menu-panel");
    if (dialog && (event.target === dialog || event.target.closest("[data-menu-close], a[href]"))) dialog.close();
});

window.limpide = {
    // Appelé par le gabarit à chaque navigation de Blazor.
    closeMenu() {
        const dialog = document.getElementById("menu");
        if (dialog && dialog.open) dialog.close();
    },
};
