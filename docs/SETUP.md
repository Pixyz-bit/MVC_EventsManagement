# Setup and deployment

Last updated: 2026-11-25 (Asia/Manila).

On 2026-11-25 both stopped local apps were restarted; SQL Server was already running. MVC login returned HTTP 200 at `http://localhost:5314/login`, and Web Forms login returned HTTP 200 at `http://localhost:51717/Frontend/Login/Login.aspx`. The MVC published executable currently uses `obj/event-description-verify` with its matching published webroot and the web-project content root. Browser workflow results and the in-app PNG-download limitation are recorded in TESTING.md.

For the explicitly authorized dual-app local admin preview, run from the MVC repository root (both local apps/SQL catalogs must already exist):

```powershell
dotnet restore tools/LocalAdminPreview/LocalAdminPreview.csproj --locked-mode
dotnet build tools/LocalAdminPreview/LocalAdminPreview.csproj --no-restore -c Release
$env:ASPNETCORE_ENVIRONMENT = 'Development'
dotnet tools/LocalAdminPreview/bin/Release/net11.0/LocalAdminPreview.dll --apply nov24-preview
node tools/LocalAdminPreview/render-qr.cjs nov24-preview
```

The verified `nov24-preview` batch adds 38 accounts (36 students, one active admin, one inactive admin), 9 events, 18 sponsors and 160 registrations per database. Events cover Open, Soon, Closed, ongoing, Completed, Cancelled, full capacity, restricted audience and scanner practice. Existing records are preserved. Repeating the same batch validates/reuses it without changing credentials, dates or consumed QR passes; a new explicitly authorized batch is needed for fresh dates/passes after expiry/use. The 8 unscanned practice passes per app currently belong to the 2026-11-25 event ending at 17:00 Philippine time.

Private output lives at `PrivateData/AdminPreview/nov24-preview`: `access.txt` provides local-only credentials; `manifest.json` provides event/pass IDs; `mvc/qr-passes.html` and `webforms/qr-passes.html` are printable sheets beside individual PNGs. These files are excluded from Git and not served by the MVC app. Open the matching event scanner before scanning its app-specific QR. A lookup stages review only; Confirm check-in changes that pass once. QR generation uses the existing bundled encoder and Node built-ins, with a four-module quiet zone and integer-scaled modules; independent decoding is recorded in TESTING.md.

Original Web Forms app (user-requested local run on 2026-11-24): IIS Express uses the existing source `.vs/241611JalopEventsManagement/config/applicationhost.config`, site `241611JalopEventsManagement`, HTTP port 51717. Its login at `http://localhost:51717/Frontend/Login/Login.aspx` returned HTTP 200 and rendered in the ChatGPT in-app browser. The source project and database were not edited; this startup check does not verify sign-in or other legacy workflows. MVC remains separate on port 5314.

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
node --test MVC_QCUEventsManagement.Tests/Client/attendance-scanner.test.cjs
$env:ASPNETCORE_ENVIRONMENT = 'Development'
dotnet run --project MVC_QCUEventsManagement --no-build --no-launch-profile --urls http://127.0.0.1:5293
```

The last command is the verified HTTP-only startup smoke configuration; `/` and `/Home/Privacy` respond successfully. It is not a camera/HTTPS acceptance test. Stop with Ctrl+C. The existing HTTPS launch profile remains available for later browser/camera verification. The HTTP-only smoke can log that no HTTPS redirect port is configured.

Integration tests require the migrated database and default to the same localhost catalog. They roll back test records. `MVC_EVENTS_TEST_CONNECTION` may override the server and authentication for tests while retaining the exact `MVC_EventsManagement` catalog. Rolled-back inserts can consume identity numbers.

`ConcurrencyTests` instead requires Windows and `SqlLocalDB.exe` on PATH (installed SQL Server LocalDB; no Docker/package dependency). It automatically creates a unique `QcuConcurrency_<GUID>` instance and an empty `MVC_EventsManagement` database, applies the EF migrations, and uses independent connections for authenticated concurrent HTTP requests. It ignores `MVC_EVENTS_TEST_CONNECTION` and does not use `localhost` or the existing `MSSQLLocalDB` instance. Its reset/drop operations validate the exact newly created instance/catalog; teardown drops only that disposable database and stops/deletes only its own instance. Data files are under ignored test build output. Run only these checks with:

```powershell
dotnet test MVC_QCUEventsManagement.slnx --no-restore -c Release --filter FullyQualifiedName~ConcurrencyTests
dotnet test MVC_QCUEventsManagement.slnx --no-restore -c Release --filter FullyQualifiedName~WorkflowTests
```

The ordinary full test command includes these checks. If a test process is forcibly terminated, its uniquely named LocalDB instance may remain; inspect the exact `QcuConcurrency_` instance and generated file location before any manual cleanup. Never reset/drop the retained development catalog to run last-admin cases.

The existing demo-seeding test accepts only the production command's supported local server targets (`localhost`, `.`, `(local)`). When `MVC_EVENTS_TEST_CONNECTION` selects a migrated disposable LocalDB catalog, exclude that test with `--filter FullyQualifiedName!~Local_demo_data_is_additive_hashed_complete_and_idempotent` and report it as not rerun. The remaining rollback fixtures run serially through their shared xUnit collection; the dedicated concurrency cases still run competing requests on separate connections. LocalDB 2025 version 17.0.4025.3 was used for the 2026-11-24 isolated checks.

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

User Directory is at `http://localhost:5314/admin/users`. The former `/admin/accounts` and `/admin/students` URLs render that same page; the latter defaults to Student role. MSSQLSERVER is running for the local app. The 2026-11-24 directory increment passed all 155 Release .NET tests, 13 scanner client checks, frontend compilation and local Release publish; see [testing](TESTING.md) for limits.

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

