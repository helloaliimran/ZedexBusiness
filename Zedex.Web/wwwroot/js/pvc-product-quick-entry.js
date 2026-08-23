// Quick PVC product entry grid. Expects window.quickLookups and window.quickInitialRows.
const lookups = window.quickLookups || { categories: [], companies: [], colors: [], gauges: [] };
const initialRows = window.quickInitialRows || [];
const body = document.getElementById('rowsBody');

function options(list, selected) {
    let html = '<option value="0">— Select —</option>';
    for (const x of list) {
        html += `<option value="${x.id}" ${String(x.id) === String(selected ?? '') ? 'selected' : ''}>${x.name}</option>`;
    }
    return html;
}

// JSON-serialized enums arrive as numbers (SaleType: 1=PerFoot, 2=Weight, 3=RateLength; GasKit: 1=None, 2=Single, 3=Double).
function saleTypeName(value) {
    if (value === 2 || value === 'WeightPerLength') return 'WeightPerLength';
    if (value === 3 || value === 'RatePerLength') return 'RatePerLength';
    return 'PerRunningFoot';
}
function gasKitName(value) {
    if (value === 2 || value === 'Single') return 'Single';
    if (value === 3 || value === 'Double') return 'Double';
    return 'None';
}

function syncWeight(tr) {
    const weightBased = tr.querySelector('.saletype').value === 'WeightPerLength';
    const wt = tr.querySelector('.wtlen');
    wt.disabled = !weightBased;
    wt.placeholder = weightBased ? 'kg' : 'n/a';
    if (!weightBased) wt.value = '';
}

function addRow(data, focusName) {
    data = data || {};
    const saleType = data.saleType != null ? saleTypeName(data.saleType) : 'PerRunningFoot';
    const gasKit = data.gasKitType != null ? gasKitName(data.gasKitType) : 'None';
    const tr = document.createElement('tr');
    tr.innerHTML = `
        <td class="text-muted row-num"></td>
        <td><input type="text" class="form-control form-control-sm name" value="${(data.name ?? '').replace(/"/g, '&quot;')}" placeholder="Section name" /></td>
        <td><select class="form-select form-select-sm category">${options(lookups.categories, data.categoryId)}</select></td>
        <td><select class="form-select form-select-sm company">${options(lookups.companies, data.companyId)}</select></td>
        <td><select class="form-select form-select-sm color">${options(lookups.colors, data.colorId)}</select></td>
        <td><select class="form-select form-select-sm gauge">${options(lookups.gauges, data.gaugeId)}</select></td>
        <td>
            <select class="form-select form-select-sm saletype">
                <option value="PerRunningFoot" ${saleType === 'PerRunningFoot' ? 'selected' : ''}>Per Running Ft</option>
                <option value="WeightPerLength" ${saleType === 'WeightPerLength' ? 'selected' : ''}>Weight / Length</option>
                <option value="RatePerLength" ${saleType === 'RatePerLength' ? 'selected' : ''}>Rate / Length</option>
            </select>
        </td>
        <td>
            <select class="form-select form-select-sm gaskit">
                <option value="None" ${gasKit === 'None' ? 'selected' : ''}>No Kit</option>
                <option value="Single" ${gasKit === 'Single' ? 'selected' : ''}>Single</option>
                <option value="Double" ${gasKit === 'Double' ? 'selected' : ''}>Double</option>
            </select>
        </td>
        <td class="text-center">
            <input type="checkbox" class="form-check-input kitincl" ${data.gasKitPriceIncludedInRate ? 'checked' : ''} />
        </td>
        <td><input type="number" min="0" step="0.01" class="form-control form-control-sm price" value="${data.price ?? ''}" placeholder="0.00" /></td>
        <td><input type="number" min="0" step="0.001" class="form-control form-control-sm wtlen" value="${data.weightPerLength ?? ''}" /></td>
        <td class="text-center text-nowrap">
            <button type="button" class="btn btn-sm btn-outline-secondary" title="Duplicate row" onclick="copyRow(this)"><i class="bi bi-files"></i></button>
            <button type="button" class="btn btn-sm btn-outline-danger" title="Remove row" onclick="removeRow(this)"><i class="bi bi-x-lg"></i></button>
        </td>`;

    tr.querySelector('.saletype').addEventListener('change', () => syncWeight(tr));

    // Enter in the last numeric field starts the next row (copies the shared selects).
    function nextRowOnEnter(e) {
        if (e.key === 'Enter') {
            e.preventDefault();
            if (tr === body.lastElementChild) {
                addRow({
                    categoryId: tr.querySelector('.category').value,
                    companyId: tr.querySelector('.company').value,
                    colorId: tr.querySelector('.color').value,
                    gaugeId: tr.querySelector('.gauge').value,
                    saleType: tr.querySelector('.saletype').value,
                    gasKitType: tr.querySelector('.gaskit').value,
                    gasKitPriceIncludedInRate: tr.querySelector('.kitincl').checked
                }, true);
            } else {
                tr.nextElementSibling?.querySelector('.name')?.focus();
            }
        }
    }
    tr.querySelector('.price').addEventListener('keydown', nextRowOnEnter);
    tr.querySelector('.wtlen').addEventListener('keydown', nextRowOnEnter);

    body.appendChild(tr);
    syncWeight(tr);
    reindex();
    if (focusName) tr.querySelector('.name').focus();
}

function copyRow(btn) {
    const tr = btn.closest('tr');
    addRow({
        name: tr.querySelector('.name').value,
        categoryId: tr.querySelector('.category').value,
        companyId: tr.querySelector('.company').value,
        colorId: tr.querySelector('.color').value,
        gaugeId: tr.querySelector('.gauge').value,
        saleType: tr.querySelector('.saletype').value,
        gasKitType: tr.querySelector('.gaskit').value,
        gasKitPriceIncludedInRate: tr.querySelector('.kitincl').checked,
        price: tr.querySelector('.price').value,
        weightPerLength: tr.querySelector('.wtlen').value
    }, true);
}

function removeRow(btn) {
    btn.closest('tr').remove();
    if (body.children.length === 0) addRow();
    reindex();
}

// Assign sequential Rows[i].X names so MVC model binding works.
function reindex() {
    [...body.querySelectorAll('tr')].forEach((tr, i) => {
        tr.querySelector('.row-num').textContent = i + 1;
        tr.querySelector('.name').name = `Rows[${i}].Name`;
        tr.querySelector('.category').name = `Rows[${i}].CategoryId`;
        tr.querySelector('.company').name = `Rows[${i}].CompanyId`;
        tr.querySelector('.color').name = `Rows[${i}].ColorId`;
        tr.querySelector('.gauge').name = `Rows[${i}].GaugeId`;
        tr.querySelector('.saletype').name = `Rows[${i}].SaleType`;
        tr.querySelector('.gaskit').name = `Rows[${i}].GasKitType`;
        tr.querySelector('.kitincl').name = `Rows[${i}].GasKitPriceIncludedInRate`;
        tr.querySelector('.price').name = `Rows[${i}].Price`;
        tr.querySelector('.wtlen').name = `Rows[${i}].WeightPerLength`;
    });
}

if (initialRows.length > 0) {
    for (const row of initialRows) addRow(row);
} else {
    for (let i = 0; i < 5; i++) addRow();
    body.querySelector('.name')?.focus();
}
