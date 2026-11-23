# Database and security rules

Last updated: 2026-11-23 (Asia/Manila).

The temporary no-login preview routes have been removed. Administrator pages require the normal authenticated session; no preview identity, preview configuration, or publish-disabled variant remains.

Event creation is administrator-only and antiforgery-protected. A dedicated input model excludes creator, counters, lifecycle and image paths; the service derives these and rechecks the active administrator. Server validation enforces lengths, positive capacity, ordered same-day event times, supported date bounds, registration dates strictly before the event day, supported audience combinations, target year 1-5 and at most 20 sponsor names of 150 characters. Event and sponsor insertion uses one EF SaveChanges transaction.

The user's date-only registration decision is implemented by migration `20261121145121_RegistrationDatesOnly`: `RegStart` and `RegEnd` are SQL `date`, mapped to .NET `DateOnly` throughout the entity, input and directory projections. EventStart/EventEnd remain SQL `datetime`. The schedule CHECK now allows RegStart <= RegEnd for a one-day registration window; both must precede the event date. Status compares these dates with the date from EventClock's Philippine wall clock, so the deadline remains open for the entire day and closes at the next Philippine midnight. Creation form years remain 1753-9998.

The reviewed migration was applied only to localhost/MVC_EventsManagement, where preflight found zero events. On other existing databases, conversion preserves calendar dates while intentionally discarding registration time components. Downgrading cannot recover those times and the old strict opening/deadline constraint cannot accept new one-day windows; review retained data before attempting a downgrade.

Optional banners allow PNG/JPEG/WEBP up to 5 MiB. SkiaSharp 4.153.1 decodes the actual bytes, accepts only those codecs, bounds dimensions to 8192 per side / 16 million pixels, rejects incomplete decoding, and re-encodes pixels to PNG. Generated GUID filenames are stored outside wwwroot; request filenames never become paths. An administrator-only endpoint serves PNG with `nosniff`. Failed database saves remove their uploaded file; failed file writes remove partial output. A process crash between file write and database save can leave an orphan file; automated orphan reconciliation is not implemented. Configure persistent image storage as documented in [setup](SETUP.md).

- Create a NEW, EMPTY SQL Server database named MVC_EventsManagement through EF Core migrations. Do not connect the application to UniversityEventDB, copy existing data, add sample data, or seed users/events. This supersedes the previous existing-database adoption plan.
- Use the source schema/behavior as reference, with explicit EF mappings and justified constraints. No live old-database inspection or adoption is required. Preserve records created in the NEW database during future changes.
- Initial scaffolding is an option for discovery, not an instruction to overwrite reviewed files. Organize mappings into Data/Configurations and use a request-scoped DbContext registered through DI.
- Use EF Core/LINQ and asynchronous I/O for application persistence. Extract stored-procedure rules into services plus database constraints/transactions as needed. Do not merely wrap the old ADO.NET layer and call it an EF migration.
- Use read-only projections/no-tracking where appropriate and tracked updates deliberately. Avoid unbounded lists, N+1 queries, and loading entire tables for filters or analytics.
- Select compatible stable SDK, EF packages, SQL Server provider, and local EF tooling versions before installation. Pin reproducible tooling/dependencies; record exact versions and commands in docs/SETUP.md once verified.
- Generate and review InitialCreate for the new database. Apply it only to MVC_EventsManagement after verifying the configured server/catalog. No adoption baseline or source SQL installer execution is needed. Verify migration repeatability and absence of pending model changes.
- Future schema changes require reviewed migrations and data-preservation checks. Do not automatically apply production migrations on application startup. Do not use EnsureDeleted or historical purge scripts against retained data. Source migration 07 is documented as fresh-install only; older scripts include a student-record purge.
- Preserve business invariants under concurrency: capacity limits, unique registration, and single check-in. A preflight count followed by an unprotected insert is insufficient. Select transactions, uniqueness constraints, and concurrency controls based on verified schema and operations.
- Authentication uses an ASP.NET Core cookie and server-side Admin/Student authorization. Every cookie-authenticated request rechecks the account's ID, email, role and active status in SQL Server. Cookies expire after a 30-minute sliding interval and require HTTPS outside Development.
- MVC form actions validate antiforgery tokens. Login allows 15 POST attempts per remote IP per minute in the running app. Failed credential checks use a generic message.
- New credentials use ASP.NET Core Identity's `PasswordHasher<User>`. The generated hash embeds the salt, so `PasswordSalt` remains an empty legacy column for newly provisioned accounts. No old credentials are imported. The first administrator is created explicitly through the [setup command](SETUP.md#create-the-first-administrator).
- Enforce student ownership, active-account checks, and role-change/deactivation behavior for existing sessions. Add antiforgery protection to modifying forms and AJAX actions. Reject unsafe return URLs and overposting.
- Validate uploads, sizes, supported content, and safe generated filenames. Preserve old image paths through deliberate mapping. Choose upload storage/lifecycle during implementation and document the result here.
- Keep secrets outside committed settings; use development secrets/environment variables. Do not log passwords, connection credentials, or private import data. Do not trim or transform passwords without reviewing compatibility implications.
- Preserve event/registration date semantics; document storage and Asia/Manila display conversions after inspecting current behavior.

