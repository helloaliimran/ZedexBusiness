// Quotation entry: dynamic scope lines and terms, live amounts / grand total.
(function () {
    const form = document.getElementById('quotationForm');
    if (!form) return;

    const itemsBody = document.querySelector('#itemsTable tbody');
    const termsList = document.getElementById('termsList');
    const itemTemplate = document.getElementById('itemTemplate');
    const termTemplate = document.getElementById('termTemplate');
    const defaultTerms = JSON.parse(document.getElementById('defaultTerms').textContent || '[]');
    const fmt = new Intl.NumberFormat('en-US', { minimumFractionDigits: 2, maximumFractionDigits: 2 });

    const num = (input) => {
        const v = parseFloat(input.value);
        return isNaN(v) ? 0 : v;
    };

    function autoGrow(el) {
        el.style.height = 'auto';
        el.style.height = el.scrollHeight + 2 + 'px';
    }

    function recalc() {
        let total = 0;
        itemsBody.querySelectorAll('.item-row').forEach((tr, i) => {
            const amount = Math.round(num(tr.querySelector('.qty')) * num(tr.querySelector('.rate')) * 100) / 100;
            tr.querySelector('.amount').textContent = fmt.format(amount);
            tr.querySelector('.line-no').textContent = i + 1;
            total += amount;
        });
        document.getElementById('grandTotal').textContent = 'Rs. ' + fmt.format(total);
        termsList.querySelectorAll('.term-row').forEach((li, i) => {
            li.querySelector('.term-no').textContent = (i + 1) + '.';
        });
    }

    function addItem(focus) {
        const tr = itemTemplate.content.firstElementChild.cloneNode(true);
        itemsBody.appendChild(tr);
        recalc();
        if (focus) tr.querySelector('.desc').focus();
        return tr;
    }

    function addTerm(text, focus) {
        const li = termTemplate.content.firstElementChild.cloneNode(true);
        const ta = li.querySelector('.term');
        ta.value = text || '';
        termsList.appendChild(li);
        autoGrow(ta);
        recalc();
        if (focus) ta.focus();
    }

    document.getElementById('addItem').addEventListener('click', () => addItem(true));
    document.getElementById('addTerm').addEventListener('click', () => addTerm('', true));
    document.getElementById('resetTerms').addEventListener('click', () => {
        const hasTerms = [...termsList.querySelectorAll('.term')].some(t => t.value.trim());
        if (hasTerms && !confirm('Replace the current terms with the default terms?')) return;
        termsList.innerHTML = '';
        defaultTerms.forEach(t => addTerm(t, false));
    });

    form.addEventListener('click', (e) => {
        const removeRow = e.target.closest('.remove-row');
        if (removeRow) {
            removeRow.closest('tr').remove();
            if (!itemsBody.querySelector('.item-row')) addItem(false);
            recalc();
        }
        const removeTerm = e.target.closest('.remove-term');
        if (removeTerm) {
            removeTerm.closest('li').remove();
            recalc();
        }
    });

    form.addEventListener('input', (e) => {
        if (e.target.matches('.auto-grow')) autoGrow(e.target);
        if (e.target.matches('.qty, .rate')) recalc();
    });

    // Enter in the Rate box of the last line adds a new line (fast keyboard entry).
    itemsBody.addEventListener('keydown', (e) => {
        if (e.key === 'Enter' && e.target.matches('.rate')) {
            e.preventDefault();
            const tr = e.target.closest('tr');
            if (tr === itemsBody.lastElementChild) addItem(true);
            else tr.nextElementSibling.querySelector('.desc').focus();
        }
    });

    // Assign sequential Items[i].X / Terms[i] names so MVC model binding works.
    form.addEventListener('submit', () => {
        itemsBody.querySelectorAll('.item-row').forEach((tr, i) => {
            tr.querySelector('.desc').name = `Items[${i}].Description`;
            tr.querySelector('.spec').name = `Items[${i}].Specification`;
            tr.querySelector('.qty').name = `Items[${i}].Quantity`;
            tr.querySelector('.rate').name = `Items[${i}].Rate`;
        });
        termsList.querySelectorAll('.term').forEach((ta, i) => {
            ta.name = `Terms[${i}]`;
        });
    });

    form.querySelectorAll('.auto-grow').forEach(autoGrow);
    recalc();
})();
