(() => {
    "use strict";
    const cancellation = document.getElementById("cancel-event");
    const trigger = document.getElementById("open-cancel-event");
    if (cancellation && trigger) {
        const open = () => { cancellation.showModal(); document.documentElement.classList.add("overflow-hidden"); };
        trigger.addEventListener("click", open);
        cancellation.querySelectorAll("[data-close-cancellation]").forEach(button => button.addEventListener("click", () => cancellation.close()));
        cancellation.addEventListener("close", () => { document.documentElement.classList.remove("overflow-hidden"); trigger.focus(); });
        if (cancellation.dataset.reopen === "true") open();
    }
    const form = document.getElementById("event-edit-form");
    if (!form) return;
    const banner = document.getElementById("Input_Banner");
    const preview = document.getElementById("edit-banner-preview");
    let previewUrl;
    banner.addEventListener("change", () => {
        if (previewUrl) URL.revokeObjectURL(previewUrl);
        preview.classList.add("hidden"); preview.removeAttribute("src"); banner.setCustomValidity("");
        const file = banner.files[0]; if (!file) return;
        if (file.size > 5 * 1024 * 1024 || file.size === 0 || !["image/png", "image/jpeg", "image/webp"].includes(file.type)) {
            banner.setCustomValidity("Choose a PNG, JPG or WEBP image up to 5 MB."); banner.reportValidity(); return;
        }
        previewUrl = URL.createObjectURL(file); preview.src = previewUrl; preview.classList.remove("hidden");
    });
    const list = document.getElementById("edit-sponsors");
    const sync = () => [...list.querySelectorAll("input")].forEach((input, i) => { input.name = `Input.Sponsors[${i}]`; input.setAttribute("aria-label", `Sponsor ${i + 1}`); });
    list.addEventListener("click", event => { if (event.target.matches("[data-remove-sponsor]")) { event.target.parentElement.remove(); sync(); } });
    document.getElementById("edit-add-sponsor").addEventListener("click", () => {
        if (list.children.length >= 20) return;
        const row = document.createElement("div"); row.className = "flex gap-2";
        const input = document.createElement("input"); input.className = "event-input"; input.required = true; input.maxLength = 150;
        const remove = document.createElement("button"); remove.type = "button"; remove.className = "event-secondary"; remove.dataset.removeSponsor = ""; remove.textContent = "Remove";
        row.append(input, remove); list.append(row); sync(); input.focus();
    });
    const college = document.getElementById("Input_TargetDepartment");
    college.addEventListener("change", () => form.querySelectorAll("[data-program-college]").forEach(label => {
        const excluded = !!college.value && label.dataset.programCollege !== college.value;
        label.hidden = excluded; const input = label.querySelector("input"); input.disabled = excluded; if (excluded) input.checked = false;
    }));
})();
