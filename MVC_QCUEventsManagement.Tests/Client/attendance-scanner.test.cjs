const { test } = require('node:test');
const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const vm = require('node:vm');

const source = fs.readFileSync(path.join(__dirname, '../../MVC_QCUEventsManagement/wwwroot/js/admin/attendance-scanner.js'), 'utf8');
const deferred = () => { let resolve, reject; const promise = new Promise((yes, no) => { resolve = yes; reject = no; }); return { promise, resolve, reject }; };
const flush = async () => { for (let i = 0; i < 4; i++) await new Promise(setImmediate); };
const attendee = { registrationId: 7, version: 'pass-version', eventVersion: 'event-version', name: 'Althea Navarro',
    ticketReference: 'TCK-0003-00007', studentId: 'QR-TEST', campus: 'San Bartolome', college: 'Computing', program: 'BSIT', year: 3, section: 'A' };
const pending = { state: 'ValidPending', canConfirm: true, attendee, message: 'Review attendee.' };

function fixture(options = {}) {
    const elements = new Map(), documentEvents = {}, windowEvents = {}, cameras = [], posts = [], tracks = [];
    function element(id = '') {
        return { id, value: '', textContent: '', hidden: false, disabled: false, dataset: {}, children: [], options: [], events: {},
            addEventListener(name, handler) { this.events[name] = handler; },
            setAttribute(name, value) { this[name] = value; },
            append(...nodes) { this.children.push(...nodes); if (id === 'camera-device') { this.options.push(...nodes); this.value ||= nodes[0].value; } },
            replaceChildren(...nodes) { this.children = nodes; if (id === 'camera-device') { this.options = [...nodes]; this.value = ''; } },
            reportValidity() { return !!get('query').value; }, focus() {}, querySelector() { return { value: 'csrf-token' }; } };
    }
    function get(id) { if (!elements.has(id)) elements.set(id, element(id)); return elements.get(id); }
    const root = get('root'); root.dataset.confirmUrl = '/confirm';
    root.querySelectorAll = () => tracks.map(track => ({ srcObject: { getTracks: () => [track] } }));
    get('lookup').action = '/lookup';
    const document = { hidden: false, getElementById: id => id === 'attendance-scanner' ? root : get(id.replace('scanner-', '')),
        createElement: () => element(), addEventListener: (event, handler) => { documentEvents[event] = handler; } };
    class Scanner {
        static async getCameras() { return options.enumerate ? options.enumerate() : [{ id: 'front', label: 'Front' }, { id: 'rear', label: 'Rear' }]; }
        constructor() { cameras.push(this); this.track = { stopped: false, stop() { this.stopped = true; } }; tracks.push(this.track); }
        async start(device, config, decode) {
            this.device = device; this.decode = decode;
            if (options.start) await options.start(this);
            this.isScanning = true;
        }
        async stop() { this.stopCalls = (this.stopCalls || 0) + 1; if (options.stop) await options.stop(this); this.isScanning = false; this.track.stop(); }
        clear() { this.cleared = true; }
    }
    const window = { isSecureContext: options.secure !== false, Html5Qrcode: Scanner,
        addEventListener: (event, handler) => { windowEvents[event] = handler; } };
    class FormData extends Map { constructor() { super([['__RequestVerificationToken', 'csrf-token']]); } }
    vm.runInNewContext(source, { document, window, Html5Qrcode: Scanner, Html5QrcodeSupportedFormats: { QR_CODE: 0 },
        AbortController, FormData, URLSearchParams, fetch: async (url, request) => {
            posts.push({ url, ...request });
            const body = options.respond ? await options.respond(url, request) : pending;
            return { ok: true, headers: { get: () => 'application/json' }, json: async () => body };
        } });
    return { get, cameras, posts, tracks, document,
        click: async id => { if (!get(id).disabled) await get(id).events.click?.(); },
        lookup: value => { get('query').value = value; get('lookup').events.submit({ preventDefault() {} }); },
        hide: () => { document.hidden = true; documentEvents.visibilitychange(); },
        pagehide: () => windowEvents.pagehide(),
        switch: async value => { get('camera-device').value = value; await get('camera-device').events.change(); } };
}

test('QR callbacks stage once, stop capture, and require an explicit confirmation', async () => {
    const ui = fixture();
    await ui.click('camera-toggle');
    const camera = ui.cameras[0];
    assert.equal(camera.device, 'rear');
    camera.decode(attendee.ticketReference); camera.decode(attendee.ticketReference);
    await flush();
    assert.equal(ui.posts.length, 1);
    assert.equal(ui.posts[0].url, '/lookup');
    assert.equal(ui.posts[0].body.get('query'), attendee.ticketReference);
    assert.equal(camera.track.stopped, true);
    assert.equal(ui.get('confirm').disabled, false);
    await ui.click('confirm');
    assert.equal(ui.posts[1].url, '/confirm');
    assert.equal(ui.posts[1].body.get('RegistrationId'), '7');
    assert.equal(ui.posts[1].body.get('Confirmed'), 'true');
});

test('a cleared lookup cannot restart the camera until its shutdown completes', async () => {
    const stopping = deferred();
    const ui = fixture({ stop: () => stopping.promise });
    await ui.click('camera-toggle');
    ui.cameras[0].decode(attendee.ticketReference);
    await flush();
    await ui.click('clear');
    assert.equal(ui.get('camera-toggle').disabled, true);
    stopping.resolve(); await flush();
    assert.equal(ui.get('camera-toggle').disabled, false);
});

