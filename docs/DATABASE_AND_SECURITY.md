# Database and security rules

Last updated: 2026-11-21 (Asia/Manila).

- Inspect the live development schema or a safe database copy and compare it with source scripts. Resolve differences before treating mappings as verified. Never infer the entire schema from model names alone.
- Start with the existing database shape. Preserve identifiers, relationships, account hashes, registrations, attendance, ticket references, and image references.
- Initial scaffolding is an option for discovery, not an instruction to overwrite reviewed files. Organize mappings into Data/Configurations and use a request-scoped DbContext registered through DI.
- Use EF Core/LINQ and asynchronous I/O for application persistence. Extract stored-procedure rules into services plus database constraints/transactions as needed. Do not merely wrap the old ADO.NET layer and call it an EF migration.
- Use read-only projections/no-tracking where appropriate and tracked updates deliberately. Avoid unbounded lists, N+1 queries, and loading entire tables for filters or analytics.
- Select compatible stable SDK, EF packages, SQL Server provider, and local EF tooling versions before installation. Pin reproducible tooling/dependencies; record exact versions and commands in docs/SETUP.md once verified.
- Establish a reviewed migration baseline with both an existing-database adoption path and a fresh-database creation path. Verify schema equivalence before marking any baseline applied. Test both paths on disposable SQL Server databases. Do not blindly run initial table-creation migrations against populated databases.
- Future schema changes require reviewed migrations and data-preservation checks. Do not automatically apply production migrations on application startup. Do not use EnsureDeleted or historical purge scripts against retained data. Source migration 07 is documented as fresh-install only; older scripts include a student-record purge.
- Preserve business invariants under concurrency: capacity limits, unique registration, and single check-in. A preflight count followed by an unprotected insert is insufficient. Select transactions, uniqueness constraints, and concurrency controls based on verified schema and operations.
- Use cookie authentication and server-side Admin/Student authorization. Replace SessionHelper/AdminPage/StudentPage access responsibilities. Session is not the primary identity or durable data store.
- Preserve verification of existing PBKDF2 hashes. Investigate the source login's plaintext-password fallback and provide a controlled upgrade/reset path for any dependent accounts; do not silently perpetuate it.
- Enforce student ownership, active-account checks, and role-change/deactivation behavior for existing sessions. Add antiforgery protection to modifying forms and AJAX actions. Reject unsafe return URLs and overposting.
- Validate uploads, sizes, supported content, and safe generated filenames. Preserve old image paths through deliberate mapping. Choose upload storage/lifecycle during implementation and document the result here.
- Keep secrets outside committed settings; use development secrets/environment variables. Do not log passwords, connection credentials, or private import data. Do not trim or transform passwords without reviewing compatibility implications.
- Preserve event/registration date semantics; document storage and Asia/Manila display conversions after inspecting current behavior.

## Source schema inventory (M1; live database verification pending)

Evidence: source Backend/Database/Migration/07_FinalDatabaseSchema.sql. This is a fresh-install script, not proof that the existing database is identical. Map explicit table names and SQL types; do not let convention-generated plural names replace them accidentally. All listed foreign keys use NO ACTION on delete/update. Fields are required unless marked nullable in the table below.

| Proposed entity | Actual dbo table | Key and relationships | Fields and mappings |
| --- | --- | --- | --- |
| User | UserTable | UserId int identity PK; referenced by students and event creators | Email nvarchar(150), PasswordHash varchar(256), PasswordSalt varchar(128), Role varchar(50), IsActive bit default 1; all required |
| Student | StudentTable | StudentId varchar(50) PK (not identity); required UserId FK | FirstName/LastName nvarchar(100) required, MiddleName nvarchar(100) nullable; Gender varchar(20), CampusBranch/Department/Program nvarchar(100) required; BirthDate date nullable |
| Event | EventsTable | EventId int identity PK; required CreatedByUserId FK to User | Title/VenueLocation nvarchar(200) required; Description nvarchar(max) nullable; MaxCapacity int; CurrentRegistrations int default 0; EventStart/EventEnd/RegStart/RegEnd datetime; Status varchar(50) default Upcoming; CancellationReason nvarchar(500) nullable; TargetBranch/TargetDepartment/TargetProgram nvarchar(100) nullable; TargetYearLevel int nullable; EventPhotoPath nvarchar(500) nullable |
| Sponsor | SponsorListTable | SponsorEntryId int identity PK; required EventId FK | SponsorName nvarchar(150), CreatedAt datetime; required |
| EventRegistration | EventRegistrationTable | EventRegistrationId int identity PK; required EventId and StudentId FKs | CurrentYearLvl int, CurrentSection varchar(50), Status varchar(50) default NoShow required; CheckInTimestamp datetime nullable |

The event status CHECK uses binary collation and exact DATALENGTH for Upcoming/Cancelled/Completed. Explicit secondary index: IX_EventRegistrationTable_StudentId. Source schema does NOT declare unique email, unique StudentTable.UserId, unique active student/event registration, role/registration-status CHECK constraints, capacity/date checks, or rowversion concurrency tokens. Those application assumptions need data audits and reviewed changes before stronger EF/database enforcement. A student-to-user one-to-one navigation is intended by application flows but is not guaranteed by a unique FK in this schema.

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

RegistrationDateTime states that stored SQL DATETIME represents local app/server time without UTC conversion; UI parsing accepts yyyy-MM-ddTHH:mm with optional seconds. SQL uses GETDATE while model helpers use DateTime.Now. Choose one explicit clock strategy during M2 and verify the deployment timezone before interpreting existing values; avoid double conversion to UTC or assuming existing values already are UTC.

Legacy PasswordHelper uses a random 32-byte salt, 10,000 PBKDF2 iterations and 32-byte derived hash, with salt/hash stored separately as Base64. It uses the .NET Framework Rfc2898DeriveBytes(password, saltBytes, iterations) constructor; modern verification must explicitly reproduce its algorithm semantics and use fixtures before changing APIs. The source compares decoded hashes with a constant-work loop. This describes compatibility, not a recommended new password strength policy. Plaintext fallback, predictable temporary credentials and inconsistent trimming are tracked under AUTH-02/STUDENT-01 and GAP-04.

QR references derive from record IDs; redirect GUIDs and SEC/GetHashCode display tokens are not persisted authentication tokens. Server authorization and database state decide admission. Source profile values may be cached in session, so role/status/ownership cannot rely on stale cached fields alone.

### M2 database acceptance prerequisites

Before applying a baseline or adding constraints: compare actual columns/types/defaults/checks/indexes/FKs and installed procedure assumptions; audit duplicate email/user-profile/active-registration records and invalid/orphan statuses; compare CurrentRegistrations to non-cancelled row counts; verify database/app clocks. Record safe resolution decisions here. Test existing-database adoption and fresh creation separately. Live connection, schema equivalence, counts and integrity are not yet verified.
