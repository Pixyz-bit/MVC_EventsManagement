(() => {
    "use strict";
    const root = document.getElementById("attendance-scanner");
    if (!root) return;
    const byId = id => document.getElementById(`scanner-${id}`);
    const form = byId("lookup"), query = byId("query"), confirm = byId("confirm"), clear = byId("clear");
    const cameraButton = byId("camera-toggle"), devices = byId("camera-device");
    let selected = null, epoch = 0, request = null, committing = false, looking = false;
    let scanner = null, active = false, cameraBusy = false, cameraEpoch = 0, cameraStopping = 0;
    let cameraQueue = Promise.resolve();
    const controls = () => {
        confirm.disabled = !selected || committing || looking;
        query.disabled = committing;
        byId("lookup-submit").disabled = committing;
        clear.disabled = committing;
        cameraButton.disabled = cameraBusy || cameraStopping > 0 || committing || looking;
        devices.disabled = cameraBusy || cameraStopping > 0 || committing || looking;
    };
    function notice(message, tone = "idle", state = "Awaiting lookup") {
        byId("notice").textContent = message; byId("notice").dataset.tone = tone; byId("state").textContent = state;
    }
    function profile(attendee) {
        byId("profile").hidden = !attendee; byId("empty").hidden = !!attendee;
        const fields = byId("fields"); fields.replaceChildren();
        if (!attendee) return;
        byId("name").textContent = attendee.name; byId("ticket").textContent = attendee.ticketReference;
        const data = [["Student ID", attendee.studentId], ["Campus", attendee.campus], ["College", attendee.college], ["Program", attendee.program], ["Year & section", `Year ${attendee.year} · ${attendee.section}`]];
        if (attendee.checkInDisplay) data.push(["Checked in (Philippine time)", attendee.checkInDisplay]);
        for (const [label, value] of data) {
            const group = document.createElement("div"), term = document.createElement("dt"), detail = document.createElement("dd");
            group.className = "min-w-0"; term.className = "mb-1 text-xs text-campus-muted"; detail.className = "font-semibold wrap-break-word";
            term.textContent = label; detail.textContent = value; group.append(term, detail); fields.append(group);
        }
    }
    async function post(url, data, signal) {
        const response = await fetch(url, { method: "POST", body: data, credentials: "same-origin", redirect: "error", signal,
            headers: { Accept: "application/json", "X-Requested-With": "XMLHttpRequest" } });
        if (!response.headers.get("content-type")?.includes("application/json")) throw new Error("Your session may have expired. Reload the page and sign in again.");
        const result = await response.json();
        if (!response.ok) throw new Error(result.message || "The request could not be completed. Reload and try again.");
        return result;
    }
    function result(data) {
        selected = data.canConfirm ? data.attendee : null;
        profile(data.attendee);
        const tone = data.state === "CheckedIn" ? "success" : data.canConfirm ? "pending" : ["DuplicateWarning", "Changed"].includes(data.state) ? "warning" : "error";
        notice(data.message, tone, data.canConfirm ? "Ready for review" : data.state === "CheckedIn" ? "Checked in" : "Check-in blocked");
        controls();
    }
    function cameraOperation(operation) {
        const pending = cameraQueue.then(operation);
        cameraQueue = pending.catch(() => {});
        return pending;
    }
    async function releaseCamera() {
        const current = scanner;
        scanner = null;
        const tracks = [...root.querySelectorAll("video")].flatMap(video => video.srcObject?.getTracks() || []);
        try {
            if (current?.isScanning) await current.stop();
            current?.clear();
        } catch {
            // A disconnected device must not prevent another camera attempt.
        } finally {
            tracks.forEach(track => track.stop());
            byId("reader").replaceChildren();
        }
    }
    function stopCamera(message = "Camera off") {
        ++cameraEpoch;
        active = false;
        cameraButton.textContent = "Start camera"; cameraButton.setAttribute("aria-pressed", "false");
        byId("camera-placeholder").hidden = false; byId("camera-title").textContent = message;
        byId("camera-message").textContent = "Start the camera to scan a student’s QR pass.";
        ++cameraStopping; controls();
        return cameraOperation(releaseCamera).finally(() => { --cameraStopping; controls(); });
    }
    async function lookup(value) {
        if (committing) return;
        const generation = ++epoch;
        request?.abort(); const lookupRequest = request = new AbortController();
        selected = null; looking = true; profile(null); controls();
        notice("Looking up attendee…", "idle", "Looking up");
        await stopCamera("Paused for attendee review");
        if (generation !== epoch) return;
        const data = new FormData(form); data.set("query", value);
        try { const found = await post(form.action, data, lookupRequest.signal); if (generation === epoch) result(found); }
        catch (error) { if (generation === epoch && error.name !== "AbortError") notice(error.message || "Lookup failed. Reload and try again.", "error", "Lookup unavailable"); }
        finally { if (generation === epoch) { looking = false; controls(); } }
    }
    form.addEventListener("submit", event => { event.preventDefault(); if (form.reportValidity()) void lookup(query.value.trim()); });
    clear.addEventListener("click", () => {
        if (committing) return;
        ++epoch; request?.abort(); selected = null; looking = false; query.value = ""; profile(null);
        notice("Scan a QR pass or look up an attendee to review their details."); controls(); query.focus();
    });
    confirm.addEventListener("click", async () => {
        if (!selected || committing || looking) return;
        const attendee = selected; selected = null; committing = true; controls();
        notice("Confirming check-in…", "idle", "Confirming");
        const data = new URLSearchParams({ RegistrationId: String(attendee.registrationId), Version: attendee.version,
            EventVersion: attendee.eventVersion, Confirmed: "true", __RequestVerificationToken: form.querySelector('[name="__RequestVerificationToken"]').value });
        try { result(await post(root.dataset.confirmUrl, data)); }
        catch (error) { notice(error.message || "Check-in could not be confirmed. Look up the pass again.", "error", "Confirmation unavailable"); }
        finally { committing = false; controls(); }
    });
    function startCamera(deviceId, generation) {
        if (!window.isSecureContext) throw new Error("Camera access requires HTTPS or localhost. Use manual lookup instead.");
        if (!window.Html5Qrcode) throw new Error("The QR scanner could not load. Use manual lookup instead.");
        return cameraOperation(async () => {
            if (generation !== cameraEpoch || document.hidden) return;
            scanner = new Html5Qrcode("scanner-reader", { formatsToSupport: [Html5QrcodeSupportedFormats.QR_CODE], verbose: false });
            await scanner.start(deviceId, { fps: 8, qrbox: (width, height) => { const side = Math.floor(Math.min(width, height) * 0.7); return { width: side, height: side }; } }, value => {
                if (generation === cameraEpoch && !document.hidden && !looking && !committing && active) { query.value = value.slice(0, 150); void lookup(value); }
            }, () => {});
            if (generation !== cameraEpoch || document.hidden) { await releaseCamera(); return; }
            active = true; byId("camera-placeholder").hidden = true; cameraButton.textContent = "Stop camera"; cameraButton.setAttribute("aria-pressed", "true");
        });
    }
    cameraButton.addEventListener("click", async () => {
        if (cameraBusy || cameraStopping > 0 || committing || looking) return;
        cameraBusy = true; controls();
        try {
            if (active) { await stopCamera(); return; }
            const generation = cameraEpoch;
            if (!window.isSecureContext) throw new Error("Camera access requires HTTPS or localhost. Use manual lookup instead.");
            selected = null; profile(null); controls(); notice("Scan a QR pass to review an attendee.");
            if (!window.Html5Qrcode) throw new Error("The QR scanner could not load. Use manual lookup instead.");
            const cameras = await Html5Qrcode.getCameras();
            if (generation !== cameraEpoch || document.hidden) return;
            if (!cameras.length) throw new Error("No camera was found. Use manual lookup instead.");
            const previous = devices.value;
            devices.replaceChildren();
            for (const [index, camera] of cameras.entries()) { const option = document.createElement("option"); option.value = camera.id; option.textContent = camera.label || `Camera ${index + 1}`; devices.append(option); }
            const preferred = cameras.find(camera => camera.id === previous) || cameras.find(camera => /back|rear|environment/i.test(camera.label)) || cameras[0];
            devices.value = preferred.id;
            byId("camera-settings").hidden = devices.options.length < 2;
            if (generation !== cameraEpoch) return;
            await startCamera(devices.value, generation);
        } catch (error) {
            await stopCamera("Camera unavailable");
            byId("camera-message").textContent = error.message?.startsWith("The QR") || error.message?.startsWith("No camera") || error.message?.startsWith("Camera access requires") ? error.message : "Camera access is unavailable or permission was denied. Use manual lookup instead.";
        } finally { cameraBusy = false; controls(); }
    });
    devices.addEventListener("change", async () => {
        if (!active || cameraBusy || cameraStopping > 0 || committing || looking) return;
        cameraBusy = true; controls();
        const stopping = stopCamera(), generation = cameraEpoch;
        try { await stopping; await startCamera(devices.value, generation); }
        catch { await stopCamera("Camera unavailable"); byId("camera-message").textContent = "Could not switch cameras. Use manual lookup instead."; }
        finally { cameraBusy = false; controls(); }
    });
    document.addEventListener("visibilitychange", () => { if (document.hidden) void stopCamera(); });
    window.addEventListener("pagehide", () => {
        request?.abort();
        root.querySelectorAll("video").forEach(video => video.srcObject?.getTracks().forEach(track => track.stop()));
        void stopCamera();
    });
    controls();
})();
