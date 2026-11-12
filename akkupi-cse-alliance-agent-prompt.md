# Agent Prompt: Build Akkupi CSE Alliance

## Your Role

Act as a senior .NET software architect and autonomous development agent. Your task is to create the initial repository and project foundation for **Akkupi CSE Alliance (ACA)**, a private-feeling social community for current students, graduates, and alumni of the Akkupi Computer Department.

Work carefully, inspect the available integrations and environment first, and then complete as much of the implementation as the available permissions allow. Do not claim that an action succeeded unless you verify it.

---

## 1. Product Overview

**Product name:** Akkupi CSE Alliance  
**Short name:** ACA  
**Tagline:** Connecting Students, Alumni & Generations.

ACA is a department-focused social platform where current students and alumni can connect and share updates across different batches and sessions.

This is **not** an e-learning platform. Do not build study-material libraries, course modules, online classes, quizzes, or learning-management features.

### Main product goals

- Bring all batches of the Akkupi Computer Department into one community.
- Let current students and alumni create profiles and connect.
- Allow members to share posts, photos, comments, and reactions.
- Make it easy to discover people and posts by batch or session.
- Provide basic community moderation and administration.

---

## 2. Required Technology

Use this stack unless a real technical or hosting constraint prevents it:

- **Backend:** C# and ASP.NET Core MVC, targeting the latest stable supported .NET version available in the environment.
- **Frontend:** Razor Views (`.cshtml`), HTML5, CSS3, Bootstrap 5, and vanilla JavaScript or jQuery where useful.
- **Database:** PostgreSQL.
- **ORM:** Entity Framework Core with the Npgsql PostgreSQL provider.
- **Authentication:** ASP.NET Core Identity.
- **Authorization:** Role-based authorization and policies.
- **Image storage:** Use a suitable external image-storage service only after checking whether its integration and credentials are available. Never commit secrets.
- **API:** Keep the architecture ready for a future versioned Web API for a mobile app, but do not build an unnecessary separate frontend.

### Strict technology restrictions

- Do **not** use React.
- Do **not** use TypeScript.
- Do **not** use Angular, Vue, or another SPA framework.
- Do **not** introduce Node.js as the main application runtime.
- Do not replace ASP.NET Core MVC with a JavaScript full-stack framework.

Use clean, maintainable C# and conventional MVC patterns. Prefer a modular, understandable architecture over unnecessary complexity.

---

## 3. Integrations: Inspect Before Acting

Check the connected services and available permissions before starting:

### GitHub

1. Check the authenticated GitHub account and available repository permissions.
2. Create a repository named **`akkupi-cse-alliance`** under the authenticated account if the integration supports repository creation.
3. Use this description:

   `Digital community connecting current students, graduates, and alumni of the Akkupi Computer Department.`

4. Prefer a public repository unless the owner explicitly chooses private.
5. Initialize with:
   - README
   - Visual Studio `.gitignore`
   - MIT License
6. If repository creation is not supported, do not pretend it was created. Report the limitation and provide the exact repository settings and creation URL. Continue with local project scaffolding if possible.
7. If the repository already exists, inspect it first. Do not overwrite or delete existing work.

### Supabase

1. Check whether Supabase is actually connected and whether a project/database is available.
2. The application requires PostgreSQL. If a usable Supabase PostgreSQL database is available and appropriate, it may be used as the PostgreSQL host.
3. If Supabase is not connected or no database is available, do not invent credentials or create fake connection strings.
4. Keep the application database provider configurable so a standard PostgreSQL host can be substituted later.
5. Never expose database passwords, API keys, or service-role keys in source code, logs, README files, or commits.

### Vercel

1. Check whether Vercel is connected and inspect its available deployment capabilities.
2. **Important:** A traditional ASP.NET Core MVC server is not directly deployable to Vercel as a normal .NET web application.
3. Do not force an incompatible Vercel deployment or convert the application to React/Next.js just to use Vercel.
4. Recommend a .NET-capable host for the MVC application, such as a suitable container or .NET application hosting service.
5. Use Vercel only for a genuinely compatible supporting purpose if one is needed. Do not add an unnecessary frontend solely to use Vercel.

---

## 4. Initial MVP Features

Build the project foundation around these features. Prioritize a working, secure foundation over implementing every feature immediately.

### Accounts and member profiles

- Registration and login.
- ASP.NET Core Identity.
- Member profile with name, profile photo, short bio, batch/session, and current status.
- Member types: Current Student and Alumni.
- Profile editing with appropriate authorization.
- Admin-controlled member approval or verification if required.

### Community feed

- Create text posts.
- Attach one or more images, subject to sensible limits.
- Edit and delete a member's own posts.
- Show author, batch/session, post time, and content.
- Paginated or efficiently loaded feed.
- Latest posts first.

### Engagement

- Like or react to posts.
- Comment on posts.
- Reply to comments if the implementation remains manageable.
- Prevent duplicate reactions by the same member where appropriate.

### Discovery

- Browse and search members.
- Filter members by batch/session and member type.
- Filter or browse posts by batch/session where appropriate.

### Notifications

- Basic in-app notifications for reactions and comments.
- Keep the design extensible for future notification types.

### Administration and moderation

- Admin dashboard.
- Manage members and batches/sessions.
- Review reported posts or members.
- Remove inappropriate content.
- Use roles such as `Admin`, `Moderator`, and `Member`.
- Enforce authorization on the server, not only by hiding UI controls.

---

## 5. Suggested Architecture

Use a clean and practical solution structure. Avoid creating projects that have no clear purpose.

