// Bulk rate update: live search filter + submit only changed rows.
const ratesBody = document.getElementById('ratesBody');
const searchInput = document.getElementById('rateSearch');
const form = document.getElementById('ratesForm');
const saveBtn = document.getElementById('saveRatesBtn');
const changedCount = document.getElementById('changedCount');

// Every search token must match somewhere in "name category color gauge".
searchInput.addEventListener('input', () => {
    const tokens = searchInput.value.toLowerCase().split(/\s+/).filter(t => t);
    for (const tr of ratesBody.querySelectorAll('tr')) {
        const hay = tr.dataset.search;
        tr.style.display = tokens.every(t => hay.includes(t)) ? '' : 'none';
    }
});

function isChanged(input) {
    const value = parseFloat(input.value);
    const original = parseFloat(input.dataset.original);
    return !isNaN(value) && value > 0 && Math.abs(value - original) > 0.0001;
}

ratesBody.addEventListener('input', e => {
    const input = e.target.closest('.rate-input');
    if (!input) return;
    input.classList.toggle('border-primary', isChanged(input));
    input.classList.toggle('bg-primary-subtle', isChanged(input));
    const n = [...ratesBody.querySelectorAll('.rate-input')].filter(isChanged).length;
    changedCount.textContent = n;
    saveBtn.disabled = n === 0;
});

// On submit, post only the changed rows as changes[i].Id / changes[i].Price.
form.addEventListener('submit', e => {
    form.querySelectorAll('input[type="hidden"].change-field').forEach(x => x.remove());
    const changed = [...ratesBody.querySelectorAll('.rate-input')].filter(isChanged);
    if (changed.length === 0) {
        e.preventDefault();
        return;
    }
    changed.forEach((input, i) => {
        const tr = input.closest('tr');
        form.insertAdjacentHTML('beforeend',
            `<input type="hidden" class="change-field" name="changes[${i}].Id" value="${tr.dataset.id}" />` +
            `<input type="hidden" class="change-field" name="changes[${i}].Price" value="${parseFloat(input.value)}" />`);
    });
});

// Warn before leaving with unsaved changes.
window.addEventListener('beforeunload', e => {
    if (!saveBtn.disabled && !form.dataset.submitting) e.preventDefault();
});
form.addEventListener('submit', () => { form.dataset.submitting = '1'; });