For the explicitly requested local fictional preview dataset, run from the web project folder:

```powershell
$env:ASPNETCORE_ENVIRONMENT = 'Development'
dotnet run -c Release --no-build -- --seed-demo
```

This prints new demo credentials once; save them locally. Optionally set `MVC_DEMO_PASSWORD` to a private 15–128 character value before running, then clear it. The command is restricted to the local MVC database, creates three Sample-prefixed events (ongoing, completed and cancelled), 32 fictional students, 76 mixed-status registrations and a preview administrator, and preserves existing records. The ongoing event runs from two hours before seed time until six hours after seed time. Re-running leaves existing demo passwords/data unchanged. It is not a public preview route or an authentication bypass. Use the emitted event IDs in `/admin/events/{id}/analytics`, `/preregistered`, `/scanner` and `/attendance`. All preview accounts use example.test addresses; demo student IDs begin DEMO-. Do not commit emitted credentials or database exports.

Creation supports optional PNG/JPEG/WEBP uploads through SkiaSharp 4.153.1 (MIT). The locked dependency includes Windows/macOS native assets. Linux deployment needs the matching supported native package and separate verification. Configure `EventImages__RootPath` to a persistent, writable directory outside wwwroot; otherwise storage defaults to `PrivateData/EventBanners` under the running content root. Keep that directory across publish-folder replacements and include it in operational backups. The local creation preview uses the repository's ignored `PrivateData/EventBanners` directory.

Authentication, Event Directory, creation, Event Details/editing/cancellation, Events History, Pre-registered, Attendance Scanner, Attendance and Analytics are implemented. Open `/admin/events/{id}/scanner`, `/admin/events/{id}/attendance` or `/admin/events/{id}/analytics`, or their enabled workflow tabs. Use `/admin/events/{id}/preregistered` or the enabled tab in Details for a real MVC event. Open `/admin/events/history` for the historical catalog. Open the directory View link or `/admin/events/{id}` for an existing MVC event; source Web Forms IDs are unrelated. Fresh databases require an administrator account through first-admin setup or the explicitly requested local demo command. The current development database has an active local administrator. General Account Management, student booking, deployment and camera checks are later milestones. History report actions open Analytics with history return context. See [testing](TESTING.md) for the checks completed so far.

For optional roster visual QA, set `MVC_EVENTS_ROSTER_CAPTURE` to an absolute path under the ignored `MVC_QCUEventsManagement/obj` folder before running the authentication tests. The populated-roster test writes its rendered HTML there while all SQL fixtures roll back. Clear the variable afterward; never commit captured HTML, antiforgery tokens or fixture output. This does not create a public preview route.

The scanner self-hosts the source-compatible html5-qrcode 2.3.8 bundle and Apache-2.0 license under wwwroot/js/vendor; no CDN runtime or additional npm dependency is required. Camera access requires HTTPS or localhost and begins only after Start camera. Permission denial, missing cameras or scanner-load failure retain manual lookup. Hidden/closed pages stop capture; operators restart capture explicitly. Camera operations run sequentially, and each new start refreshes the device list while retaining an available selected device. The Node command above checks lifecycle and lookup races using simulated camera/HTTP adapters; it does not establish physical-camera acceptance. Operator-assisted QR decoding and confirmed admission using the USB webcam were verified in Chrome on 2026-11-24; switching between physical cameras remains unverified. Optional `MVC_EVENTS_SCANNER_CAPTURE` may point to an ignored obj HTML file for the SQL-rolled-back scanner rendering fixture.

For local published-app checks, launch from the publish directory, or explicitly point `--webroot` at that publish directory's `wwwroot` when retaining the source content root. A publish static-asset manifest selects compressed files that are absent from the source webroot; mixing those locations returned empty JavaScript responses during the 2026-11-24 camera check. Starting the published executable from the web project with `--webroot` set to the absolute `obj/qr-camera-publish/wwwroot` path restored the complete scanner script (HTTP 200, 10,794 bytes). A successful page response alone does not verify its script delivery.

Student Directory is available at /admin/students through the enabled sidebar. Admins can create/edit profiles and linked student accounts, change access or reset passwords, download the 10-column template, upload UTF-8 CSV (2 MB/200 rows maximum), and export up to 10,000 filtered profiles. The legacy 12-column import header is supported; the export schema is different and is not a re-import template. Creation/import/reset require an explicitly supplied 15–128 character password. No new package, schema migration or source-data import is required. Account Management is available at /admin/accounts. The Add new student and Batch CSV import actions use the Student Directory dialogs.

Sign in with a student account to open `/student`. The Web Forms-style dashboard links to enrollment, pass and account settings pages. Event banners use the existing EventImages configuration. The student layout serves its styles, fonts and QR/PNG libraries locally.
