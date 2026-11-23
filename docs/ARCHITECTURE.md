# Architecture and folder structure

Last updated: 2026-11-23 (Asia/Manila).

Event creation uses `CreateEventController.Create` (GET/POST `/admin/events/create`), `CreateEventInput`, `Views/CreateEvent/Create.cshtml`, and `wwwroot/js/admin/create-event.js`. `EventService.CreateAsync` validates and saves the event/sponsor graph in one EF SaveChanges transaction. `EventBannerStore` decodes and re-encodes uploaded images outside the public web root; the protected `/admin/events/{id}/banner` action serves them. Existing shared admin layouts/navigation support the active creation entry and an overlay mobile menu for the viewport form. Registration opening/deadline are DateOnly values mapped to SQL date by RegistrationDatesOnly; event start/end remain timestamps. See [database and security](DATABASE_AND_SECURITY.md) for date semantics.

Use MVC controllers and Razor views, not Areas, React, Next.js, or a separate SPA. Flow: controller -> service -> ApplicationDbContext -> SQL Server. Controllers handle HTTP requests; services own business operations, queries, and transactions. Views receive view models rather than binding writable EF entities directly. Do not introduce a generic repository layer. Supporting classes may be added to the appropriate existing responsibility when needed.

M2 implemented the five EF entities, context/configurations, InitialCreate migration, EventClock, and SQL-backed tests. M3 established the Razor login, shared account layout, and Tailwind files. M4 connects the form to `LoginController` and `AccountService`, with `AccountCookieEvents` revalidating the account and `FirstAdminCommand` providing setup. M5 replaces the administrator landing with `Views/EventDirectory/Index.cshtml`. Admin pages share `_AdminLayout.cshtml` and `_AdminNavigation.cshtml` for the sidebar and compact menu. `EventDirectoryController` calls `EventService` to filter and paginate through EF Core, then passes an event list view model to Razor. `/student` uses its own typed protected landing view.

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
|   |   |-- LoginController.cs
|   |   |-- AccessDeniedController.cs
|   |   |-- EventDirectoryController.cs
|   |   |-- CreateEventController.cs
|   |   |-- EventDetailsController.cs
|   |   |-- EventHistoryController.cs
|   |   |-- EventPreRegisteredController.cs
|   |   |-- StudentDirectoryController.cs
|   |   |-- EventAnalyticsController.cs
|   |   |-- EventAttendanceController.cs
|   |   |-- AttendanceScannerController.cs
|   |   |-- PrivacyController.cs
|   |   |-- ErrorController.cs
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
|   |   |-- StudentCsvService.cs
|   |   |-- EventService.cs
|   |   |-- RegistrationService.cs
|   |   `-- AttendanceService.cs
|   |-- Views/
|   |   |-- Login/
|   |   |-- AccessDenied/
|   |   |-- EventDirectory/
|   |   |-- CreateEvent/
|   |   |-- EventDetails/
|   |   |-- EventHistory/
|   |   |-- EventPreRegistered/
|   |   |-- StudentDirectory/
|   |   |-- EventAnalytics/
|   |   |-- EventAttendance/
|   |   |-- AttendanceScanner/
|   |   |-- Student/
|   |   |-- Privacy/
|   |   |-- Error/
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

Use one page controller, a matching view folder, and a dedicated input/view model per application page. GET and POST for the same page stay together. Keep each application C# type in a separate file; EF-generated migration pairs and the Program test hook follow tooling conventions. Share entities, services, layouts, partials and assets across pages. AccountController contains the viewless logout operation; HomeController contains only the entry redirect. The static starter Privacy page has no input/display model because it has no data.

| Page | Controller | Page model | View |
| --- | --- | --- | --- |
| Login | LoginController | ViewModels/Account/LoginInput.cs | Views/Login/Login.cshtml |
| Access denied | AccessDeniedController | ViewModels/Account/AccessDeniedViewModel.cs | Views/AccessDenied/Index.cshtml |
| Event Directory | EventDirectoryController | ViewModels/Admin/EventListViewModel.cs | Views/EventDirectory/Index.cshtml |
| Create Event | CreateEventController | ViewModels/Admin/CreateEventInput.cs | Views/CreateEvent/Create.cshtml |
| Event Details/edit/cancel | EventDetailsController | ViewModels/Admin/EventDetailsViewModel.cs and EventDetailsData.cs; shared CreateEventInput | Views/EventDetails/Index.cshtml |
| Events History | EventHistoryController | ViewModels/Admin/EventHistoryViewModel.cs and EventHistoryItem.cs | Views/EventHistory/Index.cshtml |
| Pre-registered roster/void | EventPreRegisteredController | EventRosterViewModel, EventRosterQuery, EventRosterContext, RosterAttendee, VoidPassInput (each in ViewModels/Admin) | Views/EventPreRegistered/Index.cshtml |
| Attendance Scanner | AttendanceScannerController | AttendanceScannerViewModel, ScanAttendee, ScanLookupResult, CheckInInput (each in ViewModels/Admin) | Views/AttendanceScanner/Index.cshtml |
| Attendance list | EventAttendanceController | EventAttendanceViewModel, EventAttendanceQuery, AttendanceItem | Views/EventAttendance/Index.cshtml |
| Analytics | EventAnalyticsController | EventAnalyticsViewModel, EventAnalyticsQuery and separate report result records | Views/EventAnalytics/Index.cshtml |
| Student Directory | StudentDirectoryController | StudentDirectoryViewModel, StudentDirectoryQuery, StudentProfileData/Input, StudentImportReport/Error (one type per file) | Views/StudentDirectory/Index.cshtml and _QueryFields.cshtml |
| Student landing | StudentController | ViewModels/Student/StudentLandingViewModel.cs | Views/Student/Index.cshtml |
| Error | ErrorController | Models/ErrorViewModel.cs | Views/Error/Index.cshtml |
| Static starter privacy | PrivacyController | None | Views/Privacy/Index.cshtml |

Existing route paths and named routes are retained. EventListItem and RegistrationStatus each have a separate file under ViewModels/Admin. The unused starter Home/Index view is removed. Future pages follow this page-level convention.

M2 configuration: Program registers a request-scoped ApplicationDbContext, singleton TimeProvider.System and singleton EventClock. The EventsDatabase connection must explicitly target MVC_EventsManagement; startup rejects any other catalog, including the old database. Development uses local Windows authentication; deployment must supply its connection through configuration. Migrations are explicit CLI operations, not application-startup side effects. No automatic user/event seeding runs; the explicit Development-only preview command is documented below. The five feature services in the target tree will be implemented with their workflows; no empty placeholder service implementations were added.

Event Details uses GET `/admin/events/{id}` (AdminEventDetails), optional `?edit=true`, and antiforgery-protected POST edit/cancel endpoints under the same route. EventService owns the no-tracking projection and tracked concurrency-checked mutations. `wwwroot/js/admin/event-details.js` handles sponsors, audience filtering, banner preview and the cancellation disclosure.

The details cancellation UI now uses a native dialog controlled by the existing event-details.js. Native modal behavior provides focus containment/Escape dismissal; Close/Keep event return focus to the trigger. The controller retains invalid cancellation input and the submitted concurrency token when rendering an error. Edit fields remain on the same page and use shared event-edit-section/event-edit-heading styles.

EventHistoryController serves the Admin-only named route AdminEventHistory at GET `/admin/events/history`. EventService.GetHistoryAsync projects effective outcomes, global counts, academic-year options, combined filters and pagination through SQL using no tracking. Razor renders normal GET forms/links without a client data service. History title links open details with `from=history`, retaining the History navigation state and Back to history destination; historical events stay read-only.

RegistrationService is request-scoped and owns no-tracking roster projections, normalized filters, metadata/counts, 25-row pagination and atomic administrator pass voiding. EventPreRegisteredController serves GET `/admin/events/{id}/preregistered` (AdminEventPreRegistered) and POST `/admin/events/{id}/preregistered/{registrationId}/void`. `_EventWorkflowNavigation.cshtml` with EventWorkflowViewModel is shared by Details and Pre-registered. `wwwroot/js/admin/event-roster.js` manages native attendee dialogs, confirmation and focus only; persistence uses antiforgery-protected MVC forms. No email is loaded by the roster projection.

AttendanceService is request-scoped. GET `/admin/events/{id}/scanner` (AdminAttendanceScanner) renders the scanner. Antiforgery-protected POST `/lookup` and `/confirm` under that route return JSON. The service projects pass/event/student data without email output and uses one conditional EF ExecuteUpdateAsync for admission. `wwwroot/js/admin/attendance-scanner.js` owns staging, asynchronous request ordering, confirmation and camera lifecycle; `wwwroot/js/vendor/html5-qrcode-2.3.8.min.js` and its Apache-2.0 license are reused unchanged from the source. EventAttendanceController serves Admin-only GET `/admin/events/{id}/attendance` (AdminEventAttendance). AttendanceService owns its no-tracking Present-only projection, event-wide count/options, combined filters and 25-row timestamp/ID ordering. `_EventHeader.cshtml` with EventHeaderViewModel shares title/status, schedule, venue and registered capacity across Details, Pre-registered, Scanner and Attendance; management actions appear only on eligible Details pages.

AnalyticsService projects the selected event’s registrations without email, computes active/present/cancelled summaries and audience/audit groups, and bounds cohort lists to 25 rows. ArrivalSeries pages sparse 5/15/30-minute windows in groups of 24. The report view composes _Arrivals, _Turnout and _Demographics partials within its page folder. Charts use native server-rendered SVG/CSS with exact values and no chart library/CDN. event-analytics.js handles responsive straight polyline coordinates, shared chart tooltips on hover/focus/tap and demographic panel switching/query preservation. The validated Dimension query has a GET fallback. LatestCheckIn is computed globally from current Present timestamps. History report links and the shared workflow navigation use AdminEventAnalytics. DevelopmentDemoData is an explicit CLI-only transaction, never an HTTP endpoint or startup seed; its local catalog and environment restrictions are documented in DATABASE_AND_SECURITY.md.

StudentDirectoryController serves AdminStudentDirectory at GET /admin/students with GET template/export and antiforgery-protected POST create/edit/access/password/import. StudentService owns bounded filters, stable 25-row paging, profile/account transactions and protected snapshots. StudentCsvService parses bounded UTF-8 current/legacy CSV and processes each valid row independently through StudentService. student-directory.js handles native profile/editor/account/import dialogs, focus return, dependent programs and password clearing; it does not persist directly. Existing entities/keys are retained; no Student year/section or schema migration was added.
