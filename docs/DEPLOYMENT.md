# Deployment

ACA is a stateful ASP.NET Core MVC application backed by PostgreSQL. This guide covers why Vercel is
not a fit, what to use instead, the configuration the app requires, how to apply migrations during
release, and a go-live checklist.

---

## 1. Why Vercel cannot host this app

Vercel is built for static sites and short-lived serverless/edge functions. ACA needs:

- a **long-running ASP.NET Core/Kestrel process** that serves Razor-rendered HTML and holds the MVC
  pipeline in memory;
- **persistent cookie-authentication** and Data Protection keys across requests;
- **persistent database connections** to PostgreSQL;
- a **writable, durable file system** (or object storage) for uploaded post/profile images.

Vercel does not provide a .NET runtime, does not run an arbitrary long-lived Kestrel server, and its
serverless filesystem is ephemeral. Uploading a published MVC app there will not work, and there is
no supported "just point it at the container" path. Static assets can be CDN-hosted, but the MVC app
itself needs a .NET/container host.

---

## 2. Recommended hosting

Use a host that runs a normal ASP.NET Core process or container, plus managed PostgreSQL.

| Option | Notes |
| --- | --- |
| **Azure App Service (Linux)** | Simplest path for .NET 10. Set `WEBSITES_PORT`, use App Service configuration/Key Vault for secrets, and Azure Database for PostgreSQL. |
| **Azure Container Apps** | Container-based, scale-to-zero or scale-out, managed ingress; pair with Azure Database for PostgreSQL. |
| **AWS App Runner / ECS Fargate** | Container host with managed TLS and scaling; pair with Amazon RDS for PostgreSQL. |
| **Render / Railway / Fly.io** | Developer-friendly container hosts; add a managed PostgreSQL instance and persistent volume or S3 for uploads. |
| **Linux VM + systemd + Nginx** | Full control; you own TLS, patching, backups, and monitoring. |

Managed PostgreSQL choices include Azure Database for PostgreSQL, Amazon RDS, Neon, Supabase, and
Railway Postgres. **Do not** use a free/ephemeral database for production.

### Container image (example)

A multi-stage build is enough:

```dockerfile
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY . .
RUN dotnet publish src/ACA.Web/ACA.Web.csproj -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
ENTRYPOINT ["dotnet", "ACA.Web.dll"]
```

