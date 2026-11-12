(function () {
  var root = document.documentElement;
  var toggle = document.getElementById('themeToggle');
  var meta = document.querySelector('meta[name="theme-color"]');

  function sync() {
    var dark = root.getAttribute('data-bs-theme') === 'dark';
    if (meta) { meta.setAttribute('content', dark ? '#0c0c11' : '#f5f5fa'); }
    if (!toggle) { return; }
    toggle.setAttribute('aria-pressed', dark ? 'true' : 'false');
    toggle.setAttribute('title', dark ? 'Switch to light theme' : 'Switch to dark theme');
    toggle.setAttribute('aria-label', dark ? 'Switch to light color theme' : 'Switch to dark color theme');
  }

  function apply(theme) {
    root.setAttribute('data-bs-theme', theme);
    try { localStorage.setItem('aca-theme', theme); } catch (e) { }
    sync();
  }

  if (toggle) {
    toggle.addEventListener('click', function () {
      apply(root.getAttribute('data-bs-theme') === 'dark' ? 'light' : 'dark');
    });
  }

  sync();
})();
