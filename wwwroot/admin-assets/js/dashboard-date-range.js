window.dashboardRangePickers = window.dashboardRangePickers || {};
window.dashboardDateRangeApplyHandlers = window.dashboardDateRangeApplyHandlers || {};

function toDateInputValue(date) {
    const year = date.getFullYear();
    const month = String(date.getMonth() + 1).padStart(2, '0');
    const day = String(date.getDate()).padStart(2, '0');
    return `${year}-${month}-${day}`;
}

function parseDateInputValue(value) {
    if (!value) return new Date();
    const parts = value.split('-').map(Number);
    return new Date(parts[0], parts[1] - 1, parts[2]);
}

function addDays(date, days) {
    const cloned = new Date(date.getFullYear(), date.getMonth(), date.getDate());
    cloned.setDate(cloned.getDate() + days);
    return cloned;
}

function getDashboardPresetRange(presetKey) {
    const today = new Date();
    const current = new Date(today.getFullYear(), today.getMonth(), today.getDate());

    switch (presetKey) {
        case 'today': return { from: current, to: current };
        case 'yesterday': return { from: addDays(current, -1), to: addDays(current, -1) };
        case 'last7': return { from: addDays(current, -6), to: current };
        case 'last30': return { from: addDays(current, -29), to: current };
        case 'last90': return { from: addDays(current, -89), to: current };
        case 'weekToDate': {
            const firstDay = addDays(current, -current.getDay());
            return { from: firstDay, to: current };
        }
        case 'monthToDate': return { from: new Date(current.getFullYear(), current.getMonth(), 1), to: current };
        default: return null;
    }
}

function positionDashboardRangePopup(input, popup) {
    const rect = input.getBoundingClientRect();
    const popupWidth = Math.min(720, window.innerWidth - 32);
    let left = rect.left;
    if (left + popupWidth > window.innerWidth - 16) left = window.innerWidth - popupWidth - 16;
    if (left < 16) left = 16;

    popup.style.width = `${popupWidth}px`;
    popup.style.left = `${left}px`;
    popup.style.top = `${rect.bottom + 8}px`;
}

function closeDashboardRangePicker(type) {
    const state = window.dashboardRangePickers[type];
    if (!state) return;

    if (state.popup) state.popup.remove();
    if (state.closeHandler) document.removeEventListener('mousedown', state.closeHandler);
    if (state.resizeHandler) {
        window.removeEventListener('resize', state.resizeHandler);
        window.removeEventListener('scroll', state.resizeHandler, true);
    }
    delete window.dashboardRangePickers[type];
}

function applyDashboardDateRange(type, start, end) {
    const handlers = window.dashboardDateRangeApplyHandlers || {};
    if (typeof handlers[type] === 'function') {
        handlers[type](start, end, type);
        return;
    }

    if (typeof loadStatistic === 'function') {
        loadStatistic(type, 1, start, end);
    }
}

