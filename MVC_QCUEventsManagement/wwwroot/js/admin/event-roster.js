(() => {
    "use strict";
    const filterForm = document.getElementById("roster-filter-form");
    const filterMenu = document.getElementById("roster-filter-menu");
    if (filterForm && filterMenu) {
        const fields = [...filterMenu.querySelectorAll("select")];
        const applied = fields.map(field => field.value);
        const restore = () => fields.forEach((field, index) => { field.value = applied[index]; });
        const close = () => { filterMenu.open = false; restore(); };
        filterMenu.addEventListener("toggle", restore);
        document.addEventListener("pointerdown", event => { if (filterMenu.open && !filterMenu.contains(event.target)) close(); });
        document.addEventListener("keydown", event => {
            if (event.key === "Escape" && filterMenu.open) { event.preventDefault(); close(); filterMenu.querySelector("summary").focus(); }
        });
        filterForm.addEventListener("submit", event => { if (event.submitter?.id !== "roster-apply-filters") restore(); });
    }
    let opener;
    const open = dialog => { dialog.showModal(); document.documentElement.classList.add("overflow-hidden"); };
    document.querySelectorAll("[data-attendee-dialog]").forEach(button => button.addEventListener("click", () => {
        opener = button;
        open(document.getElementById(button.dataset.attendeeDialog));
    }));
    document.querySelectorAll("[data-roster-dialog]").forEach(dialog => {
        const profile = dialog.querySelector("[data-attendee-profile]");
        const confirmation = dialog.querySelector("[data-confirm-void]");
        dialog.querySelectorAll("[data-close-roster-dialog]").forEach(button => button.addEventListener("click", () => dialog.close()));
        dialog.querySelector("[data-prepare-void]")?.addEventListener("click", () => {
            profile.hidden = true; confirmation.hidden = false;
            dialog.querySelector("[data-keep-pass]").focus();
        });
        dialog.querySelector("[data-keep-pass]")?.addEventListener("click", () => {
            confirmation.hidden = true; profile.hidden = false;
            dialog.querySelector("[data-prepare-void]").focus();
        });
        dialog.addEventListener("close", () => {
            document.documentElement.classList.remove("overflow-hidden");
            profile.hidden = false; if (confirmation) confirmation.hidden = true;
            opener?.focus();
        });
        if (dialog.dataset.reopen === "true") {
            opener = document.querySelector(`[data-attendee-dialog="${dialog.id}"]`);
            open(dialog);
        }
    });
})();
