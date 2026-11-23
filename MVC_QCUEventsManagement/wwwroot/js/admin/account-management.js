(() => {
    'use strict';
    const dialog = document.getElementById('account-profile-dialog');
    if (!dialog) return;
    const tabList = dialog.querySelector('[role="tablist"]');
    const tabs = [...tabList.querySelectorAll('[role="tab"]')];
    const [studentTab, accountTab] = tabs;
    const role = dialog.querySelector('select[name="Editor.Role"]');
    const college = dialog.querySelector('select[name="Editor.College"]');
    const program = dialog.querySelector('select[name="Editor.Program"]');

    function showTab(current) {
        for (const tab of tabs) {
            const selected = tab === current;
            tab.setAttribute('aria-selected', String(selected));
            tab.tabIndex = selected ? 0 : -1;
            document.getElementById(tab.getAttribute('aria-controls')).hidden = !selected;
        }
    }
    function toggleStudentTab() {
        studentTab.hidden = role.value !== 'Student';
        if (studentTab.hidden) showTab(accountTab);
    }
    function filterPrograms() {
        for (const option of program.options) {
            option.disabled = Boolean(option.dataset.college && option.dataset.college !== college.value);
            option.hidden = option.disabled;
        }
        if (program.selectedOptions[0]?.disabled) program.value = '';
    }

    for (const tab of tabs) {
        tab.addEventListener('click', () => showTab(tab));
        tab.addEventListener('keydown', event => {
            if (studentTab.hidden || !['ArrowLeft', 'ArrowRight', 'Home', 'End'].includes(event.key)) return;
            event.preventDefault();
            const target = event.key === 'Home' ? studentTab : event.key === 'End' ? accountTab : tab === studentTab ? accountTab : studentTab;
            showTab(target);
            target.focus();
        });
    }
    dialog.addEventListener('invalid', event => {
        const panel = event.target.closest('[role="tabpanel"]');
        if (panel) showTab(tabs.find(tab => tab.getAttribute('aria-controls') === panel.id));
    }, true);
    role.addEventListener('change', toggleStudentTab);
    college.addEventListener('change', filterPrograms);
    tabList.hidden = false;
    showTab(dialog.dataset.student === 'true' ? studentTab : accountTab);
    toggleStudentTab();
    filterPrograms();
    dialog.addEventListener('cancel', event => {
        event.preventDefault();
        location.assign(dialog.querySelector('header a').href);
    });
    dialog.removeAttribute('open');
    dialog.showModal();
})();
