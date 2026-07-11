// Quick product entry grid. Expects window.quickLookups and window.quickInitialRows.
const lookups = window.quickLookups || { categories: [], colors: [], gauges: [] };
const initialRows = window.quickInitialRows || [];
const body = document.getElementById('rowsBody');

function options(list, selected) {
    let html = '<option value="0">— Select —</option>';
    for (const x of list) {
        html += `<option value="${x.id}" ${String(x.id) === String(selected ?? '') ? 'selected' : ''}>${x.name}</option>`;
    }
    return html;
}

function addRow(data, focusName) {
    data = data || {};
    const tr = document.createElement('tr');
    tr.innerHTML = `
        <td class="text-muted row-num"></td>
        <td><input type="text" class="form-control form-control-sm name" value="${(data.name ?? '').replace(/"/g, '&quot;')}" placeholder="Product name" /></td>
        <td><select class="form-select form-select-sm category">${options(lookups.categories, data.categoryId)}</select></td>
        <td><select class="form-select form-select-sm color">${options(lookups.colors, data.colorId)}</select></td>
        <td><select class="form-select form-select-sm gauge">${options(lookups.gauges, data.gaugeId)}</select></td>
        <td>
            <select class="form-select form-select-sm mode">
                <option value="PerUnit" ${data.pricingMode !== 'PerFoot' && data.pricingMode !== 2 ? 'selected' : ''}>Per Unit</option>
                <option value="PerFoot" ${data.pricingMode === 'PerFoot' || data.pricingMode === 2 ? 'selected' : ''}>Per Foot</option>
            </select>
        </td>
        <td><input type="number" min="0" step="0.01" class="form-control form-control-sm price" value="${data.price ?? ''}" placeholder="0.00" /></td>
        <td class="text-center text-nowrap">
            <button type="button" class="btn btn-sm btn-outline-secondary" title="Duplicate row" onclick="copyRow(this)"><i class="bi bi-files"></i></button>
            <button type="button" class="btn btn-sm btn-outline-danger" title="Remove row" onclick="removeRow(this)"><i class="bi bi-x-lg"></i></button>
        </td>`;

    // Enter in the price field starts the next row (copies category/color/gauge/mode).
    tr.querySelector('.price').addEventListener('keydown', e => {
        if (e.key === 'Enter') {
            e.preventDefault();
            if (tr === body.lastElementChild) {
                addRow({
                    categoryId: tr.querySelector('.category').value,
                    colorId: tr.querySelector('.color').value,
                    gaugeId: tr.querySelector('.gauge').value,
                    pricingMode: tr.querySelector('.mode').value
                }, true);
            } else {
                tr.nextElementSibling?.querySelector('.name')?.focus();
            }
        }
    });

    body.appendChild(tr);
    reindex();
    if (focusName) tr.querySelector('.name').focus();
}

function copyRow(btn) {
    const tr = btn.closest('tr');
    addRow({
        name: tr.querySelector('.name').value,
        categoryId: tr.querySelector('.category').value,
        colorId: tr.querySelector('.color').value,
        gaugeId: tr.querySelector('.gauge').value,
        pricingMode: tr.querySelector('.mode').value,
        price: tr.querySelector('.price').value
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
        tr.querySelector('.color').name = `Rows[${i}].ColorId`;
        tr.querySelector('.gauge').name = `Rows[${i}].GaugeId`;
        tr.querySelector('.mode').name = `Rows[${i}].PricingMode`;
        tr.querySelector('.price').name = `Rows[${i}].Price`;
    });
}

if (initialRows.length > 0) {
    for (const row of initialRows) addRow(row);
} else {
    for (let i = 0; i < 5; i++) addRow();
    body.querySelector('.name')?.focus();
}
