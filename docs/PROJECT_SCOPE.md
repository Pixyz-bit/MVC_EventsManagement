# Project scope and source application

Last updated: 2026-11-21 (Asia/Manila).

Source application (reference; preserve unchanged):
`C:\Martin Archive\Programming\ASP NET\241611JalopEventsManagement\241611JalopEventsManagement`

Destination solution:
`C:\Martin Archive\Programming\ASP NET\MVC_QCUEventsManagement`

Destination application:
`C:\Martin Archive\Programming\ASP NET\MVC_QCUEventsManagement\MVC_QCUEventsManagement`

Migrate the ASP.NET Web Forms application on .NET Framework 4.7.2 to the existing ASP.NET Core MVC application using Entity Framework Core and SQL Server. Preserve its features, business rules, permissions, workflows, and compatible existing data. Redesign the UI/UX using user-supplied references and Tailwind CSS, as specified in DESIGN.md. The old UI is not the visual target. Its screenshots may help understand behavior only.

Observed source: administrator and student portals, session access helpers, PBKDF2 password helper, ADO.NET repositories, stored procedures, database scripts, local fonts/images/scripts, QR generation/scanning, and PNG ticket export. Repository/page logic and SQL procedures must both be inspected for rules.

The destination is an MVC starter targeting `net11.0`; .NET SDKs 10.0.400 and 11.0.100 were observed installed. Build and package compatibility have not yet been verified. Existing starter Bootstrap references remain untouched until the Tailwind/layout milestone.
