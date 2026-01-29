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

    // Fonction pour normaliser une chaîne (suppression des accents, passage en minuscules)
    function normalize(str) {
        return (str || '')
            .toString()
            .normalize('NFD')
            .replace(/[\u0300-\u036f]/g, '') // Supprime les accents
            .toLowerCase()
            .replace(/[^a-z0-9\s]/g, ''); // Supprime la ponctuation
    }

    // Fonction de recherche floue avec distance de Levenshtein
    function fuzzyMatch(str, pattern) {
        // Si le motif est court, vérifie simplement l'inclusion
        if (pattern.length <= 3) {
            return normalize(str).includes(normalize(pattern));
        }

        // Pour les motifs plus longs, utilise la distance de Levenshtein
        const s = normalize(str);
        const p = normalize(pattern);

        // Si la chaîne contient le motif, c'est un bon match
        if (s.includes(p)) return true;

        // Si le motif est court, on est plus tolérant sur la distance
        const maxDistance = p.length <= 4 ? 1 : Math.max(1, Math.floor(p.length * 0.25));

        // Vérifie si on peut trouver le motif avec une distance de Levenshtein acceptable
        for (let i = 0; i <= s.length - p.length + maxDistance; i++) {
            const sub = s.substring(i, Math.min(i + p.length + maxDistance, s.length));
            if (levenshteinDistance(sub, p) <= maxDistance) {
                return true;
            }
        }

        return false;
    }

    // Calcul de la distance de Levenshtein
    function levenshteinDistance(a, b) {
        if (a.length === 0) return b.length;
        if (b.length === 0) return a.length;

        const matrix = [];

        // Initialisation de la première ligne et de la première colonne
        for (let i = 0; i <= b.length; i++) matrix[i] = [i];
        for (let j = 0; j <= a.length; j++) matrix[0][j] = j;

        // Remplissage de la matrice
        for (let i = 1; i <= b.length; i++) {
            for (let j = 1; j <= a.length; j++) {
                const cost = a[j-1] === b[i-1] ? 0 : 1;
                matrix[i][j] = Math.min(
                    matrix[i-1][j] + 1,    // Suppression
                    matrix[i][j-1] + 1,    // Insertion
                    matrix[i-1][j-1] + cost // Substitution
                );
            }
        }

        return matrix[b.length][a.length];
    }

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
            const fValue = (filterSelect ? filterSelect.value : 'all').toString();
            const selectedOption = filterSelect ? filterSelect.options[filterSelect.selectedIndex] : null;
            const fLabel = (selectedOption ? selectedOption.textContent : '').toString();

            return rows.filter((tr) => {
                if (fValue && fValue !== 'all') {
                    const statusAttr = (tr.getAttribute('data-status') || '').toString();
                    const status = statusAttr.toLowerCase();
                    const fLower = fValue.toLowerCase();

                    // Match either by value (e.g., numeric code) or by label (displayed text badge)
                    let match = status === fLower;
                    if (!match) {
                        const badgeText = (tr.querySelector('.status-badge')?.textContent || '').toString();
                        match = normalize(badgeText) === normalize(fLabel);
                    }
                    if (!match) return false;
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
            currencyToggle.addEventListener('change', function (e) {
                const newCurrency = this.checked ? 'CHF' : 'EUR';
                setCurrency(newCurrency);
                applyCurrencyToPrices();
            });
        }

        // Initialisation des tooltips Bootstrap
        var tooltipTriggerList = [].slice.call(document.querySelectorAll('[data-bs-toggle="tooltip"]'));
        var tooltipList = tooltipTriggerList.map(function (tooltipTriggerEl) {
            return new bootstrap.Tooltip(tooltipTriggerEl);
        });
    })();

    // Gestion de la recherche de produits + filtre + tri
    const searchInput = document.getElementById('searchInput');
    const clearSearchBtn = document.getElementById('clearSearch');
    const filterSelect = document.getElementById('parentFilter');
    const sortSelect = document.getElementById('sortSelect');
    const productsContainer = document.getElementById('productsContainer');
    const productCards = document.querySelectorAll('.product-card');

    if (searchInput && clearSearchBtn && productsContainer && productCards.length > 0) {
        let lastSearchValue = searchInput.value || '';

        function isFrench() {
            return document.documentElement.lang === 'fr' ||
                (document.documentElement.getAttribute('data-lang') || '').startsWith('fr');
        }

        // Mise à jour du placeholder en fonction de la langue
        function updateSearchPlaceholder() {
            searchInput.placeholder = isFrench()
                ? searchInput.getAttribute('data-lang-fr-placeholder')
                : searchInput.getAttribute('data-lang-en-placeholder');
        }

        function resetControls() {
            // Ne plus réinitialiser les valeurs ici
            // Les contrôles gardent leur état
        }

        function getCardName(card) {
            return (card.querySelector('h3')?.textContent || '').toString().trim();
        }

        function getCardCategory(card) {
            return (card.getAttribute('data-parent') || '').toString().trim();
        }

        function getCardPrice(card) {
            const priceEl = card.querySelector('.product-price');
            if (!priceEl) return null;
            const raw = (priceEl.getAttribute('data-price-eur') || '').toString().trim();
            if (!raw) return null;
            const n = Number(raw);
            return Number.isFinite(n) ? n : null;
        }

        function applySort(cards) {
            const mode = (sortSelect ? sortSelect.value : '').toString();
            if (!mode) return cards;

            const dir = mode.endsWith('-desc') ? -1 : 1;
            const key = mode.replace(/-(asc|desc)$/i, '');

            return cards.slice().sort((a, b) => {
                let av;
                let bv;

                if (key === 'name') {
                    av = getCardName(a).toLowerCase();
                    bv = getCardName(b).toLowerCase();
                } else if (key === 'category') {
                    av = getCardCategory(a).toLowerCase();
                    bv = getCardCategory(b).toLowerCase();
                } else if (key === 'price') {
                    av = getCardPrice(a);
                    bv = getCardPrice(b);
                    if (av == null && bv == null) return 0;
                    if (av == null) return 1;
                    if (bv == null) return -1;
                } else {
                    return 0;
                }

                if (av === bv) return 0;
                return av > bv ? 1 * dir : -1 * dir;
            });
        }

        function renderNoResults(show) {
            const existing = document.getElementById('noResultsMessage');
            if (!show) {
                if (existing) existing.remove();
                return;
            }

            if (existing) return;

            const noResults = document.createElement('div');
            noResults.id = 'noResultsMessage';
            noResults.className = 'col-12 text-center py-5';
            noResults.innerHTML = `
                <div class="mx-auto mb-3 d-flex align-items-center justify-content-center rounded-circle bg-light text-muted"
                     style="width: 64px; height: 64px;">
                    <i class="fas fa-search"></i>
                </div>
                <p class="text-muted mb-0" data-lang-fr="Aucun produit ne correspond à votre recherche."
                   data-lang-en="No products match your search.">
                    Aucun produit ne correspond à votre recherche.
                </p>
            `;
            productsContainer.appendChild(noResults);
        }

        function applyPipeline() {
            const term = (searchInput.value || '').toString().trim().toLowerCase();
            const searchActive = term.length > 0;
            
            // Mettre à jour l'état du bouton de réinitialisation
            clearSearchBtn.classList.toggle('d-none', !searchActive);
            
            // Récupérer tous les produits
            const all = Array.from(productCards);
            
            // Récupérer les valeurs actuelles des filtres et du tri
            const filterValue = filterSelect ? filterSelect.value : '';
            const sortValue = sortSelect ? sortSelect.value : '';
            
            // Étape 1: Filtrer par recherche (si terme de recherche)
            let filtered = searchActive 
                ? all.filter(card => {
                    const cardName = getCardName(card);
                    const cardCategory = getCardCategory(card);
                    
                    // Recherche dans le nom et la catégorie
                    return fuzzyMatch(cardName, term) || 
                           fuzzyMatch(cardCategory, term);
                })
                : all;
            
            // Étape 2: Appliquer le filtre (si sélectionné)
            if (filterValue) {
                filtered = filtered.filter(card => getCardCategory(card) === filterValue);
            }
            
            // Étape 3: Appliquer le tri (si sélectionné)
            let sorted = sortValue ? applySort(filtered) : filtered;
            
            // Mettre à jour l'UI
            updateUIBasedOnResults(sorted, all);
            
            // Mettre à jour la dernière valeur de recherche
            lastSearchValue = term;
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

        function updateUIBasedOnResults(visibleCards, allCards) {
            // Cacher tous les éléments d'abord
            allCards.forEach(card => card.style.display = 'none');
            
            // Afficher uniquement les cartes visibles
            visibleCards.forEach(card => card.style.display = '');
            
            // Réorganiser le DOM pour refléter l'ordre de tri
            visibleCards.forEach(card => productsContainer.appendChild(card));
            
            // Mettre à jour les prix selon la devise
            applyCurrencyToPrices();
            
            // Afficher le message "Aucun résultat" si nécessaire
            renderNoResults(visibleCards.length === 0 && (searchInput.value || '').trim().length > 0);
        }

        // Événements
        searchInput.addEventListener('input', applyPipeline);

        clearSearchBtn.addEventListener('click', () => {
            searchInput.value = '';
            applyPipeline();
            searchInput.focus();
        });

        searchInput.addEventListener('keydown', (e) => {
            if (e.key === 'Escape') {
                searchInput.value = '';
                applyPipeline();
            }
        });

        if (filterSelect) {
            filterSelect.addEventListener('change', () => {
                applyPipeline();
            });
        }

        if (sortSelect) {
            sortSelect.addEventListener('change', () => {
                applyPipeline();
            });
        }

        // Mettre à jour le placeholder au chargement
        updateSearchPlaceholder();

        // Écouter les changements de langue si nécessaire
        const observer = new MutationObserver(updateSearchPlaceholder);
        observer.observe(document.documentElement, {
            attributes: true,
            attributeFilter: ['lang', 'data-lang']
        });

        // Init état
        applyPipeline();
    }
});
