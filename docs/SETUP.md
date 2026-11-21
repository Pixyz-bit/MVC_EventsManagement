# Setup and deployment

Last updated: 2026-11-21 (Asia/Manila).

Documentation ownership/navigation is defined in [AGENTS.md](../AGENTS.md); progress belongs in [milestones](MILESTONES.md). Original starter baseline commit: 8e785e5. Source application/database are unchanged. Commands below run in PowerShell from the solution root.

## Verified toolchain

| Component | Version / configuration |
| --- | --- |
| .NET SDK/runtime | 11.0.100 / 11.0.0; target net11.0; global.json allows stable patch roll-forward |
| EF Core SQL Server and Design | 11.0.0; Design is private development tooling |
| Project-local dotnet-ef | 11.0.0 in root dotnet-tools.json |
| SQL Server | Default instance at `localhost`, engine 16.0.1000.6 |
| Test stack | Microsoft.NET.Test.Sdk 17.14.1; xunit 2.9.3; xunit.runner.visualstudio 3.1.5 |
| Dependency reproducibility | packages.lock.json in application and test project; no prerelease packages requested |

## Database configuration

Connect to `localhost` with Windows Authentication and select `MVC_EventsManagement`. EF Core created the five application tables and its migration history. The application tables start empty.

appsettings.Development.json defines the Windows-authenticated connection named EventsDatabase. The localhost connection uses `Encrypt=True` and `TrustServerCertificate=True` for development. Other deployments must provide their own validated encryption and authentication configuration.

Program requires EventsDatabase to target exactly MVC_EventsManagement, rejecting the old catalog before any connection. Production has no default connection: supply `ConnectionStrings__EventsDatabase` through the deployment environment or a secret provider. Development secrets may override the same key if credentials are ever needed. Do not change the database name to the source catalog.

The default Windows service `MSSQLSERVER` must be running. Check it with `Get-Service MSSQLSERVER` and start it from an administrator PowerShell with `Start-Service MSSQLSERVER` if needed.

## Restore and create the empty schema

```powershell
dotnet tool restore
dotnet restore MVC_QCUEventsManagement.slnx --locked-mode
dotnet ef database update --project MVC_QCUEventsManagement -- --environment Development
```

Applied migration: `20261121070748_InitialCreate`. Repeating database update reports up-to-date and does not insert application rows. Migrations are not applied on web startup. Do not execute source SQL installers, use EnsureCreated/EnsureDeleted, or apply an old-database adoption baseline.

To review future model/schema changes before application:

```powershell
dotnet ef migrations has-pending-model-changes --project MVC_QCUEventsManagement -- --environment Development
dotnet ef migrations script --project MVC_QCUEventsManagement --output MVC_QCUEventsManagement/obj/migration-review.sql -- --environment Development
```

The script output belongs under ignored obj; committed migration classes/snapshot are authoritative. Generate future migrations using EF tooling into Data/Migrations and review their SQL. InitialCreate contains no application seed operations.

## Build, test, run, publish

```powershell
dotnet build MVC_QCUEventsManagement.slnx --no-restore -c Release
dotnet test MVC_QCUEventsManagement.slnx --no-restore
$env:ASPNETCORE_ENVIRONMENT = 'Development'
dotnet run --project MVC_QCUEventsManagement --no-build --no-launch-profile --urls http://127.0.0.1:5293
```

The last command is the verified HTTP-only startup smoke configuration; `/` and `/Home/Privacy` respond successfully. It is not a camera/HTTPS acceptance test. Stop with Ctrl+C. The existing HTTPS launch profile remains available for later browser/camera verification. The HTTP-only smoke can log that no HTTPS redirect port is configured.

Integration tests require the migrated database and default to the same localhost catalog. They roll back test records. `MVC_EVENTS_TEST_CONNECTION` may override the server and authentication for tests while retaining the exact `MVC_EventsManagement` catalog. Rolled-back inserts can consume identity numbers.

```powershell
dotnet publish MVC_QCUEventsManagement/MVC_QCUEventsManagement.csproj -c Release --no-restore --output MVC_QCUEventsManagement/obj/publish-smoke
```

Publish output is ignored build output. A published app requires explicit production connection configuration.

## Login preview and Tailwind

Start the app using the command above and open `/login`. The preview uses the shared account layout. Authentication is tracked under M4.

Run these commands in `MVC_QCUEventsManagement` when changing Razor styles. Node.js 24.14.0 and npm 11.9.0 were used with Tailwind and its CLI pinned to 4.3.5.

```powershell
npm ci
npm run css:build
# Optional while editing
npm run css:watch
```

Commit the regenerated CSS with style changes. The .NET publish includes the committed CSS without running npm.

## Current limitations

Feature services/authentication/screens are not implemented. The database intentionally has no administrator; first-admin provisioning must be an explicit later setup operation. Frontend tooling, functional workflow/browser tests, deployment target and real camera checks remain later milestones. Existing Bootstrap starter files remain until M3. See testing for actual verification results rather than assuming pending features work.
