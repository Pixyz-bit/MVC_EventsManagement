# Project scope and source application

Last updated: 2026-11-24 (Asia/Manila).

Source application (reference; preserve unchanged):
`C:\Martin Archive\Programming\ASP NET\241611JalopEventsManagement\241611JalopEventsManagement`

Destination solution:
`C:\Martin Archive\Programming\ASP NET\MVC_QCUEventsManagement`

Destination application:
`C:\Martin Archive\Programming\ASP NET\MVC_QCUEventsManagement\MVC_QCUEventsManagement`

Migrate the ASP.NET Web Forms application on .NET Framework 4.7.2 to the existing ASP.NET Core MVC application using Entity Framework Core and SQL Server. Preserve its features, business rules, permissions and workflows. The user explicitly requires a NEW, EMPTY database named MVC_EventsManagement: do not reuse the old database or copy/recreate its data. Source SQL defines behavior and a starting schema, not a data-migration target. Redesign the UI/UX using user-supplied references and Tailwind CSS, as specified in DESIGN.md. The old UI is not the visual target. Its screenshots may help understand behavior only.

Observed source: administrator and student portals, session access helpers, PBKDF2 password helper, ADO.NET repositories, stored procedures, database scripts, local fonts/images/scripts, QR generation/scanning, and PNG ticket export. Repository/page logic and SQL procedures must both be inspected for rules.

The destination targets net11.0; SDK 11.0.100 builds the original starter successfully with zero warnings/errors. Exact package/setup versions belong in [setup](SETUP.md). Existing starter Bootstrap references remain untouched until the Tailwind/layout milestone.

On 2026-11-23 the user explicitly authorized fictional sample accounts/events/registrations for local visual previews. This is an additive development-only dataset in MVC_EventsManagement, not an import of source data; existing records are preserved. See SETUP.md and DATABASE_AND_SECURITY.md for the explicit command and restrictions.

On 2026-11-24 the user explicitly requested preview population and unscanned QR passes for BOTH local applications. This authorizes additive, marked fictional records in localhost MVC_EventsManagement and UniversityEventDB, without copying real data between them, changing their schemas or editing the Web Forms source. The dedicated LocalAdminPreview utility is outside the application runtime; operational credentials, manifests and QR images remain private and untracked. This narrow authorization supersedes the earlier prohibition on source-database access only for the requested local preview work.
