# Milestone plan and progress

Last updated: 2026-11-21 (Asia/Manila).

Related specifications: [scope](PROJECT_SCOPE.md), [architecture](ARCHITECTURE.md), [database and security](DATABASE_AND_SECURITY.md), [design and page references](../DESIGN.md), [skills](SKILLS.md), [functionality](FUNCTIONAL_SPEC.md), [testing](TESTING.md), and [setup](SETUP.md). Documentation ownership and commit rules are in [AGENTS.md](../AGENTS.md).

Status vocabulary: Not started, In progress, Blocked, Complete. A blocker identifies the missing input and independent work that can continue. A code milestone requires relevant passing verification and a Git checkpoint. Page-specific reference status is maintained only in DESIGN.md.

| ID | Milestone | Status | Acceptance / remaining work |
| --- | --- | --- | --- |
| M0 | Working agreement, documentation, Git | Complete | Topic-based documentation established and reviewed; local Git initialized; initial checkpoint 7819c18 recorded. Selected skill installation is pending tooling work in M2. |
| M1 | Functional inventory and migration specification | Not started | Inspect every page/action, helper, repository, script and SQL procedure. Record inputs, permissions, rules, side effects, failures, dependencies, and acceptance cases in docs/FUNCTIONAL_SPEC.md. Compare with running source when available. |
| M2 | MVC foundation and EF data model | Not started | Build starter; verify tool/package versions; configure DI, context, services, logging/errors/secrets; map actual schema; verify reads/writes and both database-baseline paths without data loss. |
| M3 | Tailwind pipeline and reference-led shared UI | Not started | Install/configure local Tailwind tooling, reproducible production build, CSS source/output paths, remove Bootstrap coupling, establish tokens/layouts/partials from first supplied references; verify mobile/desktop. |
| M4 | Authentication and authorization | Not started | Cookie login/logout, role redirects, access denial, hashes, legacy-password handling, active-account/session checks, ownership foundations, antiforgery, reference-led login. |
| M5 | Administrator event management | Not started | Event listing, creation, editing, details, audience restrictions, sponsors, registration windows, capacity, banners, cancellation and lifecycle behavior; reference-led pages. |
| M6 | Students and account management | Not started | Student CRUD as supported by source, CSV import/export and failure reporting, admin/student accounts, activation/roles/password operations, transactions and reference-led pages. |
| M7 | Student discovery and profile | Not started | Eligible event browsing/filtering, schedules/availability/details, profile and password operations; reference-led pages and ownership checks. |
| M8 | Registration and event passes | Not started | Registration steps/year/section, server-side dates/eligibility/capacity, duplicate/concurrent prevention, cancellation, pass ownership, compatible QR references and PNG downloads. |
| M9 | Attendance workflows | Not started | Preregistration/attendance lists, QR/manual lookup -> review -> confirm flow, wrong-event/cancelled/duplicate rejection, atomic check-in and timestamps; real-device camera check where available. |
| M10 | Analytics, history, supporting pages | Not started | Verified registration/attendance/cancellation/check-in metrics, charts/filters, event history, development-only diagnostics and error states. |
| M11 | Complete workflow verification and publish readiness | Not started | All docs/TESTING.md acceptance checks, clean build/test/publish, CSS included, fresh/existing database setup validated, docs/SETUP.md commands current, final documentation reconciliation and Git checkpoint. |

M2 and M3 can advance independently where dependencies allow. Authentication can begin before all references exist. Each feature milestone includes UI/backend integration, relevant verification, documentation updates and commits; do not postpone all testing to M11. No fixed completion dates are promised before inventory and references are available.

## Current handoff

Documentation is divided by topic, with one authoritative file per topic. M0 is complete; M1-M11 remain unstarted. Application code is unchanged and untracked pending a reviewed starter baseline. No new skills or packages have been installed. Page reference readiness is tracked in [DESIGN.md](../DESIGN.md).

Next work: M1 functional inventory and M2 framework/database/tooling verification, including a reviewed baseline commit of the existing starter application. Incorporate supplied page references in the design document as they arrive. Independent backend/inventory work can continue while references are pending.
