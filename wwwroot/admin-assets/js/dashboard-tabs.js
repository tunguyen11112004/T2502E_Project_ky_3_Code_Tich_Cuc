window.dashboardTabState = window.dashboardTabState || { mainTab: 'financial', statType: 'TotalRevenue' };

function updateDashboardMainTabUi(mainTab) {
    window.dashboardTabState.mainTab = mainTab;

    document.querySelectorAll('[data-main-tab]').forEach(function (btn) {
        const active = btn.dataset.mainTab === mainTab;
        btn.classList.toggle('border-blue-600', active);
        btn.classList.toggle('text-blue-600', active);
        btn.classList.toggle('dark:text-blue-400', active);
        btn.classList.toggle('border-transparent', !active);
        btn.classList.toggle('text-gray-500', !active);
        btn.classList.toggle('dark:text-gray-400', !active);
    });

    document.querySelectorAll('[data-sub-tabs]').forEach(function (panel) {
        panel.classList.toggle('hidden', panel.dataset.subTabs !== mainTab);
    });
}

function switchDashboardMainTab(mainTab, autoLoad) {
    updateDashboardMainTabUi(mainTab);

    if (autoLoad === false) {
        return;
    }

    const panel = document.querySelector('[data-sub-tabs="' + mainTab + '"]');
    const firstVisible = panel?.querySelector('.dashboard-sub-tab:not(.hidden)');
    if (firstVisible && typeof loadStatistic === 'function') {
        loadStatistic(firstVisible.dataset.statType);
    }
}

function setActiveSubTab(type) {
    window.dashboardTabState.statType = type;

    document.querySelectorAll('.dashboard-sub-tab').forEach(function (btn) {
        const active = btn.dataset.statType === type;
        btn.classList.toggle('bg-blue-600', active);
        btn.classList.toggle('text-white', active);
        btn.classList.toggle('border-blue-600', active);
        btn.classList.toggle('shadow-sm', active);
        btn.classList.toggle('bg-white', !active);
        btn.classList.toggle('dark:bg-gray-800', !active);
        btn.classList.toggle('text-gray-700', !active);
        btn.classList.toggle('dark:text-gray-300', !active);
        btn.classList.toggle('border-gray-200', !active);
        btn.classList.toggle('dark:border-gray-700', !active);
    });
}

function initDashboardTabs(defaultMainTab, defaultStatType, fromDate, toDate) {
    updateDashboardMainTabUi(defaultMainTab || 'financial');

    if (defaultStatType && typeof loadStatistic === 'function') {
        loadStatistic(defaultStatType, 1, fromDate || '', toDate || '');
        return;
    }

    const panel = document.querySelector('[data-sub-tabs="' + (defaultMainTab || 'financial') + '"]');
    const first = panel?.querySelector('.dashboard-sub-tab:not(.hidden)');
    if (first && typeof loadStatistic === 'function') {
        loadStatistic(first.dataset.statType, 1, fromDate || '', toDate || '');
    }
}
