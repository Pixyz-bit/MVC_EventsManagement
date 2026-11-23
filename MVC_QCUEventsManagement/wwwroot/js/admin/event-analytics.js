(() => {
    'use strict';
    const root = document.getElementById('event-analytics');
    if (!root) return;

    const svg = root.querySelector('[data-arrival-chart]');
    if (svg) {
        const points = [...svg.querySelectorAll('[data-arrival-point]')];
        const readout = root.querySelector('[data-arrival-readout]');
        const resize = () => {
            const width = Math.max(40, svg.getBoundingClientRect().width);
            const max = Math.max(1, Number(svg.dataset.max));
            svg.setAttribute('viewBox', `0 0 ${width} 200`);
            const coordinates = points.map((point, index) => {
                const x = points.length === 1 ? width / 2 : 12 + index * (width - 24) / (points.length - 1);
                const y = 188 - 176 * Number(point.dataset.count) / max;
                point.setAttribute('cx', x);
                point.setAttribute('cy', y);
                return `${x},${y}`;
            });
            svg.querySelector('[data-arrival-line]').setAttribute('points', coordinates.join(' '));
            svg.querySelectorAll('line').forEach(line => line.setAttribute('x2', width - 12));
        };
        points.forEach(point => {
            const show = () => { readout.textContent = point.dataset.chartLabel; };
            point.addEventListener('pointerenter', show);
            point.addEventListener('focus', show);
            point.addEventListener('click', show);
        });
        resize();
        if (globalThis.ResizeObserver) new ResizeObserver(resize).observe(svg);
    }

    const selector = root.querySelector('[data-demographic-select]');
    const panels = [...root.querySelectorAll('[data-demographic-panel]')];
    if (!selector) return;
    const synchronize = () => {
        const selected = selector.value;
        panels.forEach(panel => { panel.hidden = panel.dataset.demographicPanel !== selected; });
        root.querySelectorAll('input[name="dimension"]').forEach(input => { input.value = selected; });
        // Keep the selected dimension when paging, changing cohorts or resetting filters.
        root.querySelectorAll('a[href]').forEach(link => {
            const url = new URL(link.href, location.href);
            if (url.origin === location.origin && url.pathname === location.pathname) {
                url.searchParams.set('dimension', selected);
                link.href = url.pathname + url.search + url.hash;
            }
        });
    };
    selector.addEventListener('change', synchronize);
    synchronize();
    root.querySelector('[data-demographic-apply]').hidden = true;
    root.querySelector('[data-demographic-form]').addEventListener('submit', event => { event.preventDefault(); synchronize(); });
    panels.forEach(panel => {
        const slices = [...panel.querySelectorAll('[data-demographic-slice]')];
        const readout = panel.querySelector('[data-demographic-readout]');
        panel.querySelectorAll('[data-chart-label]').forEach(target => {
            const show = () => {
                readout.textContent = target.dataset.chartLabel;
                const selected = target.dataset.demographicSlice ?? target.dataset.demographicLegend;
                slices.forEach(slice => { slice.classList.toggle('is-muted', slice.dataset.demographicSlice !== selected); });
            };
            const clear = () => { slices.forEach(slice => slice.classList.remove('is-muted')); };
            target.addEventListener('pointerenter', show);
            target.addEventListener('focus', show);
            target.addEventListener('click', show);
            target.addEventListener('pointerleave', clear);
            target.addEventListener('blur', clear);
        });
    });
})();
