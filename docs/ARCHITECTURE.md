# ACA Architecture

This document describes how Akkupi CSE Alliance is structured, the rules that keep the layers
independent, and the deliberate tradeoffs in the current implementation.

---

## 1. Goals and shape

ACA is a server-rendered ASP.NET Core MVC community app. The architecture optimises for:

- a small, understandable codebase with a single deployable web app;
- clear separation between domain rules, application services, persistence, and UI;
- a feed and member directory that stay fast with straightforward offset pagination and indexes;
- keeping infrastructure concerns (EF Core, Identity, file storage) out of the domain model.

It is intentionally **not** a distributed system. There is no message broker, background worker,
cache tier, or API layer yet.

---

## 2. Layers and dependency rules

```
┌────────────────────────────────────────────────────────────┐
│ ACA.Web (ASP.NET Core MVC)                                 │
│ Controllers, Razor views, view models, wwwroot assets      │
└──────────────────────────┬─────────────────────────────────┘
                           │ depends on
┌──────────────────────────▼─────────────────────────────────┐
│ ACA.Application                                            │
│ Service interfaces, DTOs, business services, IAppDbContext │
└──────────────────────────┬─────────────────────────────────┘
                           │ depends on
┌──────────────────────────▼─────────────────────────────────┐
│ ACA.Domain                                                 │
│ Entities + enums, framework-free                           │
└────────────────────────────────────────────────────────────┘

ACA.Infrastructure ──depends on──> ACA.Application, ACA.Domain
(EF Core DbContext, migrations, Identity, LocalImageStorage)
ACA.Web ──depends on──> ACA.Application, ACA.Infrastructure
```

| Project | Contains | References |
| --- | --- | --- |
| `ACA.Domain` | `Post`, `Comment`, `Reaction`, `MemberProfile`, `Batch`, `Notification`, `Report`, `PostImage`; enums (`MemberType`, `ReactionType`, `NotificationType`, `ReportStatus`) | Nothing |
| `ACA.Application` | Service contracts (`IPostService`, …), DTOs, `PagedResult<T>`, `IAppDbContext`, `IImageStorage`, and the service implementations | `ACA.Domain`, EF Core (for `DbSet`/`IQueryable`/`ToListAsync`) |
| `ACA.Infrastructure` | `AppDbContext`, EF Core migrations, `ApplicationUser`, `IdentitySeeder`, `LocalImageStorage`, DI composition | `ACA.Application`, `ACA.Domain`, ASP.NET Core Identity EF Core, Npgsql |
| `ACA.Web` | MVC controllers, Razor views, `Program.cs`, vendored Bootstrap/jQuery assets | `ACA.Application`, `ACA.Infrastructure` |
| `ACA.Tests` | xUnit tests with EF Core InMemory and a fake image store | `ACA.Web` (transitively Infrastructure), `ACA.Application` |

**Rules that keep the layering honest:**

1. `ACA.Domain` has no framework, EF Core, or ASP.NET Core references.
2. Application services depend on abstractions — `IAppDbContext`, `IImageStorage`,
   `INotificationService` — not on `AppDbContext` or the file system.
3. `ACA.Infrastructure` is the only project that knows about Npgsql, Identity stores, the web root,
   or the file system.
4. Controllers stay thin: parse input, call one application service, map exceptions to HTTP results
   / `ModelState`, and render a view. Business rules live in services.
5. `Program.cs` is the composition root: `AddInfrastructure(configuration)` then `AddApplication()`,
   followed by seeding.

`AppDbContext` implements `IAppDbContext`, and DI maps the interface to the same scoped instance:

```csharp
services.AddDbContext<AppDbContext>(options => options.UseNpgsql(connectionString));
services.AddScoped<IAppDbContext>(sp => sp.GetRequiredService<AppDbContext>());
```

---

## 3. Entity relationships

```
ApplicationUser (Identity)
   1 ───── 1   MemberProfile ───── N   Batch
                     │
                     │ BatchId nullable, ON DELETE SET NULL

ApplicationUser
   1 ───── N   Post ───── N   PostImage
                 │
                 ├──── N   Comment ──── 1   Comment (ParentCommentId, self-reference)
                 └──── N   Reaction

ApplicationUser (RecipientId) 1 ───── N Notification (ActorId is a plain nullable Guid)
ApplicationUser (ReporterId)  1 ───── N Report
                                         ├── PostId?       (no FK)
                                         ├── CommentId?    (no FK)
                                         └── TargetUserId? (no FK)
```

