// Deterministic QR assets from the application's existing local encoder. No network requests.
const fs = require('node:fs');
const path = require('node:path');
const vm = require('node:vm');
const zlib = require('node:zlib');
const batch = process.argv[2];
if (!/^[a-z0-9-]{1,24}$/.test(batch || '')) throw Error('Supply the preview batch name.');
const output = path.resolve('PrivateData', 'AdminPreview', batch);
const manifest = JSON.parse(fs.readFileSync(path.join(output, 'manifest.json'), 'utf8'));
const encoder = fs.readFileSync('MVC_QCUEventsManagement/wwwroot/lib/student/qrcode-1.0.0.min.js', 'utf8');
const drawing = 'this._oDrawing=new q(this._el,this._htOption)';
if (!encoder.includes(drawing)) throw Error('QR encoder changed; review the matrix adapter.');
const context = vm.createContext({ document: { documentElement: { tagName: 'html' } }, navigator: { userAgent: '' }, window: {} });
vm.runInContext(encoder.replace(drawing, 'this._oDrawing={draw:function(){},clear:function(){}}'), context);
const crcTable = Array.from({ length: 256 }, (_, i) => {
    let value = i;
    for (let n = 0; n < 8; n++) value = value & 1 ? 0xedb88320 ^ value >>> 1 : value >>> 1;
    return value >>> 0;
});
function chunk(type, data) {
    const name = Buffer.from(type), payload = Buffer.concat([name, data]);
    let crc = 0xffffffff;
    for (const value of payload) crc = crcTable[(crc ^ value) & 255] ^ crc >>> 8;
    const length = Buffer.alloc(4), checksum = Buffer.alloc(4);
    length.writeUInt32BE(data.length); checksum.writeUInt32BE((crc ^ 0xffffffff) >>> 0);
    return Buffer.concat([length, payload, checksum]);
}
function png(reference) {
    const qr = new context.QRCode({}, { text: reference, correctLevel: context.QRCode.CorrectLevel.M })._oQRCode;
    const modules = qr.getModuleCount(), scale = 16, quiet = 4, width = (modules + quiet * 2) * scale;
    const rows = Buffer.alloc((width * 3 + 1) * width, 255);
    for (let y = 0; y < width; y++) {
        rows[y * (width * 3 + 1)] = 0;
        for (let x = 0; x < width; x++) {
            const row = Math.floor(y / scale) - quiet, col = Math.floor(x / scale) - quiet;
            if (row >= 0 && col >= 0 && row < modules && col < modules && qr.isDark(row, col)) {
                const offset = y * (width * 3 + 1) + 1 + x * 3;
                rows[offset] = rows[offset + 1] = rows[offset + 2] = 0;
            }
        }
    }
    const header = Buffer.alloc(13); header.writeUInt32BE(width, 0); header.writeUInt32BE(width, 4); header[8] = 8; header[9] = 2;
    return Buffer.concat([Buffer.from([137, 80, 78, 71, 13, 10, 26, 10]), chunk('IHDR', header), chunk('IDAT', zlib.deflateSync(rows)), chunk('IEND', Buffer.alloc(0))]);
}
const escape = text => String(text).replace(/[&<>"']/g, char => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' }[char]));
for (const app of manifest.applications) {
    const folder = path.join(output, app.app); fs.mkdirSync(folder, { recursive: true });
    for (const pass of app.unscannedPasses) fs.writeFileSync(path.join(folder, pass.reference + '.png'), png(pass.reference));
    const scanner = app.unscannedPasses.length ? app.app === 'mvc'
        ? `${app.baseUrl}/admin/events/${app.unscannedPasses[0].eventId}/scanner`
        : `${app.baseUrl}/Frontend/Admin/AttendanceScanner.aspx?eventId=${app.unscannedPasses[0].eventId}` : '';
    const cards = app.unscannedPasses.map(pass => `<article><h2>${escape(pass.name)}</h2><p>${escape(pass.studentId)}</p><img src="${escape(pass.reference)}.png" alt="QR for ${escape(pass.reference)}"><strong>${escape(pass.reference)}</strong><p>Unscanned at export · Event ${pass.eventId}</p><p>Event ends ${escape(pass.validUntilPhilippineTime.replace('T', ' '))} Philippine time</p></article>`).join('\n');
    fs.writeFileSync(path.join(folder, 'qr-passes.html'), `<!doctype html><html lang="en"><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1"><title>${escape(app.app.toUpperCase())} unscanned preview passes</title><style>body{font:16px/1.5 sans-serif;color:#142c48;max-width:1000px;margin:32px auto;padding:0 20px}main{display:grid;grid-template-columns:repeat(auto-fit,minmax(270px,1fr));gap:24px}article{border:1px solid #d6dce2;padding:20px;break-inside:avoid}h2{font-size:18px;margin:0}p{margin:6px 0}img{display:block;width:100%;max-width:320px}strong{overflow-wrap:anywhere}@media print{a{display:none}body{margin:0}main{grid-template-columns:1fr 1fr}}</style><h1>${escape(app.app.toUpperCase())} · Unscanned preview passes</h1><p>Fictional test data. Use the matching application and selected event. Confirming check-in consumes that pass once.</p><p><a href="${escape(scanner)}">Open this event’s scanner</a></p><main>${cards}</main></html>`);
    console.log(`${app.app}: ${app.unscannedPasses.length} QR PNGs and printable sheet created.`);
}
