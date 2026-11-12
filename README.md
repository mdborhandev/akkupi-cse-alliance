# Akkupi CSE Alliance (ACA)

Akkupi CSE Alliance is a community web app for the **Akkupi CSE Department** — a place where current
students and alumni can share updates, find each other, and stay connected.

It is **not an LMS**. ACA has no courses, assignments, attendance, exams, grading, or class
scheduling. It is a social/community platform with a feed, member directory, profiles, comments,
reactions, notifications, and lightweight moderation.

The application is a working ASP.NET Core MVC app backed by PostgreSQL. The solution builds cleanly
and the xUnit suite in `tests/ACA.Tests` covers the feed, reactions, comments, notifications,
reports, and profile rules.

---

## Features

- **Community feed** — newest-first posts, paginated, optional batch filter, up to four images per
  post (JPEG, PNG, or WebP, 5 MB each).
- **Post detail + authoring** — create, edit, and soft-delete your own posts; admins can manage any
  post.
- **Comments and replies** — one visual level of replies under a top-level comment; soft delete;
  authors and admins can remove their own/any comment.
- **Reactions** — Like, Love, and Support. Repeating the same reaction toggles it off; choosing a
  different reaction replaces the existing one. One reaction row per member per post.
- **Notifications** — in-app notifications for reactions, comments, and replies. Self-notifications
  are never created; unread count and mark-one/mark-all-read are supported.
- **Member directory** — search approved members by name, filter by batch and member type, paginated.
- **Profiles** — name, bio, current status, batch, member type, profile photo.
- **Batches** — public batch catalogue; admins/moderators manage batches.
- **Reports and moderation** — `ReportService` supports reports on a post, comment, or member, and
  the admin queue lets admins/moderators resolve them, including soft-deleting the reported content
  or unapproving the reported member. (A member-facing report form is not wired up yet.)
- **Roles** — `Admin`, `Moderator`, and `Member` (ASP.NET Core Identity with cookie authentication).
- **UI** — responsive Bootstrap 5 layout with light/dark theme toggle and vendored front-end assets.

---

## Tech stack

| Layer | Technology |
| --- | --- |
| Runtime | .NET 10 (verified with SDK `10.0.115`) |
| Web | ASP.NET Core MVC, Razor views, Bootstrap 5 (vendored under `wwwroot/lib`), vanilla JavaScript |
| Persistence | EF Core 10 with Npgsql provider |
| Database | PostgreSQL 18 (local `aca_dev` database) |
| Identity | ASP.NET Core Identity, cookie authentication, roles |
| Tests | xUnit 2.9, `Microsoft.EntityFrameworkCore.InMemory` |
| Tooling | `dotnet-ef` 10.0.15 as a local tool |

---

## Prerequisites

- **.NET 10 SDK** (`dotnet --version` should print `10.x`).
- **PostgreSQL 18** running locally with a `postgres` superuser (or another role that can create
  databases).
- **Bash** to run the helper scripts in `scripts/`.
- The **local `dotnet-ef` tool**. The tool manifest lives at the repository root in
  `dotnet-tools.json` (not under `.config/`), so restore it explicitly once:

  ```bash
  dotnet tool restore --tool-manifest dotnet-tools.json
  ```

  This makes `dotnet ef` (and therefore `scripts/ef.sh`) available for the repository.

---

## Local database

ACA uses a local PostgreSQL database named **`aca_dev`** with the **`postgres`** user. The password
is not stored in the repository: it lives in the gitignored `.secrets/dev-env.sh`, which both helper
scripts source automatically.

Create the database once (you will be prompted for the `postgres` password):

```bash
psql -U postgres -c "CREATE DATABASE aca_dev;"
```

Create `.secrets/dev-env.sh` with your own values. Use placeholders below — never commit real
credentials:

```bash
# .secrets/dev-env.sh  (gitignored)
export ConnectionStrings__DefaultConnection="Host=localhost;Port=5432;Database=aca_dev;Username=postgres;Password=<local-postgres-password>"
export AdminSeed__Email="admin@example.com"
export AdminSeed__Password="<choose-a-strong-local-password>"
export ASPNETCORE_ENVIRONMENT="Development"
```

```bash
chmod 600 .secrets/dev-env.sh
```

