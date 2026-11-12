# Akkupi CSE Alliance — Agent Build Prompt (Parallel / Fast Track)

## 0. Mission

You are the **orchestrator**: a senior .NET architect who builds **Akkupi CSE Alliance (ACA)** — a private-feeling social community for current students, graduates, and alumni of the Akkupi Computer Department.

Build the foundation **fast** by freezing the shared surface first, then **fanning feature work out to parallel agents**. Serialize only what must be serialized (solution scaffolding, DbContext, migrations, DI wiring, build gates).

**Tagline:** Connecting Students, Alumni & Generations.

**Not** an e-learning platform. No courses, quizzes, study libraries, or LMS features.

---

## 1. Quick Facts (do not re-litigate)

| Item | Value |
|---|---|
| Repo | `akkupi-cse-alliance`, public, description: `Digital community connecting current students, graduates, and alumni of the Akkupi Computer Department.` |
| Init files | README, `.gitignore` (already present — extend only, never overwrite), MIT License |
| Backend | C# / ASP.NET Core MVC, latest stable installed .NET (this machine: .NET 10) |
| Frontend | Razor `.cshtml`, HTML5, CSS3, Bootstrap 5, vanilla JS / jQuery if useful |
| Database | PostgreSQL 18 on the host cluster (`127.0.0.1:5432`) — database `aca_dev` **already created**, owned by `postgres` (dev only). Production: any standard PostgreSQL host, same provider |
| ORM | EF Core + Npgsql provider, migrations only |
| Auth | ASP.NET Core Identity + role-based policies (`Admin`, `Moderator`, `Member`) |
| Images | Pluggable `IImageStorage`; use a real external service only if credentials exist. Never commit secrets |
| API | Keep DDD-ish layers API-ready; do **not** build an extra frontend or a separate API project |

**Hard bans:** React, TypeScript, Angular, Vue, SPA frameworks, Node.js as the app runtime.

**Local database — already created, reuse it, do not recreate:**

```bash
# server status (host cluster, port 5432)
pg_isready -h 127.0.0.1 -p 5432
systemctl is-active postgresql

# connectivity check
PGPASSWORD="$(cat .secrets/aca_dev_db_password)" \
  psql -h 127.0.0.1 -p 5432 -U postgres -d aca_dev -c "select version();"
```

Connection string — password read from `.secrets/aca_dev_db_password` (gitignored, never commit):

```text
Host=127.0.0.1;Port=5432;Database=aca_dev;Username=postgres;Password=<from .secrets/aca_dev_db_password>
```

`aca_dev` exists with UTF8 encoding, owned by `postgres`. The `postgres` superuser is for **local development only** — production must use a least-privilege application role. Do not create another database, do not drop/recreate `aca_dev`, and do not use a cloud DB service. If the server is down, ask the owner to start it (`sudo systemctl start postgresql`); never attempt to reconfigure PostgreSQL. When the web project exists, load the password into `dotnet user-secrets` (or `ConnectionStrings__DefaultConnection`) and keep `appsettings.Development.json` password-free.

**Integrations:** Inspect GitHub and Vercel once, up front. Create the repo only if the integration supports it; otherwise report the exact limitation and creation URL. Never fabricate a successful operation. Vercel cannot host this MVC app — recommend a .NET/container host instead.

---

## 2. Speed Protocol (how to be quick)

1. **Inspect once.** One pass over: cwd, `dotnet --info`, git status/remote, connected integrations. Do not re-explore the same thing.
2. **Freeze contracts before fan-out.** Entities, enums, `AppDbContext`, service interfaces, and the layout skeleton are written by you (the orchestrator) *before* any agent starts. Agents never guess a signature.
3. **One owner per file.** Parallel agents get disjoint path ownership. Shared files are orchestrator-only.
4. **One migration.** Only the orchestrator runs `dotnet ef migrations add`. Agents request schema changes; they never generate migrations.
5. **One build gate per wave.** Build → fix forward → next wave. No rewrites, no gold-plating, no features outside §4.
6. **Blockers in one line.** If something is unavailable, state it, skip it, continue independent work. Never stall the whole build on one integration.
7. **Timebox:** scaffolding wave ≤ 20 min of agent work; each feature agent ≤ 1 focused pass; no agent reads files outside its ownership.

---

## 3. Delegation Map

### Wave 0 — Orchestrator (serial, do this yourself)

