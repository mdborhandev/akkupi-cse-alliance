# ACA UI Kit

Design tokens and reusable components for the *Akkupi CSE Alliance* UI.
Everything lives in [`wwwroot/css/site.css`](../src/ACA.Web/wwwroot/css/site.css); icons come from
`Views/Shared/_Icon.cshtml`. No build step, no new dependencies.

---

## 1. Tokens

All values are CSS custom properties on `:root`, overridden for `[data-bs-theme="dark"]`.

| Group | Tokens | Usage |
|---|---|---|
| Surfaces | `--aca-bg`, `--aca-surface`, `--aca-surface-2`, `--aca-surface-3` | page, cards, inputs/hover, pressed |
| Lines | `--aca-border`, `--aca-border-strong` | hairlines, hover borders |
| Text | `--aca-text`, `--aca-muted`, `--aca-faint` | primary, secondary, tertiary |
| Brand | `--aca-primary`, `--aca-primary-hover`, `--aca-primary-soft`, `--aca-primary-contrast` | links, primary buttons, active nav |
| Semantic | `--aca-success`, `--aca-success-soft`, `--aca-danger`, `--aca-danger-soft`, `--aca-accent` | alerts, chips, destructive actions |
| Shape | `--aca-radius-xs` 8 · `--aca-radius-sm` 10 · `--aca-radius` 14 · `--aca-radius-lg` 18 · `--aca-radius-pill` | cards, buttons, inputs |
| Elevation | `--aca-shadow-xs`, `--aca-shadow-sm`, `--aca-shadow-md`, `--aca-ring` | cards, sticky bars, focus ring |
| Motion | `--aca-ts` (150 ms), `--aca-timing` | hovers and toggles |

Bootstrap variables (`--bs-body-bg`, `--bs-border-color`, `--bs-link-color`, …) are bridged to these
tokens, so Bootstrap utilities (`.bg-body-tertiary`, `.border`, `.text-body-secondary`) follow the theme.

**Rule:** never hardcode hex values or `#fff`/`#000` in views. Add a token if a new color is needed.

---

## 2. Type and spacing

- Font: system UI stack (`--aca-font`). Base 15 px / 1.58, headings 650 weight, `-0.022em` tracking.
- Page title: `<h1>` inside `.aca-page-head`; subtitle uses `.aca-page-sub`.
- Eyebrow label: `.aca-eyebrow` (uppercase, 12 px, faint) with a 16 px icon.
- Spacing follows a 4 px scale; use Bootstrap gap/padding utilities rather than magic numbers.

---

## 3. Shell

| Element | Class | Notes |
|---|---|---|
| Header | `.aca-header` + `.aca-header-inner` | sticky, translucent, blurs page behind |
| Brand | `.aca-brand` + `.aca-brand-mark` | mark is a 34 px rounded gradient tile |
| Desktop nav | `.aca-nav` + `.aca-nav-link` (`.active`) | icon + label pills, hidden < 992 px |
| Search | `.aca-search` | GET `/Members?q=`; `.aca-search--full` inside the mobile drawer |
| Header actions | `.aca-actions`, `.aca-icon-btn`, `.aca-badge-dot` | theme toggle, notifications, account |
| Account menu | `.aca-user-btn` (Bootstrap dropdown) | avatar + name, full email in `title` |
| Mobile drawer | `.offcanvas` + `.aca-menu-link` | opened from the header hamburger |
| Bottom tabs | `.aca-bottom-nav` + `.aca-tab` (`.active`) | < 992 px only, 4 tabs |
| Content | `.aca-main` | max 1200 px, respects bottom nav padding |
| Two-column | `.aca-layout` (1fr / 300 px ≥ 1200 px) | feed + rail |
| Rail | `.aca-rail` + `.aca-rail-card` | sticky on desktop, hidden on mobile |
| Footer | `.aca-footer` + `.aca-footer-inner` | extra bottom padding on mobile |

---

## 4. Components

| Component | Markup | Notes |
|---|---|---|
| Post | `.aca-post` → `.aca-post-head`, `.aca-post-content`, `.aca-post-media[data-count]`, `.aca-post-actions` | media grid supports 1/2/3/4 images |
| Action button | `.aca-action` (`.aca-action--accent`, `.aca-action--end`) | icon + count, 42 px tall tap area |
| Chip | `.aca-chip`, `.aca-chip--alumni`, `.aca-chip--student` | batch and member-type labels |
| Avatar | `.aca-avatar`, `.aca-avatar-sm/xs/lg` | initials fallback uses the same class |
| Composer prompt | `.aca-composer` + `.aca-composer-input` | truncates with ellipsis on narrow screens |
| Page header | `.aca-page-head`, `.aca-page-sub`, `.aca-eyebrow` | title left, actions right |
| Card | `.aca-card` or Bootstrap `.card` | same radius/border/shadow |
| Empty state | `.aca-empty` + `.aca-empty-icon` | dashed border, centered copy |
| Pagination | `.aca-pagination` + `.aca-page` (`.aca-page--disabled`) | "Newer / Older" wording for the feed |
| Buttons | `.btn-primary`, `.btn-outline-secondary`, `.btn-ghost`, `.aca-icon-btn` | radius/shadow already themed |
| Alerts | `.alert-success`, `.alert-danger`, `.alert-info` | rendered from `TempData` by the layout |

### Icons

```razor
<partial name="_Icon" model="@("heart")" />
```

Available: `heart`, `message`, `bell`, `search`, `users`, `layers`, `plus`, `sun`, `moon`, `menu`, `x`,
`user`, `shield`, `flag`, `image`, `logout`, `chevron-left`, `chevron-right`, `check`, `pen`, `trash`,
`spark`. All are 24×24 stroke SVGs using `currentColor`, wrapped in `.aca-icon` (1.15 rem).

---

## 5. Breakpoints

| Range | Behavior |
|---|---|
| < 576 px | single column, 0.75 rem gutters, compact cards |
| ≥ 576 px | normal gutters |
| ≥ 768 px | header search visible, 2-up member grids |
| ≥ 992 px | desktop nav + account name, bottom tabs hidden |
| ≥ 1200 px | two-column layout with sticky rail, max 1060 px centered |

---

## 6. Theme

- Stored in `localStorage['aca-theme']` (`light` \| `dark`), applied inline in `<head>` before paint to
  avoid a flash, respected by `theme.js` for the toggle's `aria-pressed`/label and `meta[theme-color]`.
- **QA helper:** append `?theme=dark` (or `?theme=light`) to any URL to force a theme without touching
  storage — intended for screenshots and visual review.

---

## 7. Accessibility

- Focus ring comes from `--aca-ring` via `:focus-visible`; do not remove outlines.
- Icon-only controls need `aria-label`; decorative SVGs are `aria-hidden`.
- Active nav items set `aria-current="page"`.
- Tap targets are ≥ 44 px (`.aca-action` is 42 px + 0.6 rem padding, `.aca-tab` 56 px).
- Motion respects `prefers-reduced-motion`.
- Skip link `.aca-skip-link` targets `#main-content`.
