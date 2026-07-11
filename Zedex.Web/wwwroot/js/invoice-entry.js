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

function addRow(data) {
    data = data || {};
    const tr = document.createElement('tr');
    const product = findProduct(data.productId || 0);
    tr.innerHTML = `
        <td class="position-relative">${ProductSearch.cellHtml(data.productId || 0)}</td>
     
        <td><input type="number" min="0" step="0.01" class="form-control form-control-sm size" value="${data.sizeFt ?? ''}" oninput="updateRow(this, 'inputs')" /></td>
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
        <td class="text-center">
            <button type="button" class="btn btn-sm btn-outline-danger" onclick="removeRow(this)"><i class="bi bi-x-lg"></i></button>
        </td>`;
    body.appendChild(tr);
    ProductSearch.bind(tr.querySelector('td'), (_, picked) => rowChanged(tr, picked));
    rowChanged(tr, false);
}

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
