// Dynamic invoice lines. Expects window.invoiceProducts and window.invoiceInitialRows.
// Discount % and line total are two-way: editing % recomputes the total;
// editing (rounding) the total recomputes the %.
// Requires product-search.js (ProductSearch) to be loaded first.
const initialRows = window.invoiceInitialRows || [];
const body = document.getElementById('itemsBody');
const furtherDiscountInput = document.getElementById('FurtherDiscount');

ProductSearch.init(window.invoiceProducts || []);

function findProduct(id) {
    return ProductSearch.find(id);
}

function cutOptions(product, selected) {
    let html = '<option value="">— whole / none —</option>';
    if (product && product.mode === 'PerFoot') {
        for (const piece of product.pieces) {
            const sel = selected != null && Math.abs(piece.len - selected) < 0.001 ? 'selected' : '';
            html += `<option value="${piece.len}" ${sel}>${piece.len} ft (×${piece.qty})</option>`;
        }
        // Keep a previously chosen length even if no longer in stock.
        if (selected != null && !product.pieces.some(p => Math.abs(p.len - selected) < 0.001)) {
            html += `<option value="${selected}" selected>${selected} ft (out of stock)</option>`;
        }
    }
    return html;
}

// afterTr: insert the new row directly below it instead of at the end. Returns the row.
function addRow(data, afterTr) {
    data = data || {};
    const tr = document.createElement('tr');
    const product = findProduct(data.productId || 0);
    // Size is a text field so several sizes can be typed at once ("10, 12, 14") — see expandSizes().
    tr.innerHTML = `
        <td class="position-relative">${ProductSearch.cellHtml(data.productId || 0)}</td>

        <td><input type="text" inputmode="decimal" autocomplete="off" class="form-control form-control-sm size" value="${data.sizeFt ?? ''}" oninput="updateRow(this, 'inputs')"
                   title="Several sizes at once: 10, 12, 14 then Enter — one line per size" /></td>
           <td><input type="number" min="0" class="form-control form-control-sm qty" value="${data.quantity ?? ''}" oninput="updateRow(this, 'inputs')" /></td>
        <td><select class="form-select form-select-sm cutfrom">${cutOptions(product, data.cutFromLengthFt)}</select></td>
        <td><input type="number" min="0" step="0.01" class="form-control form-control-sm rate" value="${data.rate ?? ''}" oninput="updateRow(this, 'inputs')" /></td>
        <td>
            <div class="input-group input-group-sm">
                <input type="number" min="0" max="100" step="0.01" class="form-control disc" value="${data.discountPercent || ''}" oninput="updateRow(this, 'percent')" placeholder="0" />
                <span class="input-group-text">%</span>
            </div>
        </td>
        <td class="text-end feet text-muted">—</td>
        <td><input type="number" min="0" step="0.01" class="form-control form-control-sm text-end fw-semibold ltotal" value="${data.lineTotal ?? ''}" oninput="updateRow(this, 'total')" /></td>
        <td class="text-center text-nowrap">
            <button type="button" class="btn btn-sm btn-outline-secondary" title="Duplicate line (Ctrl+D)" onclick="duplicateRow(this.closest('tr'))"><i class="bi bi-copy"></i></button>
            <button type="button" class="btn btn-sm btn-outline-danger" onclick="removeRow(this)"><i class="bi bi-x-lg"></i></button>
        </td>`;
    if (afterTr) afterTr.after(tr); else body.appendChild(tr);
    ProductSearch.bind(tr.querySelector('td'), (_, picked) => rowChanged(tr, picked));
    const sizeInput = tr.querySelector('.size');
    sizeInput.addEventListener('keydown', e => {
        if (e.key === 'Enter' && expandSizes(sizeInput)) e.preventDefault();
    });
    sizeInput.addEventListener('change', () => expandSizes(sizeInput));
    rowChanged(tr, false);
    return tr;
}

// Reads a line back into the shape addRow() takes.
function rowToData(tr) {
    const cut = tr.querySelector('.cutfrom').value;
    return {
        productId: parseInt(tr.querySelector('.product-id').value) || 0,
        sizeFt: tr.querySelector('.size').value,
        quantity: tr.querySelector('.qty').value,
        cutFromLengthFt: cut === '' ? null : parseFloat(cut),
        rate: tr.querySelector('.rate').value,
        discountPercent: tr.querySelector('.disc').value,
        lineTotal: tr.querySelector('.ltotal').value
    };
}