| Relationship | Cardinality | Delete behaviour / notes |
| --- | --- | --- |
| `ApplicationUser` → `MemberProfile` | 1:1 | `MemberProfile.UserId` is unique; cascade delete. |
| `Batch` → `MemberProfile` | 1:N | `BatchId` nullable; `SET NULL` when a batch is deleted. |
| `ApplicationUser` → `Post` | 1:N | Cascade delete. Posts are normally **soft-deleted** (`IsDeleted`). |
| `Post` → `PostImage` | 1:N | Cascade delete; `SortOrder` preserves display order; max 4 per post in the service. |
| `Post` → `Comment` | 1:N | Cascade delete; comments are normally **soft-deleted**. |
| `Comment` → `Comment` | self, 1:N | `ParentCommentId` nullable; `RESTRICT` on delete. |
| `Post` + `ApplicationUser` → `Reaction` | N:N via entity | Unique index on `(PostId, UserId)` enforces one reaction per member per post. |
| `ApplicationUser` → `Notification` | 1:N (recipient) | Cascade delete; `ActorId` is **not** a configured FK, so historical notifications can outlive the actor. |
| `ApplicationUser` → `Report` | 1:N (reporter) | Cascade delete. Targets are nullable Guids with **no FK**; the service enforces exactly one target. |

### Identity model

`ApplicationUser : IdentityUser<Guid>` adds `CreatedAtUtc`. Identity tables use `Guid` keys. Roles
are seeded at startup: `Admin`, `Moderator`, `Member`. Registration creates the Identity user,
creates the member profile through `IProfileService.EnsureProfileAsync`, then best-effort assigns the
`Member` role and signs the user in. Email confirmation is not implemented; registration sets
`EmailConfirmed = true`.

### Soft delete and visibility

- Posts: `IsDeleted`; feed and detail queries filter `!IsDeleted`. The row is retained so comments,
  reactions, and reports keep referential integrity.
- Comments: `IsDeleted`; comment listings filter deleted comments, and post comment counts ignore them.
- Members: `IsApproved`; the public directory shows only approved profiles. Moderation can unapprove a
  reported member.
- There are **no global query filters**. Each query applies its own visibility predicate, which keeps
  admin/moderation queries explicit but means new queries must remember the rules.

---

## 4. Service responsibilities

All services are registered scoped in `ACA.Application/DependencyInjection.cs`.

| Service | Responsibilities | Notable rules |
| --- | --- | --- |
| `PostService` | Feed, detail, create, edit, soft delete. Coordinates `IImageStorage`. | Content required, ≤ 5000 chars, trimmed. Max 4 images. Edit/delete require author or admin. Missing post → `KeyNotFoundException`; non-owner → `UnauthorizedAccessException`. Saved uploads are deleted if persistence fails. |
| `CommentService` | Paginated top-level comments with replies, add comment/reply, soft delete. | Content required, ≤ 2000 chars. Parent must exist and belong to the same post. Delete requires author or admin. Sends `Comment`/`Reply` notifications. |
| `ReactionService` | Toggle/replace/remove a reaction. | Same type toggles off; different type replaces in place. Unknown type or deleted post → `InvalidOperationException`. Notifies the post author only when a **new** reaction row is created. |
| `NotificationService` | Create notifications, paginated list, unread count, mark one/all read. | Never notifies self (or empty actor/recipient). Read operations are always scoped to the recipient. |
| `ReportService` | Create reports, list pending, resolve. | Exactly one target (post, comment, or member) and a reason of 3–1000 chars. Resolution records status/reviewer/time; optional removal soft-deletes the target post/comment or unapproves the member. |
| `ProfileService` | Read/update profile, idempotent profile creation, photo replacement. | Full name required ≤ 120; bio ≤ 600; status ≤ 120; batch must exist when supplied; valid member type. Photo updates delete the previous photo after a successful save, and clean up the new file if persistence fails. |
| `BatchService` | Batch catalogue and CRUD. | `(BatchNo, Session)` unique (case-insensitive session); name/session length and positive batch number validation. Deletion reports a friendly error when members still reference the batch. |
| `MemberSearchService` | Paginated approved-member search. | Case-insensitive name contains, optional batch and member type filters, page clamped to the last page, size 12 default / 60 max. |

### Error conventions

| Exception | Meaning | Typical controller mapping |
| --- | --- | --- |
| `InvalidOperationException` | Business rule / validation failure | `ModelState` error or `NotFound` for `GetById` |
| `ArgumentException` | Bad report payload | validation message |
| `UnauthorizedAccessException` | Ownership violation | `Forbid()` / 403 |
| `KeyNotFoundException` | Post not found for edit/delete | `NotFound()` / 404 |

Controllers also pre-check ownership for UX (for example, post edit pages), but the service check is
the authoritative guard.

