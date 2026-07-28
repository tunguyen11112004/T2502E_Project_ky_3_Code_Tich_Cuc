const themeToggleDarkIcon = document.getElementById('theme-toggle-dark-icon');
const themeToggleLightIcon = document.getElementById('theme-toggle-light-icon');
const themeToggleBtn = document.getElementById('theme-toggle');

function isDarkPreferred() {
  const stored = localStorage.getItem('color-theme');
  if (stored === 'dark') return true;
  if (stored === 'light') return false;
  return true;
}

function applyTheme(isDark) {
  document.documentElement.classList.toggle('dark', isDark);
  document.body?.classList.toggle('dark', isDark);

  if (themeToggleDarkIcon && themeToggleLightIcon) {
    themeToggleDarkIcon.classList.toggle('hidden', isDark);
    themeToggleLightIcon.classList.toggle('hidden', !isDark);
  }
}

function toggleTheme() {
  const isDark = !document.documentElement.classList.contains('dark');
  localStorage.setItem('color-theme', isDark ? 'dark' : 'light');
  applyTheme(isDark);
  document.dispatchEvent(new Event('dark-mode'));
}

if (themeToggleBtn) {
  applyTheme(isDarkPreferred());
  themeToggleBtn.addEventListener('click', toggleTheme);
}
