# Akkupi CSE Alliance — Agent Prompt & Roadmap

> Status snapshot: **MVP implemented and verified** (`main` @ `543a1f8`).
> Running app: `http://127.0.0.1:5199` · Repo: `github.com/mdborhandev/akkupi-cse-alliance`

This file is the entry point for any agent (or developer) picking up ACA next. It states what
already exists, what is deliberately out of scope, and the prioritized list of future features.

---

## 1. Product

**Akkupi CSE Alliance (ACA)** — a private-feeling social community for current students, graduates,
and alumni of the Akkupi Computer Department.

**Tagline:** Connecting Students, Alumni & Generations.

**Not** an e-learning platform: no courses, quizzes, study material, or LMS features — now or later.

---

## 2. Baseline that already exists (do not rebuild)

| Area | Implemented |
|---|---|
| Stack | .NET 10 · ASP.NET Core MVC + Razor · EF Core 10 + Npgsql · PostgreSQL 18 · Identity · Bootstrap 5 · vanilla JS |
| Layers | `ACA.Domain` → `ACA.Application` → `ACA.Infrastructure` → `ACA.Web` (+ `tests/ACA.Tests`) |
| Accounts | Register / login / logout, Identity, roles `Admin` / `Moderator` / `Member`, access-denied page |
| Profiles | Name, photo, bio, batch/session, current status, Current Student vs Alumni, approval flag |
| Feed | Posts, up to 4 validated images, owner/admin edit + soft delete, newest-first, paginated |
| Engagement | Reactions (Like/Love/Support, one per member per post), comments + one-level replies |
| Discovery | Member directory with search, batch filter, member-type filter; batch catalogue; feed filter by batch |
| Notifications | In-app on reaction/comment, unread badge, mark read / mark all read |
| Moderation | Member reporting form, admin dashboard, member approval, batch CRUD, report queue + resolution |
| Quality | 40 xUnit tests, single `InitialCreate` migration, server-side authorization on every action |

Local setup, database, secrets, and commands: see [`README.md`](../README.md).
Design and tradeoffs: [`ARCHITECTURE.md`](ARCHITECTURE.md). Hosting: [`DEPLOYMENT.md`](DEPLOYMENT.md).

---

## 3. Future features

Priority order is intentional. Ship small slices; keep the repo buildable and tested after each one.
Effort: **S** ≈ a day, **M** ≈ 2–4 days, **L** ≈ a week or more.

### v1.1 — Trust, polish, and account basics

| # | Feature | Why | Effort |
|---|---|---|---|
| 1 | Admin member delete / deactivate (soft) | Only approval toggle exists today | S |
| 2 | Moderation audit log (who removed what, when) | Accountability for content removal | S |
| 3 | Edit / delete own comments from the UI | Services support it; no UI yet | S |
| 4 | Email verification + password reset via Identity tokens | Accounts currently auto-confirm | M |
| 5 | Report reason presets (spam, harassment, impersonation, off-topic) | Faster, cleaner moderation triage | S |
| 6 | Rate limiting on posts, comments, reactions, reports | Spam and accidental double-submits | S |
| 7 | Strip EXIF/GPS from uploaded images, cap dimensions | Privacy of members' photos | S |
| 8 | Comment pagination + "load more" | Details page loads page 1 only | S |
| 9 | Full-text member search (trigram/GIN index) | Current `ILIKE` does not scale | M |
| 10 | Profile privacy controls (hide batch / hide from directory) | Comfort in a semi-private community | M |

### v1.2 — Community depth

| # | Feature | Why | Effort |
|---|---|---|---|
| 11 | Batch pages: member list, batch feed, batch admins | Batches are the core organizing unit | M |
| 12 | Events & meetups (create, RSVP, calendar view) | Reunions and department events | M |
| 13 | Opportunity/job board posted by alumni | Highest-value alumni feature after the feed | M |
| 14 | Mentorship pairing (alumni ↔ students, interests + request flow) | Matches the "generations" tagline | L |
| 15 | Direct messages (1:1, with block/report) | Members currently have no private channel | L |
| 16 | @mentions in posts/comments + notification type | Higher engagement, reuses notifications | M |
| 17 | Bookmarks / saved posts | Lightweight personal feature | S |
| 18 | Polls in posts | Cheap engagement boost | M |
| 19 | Photo albums / galleries per batch | Preserve department history | M |
| 20 | Rich content: link previews, safe markdown | Better posts without HTML injection risk | M |

### v2 — Platform and scale

| # | Feature | Why | Effort |
|---|---|---|---|
| 21 | Versioned Web API (`/api/v1`) for a mobile app | Mobile is the stated long-term goal | L |
| 22 | Push notifications (FCM/APNs) + email digests | Reach members outside the site | L |
| 23 | Real-time updates with SignalR (feed, comments, notifications) | Live community feel | M |
| 24 | Object storage (S3-compatible) + CDN for images | Local disk does not survive multi-instance hosts | M |
| 25 | PostgreSQL full-text search over posts/comments | Discovery beyond member names | M |
| 26 | OAuth sign-in (Google) and 2FA | Lower signup friction, stronger accounts | M |
| 27 | Multi-department / multi-tenant structure | If ACA expands beyond the CSE department | L |
| 28 | Analytics dashboard (DAU, posts, retention) | Data-driven moderation and growth | M |
| 29 | Bangla + English localization | Local audience reach | M |
| 30 | PWA install + offline shell | Mobile-first usage without an app store | M |

### Explicitly out of scope (reject such requests)

- E-learning: courses, lessons, quizzes, exams, certificates, study-material libraries.
- Replacing the MVC stack with React/Vue/Angular/Next.js or a Node runtime.
- Public anonymous access, ads, paid tiers, or selling member data.
- Storing secrets in tracked files; using the `postgres` superuser in production.

---

## 4. Working rules for the next agent

1. Read `README.md` first; run `scripts/run-dev.sh` and confirm the app boots before changing code.
2. One feature slice per change set. Keep `dotnet build` at 0 warnings and `dotnet test` green.
3. Add an EF migration only when the schema truly changes (`scripts/ef.sh migrations add <Name>`), then update the database.
4. Authorization lives on the server: `[Authorize]`, role checks, and ownership checks in services.
5. Never commit `.secrets/`, connection strings, or passwords; update `README.md` when setup changes.
6. Prefer extending the existing service interfaces (`ACA.Application/Interfaces`) over new abstractions.
7. Update this roadmap when a feature ships (move it into §2) or when scope changes.
