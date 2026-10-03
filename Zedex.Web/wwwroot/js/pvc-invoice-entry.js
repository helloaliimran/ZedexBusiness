// Dynamic PVC invoice lines. Expects window.pvcProducts, window.pvcCustomers,
// window.pvcInitialRows, window.pvcGasKitRate.
// Per line: lengths gross = (PerFoot: len × qty × rate) or (Weight: wt/len × qty × rate);
// gas kit defaults to rate × (Single 1 / Double 2) × len × qty but is directly editable —
// the sticky default re-syncs whenever qty/len/rate/kit change; the discount % applies to
// the combined total (lengths + gas kit); line total (editable) = combined total net of
// the discount — editing it re-derives the discount %.
// Requires product-search.js (ProductSearch) to be loaded first.
const initialRows = window.pvcInitialRows || [];
const gasKitRate = window.pvcGasKitRate || 0;
const body = document.getElementById('itemsBody');
const furtherDiscountInput = document.getElementById('FurtherDiscount');
let rowSeq = 0;

ProductSearch.init(window.pvcProducts || []);

function findProduct(id) {
    return ProductSearch.find(id);
}

function gasMultiplier(type) {
    return type === 'Single' ? 1 : type === 'Double' ? 2 : 0;
}

function lengthOptions(product) {
    if (!product) return '';
    return product.pieces
        .map(p => `<option value="${p.len}" label="${p.len} ft (×${p.qty} in stock)"></option>`)
        .join('');
}

function gasKitSelect(selected) {
    const options = ['None', 'Single', 'Double']
        .map(v => `<option value="${v}" ${v === selected ? 'selected' : ''}>${v === 'None' ? 'No Kit' : v}</option>`)
        .join('');
    return `<select class="form-select form-select-sm gaskit" onchange="updateRow(this, 'inputs')">${options}</select>`;
}

// JSON-serialized enums arrive as numbers (1=None, 2=Single, 3=Double).
function gasKitName(value) {
    if (value === 2 || value === 'Single') return 'Single';
    if (value === 3 || value === 'Double') return 'Double';
    return 'None';
}

// afterTr: insert the new row directly below it instead of at the end. Returns the row.
function addRow(data, afterTr) {
    data = data || {};
    const tr = document.createElement('tr');
    const listId = `pvc-lengths-${rowSeq++}`;
    const product = findProduct(data.productId || 0);
    const gasKit = data.gasKitType != null ? gasKitName(data.gasKitType) : (product ? product.gasKit : 'None');
    // Length is a text field so several lengths can be typed at once ("19, 13") — see expandLengths().
    tr.innerHTML = `
        <td class="position-relative">${ProductSearch.cellHtml(data.productId || 0)}</td>
        <td>
            <input type="text" inputmode="decimal" autocomplete="off" list="${listId}" class="form-control form-control-sm len"
                   value="${data.lengthFt ?? '19'}" placeholder="ft"  oninput="updateRow(this, 'inputs')"
                   title="Several lengths at once: 19, 13 then Enter — one line per length" />
            <datalist id="${listId}">${lengthOptions(product)}</datalist>
        </td>
        <td><input type="number" min="0" class="form-control form-control-sm qty" style="width:60px" value="${data.quantity ?? ''}" oninput="updateRow(this, 'inputs')" /></td>
        <td>${gasKitSelect(gasKit)}</td>
        <td><input type="number" min="0" step="0.001" class="form-control form-control-sm wtlen" value="${data.weightPerLength ?? ''}" oninput="updateRow(this, 'inputs')" /></td>
        <td><input type="number" min="0" step="0.01" class="form-control form-control-sm rate" value="${data.rate ?? ''}" oninput="updateRow(this, 'inputs')" /></td>
        <td>
            <div class="input-group input-group-sm">
                <input type="number" min="0" max="100" step="0.01" class="form-control disc" value="${data.discountPercent || ''}" oninput="updateRow(this, 'percent')" placeholder="0" />
                
            </div>
        </td>
        <td class="text-end qtytotal text-muted">—</td>
        <td><input type="number" min="0" step="0.01" class="form-control form-control-sm text-end gasamt" value="${data.gasKitAmount ?? ''}" placeholder="0.00" oninput="updateRow(this, 'gasamt')" /></td>
        <td><input type="number" min="0" step="0.01" class="form-control form-control-sm text-end fw-semibold ltotal" value="${data.lineTotal ?? ''}" oninput="updateRow(this, 'total')" /></td>
        <td class="text-center text-nowrap">
            <button type="button" class="btn btn-sm btn-outline-secondary" title="Duplicate line (Ctrl+D)" onclick="duplicateRow(this.closest('tr'))"><i class="bi bi-copy"></i></button>
            <button type="button" class="btn btn-sm btn-outline-danger" onclick="removeRow(this)"><i class="bi bi-x-lg"></i></button>
        </td>`;
    if (afterTr) afterTr.after(tr); else body.appendChild(tr);
    ProductSearch.bind(tr.querySelector('td'), (_, picked) => rowChanged(tr, picked));
    const lenInput = tr.querySelector('.len');
    lenInput.addEventListener('keydown', e => {
        if (e.key === 'Enter' && expandLengths(lenInput)) e.preventDefault();
    });
    lenInput.addEventListener('change', () => expandLengths(lenInput));
    rowChanged(tr, false);
    return tr;
}

