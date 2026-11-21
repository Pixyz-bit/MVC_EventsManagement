# QCU Events Management: Project Specification and Milestone Plan

Last updated: 2026-11-21 (Asia/Manila).

## 1. Authority and working rules

This is the single authoritative, authored project document for the MVC migration. Read it before project work. It contains the current specification, architecture, design decisions, setup status, milestone plan, and progress. User instructions take precedence; reconcile this document when decisions change.

- Commit every meaningful, reviewable increment of progress to local Git, including documentation-only work. Implement, verify, update affected sections here, inspect the diff, then commit. Keep checkpoints small; do not wait for an entire large module to finish.
- Maintain code and documentation in the same commit when behavior, architecture, setup, design, or progress changes. Edit the existing authoritative section rather than appending conflicting instructions. Remove superseded statements.
- Give each decision one authoritative section. Refer to it elsewhere instead of repeating it. Do not create competing README, DESIGN.md, roadmap, progress, or migration-plan documents. Dependency licenses, installed skill instructions, generated files, test output, and reference images are not duplicate authored project documentation.
- Follow the established folder structure and page reference/design rules. Do not independently redesign completed pages or introduce a second component/style system.
- Preserve user work. Stage only intended files and inspect for secrets. Exclude IDE state, build output, database backups, private imports/exports, credentials, and personal data from Git.
- Report what changed, checks performed, remaining limitations, and the commit ID. Distinguish planned, implemented, and verified work. Never mark a milestone complete merely because files exist.
- Local commits are authorized by the agreed workflow. Remote pushes, publishing, and production database changes are separate from local development.
- Avoid unnecessary layers and dependencies. Use appropriate tests for consequential behavior; do not add tests that merely mirror trivial implementation details.

## 2. Scope and source of behavior

Source application (reference; preserve unchanged):
`C:\Martin Archive\Programming\ASP NET\241611JalopEventsManagement\241611JalopEventsManagement`

Destination solution:
`C:\Martin Archive\Programming\ASP NET\MVC_QCUEventsManagement`

Destination application:
`C:\Martin Archive\Programming\ASP NET\MVC_QCUEventsManagement\MVC_QCUEventsManagement`

Migrate the ASP.NET Web Forms application on .NET Framework 4.7.2 to the existing ASP.NET Core MVC application using Entity Framework Core and SQL Server. Preserve its features, business rules, permissions, workflows, and compatible existing data. Redesign the UI/UX using user-supplied references and Tailwind CSS, as specified in section 5. The old UI is not the visual target. Its screenshots may help understand behavior only.

Observed source: administrator and student portals, session access helpers, PBKDF2 password helper, ADO.NET repositories, stored procedures, database scripts, local fonts/images/scripts, QR generation/scanning, and PNG ticket export. Repository/page logic and SQL procedures must both be inspected for rules.

The destination is an MVC starter targeting `net11.0`; .NET SDKs 10.0.400 and 11.0.100 were observed installed. Build and package compatibility have not yet been verified. Existing starter Bootstrap references remain untouched until the Tailwind/layout milestone.

## 3. Architecture and folder ownership

Use MVC controllers and Razor views, not Areas, React, Next.js, or a separate SPA. Flow: controller -> service -> ApplicationDbContext -> SQL Server. Controllers handle HTTP requests; services own business operations, queries, and transactions. Views receive view models rather than binding writable EF entities directly. Do not introduce a generic repository layer. Supporting classes may be added to the appropriate existing responsibility when needed.

Planned structure (only this document and Git setup are established initially):

