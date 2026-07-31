(function () {
    window.buildDashboardDetailUrl = function (action, fromDate, toDate, filterParam, filterValue) {
        const params = new URLSearchParams();
        if (fromDate) params.set('fromDate', fromDate);
        if (toDate) params.set('toDate', toDate);
        if (filterParam && filterValue) params.set(filterParam, filterValue);
        return `/Dashboard/${action}?${params.toString()}`;
    };

    window.toggleDashboardDetailCharts = function (button, sectionId, initFunctionName) {
        const section = document.getElementById(sectionId);
        if (!section) return;

        const willShow = section.classList.contains('hidden');
        section.classList.toggle('hidden', !willShow);

        const label = button?.querySelector('[data-chart-toggle-label]');
        if (label) {
            label.textContent = willShow ? 'Ẩn biểu đồ chi tiết' : 'Hiện biểu đồ chi tiết';
        }

        if (willShow) {
            const initFn = initFunctionName ? window[initFunctionName] : null;
            if (typeof initFn === 'function') {
                initFn();
            }
            section.scrollIntoView({ behavior: 'smooth', block: 'start' });
        }
    };

    window.renderDetailPieChart = function (chartId, labels, data, colors, labelText, options) {
        const canvas = document.getElementById(chartId);
        if (!canvas || typeof Chart === 'undefined') return;

        options = options || {};
        const valueSuffix = options.valueSuffix != null ? options.valueSuffix : ' đ';
        const enableTableFilter = options.enableTableFilter === true;
        const onSliceClick = options.onSliceClick;

        window.detailCharts = window.detailCharts || {};

        if (window.detailCharts[chartId]) {
            window.detailCharts[chartId].destroy();
            delete window.detailCharts[chartId];
        }

        const numericData = (data || []).map(function (v) { return Number(v || 0); });
        if (!numericData.some(function (v) { return v > 0; })) return;

        const isDark = document.documentElement.classList.contains('dark');
        const legendColor = isDark ? '#d1d5db' : '#4b5563';
        const chartColors = (colors && colors.length > 0)
            ? colors
            : ['#8b5cf6', '#10b981', '#0ea5e9', '#f59e0b', '#ef4444', '#6b7280'];

        const legendOptions = {
            position: 'bottom',
            labels: { color: legendColor, boxWidth: 12, padding: 12 }
        };

        if (enableTableFilter && typeof window.createRevenuePieLegendClickHandler === 'function') {
            legendOptions.onClick = window.createRevenuePieLegendClickHandler();
        }

        const piePlugins = window.pieChartExternalLabelPlugin ? [window.pieChartExternalLabelPlugin] : [];

        window.detailCharts[chartId] = new Chart(canvas.getContext('2d'), {
            type: 'pie',
            plugins: piePlugins,
            data: {
                labels: labels,
                datasets: [{
                    label: labelText,
                    data: numericData,
                    backgroundColor: chartColors,
                    borderWidth: 1,
                    borderColor: isDark ? '#1f2937' : '#ffffff'
                }]
            },
            options: {
                responsive: true,
                maintainAspectRatio: false,
                layout: typeof window.getPieChartLayoutPadding === 'function'
                    ? { padding: window.getPieChartLayoutPadding() }
                    : undefined,
                plugins: {
                    legend: legendOptions,
                    tooltip: {
                        callbacks: {
                            label: typeof window.createPieChartTooltipLabel === 'function'
                                ? window.createPieChartTooltipLabel(valueSuffix)
                                : function (context) {
                                    const value = Number(context.raw || 0);
                                    return context.label + ': ' + value.toLocaleString('vi-VN') + valueSuffix;
                                }
                        }
                    }
                },
                onClick: function (event, elements) {
                    if (elements.length > 0 && typeof onSliceClick === 'function') {
                        const url = onSliceClick(elements[0].index, labels);
                        if (url) window.location.href = url;
                    }
                },
                onHover: function (event, elements) {
                    if (typeof onSliceClick === 'function') {
                        event.native.target.style.cursor = elements.length ? 'pointer' : 'default';
                    }
                }
            }
        });

        if (enableTableFilter && typeof window.applyRevenueChartTableFilters === 'function') {
            window.applyRevenueChartTableFilters();
        }
    };
})();
