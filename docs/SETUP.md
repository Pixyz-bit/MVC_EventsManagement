# Setup and deployment

Last updated: 2026-11-21 (Asia/Manila).

Local Git is initialized at the solution root. Documentation ownership/navigation is defined in [AGENTS.md](../AGENTS.md); progress belongs in [milestones](MILESTONES.md). The original MVC starter was reviewed and built successfully using SDK 11.0.100 (zero warnings/errors) before EF implementation. Its baseline is included in the M2 starting checkpoint. Source application/database are not changed.

Development SQL Server: (localdb)\MSSQLLocalDB (user-owned instance started successfully). Default and alternate full SQL Server services are stopped; starting the default service failed due to Windows service permissions. The new application will use database MVC_EventsManagement. Do not connect it to the old catalog or copy any data.

Pending M2 checks: installed EF packages/tooling, generated schema, rolled-back persistence tests and final run/publish. Frontend tooling, functional workflows, deployment target and camera checks belong to later milestones.

As work proceeds, replace pending setup items here with exact verified commands and prerequisites for: SDK/tool versions, package restore, secret configuration (names/placeholders only), database adoption/fresh creation, local run, Tailwind watch/build, tests and publish. Do not present speculative commands as tested instructions.