`scripts/run-dev.sh` and `scripts/ef.sh` source this file when it exists. If you prefer not to use
the file, export the same variables in your shell before running commands.

At startup, when `Database__SeedOnStartup` is `true`, the app creates the `Admin`, `Moderator`, and
`Member` roles and, if `AdminSeed__Email` and `AdminSeed__Password` are configured, a seed admin
account. It does **not** create demo posts, members, or batches.

---

## Environment variables

Configuration uses the standard ASP.NET Core `__` → `:` convention. On deployment, set these as
environment variables or platform secrets.

| Variable | Required | Default | Purpose |
| --- | --- | --- | --- |
| `ConnectionStrings__DefaultConnection` | Yes | — | Npgsql connection string for PostgreSQL. The app fails fast if it is missing. |
| `AdminSeed__Email` | No | — | Email for the seed admin. Seeding is skipped when email or password is missing. |
| `AdminSeed__Password` | No | — | Password for the seed admin. Use a strong value; never commit it. |
| `ASPNETCORE_ENVIRONMENT` | No | `Production` | `Development` enables developer exception pages and local tooling behaviour. |
| `Database__SeedOnStartup` | No | `true` | Creates Identity roles and the seed admin when `true`. Idempotent. Set to `false` after initial provisioning if you do not want startup seeding. |

Local placeholder example (do not commit real values):

```bash
ConnectionStrings__DefaultConnection="Host=localhost;Port=5432;Database=aca_dev;Username=postgres;Password=<password>"
AdminSeed__Email="admin@example.com"
AdminSeed__Password="<strong-password>"
ASPNETCORE_ENVIRONMENT="Development"
Database__SeedOnStartup="true"
```

A template lives in [`.env.example`](.env.example).

---

## Migrations

The initial migration is already committed under
`src/ACA.Infrastructure/Persistence/Migrations/`. Use the wrapper `scripts/ef.sh`, which sources
the local secrets and points EF Core at the Infrastructure project with the Web project as the
startup project:

```bash
# Restore the local tool once
dotnet tool restore --tool-manifest dotnet-tools.json

# Apply all pending migrations to aca_dev
scripts/ef.sh database update

# Add a new migration after changing the model
scripts/ef.sh migrations add <MigrationName>

# Generate an idempotent SQL script for a deployment pipeline
scripts/ef.sh migrations script --idempotent -o artifacts/aca-migrations.sql
```

Do not edit generated migrations or the model snapshot by hand.

---

## Run locally

```bash
# 1. Restore the local EF tool (once)
dotnet tool restore --tool-manifest dotnet-tools.json

# 2. Ensure the database exists and is up to date
scripts/ef.sh database update

# 3. Run the app (sources .secrets/dev-env.sh)
scripts/run-dev.sh
```

Then open <http://localhost:5067>. The `https` launch profile also exposes
<https://localhost:7264> (`scripts/run-dev.sh --launch-profile https`).

Register a member account from `/Account/Register`, or sign in with the seeded admin account from
`AdminSeed__Email` / `AdminSeed__Password`. The admin area is at `/Admin`.

Equivalent manual command if you do not use the wrapper:

```bash
set -a; source .secrets/dev-env.sh; set +a
dotnet run --project src/ACA.Web/ACA.Web.csproj
```

---

## Tests

The test project uses EF Core InMemory with a fresh `Guid` database name per test, plus a fake
`IImageStorage`, so no PostgreSQL instance or file-system uploads are needed.

```bash
dotnet test tests/ACA.Tests/ACA.Tests.csproj
```

The suite currently contains **40 tests** covering:

- feed ordering, pagination, batch filtering, projection of counts/reactions, soft delete;
- post ownership (non-owner `UnauthorizedAccessException`, admin override), validation, images;
- reaction toggle-off, type replacement, one row per `(post, user)`, remove;
- comment validation, cross-post parent rejection, self-notification suppression, reply notifications,
  comment ownership/delete;
- notification self-skip, unread count, mark-one/mark-all-read, feed ordering;
- report target/reason validation and resolution (reviewer, status, post/comment/member removal);
- profile validation (empty name, bio length), batch assignment, photo replacement, idempotent
  profile creation.

Build the whole solution with:

```bash
dotnet build AkkupiCseAlliance.sln
```

---

## Project layout

