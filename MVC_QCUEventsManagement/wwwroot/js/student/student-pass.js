(() => {
    const canvas = document.getElementById('qrCanvas');
    const download = document.getElementById('btnDownloadPass');
    if (!canvas || !download) return;

    const reference = canvas.dataset.reference;
    const holder = document.createElement('div');
    const size = 380;
    const padding = 36;
    new QRCode(holder, {
        text: reference,
        width: size - padding * 2,
        height: size - padding * 2,
        colorDark: '#0b1229',
        colorLight: '#ffffff',
        correctLevel: QRCode.CorrectLevel.M
    });
    canvas.width = size;
    canvas.height = size;
    const context = canvas.getContext('2d');
    context.fillStyle = '#ffffff';
    context.fillRect(0, 0, size, size);
    context.drawImage(holder.querySelector('canvas'), padding, padding);

    download.addEventListener('click', async () => {
        download.disabled = true;
        const error = document.getElementById('passDownloadError');
        try {
            error.hidden = true;
            await document.fonts.ready;
            const link = document.createElement('a');
            link.download = `${reference}.png`;
            link.href = await htmlToImage.toPng(document.getElementById('boardingPassElement'), { pixelRatio: 2 });
            link.click();
        } catch {
            error.hidden = false;
        } finally {
            download.disabled = false;
        }
    });
})();
