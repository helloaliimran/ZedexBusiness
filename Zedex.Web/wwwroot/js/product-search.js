// Shared searchable product combobox used by invoice and stock entry grids.
// Usage:
//   ProductSearch.init(productsArray);              // products need id, name; optionally product/color/gauge/category
//   td.innerHTML = ProductSearch.cellHtml(id);      // renders search input + hidden id + menu
//   ProductSearch.bind(td, (product, picked) => …); // picked=true when user chose from the list
//   ProductSearch.find(id)                          // look up a product
const ProductSearch = (() => {
    let products = [];

    function init(list) {
        products = list || [];
        // Every search token must match somewhere in "product color gauge category",
        // so e.g. "pipe white 14" narrows by product + color + gauge.
        for (const p of products) {
            p._search = `${p.product ?? p.name} ${p.color ?? ''} g${p.gauge ?? ''} ${p.gauge ?? ''} ${p.category ?? ''}`.toLowerCase();
        }
    }

    function find(id) {
        return products.find(p => p.id === id);
    }

    function filter(query) {
        const tokens = query.toLowerCase().split(/\s+/).filter(t => t);
        if (tokens.length === 0) return products;
        return products.filter(p => tokens.every(t => p._search.includes(t)));
    }

    function escapeAttr(s) {
        return String(s).replace(/&/g, '&amp;').replace(/"/g, '&quot;').replace(/</g, '&lt;');
    }

    function cellHtml(selectedId) {
        const product = find(selectedId || 0);
        return `
            <input type="text" class="form-control form-control-sm product-search" placeholder="Search product / color / gauge…"
                   autocomplete="off" value="${product ? escapeAttr(product.name) : ''}" />
            <input type="hidden" class="product-id" value="${selectedId || 0}" />
            <div class="dropdown-menu product-menu w-100"></div>`;
    }

    function renderMenu(td, query) {
        const menu = td.querySelector('.product-menu');
        const matches = filter(query).slice(0, 30);
        if (matches.length === 0) {
            menu.innerHTML = '<div class="dropdown-item disabled text-muted">No products match</div>';
        } else {
            menu.innerHTML = matches.map((p, i) =>
                `<button type="button" class="dropdown-item text-wrap ${i === 0 ? 'active' : ''}" data-id="${p.id}">${p.name}</button>`).join('');
        }
        menu.classList.add('show');
    }

    function closeMenu(td) {
        td.querySelector('.product-menu')?.classList.remove('show');
    }

    function bind(td, onChange) {
        const input = td.querySelector('.product-search');
        const menu = td.querySelector('.product-menu');
        const idInput = td.querySelector('.product-id');

        function pick(id) {
            const product = find(id);
            idInput.value = product ? product.id : 0;
            input.value = product ? product.name : '';
            closeMenu(td);
            onChange(product ?? null, true);
        }

        input.addEventListener('input', () => {
            // Typing invalidates the previous selection until a product is picked.
            idInput.value = 0;
            onChange(null, false);
            renderMenu(td, input.value);
        });
        input.addEventListener('focus', () => renderMenu(td, input.value));
        input.addEventListener('keydown', e => {
            const items = [...menu.querySelectorAll('.dropdown-item:not(.disabled)')];
            const activeIndex = items.findIndex(x => x.classList.contains('active'));
            if (e.key === 'ArrowDown' || e.key === 'ArrowUp') {
                e.preventDefault();
                if (!menu.classList.contains('show')) { renderMenu(td, input.value); return; }
                if (items.length === 0) return;
                const next = e.key === 'ArrowDown'
                    ? Math.min(activeIndex + 1, items.length - 1)
                    : Math.max(activeIndex - 1, 0);
                items.forEach(x => x.classList.remove('active'));
                items[next].classList.add('active');
                items[next].scrollIntoView({ block: 'nearest' });
            } else if (e.key === 'Enter') {
                if (menu.classList.contains('show') && activeIndex >= 0) {
                    e.preventDefault();
                    pick(parseInt(items[activeIndex].dataset.id));
                }
            } else if (e.key === 'Escape') {
                closeMenu(td);
            }
        });
        // mousedown fires before the input's blur, so the click always lands.
        menu.addEventListener('mousedown', e => {
            const item = e.target.closest('.dropdown-item[data-id]');
            if (item) { e.preventDefault(); pick(parseInt(item.dataset.id)); }
        });
        input.addEventListener('blur', () => setTimeout(() => {
            closeMenu(td);
            // Restore the name of the still-selected product if the text was left half-edited.
            const product = find(parseInt(idInput.value));
            input.value = product ? product.name : '';
        }, 150));
    }

    return { init, find, cellHtml, bind };
})();