```
akkupi-cse-alliance/
├── src/
│   ├── ACA.Domain/           # Entities and enums, no framework dependencies
│   ├── ACA.Application/      # Service interfaces, DTOs, business services
│   ├── ACA.Infrastructure/   # EF Core DbContext, migrations, Identity, image storage
│   └── ACA.Web/              # ASP.NET Core MVC: controllers, Razor views, wwwroot assets
├── tests/
│   └── ACA.Tests/            # xUnit service tests with EF Core InMemory
├── scripts/
│   ├── ef.sh                 # EF Core wrapper (sources local secrets)
│   └── run-dev.sh            # dotnet run wrapper (sources local secrets)
├── docs/
│   ├── AGENT-PROMPT.md       # UI modernization brief (presentation-only phase)
│   ├── ARCHITECTURE.md
│   └── DEPLOYMENT.md
├── .env.example              # Placeholder environment-variable template
├── dotnet-tools.json         # Local dotnet-ef tool manifest (root, not .config/)
└── AkkupiCseAlliance.sln
```

`src/ACA.Infrastructure/Persistence/AppDbContext.cs` implements `IAppDbContext`; the Web project
composes everything through `AddInfrastructure(...)` and `AddApplication()` in `Program.cs`.

---

## Security notes

- **Secrets stay out of git.** Connection strings, seed credentials, and environment files live in
  `.secrets/` (gitignored) or platform secrets. `appsettings.json` contains no credentials.
- **Authentication** uses ASP.NET Core Identity cookies. The cookie is HTTP-only, expires after 14
  days, uses sliding expiration, and redirects unauthenticated users to `/Account/Login`.
- **Authorization** combines role checks (`Admin`, `Moderator`, `Member`) with ownership checks in
  the application services. A member cannot edit or delete another member's post/comment; admins can.
  The Admin area is restricted to `Admin` and `Moderator`; member approval and batch deletion are
  `Admin`-only.
- **CSRF protection** is applied through `[ValidateAntiForgeryToken]` on state-changing MVC actions.
- **Output encoding** is on by default in Razor; user-provided content is rendered encoded.
- **Image uploads** are validated by declared content type **and** by file signature (magic bytes) in
  `LocalImageStorage`, capped at 5 MB per image and 4 images per post. Stored names are GUIDs, and
  deletion is guarded against path traversal.
- **Transport** — HTTPS redirection is enabled and HSTS is used outside Development.
- **Production hardening**: set `ASPNETCORE_ENVIRONMENT=Production`, use a strong `AdminSeed__Password`
  (or omit seeding), set `Database__SeedOnStartup=false` after initial provisioning, and terminate
  TLS at the host/reverse proxy.
- Not yet present: email confirmation, password reset, two-factor authentication, login rate
  limiting, and audit logging. See [what is not implemented yet](#not-implemented-yet).

---

## Not implemented yet

These are known gaps, not hidden features:

- No Web API for a mobile client; the app is server-rendered MVC only.
- No email delivery: no email confirmation, password reset, or email notifications. Registration
  marks the email as confirmed, and notifications are in-app only.
- No real-time notifications (no SignalR/WebSocket push); the unread badge is refreshed with `fetch`
  on page load, window focus, and tab visibility changes.
- No S3/CDN image storage. Uploads go to the local `wwwroot/uploads` folder, which is ephemeral on
  many PaaS hosts; replace `IImageStorage` with an object-storage implementation for scale-out.
- No full-text search; member search uses a case-insensitive `Contains` on full name.
- No member-facing report submission form or controller. The report service and admin resolution
  queue exist, but only the moderation side is wired into the UI.
- No rate limiting, spam heuristics, audit trail, or moderation action history.
- No image editing after publish (post edits change text only); no post drafts/scheduling.
- No cursor-based pagination, caching layer, background job queue, or outbox for notifications.
- No localization; the UI is English-only.

---

## More documentation

- [Agent prompt & roadmap](docs/AGENT-PROMPT.md) — the current **UI modernization** phase brief:
  design direction, responsive requirements, page-by-page work plan, and guardrails.
- [Architecture](docs/ARCHITECTURE.md) — layers, dependencies, data model, services, authorization,
  query strategy, storage abstraction, and tradeoffs.
- [Deployment](docs/DEPLOYMENT.md) — why Vercel cannot host MVC, recommended hosts, configuration,
  migrations on deploy, and a go-live checklist.