## Schema file locations

The MVC schema is defined by [ApplicationDbContext](../MVC_QCUEventsManagement/Data/ApplicationDbContext.cs) and the five files in [Data/Configurations](../MVC_QCUEventsManagement/Data/Configurations). Versioned schema changes live in [Data/Migrations](../MVC_QCUEventsManagement/Data/Migrations), starting with [InitialCreate](../MVC_QCUEventsManagement/Data/Migrations/20261121070748_InitialCreate.cs); [ApplicationDbContextModelSnapshot.cs](../MVC_QCUEventsManagement/Data/Migrations/ApplicationDbContextModelSnapshot.cs) records the latest model. There is no separately maintained SQL schema file. Generate review SQL using the command in [setup](SETUP.md). The historical source SQL path below is reference material, not the MVC installer.

Program choices are application validation data in CreateEventInput, not seeded database rows. Removing unsupported options requires no schema migration and does not delete existing records. The supported catalog is owned by [the functional specification](FUNCTIONAL_SPEC.md#eligible-01-eligibility).

## Historical source schema inventory (M1)

Evidence: source Backend/Database/Migration/07_FinalDatabaseSchema.sql. This is a fresh-install script, not proof that the existing database is identical. Map explicit table names and SQL types; do not let convention-generated plural names replace them accidentally. All listed foreign keys use NO ACTION on delete/update. Fields are required unless marked nullable in the table below.

| Proposed entity | Actual dbo table | Key and relationships | Fields and mappings |
| --- | --- | --- | --- |
| User | UserTable | UserId int identity PK; referenced by students and event creators | Email nvarchar(150), PasswordHash varchar(256), PasswordSalt varchar(128), Role varchar(50), IsActive bit default 1; all required |
| Student | StudentTable | StudentId varchar(50) PK (not identity); required UserId FK | FirstName/LastName nvarchar(100) required, MiddleName nvarchar(100) nullable; Gender varchar(20), CampusBranch/Department/Program nvarchar(100) required; BirthDate date nullable |
| Event | EventsTable | EventId int identity PK; required CreatedByUserId FK to User | Title/VenueLocation nvarchar(200) required; Description nvarchar(max) nullable; MaxCapacity int; CurrentRegistrations int default 0; EventStart/EventEnd/RegStart/RegEnd datetime; Status varchar(50) default Upcoming; CancellationReason nvarchar(500) nullable; TargetBranch/TargetDepartment/TargetProgram nvarchar(100) nullable; TargetYearLevel int nullable; EventPhotoPath nvarchar(500) nullable |
| Sponsor | SponsorListTable | SponsorEntryId int identity PK; required EventId FK | SponsorName nvarchar(150), CreatedAt datetime; required |
| EventRegistration | EventRegistrationTable | EventRegistrationId int identity PK; required EventId and StudentId FKs | CurrentYearLvl int, CurrentSection varchar(50), Status varchar(50) default NoShow required; CheckInTimestamp datetime nullable |

The source event status CHECK uses binary collation and exact DATALENGTH for Upcoming/Cancelled/Completed. Explicit secondary index: IX_EventRegistrationTable_StudentId. Source schema does NOT declare unique email, unique StudentTable.UserId, unique active student/event registration, role/registration-status CHECK constraints, capacity/date checks, or rowversion tokens. These historical deficiencies are addressed where appropriate by the implemented NEW schema below; no old-data audit/import is needed. A student-to-user one-to-one navigation was intended by source application flows but not enforced by its FK.

No separate cancellation table, attendance table, persistent ticket GUID/security-token column, account-photo column, check-in method/admin audit columns, or registration-created timestamp exists in this schema. Event cancellation is a field on Event; attendance is registration status/timestamp. Projection/helper classes do not imply new entities. Student year/section is deliberately per registration, not StudentTable.

### Repository-to-service migration coverage

All 67 source procedure names below are also present in 00_InstallAll.sql. This verifies name coverage only, not equality of procedure bodies or successful installation/execution. No scripts were executed.

| SQL file | Procedures | Responsibility moving to EF services |
| --- | --- | --- |
| UserRepository.sql | 17 | Identifier lookup, CRUD/account search/statistics, password updates, managed-account transaction helpers, admin guards |
| StudentRepository.sql | 17 | Profile lookup/search, user/profile creation, full update, managed profile helpers, state/password operations, dropdown data |
| EventRepository.sql | 13 | Creation/read/edit, audience predicate/unavailability, completion synchronization, counters, historical queries |
| SponsorRepository.sql | 5 | Per-event/batch reads, add, remove, replace event sponsors |
| RegistrationRepository.sql | 14 | Booking/duplicate lookup, joined views, student cancellation/admin void, scan lookup, check-in, totals/attendance |
| EventCancellationRepository.sql | 1 | Guarded event cancellation and retained reason |

Transaction boundaries must move with rules, not disappear with stored procedures. Source booking locks event row while checking capacity and incrementing, but checks duplicate active registration outside this transaction. Source managed-account edit uses serializable locking across administrator/email/profile invariants. Source student/account creation is transactional; later status/password UI operations can be separate. Source event/sponsor writes are separate. Exact functional outcomes are owned by [functional rules](FUNCTIONAL_SPEC.md), including its GAP register.

### Clock, credentials and non-secret source compatibility

Source RegistrationDateTime stores SQL DATETIME as local app/server time without UTC conversion; UI parsing accepts yyyy-MM-ddTHH:mm with optional seconds. Source SQL uses GETDATE and models use DateTime.Now. The new EventClock instead converts injected TimeProvider UTC to Asia/Manila and returns an Unspecified-kind DateTime for local SQL fields, independent of machine timezone. Future date-sensitive services must use this clock rather than SQL GETDATE or machine DateTime.Now. No old date values are imported.

Legacy PasswordHelper uses a random 32-byte salt, 10,000 PBKDF2 iterations and 32-byte derived hash, stored separately as Base64, via the .NET Framework Rfc2898DeriveBytes(password, saltBytes, iterations) constructor. The source compares decoded hashes with a constant-work loop. The new credential policy above applies only to new accounts. Temporary student credentials will be handled with student administration under M6.

QR references derive from record IDs; redirect GUIDs and SEC/GetHashCode display tokens are not persisted authentication tokens. Server authorization and database state decide admission. Source profile values may be cached in session, so role/status/ownership cannot rely on stale cached fields alone.

## Implemented MVC schema (M2)

MVC_EventsManagement was created on the development server documented in [setup](SETUP.md) using migration 20261121070748_InitialCreate (EF Core 11.0.0). It contains the same five explicit table names/column mappings above, with these intentional integrity improvements for the NEW empty database:

- Unique UX_UserTable_Email and UX_StudentTable_UserId indexes enforce email uniqueness and the optional User -> one Student relationship. Other relations remain required dependent-to-principal, with NO ACTION deletion; no automatic history cascade.
- Filtered unique UX_EventRegistrationTable_ActiveStudentEvent on EventId/StudentId WHERE Status <> 'Cancelled' prevents multiple active passes while allowing cancelled history/rebooking. IX_EventRegistrationTable_StudentId and FK indexes support queries.
- RowVersion SQL rowversion concurrency tokens on EventsTable and EventRegistrationTable detect stale EF updates. They do not alone implement the future booking counter/check-in transactions.
- CHECK constraints enforce exact supported user roles/event/registration statuses, positive/bounded event capacity counters, ordered event/registration schedule with registration dates before event date, target/submitted year 1-5, nonblank registration section, and a nonblank reason for cancelled events.
- NoShow requires null check-in time, Present requires a timestamp, and Cancelled permits the retained timestamp of an administratively voided Present record. This preserves the source's broader admin-void behavior.
- Existing integer identity/string keys, nvarchar/varchar lengths, date/datetime types, defaults and nullable optional fields remain explicit. Student BirthDate is DateOnly? mapped to SQL date. No new cancellation/attendance tables or seed/procedure installations were added.

The migration creates schema and EF history only. All five application tables have zero rows after tests. First-admin provisioning is a later explicit setup task, not a seed in M2. Migration reapplication reported up-to-date; pending-model check found no changes. SQL inspection confirmed five enabled/trusted FKs, ten enabled/trusted CHECKs and the three unique indexes. Current test results are owned by [testing](TESTING.md).

The active development catalog is on the default SQL Server instance at `localhost`. The earlier LocalDB catalog is separate. Connection settings and service commands are in [setup](SETUP.md).

## Event Details mutations

Details/edit/cancel require Admin authorization. Reads use no tracking; POSTs require antiforgery and the submitted eight-byte EventsTable RowVersion. Editing/cancellation recheck unfinished Upcoming state. Concurrent count/capacity/lifecycle changes reject the whole event/sponsor transaction. Edits preserve creator, lifecycle and registration count; capacity cannot fall below that count. Cancellation requires confirmation and a trimmed 1-500 character reason, changes only lifecycle/reason and preserves registration history. Repeated cancellation cannot overwrite the reason. SQL date registration columns are unchanged; no migration was needed. Uploads reuse the protected pixel-validation store. Failed saves clean new files; obsolete files are removed after database success. Cleanup failures are logged without reversing committed updates.

## History reads

History is Admin-only and uses no-tracking SQL queries. It includes stored Completed/Cancelled events and events whose EventEnd is at or before the captured Philippine wall-clock instant. Cancelled takes precedence; otherwise an ended event displays Completed. Reading history does not persist status changes, alter rowversions, modify counts or delete history. This requires no migration or completion job. Existing event mutations already reject ended/cancelled events. Academic years are derived from EventStart (August starts the next year label); SQL applies search, outcome/year filters, ordering and paging. No source records are imported.

## Administrator registration cancellation

Pre-registered reads and pass cancellation require the Admin role and normal account-cookie revalidation. Queries bind the selected event ID, project no email, and apply bounded name/student-ID search, academic filters and pagination in SQL. Details/modal fields come from that same authorized event projection. POSTs require antiforgery, explicit confirmation, the registration rowversion and the event rowversion. The service queries by BOTH event and registration IDs, rejecting wrong-event/missing passes. A single EF SaveChanges transaction changes the pass to Cancelled and decrements the event counter without going below zero; check-in timestamps and registration rows are retained. The event counter is marked modified even at zero so its concurrency token participates. Stale pass or event tokens roll back both writes. Already-cancelled passes reject further changes. Per source policy, Admin may void Present passes and passes on ended/cancelled events; student cancellation remains a separate future rule. Errors redirect to a fresh roster for review, reopening the attendee when still on the current page. No schema migration was needed.

## Attendance lookup and confirmation

Scanner page and JSON actions require Admin authorization/account revalidation; both POST actions require antiforgery. Lookup validates selected-event existence, bounds input to 150 characters, searches that event before other events, and stages without any writes. It accepts registration ID, student ID/email or a TCK reference; both encoded event and registration IDs are checked, including legacy pipe/space suffix compatibility. Email may be used for lookup but is not returned in profile JSON.

Explicit confirmation requires selected event ID, positive registration ID, pass/event rowversions and Confirmed=true. A single conditional EF SQL UPDATE matches these IDs/tokens, NoShow state, Upcoming event and EventEnd greater than the captured Philippine EventClock instant, then writes Present and CheckInTimestamp. An update affecting zero rows cannot overwrite a timestamp: current state is returned as duplicate/cancelled/wrong-event/not-found or Changed requiring another lookup. Booking count is unchanged; returned timestamp is reread from SQL to preserve actual stored precision. No EventStart lower bound was added. Verification-method/admin display metadata is not stored or represented as an audit log. No schema migration was needed. QR codes are lookup references, not authentication secrets.
