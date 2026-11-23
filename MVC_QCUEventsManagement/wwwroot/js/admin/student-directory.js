(() => {
    'use strict';
    const root = document.getElementById('student-directory');
    if (!root) return;
    const rows = JSON.parse(root.querySelector('#student-directory-data').textContent);
    const initial = JSON.parse(root.querySelector('#student-directory-initial').textContent);
    const dialogs = [...root.querySelectorAll('dialog')];
    const form = root.querySelector('#student-profile-form');
    const field = name => form.elements.namedItem('Input.' + name);
    let selected = null, origin = null, switching = false;
    function open(id, trigger) {
        if (trigger) origin = trigger;
        switching = true;
        dialogs.filter(d => d.open).forEach(d => d.close());
        switching = false;
        root.querySelector('#' + id).showModal();
    }
    dialogs.forEach(dialog => {
        dialog.querySelectorAll('[data-close-student-dialog]').forEach(button => button.addEventListener('click', () => dialog.close()));
        dialog.addEventListener('close', () => {
            dialog.querySelectorAll('input[type="password"]').forEach(input => { input.value = ''; });
            if (!switching && origin?.isConnected) origin.focus();
        });
    });
    function programs(value) {
        const select = field('Program');
        [...select.options].forEach(option => { option.disabled = option.dataset.college !== field('College').value; option.hidden = option.disabled; });
        const candidate = [...select.options].find(option => !option.disabled && (option.value === value || option.dataset.alias === value || option.value === 'BECEd' && value === 'Bachelor of Early Childhood Education'))
            ?? [...select.options].find(option => !option.disabled && option.value === select.value);
        select.value = candidate?.value ?? [...select.options].find(option => !option.disabled)?.value ?? '';
    }
    function editor(input, edit, trigger) {
        form.reset();
        form.action = edit ? form.dataset.editUrl : form.dataset.createUrl;
        root.querySelector('#student-editor-title').textContent = edit ? 'Edit student profile' : 'Add student';
        for (const key of ['StudentId', 'Email', 'FirstName', 'MiddleName', 'LastName', 'BirthDate', 'Gender', 'Campus', 'College', 'Token']) {
            const value = input[key[0].toLowerCase() + key.slice(1)];
            field(key).value = value ?? '';
        }
        field('Active').value = String(input.active ?? true);
        field('StudentId').readOnly = edit;
        field('BirthDate').required = !edit;
        root.querySelector('[data-birth-required]').hidden = edit;
        programs(input.program);
        root.querySelector('[data-initial-password]').hidden = edit;
        root.querySelector('#student-initial-password').required = !edit;
        root.querySelector('#student-initial-password').value = '';
        open('student-editor', trigger);
    }
    root.querySelector('[data-add-student]').addEventListener('click', event => editor({ gender: 'Prefer not to say', campus: 'San Bartolome', college: 'College of Computer Studies', program: 'BSIT', active: true }, false, event.currentTarget));
    root.querySelectorAll('[data-student-view]').forEach(button => button.addEventListener('click', () => {
        selected = rows[Number(button.dataset.studentView)];
        if (!selected) return;
        root.querySelector('[data-profile-id]').textContent = selected.studentId;
        root.querySelector('[data-profile-name]').textContent = selected.name;
        root.querySelector('[data-profile-status]').textContent = selected.active ? 'Active' : 'Inactive';
        for (const key of ['email', 'gender', 'campus', 'college', 'program']) root.querySelector('[data-profile-' + key + ']').textContent = selected[key];
        root.querySelector('[data-profile-birth-date]').textContent = selected.birthDate ? new Intl.DateTimeFormat('en', { dateStyle: 'medium', timeZone: 'UTC' }).format(new Date(selected.birthDate)) : 'Not recorded';
        root.querySelector('[data-student-access]').textContent = selected.active ? 'Deactivate account' : 'Activate account';
        open('student-profile', button);
    }));
    root.querySelector('[data-edit-student]').addEventListener('click', () => { if (selected) editor(selected, true); });
    field('College').addEventListener('change', () => programs());
    root.querySelector('[data-student-access]').addEventListener('click', () => {
        if (!selected) return;
        const dialog = root.querySelector('#student-access'); dialog.querySelector('form').reset();
        dialog.querySelector('[data-selected-student-id]').value = selected.studentId;
        dialog.querySelector('[data-selected-student-token]').value = selected.token;
        dialog.querySelector('[data-selected-student-active]').value = String(!selected.active);
        dialog.querySelector('[data-account-description]').textContent = `${selected.name} · ${selected.studentId}. ${selected.active ? 'This account will no longer be able to sign in.' : 'This account will be allowed to sign in.'}`;
        open('student-access');
    });
    root.querySelector('[data-student-password]').addEventListener('click', () => {
        if (!selected) return;
        const dialog = root.querySelector('#student-password'); dialog.querySelector('form').reset();
        dialog.querySelector('[data-selected-student-id]').value = selected.studentId;
        dialog.querySelector('[data-selected-student-token]').value = selected.token;
        dialog.querySelector('[data-password-student]').textContent = `${selected.name} · ${selected.studentId}`;
        open('student-password');
    });
    root.querySelector('[data-open-student-import]').addEventListener('click', event => open('student-import', event.currentTarget));
    const collegeFilter = root.querySelector('#student-college');
    const programFilter = root.querySelector('#student-program');
    const options = [...programFilter.options].filter(option => option.dataset.colleges)
        .map(option => ({ option, colleges: JSON.parse(option.dataset.colleges) }));
    function filterPrograms(clearUnavailable) {
        for (const {option, colleges} of options) {
            const unavailable = Boolean(collegeFilter.value && !colleges.includes(collegeFilter.value));
            option.hidden = unavailable && (!option.selected || clearUnavailable);
            option.disabled = option.hidden;
        }
        if (clearUnavailable && programFilter.selectedOptions[0]?.disabled) programFilter.value = '';
    }
    collegeFilter.addEventListener('change', () => filterPrograms(true));
    filterPrograms(false);
    if (root.dataset.reopen === 'create' || root.dataset.reopen === 'edit') editor(initial, root.dataset.reopen === 'edit', root.querySelector('[data-add-student]'));
    if (root.dataset.reopen === 'import') open('student-import', root.querySelector('[data-open-student-import]'));
})();
