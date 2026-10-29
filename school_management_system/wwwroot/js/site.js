// Please see documentation at https://learn.microsoft.com/aspnet/core/client-side/bundling-and-minification
// for details on configuring this project to bundle and minify static web assets.

// Write your JavaScript code.
document.addEventListener('DOMContentLoaded', () => {
    const btn = document.getElementById('menuBtn');
    if (btn) btn.addEventListener('click', () => document.body.classList.toggle('menu-open'));

    // confirm dangerous submits
    document.querySelectorAll('form[data-confirm]').forEach(f =>
        f.addEventListener('submit', e => { if (!confirm(f.dataset.confirm)) e.preventDefault(); }));

    // live table filter: <input data-filter="tableId">
    document.querySelectorAll('input[data-filter]').forEach(inp => {
        inp.addEventListener('input', () => {
            const q = inp.value.toLowerCase();
            document.querySelectorAll('#' + inp.dataset.filter + ' tbody tr').forEach(tr =>
                tr.style.display = tr.textContent.toLowerCase().includes(q) ? '' : 'none');
        });
    });

    // select-all helper: <input type=checkbox data-all=".cls">
    document.querySelectorAll('input[data-all]').forEach(master =>
        master.addEventListener('change', () =>
            document.querySelectorAll(master.dataset.all).forEach(c => { if (!c.disabled) c.checked = master.checked; })));

    // auto-submit selects: <select data-autosubmit>
    document.querySelectorAll('select[data-autosubmit]').forEach(s =>
        s.addEventListener('change', () => s.form.submit()));

    setTimeout(() => document.querySelectorAll('.alert.success').forEach(a => a.style.display = 'none'), 7000);
});