function initializeDashboardDateRangePicker(type, fromDate, toDate) {
    const input = document.getElementById(`date_range_${type}`);
    if (!input) return;

    closeDashboardRangePicker(type);

    const fallbackFrom = typeof currentFromDate !== 'undefined' && currentFromDate ? currentFromDate : toDateInputValue(addDays(new Date(), -30));
    const fallbackTo = typeof currentToDate !== 'undefined' && currentToDate ? currentToDate : toDateInputValue(new Date());
    const defaultFromDate = fromDate || fallbackFrom;
    const defaultToDate = toDate || fallbackTo;
    input.value = `${defaultFromDate} - ${defaultToDate}`;

    input.addEventListener('click', function (event) {
        event.preventDefault();
        event.stopPropagation();

        if (window.dashboardRangePickers[type]) {
            closeDashboardRangePicker(type);
            return;
        }

        const popup = document.createElement('div');
        popup.id = `dashboard_range_popup_${type}`;
        popup.className = 'dashboard-range-popup fixed z-[9999] rounded-xl border border-gray-700 bg-white text-gray-900 dark:bg-[#111827] dark:text-white overflow-hidden';
        popup.innerHTML = `
            <div class="grid grid-cols-1 md:grid-cols-[220px_1fr]">
                <div class="border-b md:border-b-0 md:border-r border-gray-200 dark:border-gray-700 bg-gray-50 dark:bg-gray-900 p-3 space-y-1">
                    <button type="button" data-preset="custom" class="dashboard-preset-btn w-full text-left rounded-lg px-3 py-2 text-sm font-medium bg-blue-100 text-blue-700 dark:bg-blue-900/50 dark:text-blue-200">Custom</button>
                    <button type="button" data-preset="today" class="dashboard-preset-btn w-full text-left rounded-lg px-3 py-2 text-sm hover:bg-gray-200 dark:hover:bg-gray-800">Today</button>
                    <button type="button" data-preset="yesterday" class="dashboard-preset-btn w-full text-left rounded-lg px-3 py-2 text-sm hover:bg-gray-200 dark:hover:bg-gray-800">Yesterday</button>
                    <button type="button" data-preset="last7" class="dashboard-preset-btn w-full text-left rounded-lg px-3 py-2 text-sm hover:bg-gray-200 dark:hover:bg-gray-800">Last 7 days</button>
                    <button type="button" data-preset="last30" class="dashboard-preset-btn w-full text-left rounded-lg px-3 py-2 text-sm hover:bg-gray-200 dark:hover:bg-gray-800">Last 30 days</button>
                    <button type="button" data-preset="last90" class="dashboard-preset-btn w-full text-left rounded-lg px-3 py-2 text-sm hover:bg-gray-200 dark:hover:bg-gray-800">Last 90 days</button>
                    <button type="button" data-preset="weekToDate" class="dashboard-preset-btn w-full text-left rounded-lg px-3 py-2 text-sm hover:bg-gray-200 dark:hover:bg-gray-800">Week to date</button>
                    <button type="button" data-preset="monthToDate" class="dashboard-preset-btn w-full text-left rounded-lg px-3 py-2 text-sm hover:bg-gray-200 dark:hover:bg-gray-800">Month to date</button>
                </div>
                <div class="p-4 space-y-4">
                    <div class="grid grid-cols-[1fr_auto_1fr] items-center gap-3">
                        <input type="date" data-range-start class="rounded-lg border border-gray-300 bg-white px-3 py-2 text-sm text-gray-900 dark:border-gray-700 dark:bg-gray-900 dark:text-white" value="${defaultFromDate}" />
                        <span class="text-gray-400">→</span>
                        <input type="date" data-range-end class="rounded-lg border border-gray-300 bg-white px-3 py-2 text-sm text-gray-900 dark:border-gray-700 dark:bg-gray-900 dark:text-white" value="${defaultToDate}" />
                    </div>
                    <div class="flex justify-end gap-3 border-t border-gray-200 dark:border-gray-700 pt-4">
                        <button type="button" data-range-cancel class="rounded-lg border border-gray-300 px-4 py-2 text-sm font-semibold text-gray-700 hover:bg-gray-100 dark:border-gray-600 dark:text-gray-200 dark:hover:bg-gray-800">Cancel</button>
                        <button type="button" data-range-apply class="rounded-lg bg-emerald-500 px-4 py-2 text-sm font-semibold text-white hover:bg-emerald-600">Apply</button>
                    </div>
                </div>
            </div>
        `;

        document.body.appendChild(popup);
        positionDashboardRangePopup(input, popup);

        const startInput = popup.querySelector('[data-range-start]');
        const endInput = popup.querySelector('[data-range-end]');

        popup.querySelectorAll('[data-preset]').forEach(button => {
            button.addEventListener('click', function () {
                popup.querySelectorAll('[data-preset]').forEach(item => {
                    item.classList.remove('bg-blue-100', 'text-blue-700', 'dark:bg-blue-900/50', 'dark:text-blue-200');
                });
                this.classList.add('bg-blue-100', 'text-blue-700', 'dark:bg-blue-900/50', 'dark:text-blue-200');
                const range = getDashboardPresetRange(this.dataset.preset);
                if (!range) return;
                startInput.value = toDateInputValue(range.from);
                endInput.value = toDateInputValue(range.to);
            });
        });

        function applySelectedRange() {
            let start = startInput.value;
            let end = endInput.value;
            if (!start || !end) return;
            if (parseDateInputValue(start) > parseDateInputValue(end)) {
                const temp = start;
                start = end;
                end = temp;
            }
            input.value = `${start} - ${end}`;
            closeDashboardRangePicker(type);
            applyDashboardDateRange(type, start, end);
        }

        popup.querySelector('[data-range-apply]').addEventListener('click', applySelectedRange);
        popup.querySelector('[data-range-cancel]').addEventListener('click', () => closeDashboardRangePicker(type));

        const closeHandler = (e) => {
            if (!popup.contains(e.target) && !input.contains(e.target)) closeDashboardRangePicker(type);
        };
        const resizeHandler = () => {
            if (document.body.contains(popup)) positionDashboardRangePopup(input, popup);
        };

        document.addEventListener('mousedown', closeHandler);
        window.addEventListener('resize', resizeHandler);
        window.addEventListener('scroll', resizeHandler, true);

        window.dashboardRangePickers[type] = { popup, closeHandler, resizeHandler };
    });
}

function getDashboardDateRangeValues(type, fallbackFrom, fallbackTo) {
    const input = document.getElementById(`date_range_${type}`);
    if (input?.value?.includes(' - ')) {
        const parts = input.value.split(' - ');
        return { start: parts[0].trim(), end: parts[1].trim() };
    }

    return { start: fallbackFrom, end: fallbackTo };
}