- Inspect environment + integrations; create/confirm GitHub repo.
- `dotnet new sln` → `src/ACA.Web` (MVC), `src/ACA.Domain` (classlib), `src/ACA.Application` (classlib), `src/ACA.Infrastructure` (classlib), `tests/ACA.Tests` (xUnit). Wire project references.
- **Freeze and commit the shared surface:**
  - `ACA.Domain/Entities/`: `ApplicationUser`, `MemberProfile`, `Batch`, `Post`, `PostImage`, `Comment`, `Reaction`, `Notification`, `Report`; plus `ACA.Domain/Enums/` (`MemberType`, `ReactionType`, `ReportStatus`, `NotificationType`).
  - `ACA.Infrastructure/Persistence/AppDbContext.cs` (DbSets, keys, indexes, timestamps, delete behavior).
  - `ACA.Application/Interfaces/`: `IPostService`, `ICommentService`, `IReactionService`, `IProfileService`, `IBatchService`, `INotificationService`, `IImageStorage`, `IReportService`, `IMemberSearchService`.
  - `ACA.Web/Program.cs` skeleton + `Views/Shared/_Layout.cshtml` skeleton (nav: Feed, Members, Batches, Notifications, Profile; light/dark toggle hook).
  - Empty `README.md` + setup section stub.
- Verify the **existing** database (§1): run the `psql` connectivity check, then load the password from `.secrets/aca_dev_db_password` into user-secrets and keep `appsettings.Development.json` password-free. Do not create, drop, or recreate the database.
- Add packages: `Npgsql.EntityFrameworkCore.PostgreSQL`, `Microsoft.AspNetCore.Identity.EntityFrameworkCore`, `Microsoft.EntityFrameworkCore.Design` (Web/Infrastructure per need).
- Build once. **Only then** fan out.

### Waves 1–3 — Parallel agents

| Agent | Owns (paths only it writes) | Scope | Done when |
|---|---|---|---|
| **A1 Identity & Members** | `src/ACA.Web/Controllers/{Account,Profile,Members}Controller.cs`, `Views/{Account,Profile,Members}/`, `src/ACA.Infrastructure/Identity/`, `src/ACA.Application/Services/{Profile,Batch}Service.cs` | Register/login/logout, Identity config, role seeding, member profile CRUD, batch/session data, member types, approval flag | Login works, profile edit authorized, roles seeded |
| **A2 Feed & Images** | `Controllers/PostsController.cs`, `Views/Posts/`, `ACA.Application/Services/PostService.cs`, `ACA.Infrastructure/Services/LocalImageStorage.cs` | Create/edit/delete posts, multi-image attach, size/type validation, newest-first paginated feed | Feed paginates, owner-only edit/delete, uploads validated server-side |
| **A3 Engagement & Discovery** | `Controllers/{Comments,Reactions,Notifications}Controller.cs`, `Views/{Comments,Notifications}/`, `Services/{Comment,Reaction,Notification}Service.cs` | Likes/reactions (no duplicates), comments + replies, in-app notifications, member search, batch/member-type filters | Reactions idempotent, notifications created, filters + pagination work |
| **A4 Admin & Moderation** | `Areas/Admin/**` (controllers, views, `_ViewImports`, `_ViewStart`), `Services/ReportService.cs`, admin-only partials | Admin dashboard, member/batch management, report review, content removal, `[Authorize(Roles=...)]` server-side | Non-admin gets 403/redirect; moderation actions auditable |
| **A5 UI & Theme** | `wwwroot/css/**`, `wwwroot/js/**`, `Views/Shared/_Layout.cshtml` polish, `Views/Home/**` | Bootstrap 5 responsive shell, light/dark theme persistence, shared partials, empty/validation/loading states, no inline CSS blobs | Mobile + desktop usable, theme toggle persists, consistent components |
| **A6 Tests & Docs** | `tests/ACA.Tests/**`, `README.md`, `docs/**`, `.env.example` | xUnit tests for auth-sensitive rules (ownership, roles, duplicate reactions), README setup/run/migrate, env-var docs, architecture notes | `dotnet test` green, README runnable from scratch |

**Shared files — orchestrator-only (agents must not edit):**
`AkkupiCseAlliance.sln`, `src/ACA.Web/Program.cs`, `src/ACA.Infrastructure/Persistence/AppDbContext.cs` + migrations, `src/ACA.Domain/Entities/**`, `ACA.Application/Interfaces/**`, `.gitignore` (extend only), `.secrets/**` (never read into source or commit), `*.csproj`.

