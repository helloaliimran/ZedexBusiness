// Lightweight click-to-sort for report/list tables.
// Usage: add class="sortable-table" to the <table>, and data-sort="text" or
// data-sort="number" to each sortable <th>. For numeric columns where the cell
// shows formatted text (e.g. "1,234 ft"), put the raw number on the <td> via
// data-sort-value="1234" so it sorts correctly.
(function () {
    function cellValue(td, type) {
        var raw = td.hasAttribute('data-sort-value') ? td.getAttribute('data-sort-value') : (td.textContent || '').trim();
        if (type === 'number') {
            var n = parseFloat(String(raw).replace(/,/g, ''));
            return isNaN(n) ? -Infinity : n;
        }
        return String(raw).toLowerCase();
    }

    function resetIcon(th) {
        var icon = th.querySelector('.sort-icon');
        if (icon) icon.className = 'bi bi-arrow-down-up ms-1 text-muted small sort-icon';
        th.removeAttribute('data-sort-dir');
    }

    function initTable(table) {
        var headers = Array.prototype.slice.call(table.querySelectorAll('thead th[data-sort]'));

        headers.forEach(function (th) {
            th.classList.add('sortable-col');
            th.style.cursor = 'pointer';
            th.style.whiteSpace = 'nowrap';
            var icon = document.createElement('i');
            icon.className = 'bi bi-arrow-down-up ms-1 text-muted small sort-icon';
            th.appendChild(icon);

            th.addEventListener('click', function () {
                var type = th.getAttribute('data-sort');
                var colIndex = Array.prototype.indexOf.call(th.parentElement.children, th);
                var tbody = table.querySelector('tbody');
                if (!tbody) return;

                var rows = Array.prototype.slice.call(tbody.querySelectorAll('tr'))
                    .filter(function (r) { return !r.querySelector('td[colspan]'); });
                if (rows.length === 0) return;

                var asc = th.getAttribute('data-sort-dir') !== 'asc';

                rows.sort(function (a, b) {
                    var av = cellValue(a.children[colIndex], type);
                    var bv = cellValue(b.children[colIndex], type);
                    if (av < bv) return asc ? -1 : 1;
                    if (av > bv) return asc ? 1 : -1;
                    return 0;
                });

                headers.forEach(resetIcon);
                th.setAttribute('data-sort-dir', asc ? 'asc' : 'desc');
                icon.className = 'bi ' + (asc ? 'bi-sort-down' : 'bi-sort-up') + ' ms-1 sort-icon';

                rows.forEach(function (r) { tbody.appendChild(r); });
            });
        });
    }

    document.addEventListener('DOMContentLoaded', function () {
        Array.prototype.forEach.call(document.querySelectorAll('table.sortable-table'), initTable);
    });
})();