The app currently stores uploads under `wwwroot/uploads`; on containers that path must be a mounted
persistent volume, or you must implement an object-storage `IImageStorage` (see
[ARCHITECTURE.md](ARCHITECTURE.md#7-image-storage-abstraction)).

---

## 3. Required configuration

Set these as platform environment variables / secrets, not in `appsettings.json`.

| Variable | Production value | Notes |
| --- | --- | --- |
| `ConnectionStrings__DefaultConnection` | Managed PostgreSQL connection string | Include the provider's TLS settings (`SSL Mode=Require`; use full certificate validation when your provider supplies a CA). |
| `ASPNETCORE_ENVIRONMENT` | `Production` | Enables HSTS/exception handler path; disables developer tooling. |
| `AdminSeed__Email` | Real admin email | Only needed for the initial seed. |
| `AdminSeed__Password` | Strong, unique password | Never commit it. Consider omitting after first boot. |
| `Database__SeedOnStartup` | `true` on first boot, then `false` | Seeds roles and the admin. It is idempotent, but leaving it `true` requires the seed password to remain available. |
| `ASPNETCORE_URLS` | `http://+:8080` | For containers; App Service may use `WEBSITES_PORT=8080` instead. |
| `ASPNETCORE_FORWARDEDHEADERS_ENABLED` | `true` | When TLS terminates at a proxy/ingress. Alternatively add `UseForwardedHeaders` in `Program.cs`. |

### HTTPS and forwarded headers

`Program.cs` calls `UseHttpsRedirection()` and uses HSTS outside Development, but it does **not**
currently call `UseForwardedHeaders`. If TLS terminates at a reverse proxy/ingress (the normal PaaS
setup), ensure forwarded headers are enabled — via
`ASPNETCORE_FORWARDEDHEADERS_ENABLED=true` where the host supports it, or by adding
`UseForwardedHeaders` before `UseHttpsRedirection` — otherwise HTTPS redirection can loop. Verify with
a real browser request after deployment.

### Data Protection keys

Cookie authentication depends on ASP.NET Core Data Protection. The default key ring is local to the
instance. For multiple instances or frequent redeploys, persist the key ring to shared storage
(Azure Blob, Redis, a mounted volume) and configure it at startup. This is **not currently
configured**, so scale-out can invalidate cookies and force re-login.

### Uploads

Local uploads are written under `wwwroot/uploads/posts/{yyyy}/`. On a single instance with a
persistent disk this works. On ephemeral or multi-instance hosts, mount a shared volume at that path
or switch to object storage.

---

## 4. Build and publish

```bash
# Restore, build, test
dotnet tool restore --tool-manifest dotnet-tools.json
dotnet build AkkupiCseAlliance.sln -c Release
dotnet test tests/ACA.Tests/ACA.Tests.csproj -c Release

# Publish
dotnet publish src/ACA.Web/ACA.Web.csproj -c Release -o publish
```

The publish output is a self-contained-ready ASP.NET Core app (`ACA.Web.dll`). Run it with
`dotnet ACA.Web.dll` from the publish directory, or use the container image above.

---

## 5. Migrations on deploy

ACA does **not** call `Database.Migrate()` at startup; migrations are an explicit release step. This
is deliberate: it avoids multiple instances racing to migrate and keeps schema changes visible in the
pipeline.

### Recommended: idempotent SQL script

Generate the script in CI (with the release connection string available only to the migration step):

```bash
dotnet tool restore --tool-manifest dotnet-tools.json
scripts/ef.sh migrations script --idempotent -o artifacts/aca-migrations.sql
```

Then apply it once per release, from a single job/instance:

```bash
psql "$ACA_CONNECTION_STRING" -v ON_ERROR_STOP=1 -f artifacts/aca-migrations.sql
```

If `scripts/ef.sh` is unavailable in the build image, run the equivalent directly:

```bash
dotnet ef migrations script --idempotent \
  --project src/ACA.Infrastructure/ACA.Infrastructure.csproj \
  --startup-project src/ACA.Web/ACA.Web.csproj \
  -o artifacts/aca-migrations.sql
```

### Alternative: `database update`

```bash
scripts/ef.sh database update
```

Run this from a **single** release job while the app is not starting new instances, or use a
deployment slot/run-once container. Never let every replica run it concurrently.

### Seeding order

1. Provision the database.
2. Apply migrations.
3. Start the app with `Database__SeedOnStartup=true` once to create roles and the admin, or create
   them explicitly.
4. Set `Database__SeedOnStartup=false` for steady state if you do not want the seed credentials
   present in production.

Seeding is idempotent: existing roles are skipped and an existing admin is only added to the `Admin`
role.

### First deploy sequence

```text
provision PostgreSQL
  → set secrets/env vars
  → apply migrations (single job)
  → deploy app with Database__SeedOnStartup=true
  → verify login + admin area
  → set Database__SeedOnStartup=false
  → redeploy/restart
```

---

## 6. Post-deploy smoke tests

- `GET /Posts` returns the feed (200) and renders the newest posts.
- Register a member; verify login, profile edit, and a batch assignment.
- Create a post with an image; confirm the image URL is served and the file survives a restart.
- Comment, reply, and react; verify the unread badge increments for the recipient and not for the
  actor.
- Create a report through the report service or seed data, then resolve it in `/Admin/Reports`;
  confirm the post disappears when "remove content" is chosen. (There is no member-facing report
  form yet.)
- Visit `/Admin` as a non-staff member; confirm 403/access denied.
- Confirm HTTPS redirect and that cookies are marked secure in the browser.

---

## 7. Go-live checklist

- [ ] PostgreSQL provisioned, backups and point-in-time restore enabled.
- [ ] Production connection string stored as a secret; TLS settings validated.
- [ ] `ASPNETCORE_ENVIRONMENT=Production`.
- [ ] All migrations applied successfully by a single release job.
- [ ] `AdminSeed__Password` is strong and unique; seeding disabled after provisioning (or credentials
      rotated/removed).
- [ ] HTTPS/TLS terminates at the host or ingress; forwarded headers configured; no redirect loop.
- [ ] Data Protection key ring persisted to shared storage if running more than one instance.
- [ ] Uploads on a persistent shared volume, or object storage implemented.
- [ ] Logging/monitoring/alerting configured (application logs + database metrics).
- [ ] Health probe path chosen (the app has no dedicated health endpoint; use `/` or add one).
- [ ] Rate limiting / WAF considered for public endpoints.
- [ ] Smoke tests from section 6 passed.
- [ ] Rollback plan: previous image/build retained, migration strategy understood.
- [ ] No secrets, `.secrets/`, or local connection strings committed to the repository.

---

## 8. Rollback notes

- Application rollback is a redeploy of the previous build/image.
- Database migrations are not automatically reversible in production. Prefer forward-compatible,
  additive migrations; take a backup/snapshot before applying a destructive change.
- If a release fails after migration but before the app is healthy, keep the schema change (usually
  additive) and roll back the app first, then assess the migration.
