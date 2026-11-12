(function () {
  'use strict';

  var ALERT_DISMISS_DELAY = 6000;

  /* ------------------------------------------------------------------
   * Notification badge
   * The badge is only rendered for signed-in users, so its presence is
   * the signed-in check. Refresh on load, on window focus and whenever
   * the visitor returns to the tab.
   * ------------------------------------------------------------------ */
  function initNotificationBadge() {
    var badge = document.getElementById('notificationBadge');
    if (!badge) {
      return;
    }

    var link = badge.closest('a');
    var endpoint = badge.getAttribute('data-endpoint') || '/Notifications/UnreadCount';
    var isFetching = false;

    function render(count) {
      badge.textContent = count > 99 ? '99+' : String(count);
      badge.classList.toggle('d-none', count <= 0);

      if (link) {
        if (count > 0) {
          link.setAttribute('aria-label', 'Notifications, ' + count + ' unread');
        } else {
          link.removeAttribute('aria-label');
        }
      }
    }

    function refresh() {
      if (isFetching || document.hidden) {
        return;
      }

      isFetching = true;
      window.fetch(endpoint, {
        method: 'GET',
        credentials: 'same-origin',
        cache: 'no-store',
        headers: { 'Accept': 'application/json' }
      })
        .then(function (response) {
          return response.ok ? response.json() : null;
        })
        .then(function (payload) {
          if (payload && typeof payload.count === 'number' && isFinite(payload.count)) {
            render(Math.max(0, Math.floor(payload.count)));
          }
        })
        .catch(function () {
          /* Keep the last known count when the request fails. */
        })
        .finally(function () {
          isFetching = false;
        });
    }

    window.addEventListener('focus', refresh);
    window.addEventListener('pageshow', refresh);
    document.addEventListener('visibilitychange', function () {
      if (!document.hidden) {
        refresh();
      }
    });
    if (link) {
      link.addEventListener('focus', refresh);
    }

    refresh();
  }

  /* ------------------------------------------------------------------
   * TempData alerts: dismiss after a few seconds, but pause while the
   * visitor is hovering or focused inside the message.
   * ------------------------------------------------------------------ */
  function initAutoDismissAlerts() {
    var alerts = document.querySelectorAll('.alert.alert-dismissible');
    if (!alerts.length) {
      return;
    }

    Array.prototype.forEach.call(alerts, function (alert) {
      var timer = null;
      var dismissed = false;

      function dismiss() {
        if (dismissed) {
          return;
        }

        dismissed = true;
        if (window.bootstrap && window.bootstrap.Alert) {
          window.bootstrap.Alert.getOrCreateInstance(alert).close();
        } else {
          alert.parentNode && alert.parentNode.removeChild(alert);
        }
      }

      function schedule() {
        if (dismissed) {
          return;
        }
        window.clearTimeout(timer);
        timer = window.setTimeout(dismiss, ALERT_DISMISS_DELAY);
      }

      function pause() {
        window.clearTimeout(timer);
      }

      alert.addEventListener('mouseenter', pause);
      alert.addEventListener('focusin', pause);
      alert.addEventListener('mouseleave', schedule);
      alert.addEventListener('focusout', schedule);
      schedule();
    });
  }

  /* ------------------------------------------------------------------
   * Broken profile images: swap in initials when the nearby author name
   * is available, otherwise hide the broken image.
   * ------------------------------------------------------------------ */
  function initAvatarFallbacks() {
    function initialsFrom(name) {
      var parts = String(name || '').trim().split(/\s+/).filter(Boolean);
      if (!parts.length) {
        return '';
      }

      var initials = parts[0].charAt(0);

      // A second word only contributes when it looks like another name word,
      // so message text such as "Rahman reacted to your post" stays "R".
      if (parts.length > 1) {
        var next = parts[1].charAt(0);
        if (next !== next.toLowerCase()) {
          initials += next;
        }
      }

      return initials.toUpperCase();
    }

    function nearbyName(image) {
      var container = image.closest('.card-body, li, .d-flex, .text-center') || image.parentElement;
      if (!container) {
        return '';
      }

      var node = container.querySelector('.fw-semibold, .fw-bold');
      return node ? node.textContent : '';
    }

    function resolveInitials(image) {
      var explicit = image.getAttribute('data-initials');
      if (explicit) {
        return explicit;
      }

      var alt = (image.getAttribute('alt') || '').trim();
      if (alt && !/photo|avatar|image/i.test(alt)) {
        return initialsFrom(alt);
      }

      return initialsFrom(nearbyName(image));
    }

    function replaceWithInitials(image) {
      if (image.getAttribute('data-avatar-fallback') === 'done') {
        return;
      }
      image.setAttribute('data-avatar-fallback', 'done');

      var initials = resolveInitials(image);
      if (!initials) {
        image.classList.add('d-none');
        image.setAttribute('hidden', 'hidden');
        image.setAttribute('aria-hidden', 'true');
        return;
      }

      var fallback = document.createElement('span');
      fallback.className = image.className + ' aca-avatar-fallback d-inline-flex align-items-center justify-content-center fw-semibold';
      fallback.setAttribute('aria-hidden', 'true');
      fallback.textContent = initials;

      if (image.parentNode) {
        image.parentNode.replaceChild(fallback, image);
      }
    }

    document.addEventListener('error', function (event) {
      var image = event.target;
      if (image instanceof HTMLImageElement && image.classList.contains('aca-avatar')) {
        replaceWithInitials(image);
      }
    }, true);

    function sweep() {
      var images = document.querySelectorAll('img.aca-avatar');
      Array.prototype.forEach.call(images, function (image) {
        if (image.complete && image.naturalWidth === 0) {
          replaceWithInitials(image);
        }
      });
    }

    sweep();
    window.addEventListener('load', sweep);
  }

  function init() {
    initNotificationBadge();
    initAutoDismissAlerts();
    initAvatarFallbacks();
  }

  if (document.readyState === 'loading') {
    document.addEventListener('DOMContentLoaded', init);
  } else {
    init();
  }
})();
