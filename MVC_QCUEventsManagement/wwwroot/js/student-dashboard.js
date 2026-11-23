(() => {
    'use strict';
    const root = document.getElementById('student-dashboard');
    if (!root) return;
    const theme = root.querySelector('[data-theme-toggle]');
    const themeKey = 'qcu-student-theme';
    function setTheme(value) {
        root.dataset.theme = value;
        theme.setAttribute('aria-pressed', String(value === 'dark'));
    }
    try {
        const saved = localStorage.getItem(themeKey);
        if (saved === 'light' || saved === 'dark') setTheme(saved);
    } catch { /* The default is usable with storage disabled. */ }
    theme.hidden = false;
    theme.addEventListener('click', () => {
        setTheme(root.dataset.theme === 'dark' ? 'light' : 'dark');
        try { localStorage.setItem(themeKey, root.dataset.theme); } catch { /* Keep the current page theme. */ }
    });

    for (const banner of root.querySelectorAll('[data-event-banner]')) {
        const hide = () => { banner.hidden = true; };
        banner.addEventListener('error', hide);
        if (banner.complete && !banner.naturalWidth) hide();
    }

    const slides = [...root.querySelectorAll('[data-feature]')];
    const controls = root.querySelector('[data-feature-controls]');
    if (controls) {
        let current = 0;
        const position = controls.querySelector('[data-feature-position]');
        const step = change => {
            current = (current + change + slides.length) % slides.length;
            slides.forEach((slide, index) => { slide.hidden = index !== current; });
            position.textContent = `${current + 1} of ${slides.length}`;
        };
        controls.querySelector('[data-feature-prev]').addEventListener('click', () => step(-1));
        controls.querySelector('[data-feature-next]').addEventListener('click', () => step(1));
        controls.hidden = false;
    }

    const available = root.querySelector('#student-available');
    const registered = root.querySelector('#student-registered');
    if (!available || !registered) return;
    const tabs = root.querySelector('[data-event-tabs]');
    const buttons = [...tabs.querySelectorAll('[data-event-tab]')];
    const title = root.querySelector('[data-workspace-title]');
    const filter = root.querySelector('[data-event-filter]');
    const category = root.querySelector('#student-event-category');
    const cards = [...available.querySelectorAll('[data-event-category]')];
    const empty = available.querySelector('[data-no-category-results]');
    function showView(view) {
        const own = view === 'registered';
        available.hidden = own;
        registered.hidden = !own;
        title.textContent = own ? 'My registered events' : 'Campus events';
        buttons.forEach(button => button.setAttribute('aria-pressed', String(button.dataset.eventTab === view)));
    }
    function filterCards() {
        const selected = category?.value;
        cards.forEach(card => { card.hidden = Boolean(selected && card.dataset.eventCategory !== selected); });
        if (empty) empty.hidden = cards.some(card => !card.hidden);
    }
    tabs.hidden = false;
    registered.querySelector('[data-registered-heading]').hidden = true;
    buttons.forEach(button => button.addEventListener('click', () => showView(button.dataset.eventTab)));
    if (filter) filter.hidden = false;
    category?.addEventListener('change', filterCards);
    showView('available');

    root.querySelectorAll('[data-feature-link]').forEach(link => link.addEventListener('click', () => {
        showView('available');
        if (category) category.value = '';
        filterCards();
        const card = document.getElementById(link.hash.slice(1));
        if (card) {
            card.querySelector('details').open = true;
            card.focus({ preventScroll: true });
        }
    }));
})();
