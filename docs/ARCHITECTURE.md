# Architecture and folder structure

Last updated: 2026-11-21 (Asia/Manila).

Use MVC controllers and Razor views, not Areas, React, Next.js, or a separate SPA. Flow: controller -> service -> ApplicationDbContext -> SQL Server. Controllers handle HTTP requests; services own business operations, queries, and transactions. Views receive view models rather than binding writable EF entities directly. Do not introduce a generic repository layer. Supporting classes may be added to the appropriate existing responsibility when needed.

Planned structure (documentation and Git setup are established; application changes remain pending):

```text
MVC_QCUEventsManagement/                  # Solution/repository root
|-- AGENTS.md                            # Working rules and documentation index
|-- DESIGN.md                            # Design rules and page reference register
|-- docs/                                # Topic documents; see AGENTS.md for ownership
|-- .gitignore
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
|   |       |-- <timestamp>_InitialBaseline.cs
|   |       |-- <timestamp>_InitialBaseline.Designer.cs
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
|   `-- Program.cs
`-- MVC_QCUEventsManagement.Tests/         # Meaningful rule/integration/browser tests
```

EF configuration files define table/column mappings, constraints, keys, indexes, and relationships. EF migration files describe versioned schema changes and the snapshot; generate their actual names through EF tooling. Proposed entities must be verified against the schema; display-only or operation-result models do not automatically imply additional tables.

Views/Admin maps naturally to AdminController and Views/Student to StudentController. Keep action methods focused and delegate logic to services. Revisit controller splitting only if growth justifies it and update this section before changing conventions.