---

## 5. Authorization model

### Roles

| Role | Capabilities |
| --- | --- |
| `Member` | Read public content; create/edit/delete own posts and comments; react; manage own profile; receive notifications. |
| `Moderator` | Everything a member can do, plus the Admin area: dashboard, batch create/edit, pending reports and resolution. |
| `Admin` | Everything a moderator can do, plus member approval and batch deletion. Admins can also edit/delete **any** post. |

Roles are seeded by `IdentitySeeder` when `Database__SeedOnStartup` is `true`. The seed admin is
created from `AdminSeed__Email` / `AdminSeed__Password`; seeding is idempotent.

### Enforcement points

1. **Authentication** — Identity cookie authentication. `[Authorize]` guards post authoring,
   comments, reactions, notifications, and profile pages.
2. **Area/role authorization** — `[Area("Admin")]` controllers use
   `[Authorize(Roles = "Admin,Moderator")]`; member approval and batch deletion narrow this to
   `Admin`.
3. **Ownership** — `PostService.UpdateAsync/DeleteAsync` and `CommentService.DeleteAsync` compare the
   author to the caller, unless the caller is an admin. This is enforced in the application layer,
   not only in the UI.
4. **Read scoping** — notification queries always include `RecipientId == userId`; `MarkReadAsync`
   only updates a notification owned by the caller.

Named policies (`AdminOnly`, `ModeratorOnly`, `ApprovedMember`) are configured in
`AddInfrastructure`, but the controllers currently use role attributes directly. `ApprovedMember`
is defined for future use and is not referenced by an action.

### Public vs authenticated

Public: feed (`/Posts`), post detail, member directory, member details, batch catalogue, login,
registration. Authenticated: create/edit/delete posts, comments, reactions, notifications, profile,
and the entire Admin area.

---

## 6. Feed and query strategy

### Feed projection

`PostService.GetFeedAsync` builds one query shape shared by the feed and detail pages:

1. `BuildPostQuery` starts from `Posts.AsNoTracking()` filtered by `!IsDeleted` and, optionally, by
   the author's batch (`MemberProfiles.Any(...)`).
2. `ProjectRows` projects directly into a flat row containing author name/photo, batch name, member
   type, content, timestamps, ordered image DTOs, reaction count, non-deleted comment count, and the
   viewer's reaction. Anonymous callers use `Guid.Empty`, which never matches a real user, so
   `MyReaction` is `null`.
3. The query is ordered by `CreatedAtUtc` descending, then offset-paginated and materialised once.

This avoids N+1 loads for authors, images, counts, and the viewer's reaction. The service clamps
`page` to ≥ 1 and `pageSize` to 1–50; `PagedResult<PostDto>` carries `TotalCount`/`TotalPages` and
`HasPrevious`/`HasNext`.

`IsEdited` is a heuristic: `UpdatedAtUtc > CreatedAtUtc + 1 second` (creation sets both timestamps
to the same value).

### Batch filter

The feed can filter by a member's batch via a correlated `EXISTS`-style predicate over
`MemberProfiles`. Posts by authors without a profile are still shown in the unfiltered feed (the
author name falls back to empty), but they do not match a batch filter.

### Comments

`CommentService.GetForPostAsync` loads one page of top-level comments, then all non-deleted replies
for the post, plus the author profiles for both sets. Replies are attached under their top-level
ancestor (the UI renders one level of nesting) using a parent map and a depth cap of 20. This keeps
the UI simple and bounded, at the cost of loading all replies for a page rather than paginating
them independently.

### Member search

`MemberSearchService` filters approved profiles, applies a case-insensitive `Contains` on full name
(`ToLower().Contains(term)`), optional batch and member type, then orders by name and `UserId` for a
stable page. It clamps an out-of-range page to the last page and returns a `PagedResult`.

### Indexes

Defined in `AppDbContext.OnModelCreating`:

| Entity | Index |
| --- | --- |
| `Batch` | unique `(BatchNo, Session)` |
| `MemberProfile` | unique `UserId`; `BatchId`; `MemberType`; `FullName` |
| `Post` | `CreatedAtUtc`; `AuthorId` |
| `PostImage` | `(PostId, SortOrder)` |
| `Comment` | `(PostId, CreatedAtUtc)`; `ParentCommentId` |
| `Reaction` | unique `(PostId, UserId)` |
| `Notification` | `(RecipientId, IsRead, CreatedAtUtc)` |
| `Report` | `(Status, CreatedAtUtc)` |

These support the feed ordering/filtering, member directory filters, comment lookup, notification
badge/count, and moderation queue.

---

## 7. Image storage abstraction

