(function () {
  var root = document.documentElement;
  var toggle = document.getElementById('themeToggle');

  function apply(theme) {
    root.setAttribute('data-bs-theme', theme);
    localStorage.setItem('aca-theme', theme);
    if (toggle) { toggle.textContent = theme === 'dark' ? '☀' : '☾'; }
  }

  if (toggle) {
    toggle.textContent = (root.getAttribute('data-bs-theme') === 'dark') ? '☀' : '☾';
    toggle.addEventListener('click', function () {
      apply(root.getAttribute('data-bs-theme') === 'dark' ? 'light' : 'dark');
    });
  }
})();
