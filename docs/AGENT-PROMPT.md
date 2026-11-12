# Akkupi CSE Alliance — Agent Prompt: UI Modernization

> This phase is **presentation only**. No new product features, no schema changes, no new services.
> The goal is a proper, modern, genuinely responsive UI for everything that already works.

Status: MVP is implemented and verified (`main` @ `a861077`), app runs at `http://127.0.0.1:5199`.
What exists: [`README.md`](../README.md) · design notes: [`ARCHITECTURE.md`](ARCHITECTURE.md).

---

## 1. Mission

Rebuild ACA's presentation layer so it looks and feels like a modern community product — not a
scaffolded Bootstrap demo — and behaves properly from a 360 px phone to a 1440 px desktop.

Deliverable: every existing page restyled, responsive, accessible (WCAG AA), and correct in both
light and dark themes. **Zero functional regressions.**

---

## 2. Ground truth (what is rendered today)

| Page | View(s) | Notes |
|---|---|---|
| Home | `Views/Home/Index.cshtml` | hero + CTA |
| Feed | `Views/Posts/Index.cshtml` | post cards, pagination |
| Post detail | `Views/Posts/Details.cshtml` | images, reaction bar, comments partial |
| Composer | `Views/Posts/Create.cshtml`, `Edit.cshtml` | multipart upload |
| Members | `Views/Members/Index.cshtml`, `Details.cshtml` | search, filters, cards, pagination |
| Batches | `Views/Batches/Index.cshtml` | catalogue list |
| Profile | `Views/Profile/Index.cshtml`, `Edit.cshtml` | own profile + edit |
| Auth | `Views/Account/Login.cshtml`, `Register.cshtml`, `AccessDenied.cshtml` | forms |
| Notifications | `Views/Notifications/Index.cshtml` | list + badge |
| Reports | `Views/Reports/Create.cshtml` | member report form |
| Comments | `Views/Shared/_CommentsSection.cshtml` | thread + reply forms |
| Shell | `Views/Shared/_Layout.cshtml`, `wwwroot/css/site.css` (474 lines), `wwwroot/js/{site,theme}.js` | navbar, theme toggle, alerts, badge |
| Admin | `Areas/Admin/Views/**` | dashboard, members, batches, reports, `_AdminLayout.cshtml` |
| Errors | `Views/Shared/Error.cshtml` | generic |

Stack constraints stay: Bootstrap 5 (local, no CDN), Razor, vanilla JS. No React/TS/Tailwind build step.

---

## 3. Design direction

**Feel:** calm, dense-enough social feed; generous whitespace; confident typography; soft surfaces.

- **Tokens first.** Define a real token layer in `site.css` as CSS custom properties: color roles
  (surface, surface-raised, border, text, muted, primary, accent, danger), a 4 px spacing scale,
  radii (sm 8 / md 12 / lg 16), two shadow levels, type ramp (12–28 px), and transition timings.
  Map semantic roles onto Bootstrap 5.3 variables (`--bs-body-bg`, `--bs-border-color`, …) so
  utilities keep working in both themes. No `!important` unless overriding a Bootstrap component.
- **Layout.** 12-column max-width shell (~1120 px feed+rail, ~720 px single-column pages), sticky
  top bar on desktop, compact mobile header. Consistent page header pattern (title + actions).
- **Components to design once and reuse:** post card, composer card, comment thread item, member
  card, profile header, stat pill, batch card, notification row, report row, empty state, skeleton,
  pagination, form controls, buttons (primary / subtle / danger / icon), badges, alerts/toasts.
- **Type & icons.** System UI font stack (no new font dependency); inline SVG icon set (heart, reply,
  bell, search, image, chevron, menu, sun/moon) — no icon CDN. Icons must be `currentColor`.
- **Imagery.** Fixed `aspect-ratio` containers, `object-fit: cover`, lazy loading, rounded masks,
  graceful fallback for broken avatars (initials).
- **Motion.** 150–200 ms ease-out for hovers/toggles; no parallax or gradient gimmicks; honor
  `prefers-reduced-motion`.
- **Dark mode.** Every surface, border, shadow, image overlay, badge and focus ring tuned — not just
  an inverted background. Toggle keeps `localStorage['aca-theme']` and `#themeToggle` behavior.

---

## 4. Responsive requirements

