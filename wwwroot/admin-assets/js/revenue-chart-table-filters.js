(function () {
    function isSliceVisible(chart, index) {
        if (typeof chart.getDataVisibility === 'function') {
            return chart.getDataVisibility(index);
        }

        const meta = chart.getDatasetMeta(0);
        return meta?.data?.[index] ? !meta.data[index].hidden : true;
    }

    function toggleSliceVisibility(chart, index) {
        if (typeof chart.toggleDataVisibility === 'function') {
            chart.toggleDataVisibility(index);
            return;
        }

        const meta = chart.getDatasetMeta(0);
        if (meta?.data?.[index]) {
            meta.data[index].hidden = !meta.data[index].hidden;
        }
    }

    function getVisiblePieChartLabels(chart) {
        if (!chart || !chart.data || !chart.data.labels) {
            return null;
        }

        return chart.data.labels.filter(function (_, index) {
            return isSliceVisible(chart, index);
        });
    }

    function getBusClassChart() {
        return window.dashboardCharts?.totalRevenueChart
            || window.detailCharts?.detailTotalRevenueChart
            || null;
    }

    function getPaymentChart() {
        return window.dashboardCharts?.paymentMethodChart
            || window.detailCharts?.detailPaymentMethodChart
            || null;
    }

    window.applyRevenueChartTableFilters = function () {
        const visibleBusClasses = getVisiblePieChartLabels(getBusClassChart());
        const visiblePaymentMethods = getVisiblePieChartLabels(getPaymentChart());

        document.querySelectorAll('[data-filter-bus-class]').forEach(function (row) {
            const value = row.getAttribute('data-filter-bus-class') || '';
            const show = !visibleBusClasses || visibleBusClasses.includes(value);
            row.classList.toggle('hidden', !show);
        });

        document.querySelectorAll('[data-filter-payment-method]').forEach(function (row) {
            const value = row.getAttribute('data-filter-payment-method') || '';
            const show = !visiblePaymentMethods || visiblePaymentMethods.includes(value);
            row.classList.toggle('hidden', !show);
        });
    };

    window.createRevenuePieLegendClickHandler = function () {
        return function (event, legendItem, legend) {
            const chart = legend.chart;
            toggleSliceVisibility(chart, legendItem.index);
            chart.update();
            window.applyRevenueChartTableFilters();
        };
    };
})();