function flashRow(tr) {
    tr.classList.add('table-success');
    setTimeout(() => tr.classList.remove('table-success'), 800);
}

// Copies a line (all fields, including a rounded line total) directly below it.
// fromEl: the field the cursor was in — the same field gets focus in the copy.
function duplicateRow(tr, fromEl) {
    const source = rowToData(tr);
    const copy = addRow(source, tr);
    const totalInput = copy.querySelector('.ltotal');
    if (totalInput.value !== source.lineTotal) {
        totalInput.value = source.lineTotal;
        updateRow(totalInput, 'total');
    }
    flashRow(copy);

    const field = ['size', 'qty', 'cutfrom', 'rate', 'disc', 'ltotal'].find(c => fromEl?.classList?.contains(c));
    let target = field && copy.querySelector('.' + field);
    if (!target || target.disabled) target = copy.querySelector('.size:not(:disabled)') || copy.querySelector('.qty');
    target.focus();
    target.select?.();
}

// "10, 12, 14" (commas or spaces) in Size → this line takes the first size and a copy
// of it is added below for each other size. Also flags anything that isn't a number
// (the field is text, not a number box). Returns true if the value was a list or invalid.
function expandSizes(input) {
    const parts = input.value.split(/[\s,]+/).filter(s => s);
    const sizes = parts.map(Number);
    const valid = sizes.every(n => !isNaN(n) && n >= 0);
    input.classList.toggle('is-invalid', !valid);
    if (!valid) return true;
    if (sizes.length < 2) return false;

    const tr = input.closest('tr');
    input.value = sizes[0];
    updateRow(input, 'inputs');
    const data = rowToData(tr);
    let after = tr;
    for (const size of sizes.slice(1)) {
        after = addRow({ ...data, sizeFt: size, lineTotal: null }, after);
        flashRow(after);
    }
    return true;
}

// Ctrl+D inside a line duplicates it (same key as Excel's fill-down).
body.addEventListener('keydown', e => {
    if (!e.ctrlKey || e.altKey || e.shiftKey || e.metaKey || e.key.toLowerCase() !== 'd') return;
    const tr = e.target.closest('tr');
    if (!tr) return;
    e.preventDefault();
    duplicateRow(tr, e.target);
});

// Never post an unexpanded size list; block the save if a size is invalid.
body.closest('form')?.addEventListener('submit', e => {
    for (const input of body.querySelectorAll('.size')) {
        expandSizes(input);
        if (input.classList.contains('is-invalid')) {
            e.preventDefault();
            input.focus();
            return;
        }
    }
});

function removeRow(btn) {
    btn.closest('tr').remove();
    reindex();
    recalcTotals();
}

function rowChanged(el, resetRate) {
    const tr = el.closest('tr');
    const product = findProduct(parseInt(tr.querySelector('.product-id').value));
    const perFoot = product && product.mode === 'PerFoot';

    const sizeInput = tr.querySelector('.size');
    sizeInput.disabled = !perFoot;
    if (!perFoot) sizeInput.value = '';
    sizeInput.placeholder = perFoot ? 'required' : 'n/a';

    const cutSelect = tr.querySelector('.cutfrom');
    cutSelect.disabled = !perFoot;
    if (resetRate) {
        cutSelect.innerHTML = cutOptions(product, null);
        tr.querySelector('.rate').value = product ? product.price : '';
    }

    updateRow(tr, 'inputs');
}

function grossOf(tr) {
    const product = findProduct(parseInt(tr.querySelector('.product-id').value));
    const qty = parseInt(tr.querySelector('.qty').value) || 0;
    const size = parseFloat(tr.querySelector('.size').value) || 0;
    const rate = parseFloat(tr.querySelector('.rate').value) || 0;
    const perFoot = product && product.mode === 'PerFoot';
    return { gross: perFoot ? qty * size * rate : qty * rate, feet: perFoot ? qty * size : null };
}