// Reads a line back into the shape addRow() takes.
function rowToData(tr) {
    return {
        productId: parseInt(tr.querySelector('.product-id').value) || 0,
        lengthFt: tr.querySelector('.len').value,
        quantity: tr.querySelector('.qty').value,
        gasKitType: tr.querySelector('.gaskit').value,
        weightPerLength: tr.querySelector('.wtlen').value,
        rate: tr.querySelector('.rate').value,
        discountPercent: tr.querySelector('.disc').value,
        gasKitAmount: tr.querySelector('.gasamt').value,
        lineTotal: tr.querySelector('.ltotal').value
    };
}

// addRow() re-derives gas kit amount, line total and discount % from formula
// defaults — put back the given values as-is (no re-deriving one from another).
function restoreStoredValues(tr, row) {
    if (row.gasKitAmount != null) tr.querySelector('.gasamt').value = row.gasKitAmount;
    if (row.lineTotal != null) tr.querySelector('.ltotal').value = row.lineTotal;
    if (row.discountPercent != null) tr.querySelector('.disc').value = row.discountPercent || '';
    refreshRowTotals(tr);
}

function flashRow(tr) {
    tr.classList.add('table-success');
    setTimeout(() => tr.classList.remove('table-success'), 800);
}

// Copies a line (all fields, including a manual gas kit amount or rounded line total)
// directly below it. fromEl: the field the cursor was in — the same field gets focus in the copy.
function duplicateRow(tr, fromEl) {
    const source = rowToData(tr);
    const copy = addRow(source, tr);
    restoreStoredValues(copy, source);
    flashRow(copy);

    const field = ['len', 'qty', 'gaskit', 'wtlen', 'rate', 'disc', 'gasamt', 'ltotal'].find(c => fromEl?.classList?.contains(c));
    let target = field && copy.querySelector('.' + field);
    if (!target || target.disabled) target = copy.querySelector('.len');
    target.focus();
    target.select?.();
}

