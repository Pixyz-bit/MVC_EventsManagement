# Setup and deployment

Last updated: 2026-11-23 (Asia/Manila).

Documentation ownership/navigation is defined in [AGENTS.md](../AGENTS.md); progress belongs in [milestones](MILESTONES.md). Original starter baseline commit: 8e785e5. Source application/database are unchanged. Commands below run in PowerShell from the solution root.

## Verified toolchain

| Component | Version / configuration |
| --- | --- |
| .NET SDK/runtime | 11.0.100 / 11.0.0; target net11.0; global.json allows stable patch roll-forward |
| EF Core SQL Server and Design | 11.0.0; Design is private development tooling |
| Project-local dotnet-ef | 11.0.0 in root dotnet-tools.json |
| SQL Server | Default instance at `localhost`, engine 16.0.1000.6 |
| Test stack | Microsoft.NET.Test.Sdk 17.14.1, xunit 2.9.3, xunit.runner.visualstudio 3.1.5, Microsoft.AspNetCore.Mvc.Testing 11.0.0 |
| Dependency reproducibility | packages.lock.json in application and test project; no prerelease packages requested |

## GitHub repository

The configured `origin` fetch/push URL is `https://github.com/Pixyz-bit/MVC_EventsManagement.git`. On 2026-11-22, `git ls-remote --symref origin HEAD` succeeded and returned no refs (empty repository). The local `master` branch was pushed on 2026-11-22 and tracks `origin/master`. Future pushes remain separate user-authorized actions.

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

Applied migrations: `20261121070748_InitialCreate` and `20261121145121_RegistrationDatesOnly`. The latter stores registration opening/deadline as SQL date; see [database and security](DATABASE_AND_SECURITY.md) for conversion semantics. Repeating database update reports up-to-date and does not insert application rows. Migrations are not applied on web startup. Do not execute source SQL installers, use EnsureCreated/EnsureDeleted, or apply an old-database adoption baseline.

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

## Create the first administrator

After applying the EF migration, run this from the solution root in an interactive PowerShell. Substitute the email address you want to use for the administrator account.

```powershell
$env:ASPNETCORE_ENVIRONMENT = 'Development'
dotnet run --project MVC_QCUEventsManagement --no-launch-profile -- --create-admin 'admin@example.com'
```

Enter and confirm a password of 15 to 128 characters at the prompts. Input is hidden and the command exits after creating the account. Only the first administrator can be created this way. The command checks both the administrator role and email uniqueness. Open `/login` and use the administrator email and password to sign in.

## Login and Tailwind

The temporary preview URLs have been removed. Open `http://localhost:5314/login`, then use `/admin/events` for the directory or `/admin/events/create` for creation. Credentials are supplied locally and are not stored in project documentation.

The app starts at `/login`. Administrators can use email and land on the upcoming events list at `/admin`. The same list is available at `/admin/events`. Students can use their email or student ID once their account and profile exist.

Run these commands in `MVC_QCUEventsManagement` when changing Razor styles. Node.js 24.14.0 and npm 11.9.0 were used with Tailwind and its CLI pinned to 4.3.5.

```powershell
npm ci
npm run css:build
# Optional while editing
npm run css:watch
```

Commit the regenerated CSS with style changes. The .NET publish includes the committed CSS without running npm.

## Current limitations

Creation supports optional PNG/JPEG/WEBP uploads through SkiaSharp 4.153.1 (MIT). The locked dependency includes Windows/macOS native assets. Linux deployment needs the matching supported native package and separate verification. Configure `EventImages__RootPath` to a persistent, writable directory outside wwwroot; otherwise storage defaults to `PrivateData/EventBanners` under the running content root. Keep that directory across publish-folder replacements and include it in operational backups. The local creation preview uses the repository's ignored `PrivateData/EventBanners` directory.

Authentication, Event Directory, creation, Event Details/editing/cancellation and Events History are implemented. Open `/admin/events/history` for the historical catalog. Open the directory View link or `/admin/events/{id}` for an existing MVC event; source Web Forms IDs are unrelated. Fresh databases require first-admin setup. The current development database has an active local administrator. Student/account management, registration, attendance, analytics, deployment and camera checks are later milestones. History report actions stay disabled until analytics is implemented. See [testing](TESTING.md) for the checks completed so far.