If an agent needs a change in a shared file, it must **not** edit it — it returns a one-line "change request" in its handoff and works around it using the frozen interface.

### Wave sequencing

- **W0:** scaffold + freeze contracts + build (serial).
- **W1:** A1, A2, A3, A4 launch **in parallel** against frozen contracts.
- **W2:** orchestrator integrates: register all services in `Program.cs`, run **one** `dotnet ef migrations add InitialCreate`, build, fix integration errors. Then seed data.
- **W3:** A5 (theme polish) + A6 (tests/docs) in parallel; orchestrator verifies build + tests.
- **W4:** commit, push (if repo + auth verified), final report.

### Subagent prompt template (use verbatim, fill brackets)

```text
You are [AGENT ID] on Akkupi CSE Alliance (ASP.NET Core MVC, .NET 10, EF Core + Npgsql,
PostgreSQL, Identity, Bootstrap 5; NO React/TS/SPA/Node).

Repo root: [abs path]. Work ONLY in: [owned paths].
Do NOT edit: Program.cs, AppDbContext.cs, ACA.Domain/Entities/**, ACA.Application/Interfaces/**,
*.csproj, the .sln, or another agent's folder. Do NOT run `dotnet ef`.
Frozen contracts you must code against: [paste interface + entity signatures].

Task: [scope bullets from the table].
Rules: server-side authorization on every action, validate input, anti-forgery on forms,
paginate lists, no secrets, no N+1 queries. Build your project (dotnet build [csproj]) and
report only: files added, build result, deviations, change requests (one line each).
```

---

## 4. MVP Scope (build this, nothing more)

- **Accounts:** registration, login/logout, Identity, profile (name, photo, bio, batch/session, status), Current Student vs Alumni, authorized edit, admin approval flag.
- **Feed:** text posts, 1+ images with limits, owner edit/delete, author + batch + time, newest-first, paginated.
- **Engagement:** reactions/likes, comments, replies if cheap, no duplicate reaction per member per post.
- **Discovery:** browse/search members, filter by batch/session and member type, filter posts by batch.
- **Notifications:** in-app on reaction/comment, extensible design.
- **Admin:** dashboard, member/batch management, reported-content review, removal, `Admin`/`Moderator`/`Member` roles enforced server-side.

**Architecture:** `src/ACA.Web` (Controllers, Views, wwwroot, Areas/Admin, Extensions, Program.cs) → `ACA.Application` (Interfaces, DTOs, Services) → `ACA.Domain` (Entities, Enums, Common) ← `ACA.Infrastructure` (Persistence, Identity, Repositories, Services). Adjust only with a stated reason.

---

## 5. Non-Negotiable Gates

- Build clean; tests green; site connects to `aca_dev` on `127.0.0.1:5432` (existing host database), password only from user-secrets/env.
- Identity for passwords; anti-forgery on all forms; `[Authorize]`/policies on every protected action.
- Users cannot modify others' content without authority; uploads validated by content type and size.
- Secrets only via env vars / user secrets; no credentials in Git, logs, README, or seed files.
- EF Core migrations applied; indexes on feed ordering, member search, batch filtering; no N+1.
- Pagination everywhere lists can grow; centralized error handling; structured logging.
- Light + dark theme, responsive, Bootstrap 5, accessible forms, empty states.

---

## 6. Definition of Done

1. Repo exists on GitHub with README + `.gitignore` + MIT — or the exact blocker and creation URL is documented.
2. Solution + 5 projects build with `dotnet build`.
3. Identity + role seeding in place; the existing `aca_dev` database (`127.0.0.1:5432`, user `postgres`) connects without committed secrets.
4. Responsive light/dark layout and feed/members/profile/admin screens reachable.
5. One initial migration exists and `dotnet ef database update` succeeds.
6. `dotnet test` passes for ownership/authorization/duplicate-reaction rules.
7. README lets a new developer configure, migrate, and run in three commands.

---

## 7. Final Report (≤ 25 lines, no filler)

- GitHub URL (only if verified) or blocker + creation URL.
- Solution/project tree (one line each).
- Agents run, in what waves, and what each produced.
- Features done vs. deferred (explicitly listed).
- Build result, test count/result, migration status.
- Setup/run commands + env var **names** (`ConnectionStrings__DefaultConnection`, placeholders only).
- Deployment recommendation (NOT Vercel for MVC) and its status.
- Owner action items, numbered.

**Start now: inspect the environment, execute Wave 0, then fan out. Do not stop at a plan.**