```text
MVC_QCUEventsManagement/                  # Solution/repository root
|-- AGENTS.md                            # This sole project document
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

## 4. Database and security rules

- Inspect the live development schema or a safe database copy and compare it with source scripts. Resolve differences before treating mappings as verified. Never infer the entire schema from model names alone.
- Start with the existing database shape. Preserve identifiers, relationships, account hashes, registrations, attendance, ticket references, and image references.
- Initial scaffolding is an option for discovery, not an instruction to overwrite reviewed files. Organize mappings into Data/Configurations and use a request-scoped DbContext registered through DI.
- Use EF Core/LINQ and asynchronous I/O for application persistence. Extract stored-procedure rules into services plus database constraints/transactions as needed. Do not merely wrap the old ADO.NET layer and call it an EF migration.
- Use read-only projections/no-tracking where appropriate and tracked updates deliberately. Avoid unbounded lists, N+1 queries, and loading entire tables for filters or analytics.
- Select compatible stable SDK, EF packages, SQL Server provider, and local EF tooling versions before installation. Pin reproducible tooling/dependencies; record exact versions and commands in section 10 once verified.
- Establish a reviewed migration baseline with both an existing-database adoption path and a fresh-database creation path. Verify schema equivalence before marking any baseline applied. Test both paths on disposable SQL Server databases. Do not blindly run initial table-creation migrations against populated databases.
- Future schema changes require reviewed migrations and data-preservation checks. Do not automatically apply production migrations on application startup. Do not use EnsureDeleted or historical purge scripts against retained data. Source migration 07 is documented as fresh-install only; older scripts include a student-record purge.
- Preserve business invariants under concurrency: capacity limits, unique registration, and single check-in. A preflight count followed by an unprotected insert is insufficient. Select transactions, uniqueness constraints, and concurrency controls based on verified schema and operations.
- Use cookie authentication and server-side Admin/Student authorization. Replace SessionHelper/AdminPage/StudentPage access responsibilities. Session is not the primary identity or durable data store.
- Preserve verification of existing PBKDF2 hashes. Investigate the source login's plaintext-password fallback and provide a controlled upgrade/reset path for any dependent accounts; do not silently perpetuate it.
- Enforce student ownership, active-account checks, and role-change/deactivation behavior for existing sessions. Add antiforgery protection to modifying forms and AJAX actions. Reject unsafe return URLs and overposting.
- Validate uploads, sizes, supported content, and safe generated filenames. Preserve old image paths through deliberate mapping. Choose upload storage/lifecycle during implementation and document the result here.
- Keep secrets outside committed settings; use development secrets/environment variables. Do not log passwords, connection credentials, or private import data. Do not trim or transform passwords without reviewing compatibility implications.
- Preserve event/registration date semantics; document storage and Asia/Manila display conversions after inspecting current behavior.

## 5. UI/UX, Tailwind, and reference workflow

The user will supply a design reference for each page. Reproduce the supplied visual direction while retaining the source system's required functionality. Do not copy the old Web Forms UI or invent final page designs before their references arrive.

For each page:
1. Record the supplied reference identifier/link/local asset and intended scope in the register below. If the user supplies a local-copyable asset, place it in DesignReferences under its portal; this folder is outside wwwroot and is not served publicly. Do not copy private reference data into public assets.
2. Inspect layout, spacing, typography, colors, navigation, components, responsive behavior, and interactions. Identify functional controls/states absent from the reference.
3. Reuse established shared patterns. If a new reference conflicts with existing shared navigation/tokens, resolve the intended exception or shared update explicitly; do not silently create inconsistent components.
4. Build with Razor, Tailwind, shared partials, and only necessary JavaScript. Reference matching must retain accessibility and required business functionality.
5. Verify desktop/mobile presentation, keyboard use, focus, contrast, form labels, validation, empty/loading/error states, and long/realistic content. Compare against the supplied reference.
6. Update the page register, shared design decisions, milestone status, and checks in this document; commit the increment.

Tailwind implementation conventions:
- Install Tailwind through local frontend build tooling, not a production CDN runtime. Select the supported version and compatible CLI/build integration during M3 after checking official documentation.
- Keep authored Tailwind source, imports, tokens, and minimal shared custom CSS in Styles/app.css. Compile to wwwroot/css/app.css. Do not hand-edit generated CSS.
- Keep npm dependencies and build scripts in the web application's package.json, beside its csproj, and commit its lockfile. No extra frontend application.
- Ensure Tailwind discovers Razor views and JavaScript source. Avoid dynamically concatenated utility classes that the scanner cannot discover; use complete class mappings or the selected version's supported explicit inclusion.
- Provide development watch and production build scripts; integrate production CSS generation with the documented .NET build/publish workflow. Verify a clean checkout can generate CSS with no watch process running.
- Link generated CSS from shared layouts with cache busting. Replace Bootstrap starter styling during M3 rather than mixing competing layout frameworks. Retain unrelated validation dependencies only when needed.
- Keep page interaction scripts in wwwroot/js/admin or student and shared scripts in site.js. Keep production images/fonts in their wwwroot folders. Create per-page CSS only when utilities/shared styles cannot reasonably express a requirement.
- Generated CSS is build output; its final tracking policy must be settled in M3 together with build integration. Regardless of tracking, publishing must reliably include the generated file.

### Page reference and verification register

All references are pending. The rows are a source-derived starting inventory; M1 must confirm every action and supporting page. Status progresses through Awaiting reference -> Reference reviewed -> Implementing -> Implemented -> Verified.

| Portal | Page/workflow | Source reference | Design reference | Status |
| --- | --- | --- | --- | --- |
| Account | Login | Frontend/Login/Login.aspx | Pending | Awaiting reference |
| Shared | Access denied / entry routing | Frontend/AccessDenied.aspx; Default.aspx | Pending | Awaiting reference |
| Admin | Event list | AdminEvents.aspx | Pending | Awaiting reference |
| Admin | Create event | CreateEvent.aspx | Pending | Awaiting reference |
| Admin | Event details/edit/sponsors/cancellation | EventDetails.aspx | Pending | Awaiting reference |
| Admin | Student directory/import/export | StudentList.aspx | Pending | Awaiting reference |
| Admin | Account management | AccountManagement.aspx | Pending | Awaiting reference |
| Admin | Preregistered students | EventPreRegistered.aspx | Pending | Awaiting reference |
| Admin | Attendance list | EventAttendance.aspx | Pending | Awaiting reference |
| Admin | QR/manual attendance terminal | AttendanceScanner.aspx | Pending | Awaiting reference |
| Admin | Analytics | EventAnalytics.aspx | Pending | Awaiting reference |
| Admin | History | EventHistory.aspx | Pending | Awaiting reference |
| Admin | Connection diagnostics (development scope) | TestConnection.aspx | Pending | Awaiting reference |
| Student | Dashboard/discovery | Dashboard.aspx | Pending | Awaiting reference |
| Student | Registration/cancellation | EventRegistration.aspx | Pending | Awaiting reference |
| Student | Pass/QR/download | EventPass.aspx | Pending | Awaiting reference |
| Student | Profile/password | StudentProfile.aspx | Pending | Awaiting reference |

Admin filenames above are under source Frontend/Admin; Student filenames are under Frontend/User. Shared layouts/partials are derived from supplied page references rather than assumed to require separate screens. Backend work may proceed while references are pending; final page design must wait for its reference or an explicit user decision to reuse an established pattern.

### Established design decisions

- Framework: Tailwind CSS with server-rendered Razor MVC.
- Visual direction: page-specific user references; none supplied yet.
- Typography, palette, spacing scale, breakpoints, component rules, and motion: pending reference review. Record actual shared decisions here as they are established.
- Accessibility and responsive behavior are required for all implemented pages.

## 6. Skills and technical references

Research completed; no new skills installed. Existing available design skills: Frontend God Mode, Impeccable, UI/UX Pro Max. Use focused relevant guidance rather than loading every overlapping skill. User architecture, Tailwind, references, and this single-document requirement override generic skill defaults, including requests for React/Next.js or a separate DESIGN.md.

| Candidate | Source | Intended use | State |
| --- | --- | --- | --- |
| aspnet-core | https://github.com/openai/skills/tree/main/skills/.curated/aspnet-core | MVC, DI, routing, authentication, configuration, testing | Recommended; not installed |
| efcore-patterns | https://github.com/aaronontheweb/dotnet-skills/tree/master/skills/efcore-patterns | Supplementary queries, tracking, migrations, transaction guidance | Recommended; not installed |
| webapp-testing | https://github.com/anthropics/skills/tree/main/skills/webapp-testing | Browser flows, screenshots, console errors | Recommended; not installed |

Skills.sh research on 2026-11-21 showed approximately 4.3K, 1.6K, and 210.6K installs respectively; counts are historical selection signals, not quality guarantees. Source instructions were reviewed. Adapt restrictive/community defaults to actual requirements; Microsoft guidance is authoritative for framework behavior. Review install compatibility and supporting scripts when installation begins. Install only selected skills, not entire unrelated packs.

Previously researched installation commands (not executed):
```powershell
npx skills add https://github.com/openai/skills --skill aspnet-core
npx skills add https://github.com/aaronontheweb/dotnet-skills --skill efcore-patterns
npx skills add https://github.com/anthropics/skills --skill webapp-testing
```

Technical references to verify during relevant work:
- ASP.NET Core authentication migration: https://learn.microsoft.com/en-us/aspnet/core/migration/fx-to-core/areas/authentication
- EF reverse engineering: https://learn.microsoft.com/en-us/ef/core/managing-schemas/scaffolding/
- EF migrations: https://learn.microsoft.com/en-us/ef/core/managing-schemas/migrations/
- EF concurrency: https://learn.microsoft.com/en-us/ef/core/saving/concurrency
- Tailwind CLI/build/source detection: https://tailwindcss.com/docs/installation/tailwind-cli

## 7. Milestone plan and progress

Status vocabulary: Not started, In progress, Blocked, Complete. A blocker identifies the missing input and independent work that can continue. A code milestone requires relevant passing verification and a Git checkpoint. Page-specific reference status is maintained only in section 5.

| ID | Milestone | Status | Acceptance / remaining work |
| --- | --- | --- | --- |
| M0 | Working agreement, documentation, Git | Complete | Single specification created and reviewed; local Git initialized and documentation checkpoint prepared. Selected skill installation is pending tooling work in M2. |
| M1 | Functional inventory and migration specification | Not started | Inspect every page/action, helper, repository, script and SQL procedure. Record inputs, permissions, rules, side effects, failures, dependencies, and acceptance cases in section 8. Compare with running source when available. |
| M2 | MVC foundation and EF data model | Not started | Build starter; verify tool/package versions; configure DI, context, services, logging/errors/secrets; map actual schema; verify reads/writes and both database-baseline paths without data loss. |
| M3 | Tailwind pipeline and reference-led shared UI | Not started | Install/configure local Tailwind tooling, reproducible production build, CSS source/output paths, remove Bootstrap coupling, establish tokens/layouts/partials from first supplied references; verify mobile/desktop. |
| M4 | Authentication and authorization | Not started | Cookie login/logout, role redirects, access denial, hashes, legacy-password handling, active-account/session checks, ownership foundations, antiforgery, reference-led login. |
| M5 | Administrator event management | Not started | Event listing, creation, editing, details, audience restrictions, sponsors, registration windows, capacity, banners, cancellation and lifecycle behavior; reference-led pages. |
| M6 | Students and account management | Not started | Student CRUD as supported by source, CSV import/export and failure reporting, admin/student accounts, activation/roles/password operations, transactions and reference-led pages. |
| M7 | Student discovery and profile | Not started | Eligible event browsing/filtering, schedules/availability/details, profile and password operations; reference-led pages and ownership checks. |
| M8 | Registration and event passes | Not started | Registration steps/year/section, server-side dates/eligibility/capacity, duplicate/concurrent prevention, cancellation, pass ownership, compatible QR references and PNG downloads. |
| M9 | Attendance workflows | Not started | Preregistration/attendance lists, QR/manual lookup -> review -> confirm flow, wrong-event/cancelled/duplicate rejection, atomic check-in and timestamps; real-device camera check where available. |
| M10 | Analytics, history, supporting pages | Not started | Verified registration/attendance/cancellation/check-in metrics, charts/filters, event history, development-only diagnostics and error states. |
| M11 | Complete workflow verification and publish readiness | Not started | All section 9 acceptance checks, clean build/test/publish, CSS included, fresh/existing database setup validated, section 10 commands current, final documentation reconciliation and Git checkpoint. |

M2 and M3 can advance independently where dependencies allow. Authentication can begin before all references exist. Each feature milestone includes UI/backend integration, relevant verification, documentation updates and commits; do not postpone all testing to M11. No fixed completion dates are promised before inventory and references are available.

## 8. Functional specification and parity checklist

Initial inventory below is not yet a verified exhaustive specification. During M1 expand each capability with precise inputs, allowed roles, conditions, state transitions, database effects, failure cases, and testable acceptance criteria. Record the discovered rules here once; tests reference those rules.

- [ ] Account access: login, logout, role-specific entry routing, access denial, inactive accounts.
- [ ] Administrator events: create/update/detail/list, registration periods, capacity, campus/department/program/year restrictions, sponsors, image uploads, cancellations, event outcomes/history.
- [ ] Student administration: maintain records and linked accounts, CSV roster import, exports, password resets, transaction/failure behavior.
- [ ] Account administration: administrator/student account maintenance, roles, activation, passwords, profile photos where supported.
- [ ] Student portal: discover eligible events, filter/view details, profile information and password changes.
- [ ] Registration: steps and year/section capture, duplicates, capacity, eligibility, date windows, eligible cancellation, ticket-reference generation.
- [ ] Passes: owned registrations only, QR payload compatibility, downloads and responsive ticket content.
- [ ] Attendance: preregistered lists, manual/QR lookup, attendee review, confirmation, rejected passes, one-time attendance recording.
- [ ] Analytics/history: registration, attendance, cancellation and check-in counts/charts/filters with source-equivalent semantics.
- [ ] Shared: validation, notifications, error handling, navigation, development diagnostics, preserved date and image behavior.

## 9. Verification and definition of done

Use business-rule tests for dates/eligibility/lifecycle/authorization; SQL Server integration tests for mapping, transactions, constraints, concurrency and migrations; browser tests for complete workflows, redesigned pages, downloads, console errors and responsiveness. In-memory EF tests alone cannot establish SQL Server correctness. Verify camera scanning with a real device/browser where available and identify it as unverified until performed.

End-to-end acceptance: administrator creates an eligible event -> student discovers/registers -> student opens/downloads pass -> administrator scans or manually looks up attendee -> reviews and confirms once -> lists, counts, history and analytics reflect the result.

Negative/concurrency cases: unauthenticated and wrong-role direct URLs, another student's pass, inactive users, full events, closed periods, duplicate simultaneous registration, duplicate check-in, cancelled registration, wrong-event pass, invalid uploads, malformed/partially invalid CSV, expired sessions, and conflicting edits.

A feature is done only when required behavior is implemented, its reference-led UI is consistent and accessible, server authorization/validation is enforced, relevant checks pass, affected sections here are current, and work is committed. Report any checks not run and why. Final release checks include clean dependency restore, .NET build/test/publish, frontend production build and generated CSS inclusion, and validated database setup for both supported paths.

## 10. Setup, deployment, and current handoff

Verified now: destination/source identified; source files inspected at overview level; accepted architecture, EF choice, Tailwind choice, reference-led redesign and development rules documented. Local Git initialized at the solution root for the documentation checkpoint; only AGENTS.md and .gitignore are included in this increment. Existing application files remain untracked until their contents are reviewed for an application baseline checkpoint. Starter project and Bootstrap layout were read without edits.

Not yet verified: starter build/run, database connectivity/live schema, frontend tools, EF packages, migrations, app workflows, deployment target, camera access. No user page design references have been supplied. No application implementation changes or package installations are part of the documentation checkpoint.

As work proceeds, replace pending setup items here with exact verified commands and prerequisites for: SDK/tool versions, package restore, secret configuration (names/placeholders only), database adoption/fresh creation, local run, Tailwind watch/build, tests and publish. Do not present speculative commands as tested instructions.

Next work: M1 inventory and M2 framework/database/tooling verification, including a reviewed baseline commit of the existing starter application. Review each supplied page reference and update section 5 when it arrives. Continue independent backend/inventory work when page references are pending. Do not substitute the source UI for missing references.
