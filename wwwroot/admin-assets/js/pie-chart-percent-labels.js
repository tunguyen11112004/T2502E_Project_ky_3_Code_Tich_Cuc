(function () {
    if (typeof Chart === 'undefined') return;

    function getDatasetTotal(dataset) {
        return (dataset || []).reduce(function (sum, value) {
            return sum + Number(value || 0);
        }, 0);
    }

    window.formatPieChartPercent = function (value, total) {
        if (!total || total <= 0) return '0%';
        var pct = Number(value || 0) / total * 100;
        var rounded = Math.round(pct * 10) / 10;
        if (Math.abs(rounded - Math.round(rounded)) < 0.05) {
            return Math.round(rounded) + '%';
        }
        return rounded.toFixed(1) + '%';
    };

    window.getPieChartLayoutPadding = function () {
        return { top: 28, bottom: 28, left: 56, right: 56 };
    };

    window.createPieChartTooltipLabel = function (valueSuffix) {
        var suffix = valueSuffix != null ? valueSuffix : '';

        return function (context) {
            var dataset = context.dataset.data.map(function (v) { return Number(v || 0); });
            var total = getDatasetTotal(dataset);
            var value = Number(context.raw || 0);
            var percent = window.formatPieChartPercent(value, total);
            return context.label + ': ' + value.toLocaleString('vi-VN') + suffix + ' (' + percent + ')';
        };
    };

    function getArcCenter(element) {
        if (typeof element.getProps === 'function') {
            var props = element.getProps(['x', 'y', 'outerRadius'], true);
            return {
                x: props.x,
                y: props.y,
                outerRadius: props.outerRadius
            };
        }

        return {
            x: element.x,
            y: element.y,
            outerRadius: element.outerRadius
        };
    }

    function getSliceColor(dataset, index) {
        var colors = dataset.backgroundColor;
        if (Array.isArray(colors)) {
            return colors[index] || '#374151';
        }
        return colors || '#374151';
    }

    function getCalloutPosition(element) {
        var center = getArcCenter(element);
        var tip = element.tooltipPosition();
        var angle = Math.atan2(tip.y - center.y, tip.x - center.x);
        var cos = Math.cos(angle);
        var sin = Math.sin(angle);
        var outerRadius = center.outerRadius;

        if (!outerRadius || outerRadius <= 0) {
            outerRadius = Math.hypot(tip.x - center.x, tip.y - center.y) * 1.5;
        }

        var radialGap = 14;
        var horizontalGap = 22;
        var edgeX = center.x + cos * outerRadius;
        var edgeY = center.y + sin * outerRadius;
        var elbowX = center.x + cos * (outerRadius + radialGap);
        var elbowY = center.y + sin * (outerRadius + radialGap);
        var labelX = elbowX + (cos >= 0 ? horizontalGap : -horizontalGap);
        var labelY = elbowY;

        return {
            edgeX: edgeX,
            edgeY: edgeY,
            elbowX: elbowX,
            elbowY: elbowY,
            labelX: labelX,
            labelY: labelY,
            isRight: cos >= 0
        };
    }

    window.drawExternalPiePercentLabels = function (chart) {
        var chartType = chart.config.type;
        if (chartType !== 'pie' && chartType !== 'doughnut') return;

        var dataset = chart.data.datasets[0];
        if (!dataset || !dataset.data || dataset.data.length === 0) return;

        var numericData = dataset.data.map(function (v) { return Number(v || 0); });
        var total = getDatasetTotal(numericData);
        if (total <= 0) return;

        var meta = chart.getDatasetMeta(0);
        var ctx = chart.ctx;

        meta.data.forEach(function (element, index) {
            if (typeof chart.getDataVisibility === 'function' && !chart.getDataVisibility(index)) return;

            var value = numericData[index];
            if (value <= 0) return;

            var percentText = window.formatPieChartPercent(value, total);
            var sliceColor = getSliceColor(dataset, index);
            var callout = getCalloutPosition(element);

            ctx.save();

            ctx.beginPath();
            ctx.strokeStyle = sliceColor;
            ctx.lineWidth = 1.5;
            ctx.lineCap = 'round';
            ctx.lineJoin = 'round';
            ctx.moveTo(callout.edgeX, callout.edgeY);
            ctx.lineTo(callout.elbowX, callout.elbowY);
            ctx.lineTo(callout.labelX, callout.labelY);
            ctx.stroke();

            ctx.font = '700 13px Inter, system-ui, sans-serif';
            ctx.textAlign = callout.isRight ? 'left' : 'right';
            ctx.textBaseline = 'middle';
            ctx.fillStyle = sliceColor;
            ctx.fillText(percentText, callout.labelX, callout.labelY);

            ctx.restore();
        });
    };

    window.pieChartExternalLabelPlugin = {
        id: 'pieExternalPercentLabels',
        afterDraw: function (chart) {
            window.drawExternalPiePercentLabels(chart);
        }
    };

    if (!Chart.registry.getPlugin('pieExternalPercentLabels')) {
        Chart.register(window.pieChartExternalLabelPlugin);
    }
})();
