(() => {
    const get = id => document.getElementById(id);
    const setText = (id, value) => { get(id).textContent = value; };
    const background = (id, url) => { get(id).style.backgroundImage = `url("${url}")`; };
    const details = get('modalEventDetails');
    const cancellation = get('modalCancelRegistration');
    const logout = get('logoutConfirmDialog');
    const hero = get('heroGallery');
    const navbar = get('portalNavbar');
    const slideCards = [...document.querySelectorAll('#featuredEvents [data-card]')].map(item => get(item.dataset.card));
    const dots = [...document.querySelectorAll('[data-slide]')];
    const tabs = [...document.querySelectorAll('[data-event-tab]')];
    let slide = 0;

    function showDialog(dialog) {
        dialog.classList.add('active');
        dialog.showModal();
    }

    function showDetails(card) {
        const event = card.dataset;
        const open = event.status === 'Open';
        background('modalBannerHeader', event.banner);
        setText('modalEventTitle', event.title);
        setText('modalEventDate', event.date);
        setText('modalEventTime', event.time);
        setText('modalEventVenue', event.venue);
        setText('modalEventDescription', event.caption);
        setText('modalRegStart', event.start);
        setText('modalRegEnd', event.end);
        setText('modalRegStatusText', open ? 'Open for Enrolled Students' : event.status);
        setText('modalEventSeats', `${event.seats} of ${event.capacity} seats remaining`);
        setText('modalCapacityBadgeText', `${event.seats} SEATS LEFT`);
        get('modalCapacityBar').style.width = `${(1 - Number(event.seats) / Math.max(1, Number(event.capacity))) * 100}%`;
        get('modalRegStatusBadge').replaceChildren(card.querySelector('.card-status-pill').cloneNode(true));
        get('modalSponsorsBadges').replaceChildren(...[...card.querySelectorAll('[data-sponsor]')].map(sponsor => {
            const badge = sponsor.cloneNode(true);
            badge.className = 'sponsor-badge';
            return badge;
        }));
        const action = get('modalRegisterBtn');
        action.querySelector('span').textContent = open ? 'Register to this event' : event.status;
        if (open) action.href = event.register;
        else action.removeAttribute('href');
        action.setAttribute('aria-disabled', String(!open));
        showDialog(details);
    }

    function showCancellation(card) {
        const event = card.dataset;
        get('cancelPassForm').action = event.cancel;
        setText('cancelModalPassIdBadge', event.pass);
        setText('cancelModalEventTitle', event.title);
        setText('cancelModalEventDate', event.date);
        setText('cancelModalEventTime', event.time);
        setText('cancelModalEventVenue', event.venue);
        showDialog(cancellation);
    }

    function selectSlide(index) {
        slide = (index + slideCards.length) % slideCards.length;
        const event = slideCards[slide].dataset;
        background('heroBgImage', event.banner);
        setText('heroTitle', event.title);
        setText('heroDescription', event.caption);
        setText('heroVenue', event.venue);
        setText('heroDate', event.date);
        setText('heroTime', event.time);
        get('heroActionBtn').href = event.register;
        dots.forEach((dot, index) => {
            dot.classList.toggle('active', index === slide);
            dot.setAttribute('aria-pressed', String(index === slide));
        });
    }

    function switchTab(view) {
        const registered = view === 'registered';
        get('catalogContentArea').hidden = registered;
        get('registeredContentArea').hidden = !registered;
        get('registeredContentArea').classList.toggle('active-view', registered);
        setText('viewSectionTitle', registered ? 'My Registered Events & Passes' : 'Campus Event Matrix');
        setText('viewSectionSubtitle', registered ? 'Inspect your enrolled passes and verified event schedules.'
            : 'Explore open registrations or inspect your booked electronic passes.');
        tabs.forEach(tab => {
            const active = tab.dataset.eventTab === view;
            tab.classList.toggle('active', active);
            tab.setAttribute('aria-pressed', String(active));
        });
    }

    dots.forEach(dot => dot.addEventListener('click', () => selectSlide(Number(dot.dataset.slide))));
    document.querySelectorAll('[data-slide-step]').forEach(button =>
        button.addEventListener('click', () => selectSlide(slide + Number(button.dataset.slideStep))));
    get('heroActionBtn')?.addEventListener('click', event => {
        event.preventDefault();
        showDetails(slideCards[slide]);
    });
    tabs.forEach(tab => tab.addEventListener('click', () => switchTab(tab.dataset.eventTab)));
    document.querySelector('[data-browse-events]')?.addEventListener('click', () => {
        switchTab('catalog');
        tabs[0].focus();
    });
    document.querySelectorAll('[data-event-details]').forEach(button =>
        button.addEventListener('click', () => showDetails(button.closest('.event-card'))));
    document.querySelectorAll('[data-cancel-pass]').forEach(button =>
        button.addEventListener('click', () => showCancellation(button.closest('.event-card'))));
    get('openLogout').addEventListener('click', () => logout.showModal());
    get('logoutStayButton').addEventListener('click', () => logout.close());
    for (const dialog of [details, cancellation]) {
        dialog.addEventListener('close', () => dialog.classList.remove('active'));
        dialog.addEventListener('click', event => { if (event.target === dialog) dialog.close(); });
        dialog.querySelectorAll('[data-close-dialog]').forEach(button =>
            button.addEventListener('click', () => dialog.close()));
    }
    const updateNavbar = () => navbar.classList.toggle('nav-scrolled', !hero || hero.getBoundingClientRect().bottom <= navbar.offsetHeight);
    addEventListener('scroll', updateNavbar, { passive: true });
    addEventListener('resize', updateNavbar);
    updateNavbar();
})();