// "19, 13" (commas or spaces) in Length → this line takes the first length and a copy
// of it is added below for each other length (gas kit and total recalculated per length).
// Also flags anything that isn't a number (the field is text, not a number box).
// Returns true if the value was a list or invalid.
function expandLengths(input) {
    const parts = input.value.split(/[\s,]+/).filter(s => s);
    const lengths = parts.map(Number);
    const valid = lengths.every(n => !isNaN(n) && n >= 0);
    input.classList.toggle('is-invalid', !valid);
    if (!valid) return true;
    if (lengths.length < 2) return false;

    const tr = input.closest('tr');
    input.value = lengths[0];
    updateRow(input, 'inputs');
    const data = rowToData(tr);
    let after = tr;
    for (const len of lengths.slice(1)) {
        after = addRow({ ...data, lengthFt: len, gasKitAmount: null, lineTotal: null }, after);
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

// Never post an unexpanded length list; block the save if a length is invalid.
body.closest('form')?.addEventListener('submit', e => {
    for (const input of body.querySelectorAll('.len')) {
        expandLengths(input);
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

// picked=true when the user chose a product: reset rate/kit/weight to product defaults.
function rowChanged(el, picked) {
    const tr = el.closest('tr');
    const product = findProduct(parseInt(tr.querySelector('.product-id').value));
    const weightBased = product && product.saleType === 'Weight';

    const wtInput = tr.querySelector('.wtlen');
    wtInput.disabled = !weightBased;
    wtInput.placeholder = weightBased ? 'kg' : 'n/a';
    if (!weightBased) wtInput.value = '';

    if (picked) {
        tr.querySelector('.rate').value = product ? product.price : '';
        tr.querySelector('.gaskit').value = product ? product.gasKit : 'None';
        if (weightBased) wtInput.value = product.weightPerLength ?? '';
        tr.querySelector('datalist').innerHTML = lengthOptions(product);
    }

    // Rate unit hint on the rate cell.
    tr.querySelector('.rate').title = product
        ? (weightBased ? 'Rs. per kg' : product.saleType === 'RateLength' ? 'Rs. per whole length' : 'Rs. per running foot')
        : '';

    updateRow(tr, 'inputs');
}

function amountsOf(tr) {
    const product = findProduct(parseInt(tr.querySelector('.product-id').value));
    const qty = parseInt(tr.querySelector('.qty').value) || 0;
    const len = parseFloat(tr.querySelector('.len').value) || 0;
    const rate = parseFloat(tr.querySelector('.rate').value) || 0;
    const wtLen = parseFloat(tr.querySelector('.wtlen').value) || 0;
    const weightBased = product && product.saleType === 'Weight';
    const ratePerLength = product && product.saleType === 'RateLength';
    const mult = gasMultiplier(tr.querySelector('.gaskit').value);

    const totalFeet = len * qty;
    const totalWeight = wtLen * qty;
    const lengthsGross = weightBased ? totalWeight * rate : ratePerLength ? qty * rate : totalFeet * rate;
    // Products that bundle the gas kit into their rate get no separate gas kit charge.
    const defaultGasAmount = product && product.gasKitIncludedInRate
        ? 0
        : Math.round(gasKitRate * mult * len * qty * 100) / 100;
    return { lengthsGross, defaultGasAmount, totalFeet, totalWeight, weightBased };
}

// source: 'inputs' (qty/len/rate/kit/wt changed — re-syncs gas kit amount to its
// formula default and keeps disc %), 'percent' (disc % edited), 'gasamt' (gas kit
// amount edited directly — keeps disc %), 'total' (line total edited — re-derives disc %).
// The discount % applies to the combined total (lengths + gas kit), not to lengths alone.
function updateRow(el, source) {
    const tr = el.closest('tr');
    const { lengthsGross, defaultGasAmount, totalFeet, totalWeight, weightBased } = amountsOf(tr);
    const product = findProduct(parseInt(tr.querySelector('.product-id').value));
    const discInput = tr.querySelector('.disc');
    const totalInput = tr.querySelector('.ltotal');
    const gasInput = tr.querySelector('.gasamt');

    // Products that bundle the gas kit into their rate never carry a separate gas
    // kit charge — the field is locked at zero and hidden from editing.
    const gasIncludedInRate = !!product?.gasKitIncludedInRate;
    gasInput.disabled = gasIncludedInRate;

    // Gas kit amount tracks the formula default until the user edits it directly;
    // changing qty/length/rate/kit re-syncs it (discarding a manual override, same
    // as line total resets when those inputs change).
    if (source === 'inputs' || gasIncludedInRate) {
        gasInput.value = gasIncludedInRate || defaultGasAmount <= 0 ? '' : defaultGasAmount.toFixed(2);
    }
    const gasAmountRaw = parseFloat(gasInput.value);
    gasInput.classList.toggle('is-invalid', gasAmountRaw < 0);
    const gasAmount = Math.max(0, gasAmountRaw || 0);
    const combinedGross = lengthsGross + gasAmount;

    if (source === 'total') {
        let total = parseFloat(totalInput.value);
        const valid = !isNaN(total) && total >= 0 && total <= combinedGross;
        totalInput.classList.toggle('is-invalid', !valid);
        if (isNaN(total)) total = combinedGross;
        let net = Math.min(Math.max(total, 0), combinedGross);
        const pct = combinedGross > 0 ? (combinedGross - net) / combinedGross * 100 : 0;
        discInput.value = pct === 0 ? '' : pct.toFixed(2);
        tr.dataset.net = net;
    } else {
        const pct = parseFloat(discInput.value) || 0;
        discInput.classList.toggle('is-invalid', pct < 0 || pct > 100);
        const discAmount = Math.round(combinedGross * Math.min(Math.max(pct, 0), 100)) / 100;
        const net = Math.max(0, combinedGross - discAmount);
        totalInput.value = net.toFixed(2);
        totalInput.classList.remove('is-invalid');
        tr.dataset.net = net;
    }

    tr.dataset.gross = combinedGross;
    tr.querySelector('.qtytotal').textContent = weightBased
        ? (totalWeight > 0 ? totalWeight.toFixed(3) + ' kg' : '—')
        : (totalFeet > 0 ? totalFeet.toFixed(2) + ' ft' : '—');
    reindex();
    recalcTotals();
}

// Recomputes only the read-only running-total bookkeeping (dataset.net/gross used
// by recalcTotals, and the ft/kg cell) from whatever is currently in the row's
// inputs, WITHOUT touching disc %, gas kit amount, or line total. Used on load to
// reconcile restored stored values without re-deriving (and rounding-drifting) any
// of them — re-deriving % from an already-rounded total on every reload is exactly
// what causes a typed 7% to come back as 6.99%.
function refreshRowTotals(tr) {
    const { lengthsGross, totalFeet, totalWeight, weightBased } = amountsOf(tr);
    const gasAmount = Math.max(0, parseFloat(tr.querySelector('.gasamt').value) || 0);
    tr.dataset.net = parseFloat(tr.querySelector('.ltotal').value) || 0;
    tr.dataset.gross = lengthsGross + gasAmount;
    tr.querySelector('.qtytotal').textContent = weightBased
        ? (totalWeight > 0 ? totalWeight.toFixed(3) + ' kg' : '—')
        : (totalFeet > 0 ? totalFeet.toFixed(2) + ' ft' : '—');
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
        tr.querySelector('.len').name = `Items[${i}].LengthFt`;
        tr.querySelector('.qty').name = `Items[${i}].Quantity`;
        tr.querySelector('.gaskit').name = `Items[${i}].GasKitType`;
        tr.querySelector('.wtlen').name = `Items[${i}].WeightPerLength`;
        tr.querySelector('.rate').name = `Items[${i}].Rate`;
        tr.querySelector('.disc').name = `Items[${i}].DiscountPercent`;
        tr.querySelector('.gasamt').name = `Items[${i}].GasKitAmount`;
        tr.querySelector('.ltotal').name = `Items[${i}].LineTotal`;
    });
}

furtherDiscountInput.addEventListener('input', recalcTotals);

// Customer balance hint (window.pvcCustomers = [{id, balance}]).
const customers = window.pvcCustomers || [];
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
    // addRow()'s construction pass re-derives gas kit amount, line total, and
    // discount % from formula defaults — restore the actual stored values for all
    // three (trusting them outright, not re-deriving one from another) so a manual
    // gas kit override or a discount % survive a reload exactly as saved, then
    // just refresh the read-only running totals to match.
    [...body.querySelectorAll('tr')].forEach((tr, i) => {
        const row = initialRows[i];
        if (row) restoreStoredValues(tr, row);
    });
} else {
    addRow();
}
