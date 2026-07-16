# HoaiKhoi_SE1950_A02 — Academic Document RAG (Razor Pages, PRN222 A02)

```
HoaiKhoi_SE1950_A02.sln
├── AcademicDocumentRagSystem.RazorPages   (presentation — Razor Pages + SignalR)
├── AcademicDocumentRagSystem.Services      (business logic, DTOs, IEmailService)
└── AcademicDocumentRagSystem.DataAccess    (EF Core DbContext, entities, repositories)
```

Dependency direction: `RazorPages → Services → DataAccess`.
Razor PageModels never touch `DbContext` directly — all data access goes through
services and repositories.

## How to run

```powershell
# from repo root
dotnet restore dotnet-razor/HoaiKhoi_SE1950_A02/HoaiKhoi_SE1950_A02.sln
dotnet build   dotnet-razor/HoaiKhoi_SE1950_A02/HoaiKhoi_SE1950_A02.sln
dotnet run --project dotnet-razor/HoaiKhoi_SE1950_A02/AcademicDocumentRagSystem.RazorPages
```

Then open `https://localhost:7150/` — the **Login page is the default start page**.

Sign in with the existing accounts in the shared database, or the configured admin
account (`appsettings.json → AdminAccount`). Role-based landing pages:

| Role    | Lands on         | Can do                                      |
| ------- | ---------------- | ------------------------------------------- |
| Admin   | `/Admin/Index`   | Course CRUD + search, Account CRUD + search |
| Teacher | `/Teacher/Index` | View live course catalogue (auto-updates)   |
| Student | `/Student/Index` | Welcome page                                |

## Configuration (`appsettings.json`)

All secrets use **placeholders** — never commit real values. Prefer
[user-secrets](https://learn.microsoft.com/aspnet/core/security/app-secrets) for
local development:

```powershell
cd dotnet-razor/HoaiKhoi_SE1950_A02/AcademicDocumentRagSystem.RazorPages
dotnet user-secrets init
dotnet user-secrets set "Smtp:Host"     "smtp.gmail.com"
dotnet user-secrets set "Smtp:UserName" "you@gmail.com"
dotnet user-secrets set "Smtp:Password" "your-app-password"
dotnet user-secrets set "Smtp:FromEmail" "you@gmail.com"
```

| Section             | Purpose                                                 |
| ------------------- | ------------------------------------------------------- |
| `ConnectionStrings` | SQL Server connection (same DB as MVC)                  |
| `AdminAccount`      | Config-based admin login                                |
| `RagService`        | Existing Python RAG service base URL                    |
| `App:LoginUrl`      | Login link included in the lecturer onboarding email    |
| `Smtp`              | SMTP host/credentials for outgoing email (placeholders) |

## SMTP email (lecturer onboarding)

When an **Admin creates a Teacher/Lecturer account**, the account is saved and then
`AccountService` calls `IEmailService` to send the new lecturer an email containing:

- their login email,
- the temporary password assigned during creation,
- the assigned course (code + name), and
- the login URL (`App:LoginUrl`).

Email is **never sent from the PageModel** — the PageModel calls the service, and the
service sends the email after the account is persisted. If sending fails, the account
is **not** rolled back: the exception is logged and the admin sees a yellow warning
("account created, but email failed"). Configure the `Smtp` section to enable it; if
left blank, account creation still succeeds and the warning explains SMTP is not set.

## SignalR — real-time course list

One SignalR feature: when an Admin **creates/updates/deletes a course**, lecturers
viewing `/Teacher/Courses` see the list update **without refreshing**.

- Hub: `Hubs/CourseHub.cs`, mapped at **`/hubs/courses`** (registered in `Program.cs`).
- After a successful service-layer CRUD call, the Courses PageModel broadcasts
  `CourseCreated` / `CourseUpdated` / `CourseDeleted` **and** a coarse `CoursesChanged`
  event through `IHubContext<CourseHub>`.
- `wwwroot/js/course-realtime.js` subscribes to `CoursesChanged` and re-fetches only
  the course-table fragment (`/Teacher/Courses?handler=Table` → `_CourseTable` partial),
  swapping it into the page. The full page is never reloaded.

## Notes / scope

- The Razor app focuses on the PRN222 A02 requirements (Razor Pages + SignalR +
  3-layer + Repository + CRUD/search/validation + SMTP) over the existing Academic
  Document RAG domain (Course = subject, Teacher = lecturer).
- Document upload and the RAG chatbot remain available in the MVC app and were not
  re-implemented here; the shared services for them are still registered.
