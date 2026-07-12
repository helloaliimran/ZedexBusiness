// Dynamic PVC invoice lines. Expects window.pvcProducts, window.pvcCustomers,
// window.pvcInitialRows, window.pvcGasKitRate.
// Per line: lengths gross = (PerFoot: len × qty × rate) or (Weight: wt/len × qty × rate);
// gas kit = rate × (Single 1 / Double 2) × len × qty (never discounted);
// line total (editable) = lengths net + gas kit — editing it re-derives the discount %.
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

function addRow(data) {
    data = data || {};
    const tr = document.createElement('tr');
    const listId = `pvc-lengths-${rowSeq++}`;
    const product = findProduct(data.productId || 0);
    const gasKit = data.gasKitType != null ? gasKitName(data.gasKitType) : (product ? product.gasKit : 'None');
    tr.innerHTML = `
        <td class="position-relative">${ProductSearch.cellHtml(data.productId || 0)}</td>
        <td>
            <input type="number" min="0" step="0.01" list="${listId}" class="form-control form-control-sm len"
                   value="${data.lengthFt ?? ''}" placeholder="ft" oninput="updateRow(this, 'inputs')" />
            <datalist id="${listId}">${lengthOptions(product)}</datalist>
        </td>
        <td><input type="number" min="0" class="form-control form-control-sm qty" value="${data.quantity ?? ''}" oninput="updateRow(this, 'inputs')" /></td>
        <td>${gasKitSelect(gasKit)}</td>
        <td><input type="number" min="0" step="0.001" class="form-control form-control-sm wtlen" value="${data.weightPerLength ?? ''}" oninput="updateRow(this, 'inputs')" /></td>
        <td><input type="number" min="0" step="0.01" class="form-control form-control-sm rate" value="${data.rate ?? ''}" oninput="updateRow(this, 'inputs')" /></td>
        <td>
            <div class="input-group input-group-sm">
                <input type="number" min="0" max="100" step="0.01" class="form-control disc" value="${data.discountPercent || ''}" oninput="updateRow(this, 'percent')" placeholder="0" />
                <span class="input-group-text">%</span>
            </div>
        </td>
        <td class="text-end qtytotal text-muted">—</td>
        <td class="text-end gasamt text-muted">—</td>
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
        ? (weightBased ? 'Rs. per kg' : 'Rs. per running foot')
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
    const mult = gasMultiplier(tr.querySelector('.gaskit').value);

    const totalFeet = len * qty;
    const totalWeight = wtLen * qty;
    const lengthsGross = weightBased ? totalWeight * rate : totalFeet * rate;
    const gasAmount = Math.round(gasKitRate * mult * len * qty * 100) / 100;
    return { lengthsGross, gasAmount, totalFeet, totalWeight, weightBased };
}

// source: 'inputs' (qty/len/rate/kit/wt changed — keep %), 'percent', 'total'
function updateRow(el, source) {
    const tr = el.closest('tr');
    const { lengthsGross, gasAmount, totalFeet, totalWeight, weightBased } = amountsOf(tr);
    const discInput = tr.querySelector('.disc');
    const totalInput = tr.querySelector('.ltotal');

    if (source === 'total') {
        let total = parseFloat(totalInput.value);
        const valid = !isNaN(total) && total >= gasAmount && total <= lengthsGross + gasAmount;
        totalInput.classList.toggle('is-invalid', !valid);
        if (isNaN(total)) total = lengthsGross + gasAmount;
        let net = Math.min(Math.max(total - gasAmount, 0), lengthsGross);
        const pct = lengthsGross > 0 ? (lengthsGross - net) / lengthsGross * 100 : 0;
        discInput.value = pct === 0 ? '' : pct.toFixed(2);
        tr.dataset.net = net + gasAmount;
    } else {
        const pct = parseFloat(discInput.value) || 0;
        discInput.classList.toggle('is-invalid', pct < 0 || pct > 100);
        const discAmount = Math.round(lengthsGross * Math.min(Math.max(pct, 0), 100)) / 100;
        const net = Math.max(0, lengthsGross - discAmount);
        totalInput.value = (net + gasAmount).toFixed(2);
        totalInput.classList.remove('is-invalid');
        tr.dataset.net = net + gasAmount;
    }

    tr.dataset.gross = lengthsGross + gasAmount;
    tr.querySelector('.qtytotal').textContent = weightBased
        ? (totalWeight > 0 ? totalWeight.toFixed(3) + ' kg' : '—')
        : (totalFeet > 0 ? totalFeet.toFixed(2) + ' ft' : '—');
    tr.querySelector('.gasamt').textContent = gasAmount > 0 ? gasAmount.toFixed(2) : '—';
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
    // Preserve stored (possibly rounded) line totals on load.
    body.querySelectorAll('.ltotal').forEach(input => updateRow(input, 'total'));
} else {
    addRow();
}
