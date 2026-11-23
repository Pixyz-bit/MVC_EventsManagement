# Milestone plan and progress

Last updated: 2026-11-23 (Asia/Manila).

Related specifications: [scope](PROJECT_SCOPE.md), [architecture](ARCHITECTURE.md), [database and security](DATABASE_AND_SECURITY.md), [design and page references](../DESIGN.md), [skills](SKILLS.md), [functionality](FUNCTIONAL_SPEC.md), [testing](TESTING.md), and [setup](SETUP.md). Documentation ownership and commit rules are in [AGENTS.md](../AGENTS.md).

Status vocabulary: Not started, In progress, Blocked, Complete. A blocker identifies the missing input and independent work that can continue. A code milestone requires relevant passing verification and a Git checkpoint. Page-specific reference status is maintained only in DESIGN.md.

| ID | Milestone | Status | Acceptance / remaining work |
| --- | --- | --- | --- |
| M0 | Working agreement, documentation, Git | Complete | Topic-based documentation established and reviewed; local Git initialized; initial checkpoint 7819c18 recorded. Selected skill installation is pending tooling work in M2. |
| M1 | Functional inventory and migration specification | Complete | Static page/action inventory, functional rules/acceptance cases, schema/procedure coverage and gap register documented. 17 portal pages plus entry page and 67 repository procedures inventoried; static name audits passed. Running-source/live-schema verification remains explicitly pending in M2/testing. |
| M2 | MVC foundation and EF data model | Complete | Starter baseline committed (8e785e5). EF foundation and InitialCreate verified. The development app and integration tests now use the new MVC_EventsManagement database on SQL Server at localhost. See [testing](TESTING.md) for results. |
| M3 | Tailwind pipeline and reference-led shared UI | In progress | `/login` uses the white QCU split layout from the selected reference, with a mobile form view and password visibility control. Extend the styling to other screens as their designs are selected. |
| M4 | Authentication and authorization | Complete | Live login, protected role entry pages, logout, antiforgery and account revalidation verified. First-admin setup command is available. Ownership checks for specific records follow their feature milestones. |
| M5 | Administrator event management | In progress | Reference-led directory and creation workflow are implemented with validated sponsor/audience/banner saving. Registration dates use SQL date/DateOnly with full-day Philippine boundaries; the relevant tests pass. Temporary preview routes are removed; use normal administrator sign-in. Creation includes requested copy cleanup and STEP 01–05 tabs. Event details, editing, cancellation and history are implemented. History derives completion at EventEnd without a status-writing job; remaining cross-workflow verification follows M8-M11. |
| M6 | Students and account management | In progress | Student Directory implemented with profile/account controls, protected snapshots, CSV template/import/export and SQL/HTTP checks. Account Management includes role/access changes, protected account updates and filtered export. Broader cross-workflow verification remains. |
| M7 | Student discovery and profile | Not started | Eligible event browsing/filtering, schedules/availability/details, profile and password operations; reference-led pages and ownership checks. |
| M8 | Registration and event passes | Not started | Registration steps/year/section, server-side dates/eligibility/capacity, duplicate/concurrent prevention, cancellation, pass ownership, compatible QR references and PNG downloads. |
| M9 | Attendance workflows | In progress | Pre-registered implemented with approved column order, email removed, filters, modal and atomic pass cancellation. Scanner lookup/review/explicit confirmation, wrong-event/cancelled/duplicate rejection and atomic timestamps are implemented. Attendance listing and shared headers are implemented; live camera/browser and broader concurrent integration checks remain. |
| M10 | Analytics, history, supporting pages | In progress | Approved Event History implemented with effective outcomes, academic-year/search/outcome filters and read-only details navigation. Analytics metrics, arrivals, distributions/audit, cohorts and history report navigation are implemented with SQL/HTTP coverage. Development-only diagnostics, live report-layout checks and remaining error states are pending. |
| M11 | Complete workflow verification and publish readiness | Not started | All docs/TESTING.md acceptance checks, clean build/test/publish, CSS included, fresh database setup validated, docs/SETUP.md commands current, final documentation reconciliation and Git checkpoint. |

M2 and M3 can advance independently where dependencies allow. Authentication can begin before all references exist. Each feature milestone includes UI/backend integration, relevant verification, documentation updates and commits; do not postpone all testing to M11. No fixed completion dates are promised before inventory and references are available.

Page organization now follows one controller, page model and view per application page; see [architecture](ARCHITECTURE.md) for the file map. Event creation exposes nine supported programs and rejects unsupported codes. The schema location guide is in [database and security](DATABASE_AND_SECURITY.md#schema-file-locations).

Create Event now shares Event Directory's responsive page padding and heading/subheading treatment. Tailwind, layout scanning and the Release build passed; rendered verification of this spacing increment remains pending because Computer Use URL detection blocked browser access. See [testing](TESTING.md).

## Current handoff

The user approved the Event Details proposal on 2026-11-23 with header/copy refinements. Details, directory View navigation, editing and confirmed cancellation are implemented. All 61 tests pass. History now uses its approved layout and SQL query; its sidebar entry is enabled, titles open read-only details, and report View remains disabled pending analytics. The editor uses a balanced bento grid with shared row heights. Live desktop/mobile history/filter states and editor responsiveness were checked; populated history was verified through HTTP/SQL integration tests.

Event Directory row content now follows the supplied field reference while retaining the shared QCU design. View opens the implemented Event Details page.

M0-M2 are complete. Source application/database are unchanged. Integration fixtures roll back; the development catalog retains locally created administrator/event records. Skill installation state is owned by SKILLS.md, exact commands/limitations by SETUP.md, and page reference readiness by [DESIGN.md](../DESIGN.md). The user's new-database instruction supersedes old-data migration/adoption requirements.

M4 authentication is complete, and the login uses the restored white fade. The M5 sidebar follows the supplied white Web Forms reference with the first MVC branding. The directory and sidebar link to Create Event. The temporary preview routes have been removed; access uses normal administrator sign-in. Creation and banner validation have passing integration coverage, and viewport/step interactions were checked with a rendered fixture. Continue with the event-specific Attendance scanner, Attendance and Analytics before student/account management, per the latest user priority.

The redundant creation footer step counter was removed on 2026-11-23. Event details, editing and cancellation are now implemented.

Directory row spacing and typography were refined on 2026-11-23 and verified on five desktop/mobile widths. Suggested additional row data remains unimplemented pending user selection.

The standalone directory count/time strip was removed and the three main data columns were equalized on 2026-11-23. Event Details/editing/cancellation now use the approved layout and refinements.

Current priority: Student Directory, followed by Account Management. Analytics is deferred at the user’s request; its implemented state and verification history remain in DESIGN.md and TESTING.md. Student Directory is implemented with the approved design, actual MVC data, student account access/reset and CSV workflows. Account Management implements the View-only row and tabbed profile. The student dashboard has a Web Forms-inspired UI sample ready for review. Student Directory now shares the Account Management filter-panel style. The source remains unchanged; regression fixtures roll back and the existing sample catalog is retained. Live layout/camera verification remains pending as documented in TESTING.md.
