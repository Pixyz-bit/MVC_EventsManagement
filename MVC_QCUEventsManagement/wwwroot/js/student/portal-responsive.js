/* Keep sticky content and anchor targets below the actual portal header. */
(function () {
    function initializePortalLayout() {
        var header = document.querySelector('.user-portal .portal-navbar');
        if (!header) return;

        function updateHeaderHeight() {
            document.documentElement.style.setProperty('--portal-header-height', Math.ceil(header.getBoundingClientRect().height) + 'px');
        }

        updateHeaderHeight();
        if (window.ResizeObserver) {
            var observer = new ResizeObserver(updateHeaderHeight);
            observer.observe(header);
        } else {
            window.addEventListener('resize', updateHeaderHeight, { passive: true });
        }
    }

    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', initializePortalLayout);
    } else {
        initializePortalLayout();
    }
})();
