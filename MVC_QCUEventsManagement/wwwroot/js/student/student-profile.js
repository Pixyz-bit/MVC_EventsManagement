(() => {
    const tabs = [...document.querySelectorAll('[data-profile-tab]')];

    function selectTab(selected) {
        for (const tab of tabs) {
            const active = selected === tab;
            tab.classList.toggle('active', active);
            tab.setAttribute('aria-selected', String(active));
            tab.tabIndex = active ? 0 : -1;
            const panel = document.getElementById(tab.getAttribute('aria-controls'));
            panel.classList.toggle('active-tab', active);
            panel.hidden = !active;
        }
    }

    for (const tab of tabs) {
        tab.addEventListener('click', () => selectTab(tab));
        tab.addEventListener('keydown', event => {
            const index = tabs.indexOf(tab);
            const next = { ArrowLeft: 1 - index, ArrowRight: 1 - index, Home: 0, End: 1 }[event.key];
            if (next === undefined) return;
            event.preventDefault();
            selectTab(tabs[next]);
            tabs[next].focus();
        });
    }
    if (location.hash === '#security') selectTab(tabs[1]);
})();
