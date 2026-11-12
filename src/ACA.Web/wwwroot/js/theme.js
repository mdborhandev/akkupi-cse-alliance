(function () {
  'use strict';

  var STORAGE_KEY = 'aca-theme';
  var root = document.documentElement;
  var toggle = document.getElementById('themeToggle');
  var media = window.matchMedia ? window.matchMedia('(prefers-color-scheme: dark)') : null;

  function storedTheme() {
    try {
      return window.localStorage.getItem(STORAGE_KEY);
    } catch (error) {
      return null;
    }
  }

  function currentTheme() {
    return root.getAttribute('data-bs-theme') === 'dark' ? 'dark' : 'light';
  }

  function updateToggle(theme) {
    if (!toggle) {
      return;
    }

    var isDark = theme === 'dark';
    toggle.textContent = isDark ? '☀' : '☾';
    toggle.setAttribute('aria-pressed', isDark ? 'true' : 'false');
    toggle.setAttribute('title', isDark ? 'Switch to light theme' : 'Switch to dark theme');
    toggle.setAttribute('aria-label', isDark ? 'Switch to light color theme' : 'Switch to dark color theme');
  }

  function apply(theme) {
    root.setAttribute('data-bs-theme', theme);
    try {
      window.localStorage.setItem(STORAGE_KEY, theme);
    } catch (error) {
      /* Private browsing can block storage; the visual change still applies. */
    }
    updateToggle(theme);
  }

  if (toggle) {
    updateToggle(currentTheme());
    toggle.addEventListener('click', function () {
      apply(currentTheme() === 'dark' ? 'light' : 'dark');
    });
  }

  // Follow the operating-system preference only while the visitor has not picked a theme.
  if (media && media.addEventListener) {
    media.addEventListener('change', function (event) {
      if (!storedTheme()) {
        apply(event.matches ? 'dark' : 'light');
      }
    });
  }
})();