Suggested starting structure:

```text
AkkupiCseAlliance/
├── src/
│   ├── ACA.Web/
│   │   ├── Controllers/
│   │   ├── Views/
│   │   ├── wwwroot/
│   │   ├── Areas/
│   │   │   └── Admin/
│   │   ├── Extensions/
│   │   └── Program.cs
│   ├── ACA.Application/
│   │   ├── Interfaces/
│   │   ├── DTOs/
│   │   ├── Services/
│   │   └── Features/
│   ├── ACA.Domain/
│   │   ├── Entities/
│   │   ├── Enums/
│   │   └── Common/
│   └── ACA.Infrastructure/
│       ├── Persistence/
│       ├── Identity/
│       ├── Repositories/
│       └── Services/
├── tests/
│   └── ACA.Tests/
├── docs/
├── .gitignore
├── README.md
└── AkkupiCseAlliance.sln
```

You may adjust the structure if the existing repository or current .NET conventions justify it. Explain meaningful deviations.

### Initial domain entities

Consider these entities and relationships:

- `ApplicationUser`
- `MemberProfile`
- `Batch`
- `Post`
- `PostImage`
- `Comment`
- `Reaction`
- `Notification`
- `Report`

Use appropriate foreign keys, indexes, constraints, timestamps, and deletion behavior. Avoid storing duplicate information unnecessarily. Do not add every entity blindly if the first implementation phase does not need it.

---

## 6. UI and UX Direction

Create a clean, modern, responsive community interface.

- Use Bootstrap 5.
- Include both light and dark theme support.
- Make the layout comfortable on desktop and mobile.
- Use a recognizable community/social-feed layout.
- Keep navigation simple: Feed, Members, Batches, Notifications, and Profile.
- Use consistent spacing, typography, buttons, forms, and cards.
- Provide empty states, validation messages, loading states, and error feedback.
- Avoid excessive gradients, animations, and decorative UI.
- Do not use a paid theme or copy a proprietary template.
- Keep custom CSS organized in separate files; avoid large inline CSS blocks.

---

## 7. Security and Quality Requirements

- Use ASP.NET Core Identity for password handling and authentication.
- Validate all input on the server.
- Use anti-forgery protection for form submissions.
- Enforce authorization on every protected action.
- Prevent users from editing or deleting other users' content unless authorized.
- Validate uploaded file type and size; do not trust file extensions alone.
- Store secrets in environment variables or user secrets, never in Git.
- Use EF Core migrations.
- Add useful database indexes for feed, member search, and batch filtering.
- Avoid N+1 database queries and load only the data required by each page.
- Use pagination for feeds and member lists.
- Add basic logging and centralized error handling.
- Write tests for important business rules and authorization-sensitive behavior.

---

## 8. Implementation Phases

Work in these phases and keep the repository usable after each phase.

### Phase 1: Repository and foundation

- Inspect integrations and existing repository contents.
- Create or confirm the GitHub repository.
- Create the solution and projects.
- Configure dependency injection and application settings.
- Configure PostgreSQL and EF Core.
- Add ASP.NET Core Identity and roles.
- Add the first migration.
- Create a basic responsive layout with light/dark theme.
- Write setup instructions in README.

### Phase 2: Core community

- Member profile and batch management.
- Registration/login flow.
- Create, edit, delete, and view posts.
- Image upload abstraction and safe validation.
- Feed pagination.

### Phase 3: Engagement and discovery

- Reactions.
- Comments and replies.
- Member search and batch filters.
- Post filtering.

### Phase 4: Administration

- Admin dashboard.
- Member moderation.
- Post reporting and review.
- Basic in-app notifications.

### Phase 5: Deployment readiness

- Production configuration.
- Environment-variable documentation.
- Database migration instructions.
- Health checks and logging.
- Deployment recommendation for a .NET-capable host.
- Deploy only when the target hosting integration supports the application and all required configuration is available.

Do not skip ahead to deployment while the application does not build or run locally.

---

## 9. Working Rules for the Agent

1. Start by inspecting the current working directory, installed .NET SDKs, Git status, and connected integrations.
2. Inspect an existing repository before changing it.
3. Make changes in small, verifiable steps.
4. Build the solution after creating the foundation and after significant changes.
5. Run tests when available.
6. Fix build errors before moving to the next phase.
7. Do not fabricate successful GitHub, Supabase, or Vercel operations.
8. Do not ask for secrets in chat or write them into tracked files.
9. Do not delete existing files or rewrite project history without explicit approval.
10. If a required integration action is unavailable, explain exactly what is blocked and continue with independent work.
11. Do not implement features outside the product scope without a clear reason.
12. Keep documentation concise and accurate.

---

## 10. Final Deliverables

At the end of the work, provide:

- GitHub repository URL, only if successfully created or verified.
- Summary of the solution structure.
- Technology and architecture decisions.
- Features implemented and features not yet implemented.
- Build and test results.
- Database setup and migration commands.
- Local run instructions.
- Required environment variables, with example placeholder names only.
- Hosting recommendation and deployment status.
- Any blocked actions requiring the owner's manual intervention.

### Success criteria

The first milestone is complete when:

- The repository exists or the exact creation limitation is documented.
- The ASP.NET Core MVC solution and projects are created.
- The solution builds successfully.
- PostgreSQL configuration is ready without committed credentials.
- Identity and role setup are in place.
- A responsive starter layout with light/dark theme exists.
- README explains how to configure and run the project.

**Begin by inspecting the available environment and integrations. Then execute Phase 1. Do not stop after producing a plan.**
