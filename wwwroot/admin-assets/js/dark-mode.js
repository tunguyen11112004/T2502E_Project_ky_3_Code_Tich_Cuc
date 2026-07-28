(function () {
  function isDarkPreferred() {
    const stored = localStorage.getItem('color-theme');
    if (stored === 'dark') return true;
    if (stored === 'light') return false;
    return true;
  }

  function refreshTailwind() {
    if (window.tailwind && typeof window.tailwind.refresh === 'function') {
      window.tailwind.refresh();
    }
  }

  function applyTheme(isDark) {
    document.documentElement.classList.toggle('dark', isDark);
    document.body?.classList.toggle('dark', isDark);

    const themeToggleDarkIcon = document.getElementById('theme-toggle-dark-icon');
    const themeToggleLightIcon = document.getElementById('theme-toggle-light-icon');

    if (themeToggleDarkIcon && themeToggleLightIcon) {
      themeToggleDarkIcon.classList.toggle('hidden', isDark);
      themeToggleLightIcon.classList.toggle('hidden', !isDark);
    }

    refreshTailwind();
  }

  function toggleTheme() {
    const isDark = !document.documentElement.classList.contains('dark');
    localStorage.setItem('color-theme', isDark ? 'dark' : 'light');
    applyTheme(isDark);
    document.dispatchEvent(new Event('dark-mode'));
  }

  function initThemeToggle() {
    const themeToggleBtn = document.getElementById('theme-toggle');
    if (!themeToggleBtn || themeToggleBtn.dataset.themeBound === 'true') return;

    themeToggleBtn.dataset.themeBound = 'true';
    applyTheme(isDarkPreferred());
    themeToggleBtn.addEventListener('click', toggleTheme);
  }

  if (document.readyState === 'loading') {
    document.addEventListener('DOMContentLoaded', initThemeToggle);
  } else {
    initThemeToggle();
  }
})();
