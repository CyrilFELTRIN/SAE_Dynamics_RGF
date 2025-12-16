// Please see documentation at https://learn.microsoft.com/aspnet/core/client-side/bundling-and-minification
// for details on configuring this project to bundle and minify static web assets.

// Write your JavaScript code.
// Mise à jour des éléments avec des couleurs en dur

document.addEventListener('DOMContentLoaded', function () {
    const replacements = [
        { from: '#007bff', to: '#8b5cf6' },
        { from: '#0056b3', to: '#7c3aed' },
        { from: '#0d6efd', to: '#8b5cf6' },
        { from: '#0a58ca', to: '#7c3aed' },
        { from: '#0077cc', to: '#8b5cf6' },
        { from: '#1b6ec2', to: '#8b5cf6' },
        { from: '#1861ac', to: '#7c3aed' }
    ];

    const normalize = (v) => (v || '').toLowerCase();

    document.querySelectorAll('[style]').forEach((el) => {
        const style = el.getAttribute('style');
        if (!style) return;

        let next = style;
        for (const r of replacements) {
            const from = r.from.toLowerCase();
            const to = r.to;
            next = next.replace(new RegExp(from, 'gi'), to);
        }

        if (next !== style) {
            el.setAttribute('style', next);
        }
    });

    document.querySelectorAll('a, button, input, select, textarea').forEach((el) => {
        const c = normalize(getComputedStyle(el).color);
        const bg = normalize(getComputedStyle(el).backgroundColor);
        if ((c === 'rgb(139, 92, 246)' || c === 'rgb(124, 58, 237)') && (bg === 'rgb(139, 92, 246)' || bg === 'rgb(124, 58, 237)')) {
            el.style.color = '#ffffff';
        }
    });

    function parseSortValue(raw) {
        const v = (raw || '').toString().trim();
        if (!v) return '';

        const numeric = v.replace(/[^0-9,.-]/g, '').replace(',', '.');
        if (/^-?\d+(\.\d+)?$/.test(numeric)) {
            const n = Number(numeric);
            if (!Number.isNaN(n)) return n;
        }

        const m = v.match(/^(\d{2})\/(\d{2})\/(\d{4})$/);
        if (m) {
            const d = new Date(Number(m[3]), Number(m[2]) - 1, Number(m[1]));
            const t = d.getTime();
            if (!Number.isNaN(t)) return t;
        }

        return v.toLowerCase();
    }

    function initRolixTable(table) {
        const tbody = table.tBodies && table.tBodies[0];
        if (!tbody) return;

        const pageSize = Math.max(1, Number(table.getAttribute('data-rolix-page-size') || '10'));
        const wrapper = table.closest('[data-rolix-scope]') || document;

        const searchInput = wrapper.querySelector(`[data-rolix-search-for="${table.id}"]`);
        const filterSelect = wrapper.querySelector(`[data-rolix-filter-for="${table.id}"]`);
        const infoEl = wrapper.querySelector(`[data-rolix-info-for="${table.id}"]`);
        const paginationEl = wrapper.querySelector(`[data-rolix-pagination-for="${table.id}"]`);
        const paginationWrapper = wrapper.querySelector(`[data-rolix-pagination-wrapper-for="${table.id}"]`);

        let sortIndex = -1;
        let sortDir = 'asc';
        let page = 1;

        function getAllRows() {
            return Array.from(tbody.querySelectorAll('tr'));
        }

        function applyFilterAndSearch(rows) {
            const q = (searchInput ? searchInput.value : '').toString().trim().toLowerCase();
            const f = (filterSelect ? filterSelect.value : 'all').toString();

            return rows.filter((tr) => {
                if (f && f !== 'all') {
                    const status = (tr.getAttribute('data-status') || '').toString().toLowerCase();
                    if (status !== f.toLowerCase()) return false;
                }

                if (!q) return true;
                return (tr.innerText || tr.textContent || '').toString().toLowerCase().includes(q);
            });
        }

        function applySort(rows) {
            if (sortIndex < 0) return rows;

            const dir = sortDir === 'desc' ? -1 : 1;
            return rows.slice().sort((a, b) => {
                const aCell = a.children[sortIndex];
                const bCell = b.children[sortIndex];
                const av = parseSortValue(aCell ? aCell.getAttribute('data-sort') || aCell.innerText : '');
                const bv = parseSortValue(bCell ? bCell.getAttribute('data-sort') || bCell.innerText : '');

                if (av === bv) return 0;
                if (av > bv) return 1 * dir;
                return -1 * dir;
            });
        }

        function renderPagination(total, totalPages) {
            if (!paginationEl || !paginationWrapper) return;

            if (totalPages <= 1) {
                paginationWrapper.style.display = 'none';
                paginationEl.innerHTML = '';
                return;
            }

            paginationWrapper.style.display = '';

            const ul = document.createElement('ul');
            ul.className = 'pagination mb-0';

            function addItem(label, targetPage, disabled, active, ariaLabel) {
                const li = document.createElement('li');
                li.className = 'page-item' + (disabled ? ' disabled' : '') + (active ? ' active' : '');
                const a = document.createElement('a');
                a.className = 'page-link';
                a.href = '#';
                if (ariaLabel) a.setAttribute('aria-label', ariaLabel);
                a.textContent = label;
                a.addEventListener('click', (e) => {
                    e.preventDefault();
                    if (disabled) return;
                    page = targetPage;
                    render();
                });
                li.appendChild(a);
                ul.appendChild(li);
            }

            addItem('‹', Math.max(1, page - 1), page === 1, false, 'Previous');

            const maxButtons = 5;
            let start = Math.max(1, page - Math.floor(maxButtons / 2));
            let end = Math.min(totalPages, start + maxButtons - 1);
            start = Math.max(1, end - maxButtons + 1);

            for (let p = start; p <= end; p++) {
                addItem(String(p), p, false, p === page);
            }

            addItem('›', Math.min(totalPages, page + 1), page === totalPages, false, 'Next');

            paginationEl.innerHTML = '';
            paginationEl.appendChild(ul);
        }

        function renderInfo(startIndex, endIndex, total) {
            if (!infoEl) return;
            infoEl.textContent = `Affichage de ${startIndex} à ${endIndex} sur ${total} entrées`;
        }

        function render() {
            const all = getAllRows();
            const filtered = applyFilterAndSearch(all);
            const sorted = applySort(filtered);

            const total = sorted.length;
            const totalPages = Math.max(1, Math.ceil(total / pageSize));
            if (page > totalPages) page = totalPages;

            const start = (page - 1) * pageSize;
            const end = Math.min(start + pageSize, total);

            all.forEach((tr) => (tr.style.display = 'none'));
            sorted.slice(start, end).forEach((tr) => (tr.style.display = ''));

            renderInfo(total === 0 ? 0 : start + 1, end, total);
            renderPagination(total, totalPages);
        }

        const headers = Array.from(table.querySelectorAll('thead th'));
        headers.forEach((th, idx) => {
            if (th.hasAttribute('data-rolix-nosort')) return;
            th.style.cursor = 'pointer';
            th.addEventListener('click', () => {
                if (sortIndex === idx) {
                    sortDir = sortDir === 'asc' ? 'desc' : 'asc';
                } else {
                    sortIndex = idx;
                    sortDir = 'asc';
                }
                page = 1;
                render();
            });
        });

        if (searchInput) {
            searchInput.addEventListener('input', () => {
                page = 1;
                render();
            });
        }

        if (filterSelect) {
            filterSelect.addEventListener('change', () => {
                page = 1;
                render();
            });
        }

        render();
    }

    document.querySelectorAll('table[data-rolix-table]').forEach((t) => initRolixTable(t));

    // Gestion de la devise (EUR/CHF) pour les prix produit
    (function initCurrency() {
        const currencyToggle = document.getElementById('currency-toggle');

        function normalizeCurrency(v) {
            const raw = (v || '').toString().trim().toUpperCase();
            return raw === 'CHF' ? 'CHF' : 'EUR';
        }

        function getCurrency() {
            return normalizeCurrency(localStorage.getItem('currency') || 'EUR');
        }

        function setCurrency(next) {
            const c = normalizeCurrency(next);
            localStorage.setItem('currency', c);
            if (currencyToggle) {
                currencyToggle.checked = c === 'CHF';
            }
            applyCurrencyToPrices();
        }

        function formatPrice(value, currency) {
            const n = Number(value);
            if (Number.isNaN(n)) return null;
            try {
                const locale = (localStorage.getItem('language') || document.documentElement.lang || 'fr').startsWith('fr') ? 'fr-FR' : 'en-US';
                return new Intl.NumberFormat(locale, { style: 'currency', currency }).format(n);
            } catch {
                const symbol = currency === 'CHF' ? 'CHF' : '€';
                return `${n.toFixed(2)} ${symbol}`;
            }
        }

        function applyCurrencyToPrices() {
            const currency = getCurrency();
            document.querySelectorAll('.product-price').forEach((el) => {
                const eur = (el.getAttribute('data-price-eur') || '').toString().trim();
                const chf = (el.getAttribute('data-price-chf') || '').toString().trim();
                const raw = currency === 'CHF' ? chf : eur;

                if (!raw) {
                    el.style.display = 'none';
                    el.textContent = '';
                    return;
                }

                const formatted = formatPrice(raw, currency);
                if (!formatted) {
                    el.style.display = 'none';
                    el.textContent = '';
                    return;
                }

                el.textContent = formatted;
                el.style.display = '';
            });
        }

        // init UI
        setCurrency(getCurrency());

        if (currencyToggle) {
            currencyToggle.addEventListener('change', () => {
                setCurrency(currencyToggle.checked ? 'CHF' : 'EUR');
            });
        }

        // Si la langue change via le bouton existant, on reformate les prix
        const langToggle = document.getElementById('lang-toggle');
        if (langToggle) {
            langToggle.addEventListener('change', () => {
                setTimeout(applyCurrencyToPrices, 0);
            });
        }
    })();
});
