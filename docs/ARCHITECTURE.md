# Architecture and folder structure

Last updated: 2026-11-21 (Asia/Manila).

Use MVC controllers and Razor views, not Areas, React, Next.js, or a separate SPA. Flow: controller -> service -> ApplicationDbContext -> SQL Server. Controllers handle HTTP requests; services own business operations, queries, and transactions. Views receive view models rather than binding writable EF entities directly. Do not introduce a generic repository layer. Supporting classes may be added to the appropriate existing responsibility when needed.

M2 implemented the five EF entities, context/configurations, InitialCreate migration, EventClock, and SQL-backed tests. M3 established the Razor login, shared account layout, and Tailwind files. M4 connects the form to `AccountController` and `AccountService`, with `AccountCookieEvents` revalidating the account and `FirstAdminCommand` providing setup. M5 replaces the administrator landing with `Views/Admin/Events.cshtml`. `AdminController` calls `EventService` to filter and paginate through EF Core, then passes an event list view model to Razor. `/student` still uses the shared protected landing view.

```text
MVC_QCUEventsManagement/                  # Solution/repository root
|-- AGENTS.md                            # Working rules and documentation index
|-- DESIGN.md                            # Design rules and page reference register
|-- docs/                                # Topic documents; see AGENTS.md for ownership
|-- .gitignore
|-- global.json                          # SDK 11.0.100, stable patch roll-forward
|-- dotnet-tools.json                    # Project-local EF CLI 11.0.0
|-- MVC_QCUEventsManagement.slnx
|-- MVC_QCUEventsManagement/              # Web application
|   |-- Controllers/
|   |   |-- AccountController.cs
|   |   |-- AdminController.cs
|   |   |-- StudentController.cs
|   |   `-- HomeController.cs
|   |-- Data/
|   |   |-- ApplicationDbContext.cs
|   |   |-- Configurations/
|   |   |   |-- UserConfiguration.cs
|   |   |   |-- StudentConfiguration.cs
|   |   |   |-- EventConfiguration.cs
|   |   |   |-- SponsorConfiguration.cs
|   |   |   `-- EventRegistrationConfiguration.cs
|   |   `-- Migrations/
|   |       |-- 20261121070748_InitialCreate.cs
|   |       |-- 20261121070748_InitialCreate.Designer.cs
|   |       `-- ApplicationDbContextModelSnapshot.cs
|   |-- Models/
|   |   |-- User.cs
|   |   |-- Student.cs
|   |   |-- Event.cs
|   |   |-- Sponsor.cs
|   |   `-- EventRegistration.cs
|   |-- ViewModels/
|   |   |-- Account/
|   |   |-- Admin/
|   |   `-- Student/
|   |-- Services/
|   |   |-- EventClock.cs                # Implemented Philippine wall-clock service
|   |   |-- AccountService.cs
|   |   |-- StudentService.cs
|   |   |-- EventService.cs
|   |   |-- RegistrationService.cs
|   |   `-- AttendanceService.cs
|   |-- Views/
|   |   |-- Account/                      # Login
|   |   |-- Admin/
|   |   |-- Student/
|   |   `-- Shared/                       # Layouts and reusable partials
|   |-- Styles/
|   |   `-- app.css                       # Tailwind source and shared tokens
|   |-- DesignReferences/
|   |   |-- Account/
|   |   |-- Admin/
|   |   `-- Student/
|   |-- wwwroot/
|   |   |-- css/
|   |   |   `-- app.css                   # Generated Tailwind CSS
|   |   |-- js/
|   |   |   |-- site.js
|   |   |   |-- admin/
|   |   |   `-- student/
|   |   |-- images/
|   |   `-- fonts/
|   |-- package.json                      # Tailwind development/build scripts
|   |-- package-lock.json                 # Reproducible frontend dependencies
|   |-- appsettings.json
|   |-- appsettings.Development.json
|   |-- packages.lock.json               # Locked .NET dependency graph
|   `-- Program.cs
`-- MVC_QCUEventsManagement.Tests/         # Meaningful rule/integration/browser tests
```

EF configuration files define table/column mappings, constraints, keys, indexes, and relationships. EF migration files describe versioned schema changes and the snapshot; generate their actual names through EF tooling. Proposed entities must be verified against the schema; display-only or operation-result models do not automatically imply additional tables.

Views/Admin maps naturally to AdminController and Views/Student to StudentController. Keep action methods focused and delegate logic to services. Revisit controller splitting only if growth justifies it and update this section before changing conventions.

M2 configuration: Program registers a request-scoped ApplicationDbContext, singleton TimeProvider.System and singleton EventClock. The EventsDatabase connection must explicitly target MVC_EventsManagement; startup rejects any other catalog, including the old database. Development uses local Windows authentication; deployment must supply its connection through configuration. Migrations are explicit CLI operations, not application-startup side effects. No user/event seeding exists. The five feature services in the target tree will be implemented with their workflows; no empty placeholder service implementations were added.