```csharp
public interface IImageStorage
{
    Task<string> SaveAsync(ImageUpload image, CancellationToken ct = default);
    Task DeleteAsync(string url, CancellationToken ct = default);
}
```

- `ImageUpload` carries a `Stream`, file name, content type, and length.
- `LocalImageStorage` writes to `wwwroot/uploads/posts/{yyyy}/{guid}{ext}`, returning a public
  `/uploads/posts/...` URL.
- Validation is byte-level: declared content type must be `image/jpeg`, `image/png`, or
  `image/webp`; the file signature (magic bytes) must match; each file is capped at 5 MB.
- Deletion only touches URLs under `/uploads/posts/` and resolves paths against that root, guarding
  against traversal. Missing/already-deleted files are ignored.
- `PostService.CreateAsync` deletes any uploads it saved if validation/persistence later fails.
- `ProfileService.UpdateAsync` saves the new photo, updates the row, then deletes the previous photo;
  if persistence fails, it deletes the new file and leaves the old photo untouched.

The abstraction is the seam for object storage (S3-compatible, Azure Blob). The Web project only
depends on `IImageStorage`; swapping the implementation requires no service or controller changes.

**Tradeoff:** local disk is not shared across instances and is ephemeral on many PaaS hosts. For a
production multi-instance deployment, either mount persistent shared storage or implement an
S3-compatible `IImageStorage`.

---

## 8. Testing strategy

`tests/ACA.Tests` uses xUnit and `Microsoft.EntityFrameworkCore.InMemory`:

- `TestDb.Create()` creates a unique `Guid`-named InMemory database per test, so parallel tests are
  isolated and no PostgreSQL instance is required.
- `FakeImageStorage` records saved/deleted URLs and never touches the file system.
- `TestData` builders create members, batches, posts, comments, and notifications.
- Tests exercise real service classes (no mocks of the services) and assert on persisted state.

Coverage includes feed ordering/pagination/filtering/projection, soft delete, ownership and admin
override, reaction toggle/replace, comment validation and notification targeting, notification
unread/read behaviour, report validation/resolution, and profile validation/batch/photo rules.

InMemory does **not** enforce relational constraints (unique indexes, FKs), so tests verify service
behaviour, not database constraints. Migration/constraint verification should be done against
PostgreSQL.

---

## 9. Known tradeoffs and next steps

### Current tradeoffs

- **Local image storage** — simple and dependency-free, but not durable or shared on ephemeral
  hosts.
- **Offset pagination** — simple and supports page numbers; slower on very deep pages and can shift
  when new posts arrive.
- **No global query filters** — explicit predicates keep moderation queries flexible, but every new
  query must apply soft-delete/approval rules correctly.
- **Report targets are not FK-constrained** — reports can outlive deleted posts/comments and can
  reference a missing member; resolution tolerates missing targets.
- **Notification actor is not FK-constrained** — notifications display a fallback name when the actor
  profile is missing.
- **No outbox/queue** — notifications are written by a follow-up `SaveChanges` in the same request,
  not an atomic transaction; a failure between the two saves can lose the notification, and there is
  no retry or asynchronous fan-out.
- **In-app only** — no email, push, or real-time delivery; the navbar unread badge refreshes via
  `fetch` on load, focus, and tab visibility changes.
- **Comments load all replies for a page** — acceptable at community scale; revisit for very large
  threads.
- **Reports have a service and moderation queue but no member-facing submission UI** — the
  `ReportService.CreateAsync` contract exists, while the Web project only wires up the Admin area's
  pending list and resolution actions.
- **No caching layer** — every page hits PostgreSQL; indexes carry the current load.
- **Enum/business validation duplicated in controllers and services** — controllers validate for UX,
  services remain authoritative.

### Next steps

1. **Web API for mobile** — expose versioned JSON endpoints (or minimal APIs) over the same
   application services; add token authentication while keeping cookie auth for MVC.
2. **S3-compatible image storage** — implement `IImageStorage` against object storage, serve via CDN,
   and migrate existing local files.
3. **Real-time notifications** — SignalR hub for badge updates and a notification list, with a
   fallback poll.
4. **Email delivery** — confirmation, password reset, and optional email digests (requires an email
   sender abstraction and background delivery).
5. **Operational hardening** — a member-facing report submission form/endpoint, rate limiting, audit
   log/moderation history, health checks, structured logging, and persisted Data Protection keys.
6. **Search** — PostgreSQL full-text/trigram indexes for member and content search.
7. **Feed scalability** — cursor pagination and optional caching for the first page.
8. **Database integrity** — consider a check constraint for report targets and FKs for notification
   actors, accepting the migration/cleanup cost.
