// Dynamic stock entry lines. Expects window.stockProducts and window.stockInitialRows.
// Requires product-search.js (ProductSearch) to be loaded first.
const initialRows = window.stockInitialRows || [];
const body = document.getElementById('linesBody');

ProductSearch.init(window.stockProducts || []);

function addRow(data) {
    data = data || {};
    const tr = document.createElement('tr');
    tr.innerHTML = `
        <td class="position-relative">${ProductSearch.cellHtml(data.productId || 0)}</td>
        <td><input type="number" min="0" class="form-control form-control-sm qty" value="${data.quantity ?? ''}" oninput="recalc(this)" /></td>
        <td><input type="number" min="0" class="form-control form-control-sm cartons" value="${data.cartons ?? ''}" oninput="recalc(this)" /></td>
        <td><input type="number" min="0" class="form-control form-control-sm ipc" value="${data.itemsPerCarton ?? ''}" oninput="recalc(this)" /></td>
        <td><input type="number" min="0" step="0.01" class="form-control form-control-sm len" value="${data.lengthFt ?? ''}" oninput="recalc(this)" /></td>
        <td class="text-end total fw-semibold">0</td>
        <td class="text-end feet text-muted">—</td>
        <td class="text-center">
            <button type="button" class="btn btn-sm btn-outline-danger" onclick="removeRow(this)"><i class="bi bi-x-lg"></i></button>
        </td>`;
    body.appendChild(tr);
    ProductSearch.bind(tr.querySelector('td'), () => rowChanged(tr));
    rowChanged(tr);
}

function removeRow(btn) {
    btn.closest('tr').remove();
    reindex();
}

function rowChanged(tr) {
    const product = ProductSearch.find(parseInt(tr.querySelector('.product-id').value));
    const lenInput = tr.querySelector('.len');
    const perFoot = product && product.mode === 'PerFoot';
    lenInput.disabled = !perFoot;
    if (!perFoot) lenInput.value = '';
    lenInput.placeholder = perFoot ? 'required' : 'n/a';
    recalc(tr);
}

function recalc(el) {
    const tr = el.closest('tr');
    const qty = parseInt(tr.querySelector('.qty').value) || 0;
    const cartons = parseInt(tr.querySelector('.cartons').value) || 0;
    const ipc = parseInt(tr.querySelector('.ipc').value) || 0;
    const len = parseFloat(tr.querySelector('.len').value) || 0;
    const total = qty + cartons * ipc;
    tr.querySelector('.total').textContent = total;
    const perFoot = !tr.querySelector('.len').disabled;
    tr.querySelector('.feet').textContent = perFoot && len > 0 ? (total * len).toFixed(2) + ' ft' : '—';
    reindex();
}

// Assign sequential Details[i].X names so MVC model binding works.
function reindex() {
    [...body.querySelectorAll('tr')].forEach((tr, i) => {
        tr.querySelector('.product-id').name = `Details[${i}].ProductId`;
        tr.querySelector('.qty').name = `Details[${i}].Quantity`;
        tr.querySelector('.cartons').name = `Details[${i}].Cartons`;
        tr.querySelector('.ipc').name = `Details[${i}].ItemsPerCarton`;
        tr.querySelector('.len').name = `Details[${i}].LengthFt`;
    });
}

if (initialRows.length > 0) {
    for (const row of initialRows) addRow(row);
} else {
    addRow();
}