| Width | Expected behavior |
|---|---|
| 360–390 px | single column, edge padding 16 px, 44 px touch targets, nav collapses to menu/compact bar, comment forms and reaction pills wrap, no horizontal scroll |
| 576–767 px | single column, denser cards, images 2-up when 2+ |
| 768–991 px | 2-column member grid, wider feed card, filters as inline selects |
| 992–1199 px | desktop nav visible, feed max 720 px centered with optional right rail (suggested members/batches) |
| ≥1200 px | full shell, right rail sticky, admin tables stay tables |
| Admin < 768 px | tables become stacked definition-list cards, sidebar becomes horizontal scroll/tab list |

Hard rules: no element may exceed viewport width; long words/URLs wrap; tap targets ≥ 44×44 px;
forms use `inputmode`/`autocomplete` appropriately.

---

## 5. Work plan

Do these in order; each one must leave the app fully usable.

**U1 — Token layer + shell** (`site.css`, `_Layout.cshtml`, `theme.js`)
Design tokens, typography scale, app bar (brand, primary nav, search entry point, theme toggle,
avatar menu), mobile menu, page container, footer, skip link, TempData alerts as dismissible toasts.
Include a small `docs/UI-KIT.md` documenting tokens and component classes.

**U2 — Feed + post cards** (`Views/Posts/Index.cshtml`, `Details.cshtml`, `Create.cshtml`, `Edit.cshtml`)
Post card hierarchy (author → batch/type → time → content → images → action bar), image grid for
1–4 images, reaction pills with counts, comment count link, owner/admin menu (edit/delete),
composer with drag/drop hint and image previews (progressive enhancement only).

**U3 — Comments & reactions** (`Views/Shared/_CommentsSection.cshtml`, reaction forms)
Threaded list with connector lines, compact reply form, empty state, "log in to comment" prompt,
reaction buttons that look pressed when selected (`aria-pressed` already present).

**U4 — Members, profile, batches**
Member cards (avatar, name, batch badge, type chip, status), filter bar that stacks on mobile,
profile header with cover/avatar/actions, batch cards with member counts.

**U5 — Auth, reports, notifications, errors**
Centered auth card with product context, inline validation styling, friendly 403/404/error pages,
notification rows with unread indicator and relative time, report form with clear target summary.

**U6 — Admin area** (`Areas/Admin/Views/**`, `_AdminLayout.cshtml`)
Sidebar + dense tables on desktop, table→card conversion on mobile, stat tiles on the dashboard,
consistent destructive-action styling and confirmations.

**U7 — QA pass**
360 / 390 / 768 / 1024 / 1440 widths; light + dark; keyboard-only; zoom 200%; reduced motion;
verify every route returns 200 (or the expected 302/403) with `curl`; `dotnet test` still green.

---

## 6. Guardrails (non-negotiable)

- **Do not add features or fields.** No new routes, controllers, services, entities, migrations,
  packages, or JS libraries. This phase is CSS/JS/Razor markup only.
- **Preserve contracts:** route/action names, form field names (`postId`, `content`, `type`,
  `returnUrl`, token fields), `[Authorize]` attributes, anti-forgery tags, view models/DTOs, and all
  `@inject`ed services.
- **Keep these hooks** used by `site.js`/`theme.js`: `#themeToggle`, `#notificationBadge`,
  `data-bs-theme`, `.aca-avatar`; or update the JS in the same change.
- **No CDN assets, no inline `<style>` blocks, no build step.** Bootstrap 5 bundle already vendored in
  `wwwroot/lib`.
- Never edit `.secrets/`, connection docs, migrations, or the test suite. Copy/markup changes are fine;
  service behavior is not.
- Keep both themes and all empty/loading/error states covered — a component is done only when it
  looks right empty, full, and broken.

---

## 7. Definition of done

- [ ] Every page listed in §2 restyled; no page still uses default Bootstrap chrome.
- [ ] 360 px: no horizontal scroll on any page; touch targets ≥ 44 px.
- [ ] Light and dark mode both pass visual check; no hardcoded white/black surfaces.
- [ ] Keyboard: visible focus ring, logical tab order, menu and toggles operable.
- [ ] `dotnet build` 0 warnings/0 errors; `dotnet test` 40/40 pass.
- [ ] Existing flows still work: register → post → comment → react → report → admin resolve.
- [ ] `docs/UI-KIT.md` documents tokens and reusable classes.

---

## 8. Deferred — do NOT build in this phase

New capabilities from the earlier roadmap (admin member deletion, audit log, email verification,
rate limiting, batch pages, events, job board, messaging, API, real-time, S3 storage, localization)
stay deferred until the UI phase is complete and reviewed. If a design idea needs new data or
endpoints, write it down as a proposal instead of implementing it.
