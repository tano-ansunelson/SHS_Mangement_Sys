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

// photo fields: show the chosen file name inside the drop area
document.addEventListener('change', e => {
    const input = e.target;
    if (!(input instanceof HTMLInputElement) || input.type !== 'file' || !input.hasAttribute('data-drop')) return;
    const box = input.closest('.drop'), text = box.querySelector('.drop-text');
    if (input.files.length) { text.textContent = input.files[0].name; box.classList.add('has-file'); }
});


// ---------- light / dark theme ----------
(function () {
    const root = document.documentElement;
    const label = () => document.querySelectorAll('[data-theme-label]').forEach(el =>
        el.textContent = root.dataset.theme === 'dark' ? 'Light mode' : 'Dark mode');
    const apply = t => { root.dataset.theme = t; label(); };
    document.addEventListener('DOMContentLoaded', () => {
        label();
        document.querySelectorAll('[data-theme-toggle]').forEach(b => b.addEventListener('click', () => {
            const next = root.dataset.theme === 'dark' ? 'light' : 'dark';
            try { localStorage.setItem('theme', next); } catch (e) { }
            apply(next);
        }));
    });
    if (window.matchMedia) matchMedia('(prefers-color-scheme: dark)').addEventListener('change', e => {
        let saved = null; try { saved = localStorage.getItem('theme'); } catch (x) { }
        if (!saved) apply(e.matches ? 'dark' : 'light');
    });
})();
