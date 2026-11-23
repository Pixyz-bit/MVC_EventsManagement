(() => {
    'use strict';
    const root = document.getElementById('event-analytics');
    if (!root) return;

    const tooltip = document.createElement('div');
    tooltip.id = 'analytics-chart-tooltip';
    tooltip.className = 'analytics-chart-tooltip';
    tooltip.setAttribute('role', 'tooltip');
    tooltip.hidden = true;
    root.append(tooltip);
    let activeTarget = null;
    const hideTooltip = () => {
        tooltip.hidden = true;
        if (activeTarget) activeTarget.removeAttribute('aria-describedby');
        activeTarget = null;
    };
    const positionTooltip = (target, event) => {
        const bounds = target.getBoundingClientRect();
        const x = Number.isFinite(event?.clientX) ? event.clientX : bounds.left + bounds.width / 2;
        const y = Number.isFinite(event?.clientY) ? event.clientY : bounds.bottom;
        const size = tooltip.getBoundingClientRect();
        tooltip.style.left = `${Math.max(8, Math.min(x + 12, window.innerWidth - size.width - 8))}px`;
        tooltip.style.top = `${Math.max(8, Math.min(y + 12, window.innerHeight - size.height - 8))}px`;
    };
    root.querySelectorAll('[data-chart-label]').forEach(target => {
        const show = event => {
            if (activeTarget !== target) hideTooltip();
            activeTarget = target;
            tooltip.textContent = target.dataset.chartLabel;
            tooltip.hidden = false;
            target.setAttribute('aria-describedby', tooltip.id);
            positionTooltip(target, event);
        };
        // The custom tooltip replaces the browser's delayed SVG title bubble.
        target.querySelector('title')?.remove();
        target.addEventListener('pointerenter', show);
        target.addEventListener('pointermove', event => { if (activeTarget === target) positionTooltip(target, event); });
        target.addEventListener('focus', show);
        target.addEventListener('click', show);
        target.addEventListener('pointerleave', hideTooltip);
        target.addEventListener('blur', hideTooltip);
    });
    document.addEventListener('keydown', event => { if (event.key === 'Escape') hideTooltip(); });
    document.addEventListener('pointerdown', event => { if (!event.target.closest('[data-chart-label]')) hideTooltip(); });
    window.addEventListener('scroll', hideTooltip, true);
    window.addEventListener('resize', hideTooltip);

    const svg = root.querySelector('[data-arrival-chart]');
    if (svg) {
        const points = [...svg.querySelectorAll('[data-arrival-point]')];
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
        resize();
        if (globalThis.ResizeObserver) new ResizeObserver(resize).observe(svg);
    }

    const selector = root.querySelector('[data-demographic-select]');
    const panels = [...root.querySelectorAll('[data-demographic-panel]')];
    if (!selector) return;
    const synchronize = () => {
        hideTooltip();
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
        panel.querySelectorAll('[data-chart-label]').forEach(target => {
            const show = () => {
                const selected = target.dataset.demographicSlice;
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
