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
        tabList.hidden = studentTab.hidden;
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

(() => {
    'use strict';
    const create = document.getElementById('create-account-dialog');
    const importer = document.getElementById('student-import');
    if (!create || !importer) return;
    const type = create.querySelector('[name="NewAccount.Role"]');
    const studentFields = document.getElementById('create-student-fields');
    const description = document.getElementById('create-account-description');
    function switchType() {
        const student = type.value === 'Student';
        studentFields.hidden = !student;
        studentFields.disabled = !student;
        description.textContent = student
            ? 'Student accounts include a student profile and access to event registration.'
            : 'Administrator accounts manage events, attendance and user access.';
    }
    function programs(college, program) {
        for (const option of program.options) {
            const allowed = option.dataset.colleges ? JSON.parse(option.dataset.colleges) : [option.dataset.college];
            option.disabled = Boolean(college.value && allowed[0] && !allowed.includes(college.value));
            option.hidden = option.disabled;
        }
        if (program.selectedOptions[0]?.disabled) program.value = '';
    }
    const createCollege = create.querySelector('[name="NewAccount.College"]');
    const createProgram = create.querySelector('[name="NewAccount.Program"]');
    createCollege.addEventListener('change', () => programs(createCollege, createProgram));
    type.addEventListener('change', switchType);
    switchType();
    programs(createCollege, createProgram);
    for (const modal of [create, importer]) {
        modal.querySelectorAll('[data-close-user-dialog]').forEach(button => button.addEventListener('click', () => modal.close()));
        modal.addEventListener('close', () => {
            modal.querySelectorAll('input[type="password"]').forEach(input => { input.value = ''; });
        });
        if (modal.hasAttribute('open')) {
            modal.removeAttribute('open');
            modal.showModal();
        }
    }
    document.querySelectorAll('header a[href]').forEach(link => {
        const open = new URL(link.href).searchParams.get('open');
        if (!['create', 'import'].includes(open)) return;
        link.addEventListener('click', event => {
            if (event.ctrlKey || event.metaKey || event.shiftKey || event.altKey) return;
            event.preventDefault();
            (open === 'create' ? create : importer).showModal();
        });
    });
    const filters = document.getElementById('user-filter-menu');
    const filterForm = document.getElementById('user-filter-form');
    const fields = [...filters.querySelectorAll('select')];
    const applied = fields.map(field => field.value);
    const filterCollege = filters.querySelector('[name="college"]');
    const filterProgram = filters.querySelector('[name="program"]');
    function restore() {
        fields.forEach((field, index) => { field.value = applied[index]; });
        programs(filterCollege, filterProgram);
    }
    filterCollege.addEventListener('change', () => programs(filterCollege, filterProgram));
    filters.addEventListener('toggle', () => { if (filters.open) restore(); });
    document.addEventListener('pointerdown', event => {
        if (filters.open && !filters.contains(event.target)) { restore(); filters.open = false; }
    });
    filters.addEventListener('keydown', event => {
        if (event.key === 'Escape') { restore(); filters.open = false; filters.querySelector('summary').focus(); }
    });
    filterForm.addEventListener('submit', event => {
        if (event.submitter?.id !== 'user-apply-filters') restore();
    });
})();
