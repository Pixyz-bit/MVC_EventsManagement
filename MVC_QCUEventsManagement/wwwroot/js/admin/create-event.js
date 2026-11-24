(() => {
    "use strict";
    const form = document.getElementById("create-event-form");
    if (!form) return;
    const tabs = [...form.querySelectorAll("[data-step]")];
    const panels = [...form.querySelectorAll("[data-panel]")];
    const next = document.getElementById("event-next");
    const back = document.getElementById("event-back");
    const publish = document.getElementById("event-publish");
    const body = document.getElementById("event-editor-body");
    const field = name => document.getElementById(name) ?? form.elements.namedItem(name);
    const labels = tabs.map(tab => tab.querySelector(".event-step-name").textContent);
    let current = 0;
    let dirty = false;
    let submitting = false;
    let previewUrl;

    function show(index, focus = false) {
        current = index;
        panels.forEach((panel, i) => { panel.hidden = i !== index; });
        tabs.forEach((tab, i) => {
            tab.setAttribute("aria-selected", String(i === index));
            tab.tabIndex = i === index ? 0 : -1;
            tab.dataset.complete = String(i < index);
        });
        back.classList.toggle("hidden", index === 0);
        next.classList.toggle("hidden", index === 4);
        publish.classList.toggle("hidden", index !== 4);
        next.querySelector("span").textContent = window.matchMedia("(max-width: 639px)").matches ? "Next" : labels[Math.min(index + 1, 4)];
        document.getElementById("event-current-step").textContent = `${index + 1} of 5 · ${labels[index]}`;
        if (index === 4) updateSummary();
        body.scrollTop = 0;
        if (focus) tabs[index].focus();
    }

    function checkSchedule() {
        ["EventDate", "StartTime", "EndTime", "RegStart", "RegEnd"].forEach(name => field(name).setCustomValidity(""));
        const date = field("EventDate").value;
        const start = field("StartTime").value;
        const end = field("EndTime").value;
        const open = field("RegStart").value;
        const close = field("RegEnd").value;
        if (start && end && start >= end) field("EndTime").setCustomValidity("End time must be after start time on the same day.");
        if (open && close && open > close) field("RegEnd").setCustomValidity("The registration deadline must be on or after the opening date.");
        for (const name of ["RegStart", "RegEnd"]) {
            if (date && field(name).value && field(name).value.slice(0, 10) >= date)
                field(name).setCustomValidity("Registration dates must be before the event day.");
        }
    }

    function validate(index) {
        checkSchedule();
        if (index === 3 && document.getElementById("sponsor-name").value.trim() && !addSponsor(document.getElementById("sponsor-name").value)) return false;
        const invalid = [...panels[index].querySelectorAll("input, textarea, select")].find(input => !input.checkValidity());
        if (!invalid) return true;
        show(index);
        invalid.focus();
        invalid.reportValidity();
        return false;
    }

    function go(index) {
        if (index > current) {
            for (let i = 0; i < index; i++) if (!validate(i)) return;
        }
        show(index, true);
    }
    tabs.forEach((tab, i) => {
        tab.addEventListener("click", () => go(i));
        tab.addEventListener("keydown", event => {
            let target;
            if (event.key === "ArrowRight") target = Math.min(4, i + 1);
            if (event.key === "ArrowLeft") target = Math.max(0, i - 1);
            if (event.key === "Home") target = 0;
            if (event.key === "End") target = 4;
            if (target !== undefined) { event.preventDefault(); go(target); }
        });
    });
    next.addEventListener("click", () => go(current + 1));
    back.addEventListener("click", () => go(current - 1));

    const sponsorList = document.getElementById("sponsor-list");
    const sponsorName = document.getElementById("sponsor-name");
    const sponsorFeedback = document.getElementById("sponsor-feedback");
    function syncSponsors() {
        [...sponsorList.querySelectorAll("input")].forEach((input, i) => { input.name = `Sponsors[${i}]`; });
        document.getElementById("no-sponsors").hidden = sponsorList.children.length > 0;
    }
    function addSponsor(value) {
        const name = value.trim();
        sponsorFeedback.textContent = "";
        if (!name) { sponsorFeedback.textContent = "Enter a sponsor name first."; sponsorName.focus(); return false; }
        const existing = [...sponsorList.querySelectorAll("input")];
        if (existing.some(input => input.value.toLocaleLowerCase() === name.toLocaleLowerCase())) {
            sponsorFeedback.textContent = "This sponsor has already been added.";
            return false;
        }
        if (name.length > 150 || existing.length >= 20) { sponsorFeedback.textContent = "Add up to 20 sponsors, each up to 150 characters."; return false; }
        const li = document.createElement("li");
        li.className = "event-sponsor";
        const span = document.createElement("span");
        span.textContent = name;
        const input = document.createElement("input");
        input.type = "hidden";
        input.value = name;
        const remove = document.createElement("button");
        remove.type = "button";
        remove.className = "event-sponsor-remove";
        remove.setAttribute("aria-label", `Remove ${name}`);
        remove.textContent = "×";
        li.append(span, input, remove);
        sponsorList.append(li);
        sponsorName.value = "";
        field("Confirm").checked = false;
        dirty = true;
        syncSponsors();
        return true;
    }
    document.getElementById("add-sponsor").addEventListener("click", () => addSponsor(sponsorName.value));
    sponsorName.addEventListener("keydown", event => { if (event.key === "Enter") { event.preventDefault(); addSponsor(sponsorName.value); } });
    form.querySelectorAll("[data-sponsor]").forEach(button => button.addEventListener("click", () => addSponsor(button.dataset.sponsor)));
    sponsorList.addEventListener("click", event => {
        const remove = event.target.closest("button");
        if (!remove) return;
        remove.closest("li").remove();
        syncSponsors();
        dirty = true;
        field("Confirm").checked = false;
        sponsorName.focus();
    });
    syncSponsors();

    const department = field("TargetDepartment");
    function filterPrograms() {
        form.querySelectorAll(".event-program").forEach(label => {
            const hidden = !!department.value && label.dataset.department !== department.value;
            label.hidden = hidden;
            const input = label.querySelector("input");
            if (hidden) input.checked = false;
            input.disabled = hidden;
        });
    }
    department.addEventListener("change", filterPrograms);
    filterPrograms();

    const banner = field("Banner");
    const preview = document.getElementById("banner-preview");
    const removeBanner = document.getElementById("remove-banner");
    function clearBanner() {
        if (previewUrl) URL.revokeObjectURL(previewUrl);
        previewUrl = undefined;
        preview.removeAttribute("src");
        preview.classList.add("hidden");
        document.getElementById("banner-placeholder").classList.remove("hidden");
        document.getElementById("banner-file-name").textContent = "";
        removeBanner.classList.add("hidden");
        document.getElementById("banner-error").textContent = "";
        banner.setCustomValidity("");
    }
    banner.addEventListener("change", () => {
        clearBanner();
        const file = banner.files[0];
        if (!file) return;
        if (!/\.(png|jpe?g|webp)$/i.test(file.name) || !["image/png", "image/jpeg", "image/webp"].includes(file.type) || file.size > 5 * 1024 * 1024 || file.size === 0) {
            banner.setCustomValidity("Choose a PNG, JPG or WEBP image up to 5 MB.");
            document.getElementById("banner-error").textContent = banner.validationMessage;
            removeBanner.classList.remove("hidden");
            return;
        }
        previewUrl = URL.createObjectURL(file);
        preview.src = previewUrl;
        preview.classList.remove("hidden");
        document.getElementById("banner-placeholder").classList.add("hidden");
        document.getElementById("banner-file-name").textContent = file.name;
        removeBanner.classList.remove("hidden");
    });
    preview.addEventListener("error", () => {
        if (!previewUrl) return;
        banner.setCustomValidity("The image could not be read. Choose another file.");
        document.getElementById("banner-error").textContent = banner.validationMessage;
    });
    removeBanner.addEventListener("click", () => { banner.value = ""; clearBanner(); dirty = true; field("Confirm").checked = false; banner.focus(); });

    function updateSummary() {
        const text = name => field(name).value.trim();
        const programs = [...form.querySelectorAll("input[name='Programs']:checked")].map(input => input.value);
        const sponsors = [...sponsorList.querySelectorAll("input")].map(input => input.value);
        const date = value => {
            if (!value) return "Not set";
            const [year, month, day] = value.split("-").map(Number);
            return new Intl.DateTimeFormat("en-PH", { month: "long", day: "numeric", year: "numeric" }).format(new Date(year, month - 1, day, 12));
        };
        const time = value => {
            if (!value) return "Not set";
            const [hour, minute] = value.split(":").map(Number);
            return new Intl.DateTimeFormat("en-PH", { hour: "numeric", minute: "2-digit", hour12: true }).format(new Date(2000, 0, 1, hour, minute));
        };
        const rows = (id, entries) => {
            const list = document.getElementById(id);
            list.replaceChildren();
            for (const [label, value] of entries) {
                const row = document.createElement("div"), term = document.createElement("dt"), detail = document.createElement("dd");
                term.textContent = label; detail.textContent = value; row.append(term, detail); list.append(row);
            }
        };
        document.getElementById("review-title").textContent = text("Title");
        document.getElementById("review-description").textContent = text("Description") || "No description";
        rows("review-venue", [["Location", text("VenueLocation")], ["Maximum capacity", `${Number(text("MaxCapacity")).toLocaleString("en-PH")} seats`]]);
        rows("review-schedule", [["Event date", date(text("EventDate"))], ["Philippine time", `${time(text("StartTime"))} – ${time(text("EndTime"))}`]]);
        rows("review-registration", [["Opens", date(text("RegStart"))], ["Deadline (inclusive)", date(text("RegEnd"))]]);
        rows("review-audience", [["Campus", text("TargetBranch") || "All campuses"], ["College", text("TargetDepartment") || "All colleges"],
            ["Year level", text("TargetYearLevel") ? `Year ${text("TargetYearLevel")}` : "All year levels"], ["Programs", programs.length ? programs.join(", ") : "All programs"]]);
        const sponsorSummary = document.getElementById("review-sponsors");
        sponsorSummary.replaceChildren();
        for (const name of sponsors.length ? sponsors : ["No sponsors"]) { const item = document.createElement("li"); item.textContent = name; sponsorSummary.append(item); }
        const image = document.getElementById("review-banner");
        image.hidden = !previewUrl;
        if (previewUrl) image.src = previewUrl; else image.removeAttribute("src");
        document.getElementById("review-banner-empty").hidden = !!previewUrl;
        document.getElementById("review-banner-name").textContent = banner.files[0]?.name || "";
    }

    form.addEventListener("input", event => {
        dirty = true;
        if (event.target.name !== "Confirm") field("Confirm").checked = false;
        checkSchedule();
    });
    form.addEventListener("submit", event => {
        if (submitting) { event.preventDefault(); return; }
        if (current !== 4) { event.preventDefault(); go(current + 1); return; }
        for (let i = 0; i < panels.length; i++) {
            if (!validate(i)) { event.preventDefault(); return; }
        }
        submitting = true;
        dirty = false;
        publish.disabled = true;
        publish.textContent = "Publishing…";
    });
    document.getElementById("discard-event").addEventListener("click", event => {
        if (dirty && !window.confirm("Discard this event draft? Your changes will not be saved.")) event.preventDefault();
        else dirty = false;
    });
    window.addEventListener("beforeunload", event => { if (dirty) { event.preventDefault(); event.returnValue = ""; } });
    window.addEventListener("resize", () => { next.querySelector("span").textContent = window.matchMedia("(max-width: 639px)").matches ? "Next" : labels[Math.min(current + 1, 4)]; });
    const errorField = form.querySelector(".input-validation-error");
    const initial = errorField?.closest("[data-panel]");
    show(initial ? Number(initial.dataset.panel) : 0);
    document.getElementById("event-errors")?.focus();
})();