// source: 'inputs' (qty/size/rate changed — keep %), 'percent', 'total'
function updateRow(el, source) {
    const tr = el.closest('tr');
    const { gross, feet } = grossOf(tr);
    const discInput = tr.querySelector('.disc');
    const totalInput = tr.querySelector('.ltotal');

    if (source === 'total') {
        let net = parseFloat(totalInput.value);
        const valid = !isNaN(net) && net >= 0 && net <= gross;
        totalInput.classList.toggle('is-invalid', !valid);
        if (isNaN(net)) net = gross;
        net = Math.min(Math.max(net, 0), gross);
        const pct = gross > 0 ? (gross - net) / gross * 100 : 0;
        discInput.value = pct === 0 ? '' : pct.toFixed(2);
        tr.dataset.net = net;
    } else {
        const pct = parseFloat(discInput.value) || 0;
        discInput.classList.toggle('is-invalid', pct < 0 || pct > 100);
        const discAmount = Math.round(gross * Math.min(Math.max(pct, 0), 100)) / 100;
        const net = Math.max(0, gross - discAmount);
        totalInput.value = net.toFixed(2);
        totalInput.classList.remove('is-invalid');
        tr.dataset.net = net;
    }

    tr.dataset.gross = gross;
    tr.querySelector('.feet').textContent = feet !== null ? feet.toFixed(2) + ' ft' : '—';
    reindex();
    recalcTotals();
}

function recalcTotals() {
    let subTotal = 0, netSum = 0;
    body.querySelectorAll('tr').forEach(tr => {
        subTotal += parseFloat(tr.dataset.gross) || 0;
        netSum += parseFloat(tr.dataset.net) || 0;
    });
    const further = parseFloat(furtherDiscountInput.value) || 0;
    furtherDiscountInput.classList.toggle('is-invalid', further < 0 || further > netSum);
    document.getElementById('subTotalDisplay').textContent = 'Rs. ' + subTotal.toFixed(2);
    document.getElementById('discountDisplay').textContent = 'Rs. ' + (subTotal - netSum).toFixed(2);
    document.getElementById('grandTotalDisplay').textContent = 'Rs. ' + Math.max(0, netSum - further).toFixed(2);
}

// Assign sequential Items[i].X names so MVC model binding works.
function reindex() {
    [...body.querySelectorAll('tr')].forEach((tr, i) => {
        tr.querySelector('.product-id').name = `Items[${i}].ProductId`;
        tr.querySelector('.qty').name = `Items[${i}].Quantity`;
        tr.querySelector('.size').name = `Items[${i}].SizeFt`;
        tr.querySelector('.cutfrom').name = `Items[${i}].CutFromLengthFt`;
        tr.querySelector('.rate').name = `Items[${i}].Rate`;
        tr.querySelector('.disc').name = `Items[${i}].DiscountPercent`;
        tr.querySelector('.ltotal').name = `Items[${i}].LineTotal`;
    });
}

furtherDiscountInput.addEventListener('input', recalcTotals);

// Customer balance hint (window.invoiceCustomers = [{id, balance}]).
const customers = window.invoiceCustomers || [];
const customerSelect = document.getElementById('CustomerId');
function updateCustomerBalance() {
    const hint = document.getElementById('custBalanceHint');
    if (!hint || !customerSelect) return;
    const customer = customers.find(c => c.id === parseInt(customerSelect.value));
    if (!customer) { hint.textContent = ''; return; }
    const balance = customer.balance;
    if (balance > 0) {
        hint.innerHTML = `Current balance: <strong class="text-danger">Rs. ${balance.toFixed(2)} owed</strong>`;
    } else if (balance < 0) {
        hint.innerHTML = `Advance held: <strong class="text-success">Rs. ${(-balance).toFixed(2)}</strong> — post as Credit Sale to apply it`;
    } else {
        hint.textContent = 'Balance: Rs. 0.00';
    }
}
customerSelect?.addEventListener('change', updateCustomerBalance);
updateCustomerBalance();

if (initialRows.length > 0) {
    for (const row of initialRows) addRow(row);
    // Preserve stored (possibly rounded) line totals on load.
    body.querySelectorAll('.ltotal').forEach(input => updateRow(input, 'total'));
} else {
    addRow();
}