test('clearing during camera shutdown prevents the obsolete lookup from being sent', async () => {
    const stopping = deferred();
    const ui = fixture({ stop: () => stopping.promise });
    await ui.click('camera-toggle');
    ui.cameras[0].decode(attendee.ticketReference);
    await flush(); await ui.click('clear');
    stopping.resolve(); await flush();
    assert.equal(ui.posts.length, 0);
    assert.equal(ui.get('confirm').disabled, true);
});

test('overlapping manual lookups during camera shutdown send only the latest query', async () => {
    const stopping = deferred();
    const ui = fixture({ stop: () => stopping.promise });
    await ui.click('camera-toggle');
    ui.lookup('old'); await flush(); ui.lookup('current');
    stopping.resolve(); await flush();
    assert.equal(ui.posts.length, 1);
    assert.equal(ui.posts[0].body.get('query'), 'current');
    assert.equal(ui.posts[0].signal.aborted, false);
});

test('a hidden page cancels pending startup and releases every camera track', async () => {
    const starting = deferred();
    const ui = fixture({ start: () => starting.promise });
    const started = ui.click('camera-toggle'); await flush();
    ui.hide(); starting.resolve(); await started; await flush();
    assert.equal(ui.tracks.every(track => track.stopped), true);
    assert.equal(ui.get('camera-toggle')['aria-pressed'], 'false');
    assert.equal(ui.posts.length, 0);
});

test('switching cameras while the page hides cannot leave a new capture running', async () => {
    const stopping = deferred();
    const ui = fixture({ stop: () => stopping.promise });
    await ui.click('camera-toggle');
    const switched = ui.switch('front'); await flush();
    ui.hide(); stopping.resolve(); await switched; await flush();
    assert.equal(ui.tracks.every(track => track.stopped), true);
    assert.equal(ui.get('camera-toggle')['aria-pressed'], 'false');
});

test('camera permission denial preserves manual lookup and allows a fresh camera retry', async () => {
    let attempts = 0;
    const ui = fixture({ enumerate: async () => { if (++attempts === 1) throw new Error('NotAllowedError'); return [{ id: 'usb', label: 'USB webcam' }]; } });
    await ui.click('camera-toggle');
    assert.match(ui.get('camera-message').textContent, /permission was denied/);
    ui.lookup('QR-TEST'); await flush();
    assert.equal(ui.posts[0].url, '/lookup');
    await ui.click('camera-toggle');
    assert.equal(ui.cameras[0].device, 'usb');
    assert.equal(ui.get('confirm').disabled, true);
});

test('a failed camera stop still releases tracks and permits a clean restart', async () => {
    const ui = fixture({ stop: async () => { throw new Error('device disconnected'); } });
    await ui.click('camera-toggle'); await ui.click('camera-toggle');
    assert.equal(ui.tracks.every(track => track.stopped), true);
    await ui.click('camera-toggle');
    assert.equal(ui.cameras.length, 2);
    assert.equal(ui.get('camera-toggle')['aria-pressed'], 'true');
});

test('switching cameras releases the old device before starting the selected device', async () => {
    const ui = fixture(); await ui.click('camera-toggle'); await ui.switch('front');
    assert.equal(ui.cameras.length, 2);
    assert.equal(ui.cameras[0].track.stopped, true);
    assert.equal(ui.cameras[1].device, 'front');
    assert.equal(ui.cameras[1].track.stopped, false);
    ui.cameras[0].decode('obsolete-camera-result'); await flush();
    assert.equal(ui.posts.length, 0);
    ui.cameras[1].decode(attendee.ticketReference); await flush();
    assert.equal(ui.posts.length, 1);
});

test('a failed camera start releases a partially opened device and can be retried', async () => {
    let attempts = 0;
    const ui = fixture({ start: async () => { if (++attempts === 1) throw new Error('device unavailable'); } });
    await ui.click('camera-toggle');
    assert.equal(ui.tracks[0].stopped, true);
    assert.equal(ui.get('camera-toggle').disabled, false);
    await ui.click('camera-toggle');
    assert.equal(ui.cameras.length, 2);
    assert.equal(ui.get('camera-toggle')['aria-pressed'], 'true');
});

test('an old HTTP lookup cannot replace the currently reviewed QR attendee', async () => {
    const old = deferred();
    const ui = fixture({ respond: (url, request) => request.body.get('query') === 'old' ? old.promise : pending });
    ui.lookup('old'); await flush();
    ui.lookup(attendee.ticketReference); await flush();
    assert.equal(ui.posts[0].signal.aborted, true);
    old.resolve({ ...pending, attendee: { ...attendee, name: 'Obsolete attendee' } }); await flush();
    assert.equal(ui.get('name').textContent, attendee.name);
    await ui.click('confirm');
    assert.equal(ui.posts[2].body.get('RegistrationId'), '7');
});

test('wrong-event and duplicate camera lookup results cannot enable confirmation', async () => {
    for (const state of ['WrongEventWarning', 'DuplicateWarning', 'CancelledWarning']) {
        const ui = fixture({ respond: () => ({ ...pending, state, canConfirm: false }) });
        await ui.click('camera-toggle'); ui.cameras[0].decode(attendee.ticketReference); await flush();
        assert.equal(ui.get('confirm').disabled, true);
        await ui.click('confirm'); assert.equal(ui.posts.length, 1);
    }
});

test('insecure origins and missing devices leave manual lookup available', async () => {
    for (const options of [{ secure: false }, { enumerate: async () => [] }]) {
        const ui = fixture(options); await ui.click('camera-toggle');
        assert.equal(ui.cameras.length, 0);
        assert.equal(ui.get('query').disabled, false);
        ui.lookup('QR-TEST'); await flush(); assert.equal(ui.posts.length, 1);
    }
});
