# Functional specification and parity checklist

Last updated: 2026-11-23 (Asia/Manila).

## Evidence and status

M1 is a static source-code inventory, not a claim of successful execution. Evidence includes 17 portal page/action inventories plus Default.aspx, shared helpers/models, repository operations and SQL definitions, and functional scripts for filtering, scanning, charts and ticket downloads. A static audit found 67 repository procedure definitions across six repository SQL files, no C# referenced procedure missing a definition, and no unresolved declared portal server handlers among the audited event attributes.

Source paths are relative to the application in [project scope](PROJECT_SCOPE.md). Database mappings/security details belong in [database and security](DATABASE_AND_SECURITY.md); visual references in [DESIGN.md](../DESIGN.md); progress in [milestones](MILESTONES.md). The NEW MVC_EventsManagement schema is verified in M2; no old records/accounts are imported. Legacy browser workflows remain unverified. Checkboxes below mean MVC feature acceptance, not inventory or database-foundation completion.

## Page and action inventory

Destination ownership is proposed until implementation establishes routes. Every Admin action, including JSON/export, requires administrator authorization; every Student action requires student authorization.

| Source | Functional actions | Destination ownership | Rules |
| --- | --- | --- | --- |
| Default.aspx | Entry routing | Home/AccountService | AUTH-01 |
| Frontend/Login/Login.aspx | Login, local return URL, role redirect | Account/AccountService | AUTH-01, AUTH-02 |
| Frontend/AccessDenied.aspx | Denial reason and portal return | Account | AUTH-01 |
| Frontend/Admin/Admin.Master | Identity/navigation/logout | Shared layouts/Account | AUTH-01 |
| Frontend/Admin/AdminEvents.aspx | Status tabs, search/reset, details navigation, cancellation confirmation | Admin/EventService | EVENT-01, EVENT-03 |
| Frontend/Admin/CreateEvent.aspx | Form steps, sponsors add/preset/remove, image preview/upload, publish | Admin/EventService | EVENT-02, EVENT-04 |
| Frontend/Admin/EventDetails.aspx | View/edit/save/cancel edit, sponsors, banner replacement, cancellation navigation | Admin/EventService | EVENT-02, EVENT-04 |
| Frontend/Admin/StudentList.aspx | Search/filter/reset, add/edit, status, password reset, CSV template/import/export | Admin/StudentService | STUDENT-01, CSV-01 |
| Frontend/Admin/AccountManagement.aspx | Search/filter/reset, manage account/profile/role/status/password, create admin, export | Admin/AccountService | ACCOUNT-01, ACCOUNT-02 |
| Frontend/Admin/EventPreRegistered.aspx | Active/revoked cohorts, search/demographic filters, attendee details, void | Admin/RegistrationService | ROSTER-01, CANCEL-02 |
| Frontend/Admin/EventAttendance.aspx | Confirmed roster, filters, latest arrival, turnout | Admin/AttendanceService | ROSTER-01 |
| Frontend/Admin/AttendanceScanner.aspx | Camera/manual lookup, review, explicit confirmation | Admin JSON/AttendanceService | SCAN-01, CHECKIN-01 |
| Frontend/Admin/EventAnalytics.aspx | Metrics, cohorts, demographic charts/inspection, arrival intervals, history return | Admin/AttendanceService and EventService | REPORT-01 |
| Frontend/Admin/EventHistory.aspx | Search/year/outcome filters/reset, totals, report navigation | Admin/EventService | HISTORY-01 |
| Frontend/Admin/TestConnection.aspx | Connectivity/retest, latency, masked settings, errors | Admin/development diagnostics | SHARED-01 |
| Frontend/User/Dashboard.aspx | Eligible catalog/category filter, registered events, pass navigation, cancel, logout | Student/EventService and RegistrationService | DISCOVER-01, CANCEL-01 |
| Frontend/User/EventRegistration.aspx | Event/profile/confirmation steps | Student/RegistrationService | REGISTER-01, ELIGIBLE-01 |
| Frontend/User/EventPass.aspx | Owned pass, status, QR, PNG export | Student/RegistrationService | PASS-01 |
| Frontend/User/StudentProfile.aspx | Read-only profile and own password change | Student/StudentService and AccountService | PROFILE-01 |

## Authentication and account rules

### AUTH-01: Login, roles and logout

The temporary no-login design preview was removed. All administrator pages, including the directory and creation form, require authenticated administrator sessions.

- [x] Sign in with a trimmed student ID or email and a password. Missing input, wrong or unknown credentials, and inactive accounts fail authentication. If an identifier matches both, the email match takes precedence.
- [x] Supported roles are Admin and Student. Sign-in accepts validated local return URLs and otherwise routes to the correct portal. Role checks protect both portal entry routes. Apply the same authorization to future export and JSON actions.
- [x] Logout clears authentication. Account deactivation or a role change invalidates existing access on the next request.

Acceptance: both identifier forms work, inactive/unknown accounts fail, direct wrong-role endpoints are denied, external return URLs are rejected, and protected requests require login after logout.

Evidence: Login.aspx.cs, Default.aspx, AccessDenied.aspx.cs, Admin.Master.cs, SessionHelper/AdminPage/StudentPage, UserRepository.cs/.sql.

### AUTH-02: New-account credentials

- [x] The first administrator uses ASP.NET Core Identity password hashing and a password of 15 to 128 characters. The setup command prompts for confirmation and preserves password whitespace. The new database starts without imported or seeded accounts.
- [ ] Apply the new-account hashing and password policy to account creation, password change and reset flows as those screens are implemented.

Acceptance: verify new-account hashes, incorrect passwords, malformed hashes, salt regeneration and deliberate whitespace handling. No source-account upgrade or seed transition is required.

### ACCOUNT-01: Account maintenance

- [ ] List/search/filter/reset by role and Active/Locked status; show total accounts, active admins, students, locked accounts. Create active admins with email, password and matching confirmation; reject duplicates and invalid input.
- [ ] Manage email, role, active state, optional new password and student demographics when Student is selected. Validate email shape, supported role, lengths and required profile fields.
- [ ] Save managed user/profile/password changes atomically. Acting admin must remain active; block self-demotion/self-deactivation and leaving no active administrator. Recheck guards inside the transaction to protect concurrent changes.
- [ ] Preserve existing StudentId and BirthDate through account management. Create an absent student profile when converting to Student; reject duplicate profiles. Promotion to Admin does not automatically delete a linked profile/history.

Acceptance: invalid email/profile causes full rollback; forged IDs cannot bypass guards; concurrent changes cannot remove the last active admin. No persisted account-photo field/write was found in the five-table schema/current account save handler; an unused upload control is not a completed feature.

### ACCOUNT-02: Account CSV export

- [ ] Export current filtered accounts with columns UserId,Email,Role,AccountStatus,StudentId,FullName,Gender,CampusBranch,Department,Program. Never export passwords.

Acceptance: filtered counts match, quoted/Unicode fields round-trip, and spreadsheet formula payload handling is deliberate.

Evidence: AccountManagement.aspx/.cs; UserRepository.SaveManagedAccount/CreateAdminUser/GetAllUsers/GetAccountStatistics; StudentRepository.SaveManagedProfile; related SQL.

## Event management

### EVENT-01: Matrix and lifecycle

Directory rows show only status, title, venue/event date, date-only registration opening/deadline, registered/capacity occupancy and a View action. Opening and deadline are shown for every status. Department remains searchable without a visible row field. View links to the approved details page at `/admin/events/{id}`.

- [x] `/admin` and `/admin/events` list unfinished Upcoming events with All upcoming, Open, Soon, and Closed filters, search/reset, and chronological ordering. Search matches title, venue, or department. Filter counts cover all upcoming events, and results paginate at 25 per page.
- [x] Add event details navigation. Completed and cancelled events appear in `/admin/events/history`; titles open their read-only details.
- [x] Stored outcomes are Upcoming, Cancelled, Completed. Effective completion occurs at now >= EventEnd. The upcoming list excludes those rows, and history includes them as Completed at the same instant. Cancellation takes precedence. Reads derive completion without writing stored outcomes or requiring a background completion job.
- [x] Registration opening/deadline are calendar dates. Using today's date in Asia/Manila, registration is Closed if today > RegEnd or capacity has been reached, Soon if today < RegStart, and Open otherwise. The opening and deadline days are inclusive; a one-day window is supported. Event completion still uses the event end timestamp.

Acceptance: Philippine midnight at opening, the last moment of the deadline day, midnight the following day, a one-day window, exactly EventEnd, full, cancelled and completed fixtures produce correct states. Open/Soon/Close are display states, not stored outcomes.

### EVENT-02: Creation and editing

- [x] Creation inputs: title, optional description, venue, positive integer capacity, one event date/start/end, registration opening/deadline, optional audience, sponsors and image. Title/venue are required; creator comes from authentication; initial registration count is zero. `/admin/events/create` provides five steps and a final confirmation; successful publishing redirects to the directory with feedback. Editing uses the same validated input rules on the details page.
- [x] Creation validation: same-day event start precedes end; registration opening is on or before the deadline, and BOTH registration dates are strictly before the event date. Inputs use date-only registration controls and DateOnly values. Supported form years are 1753-9998, and events that have already ended are rejected. These rules also apply to editing.
- [x] Edit only unfinished Upcoming events and recheck at save. Capacity cannot fall below current reservations, including concurrent bookings. Preserve creator, lifecycle and counts during edits.
- [x] Missing/invalid event IDs return unavailable/not-found; database errors never fabricate example records.

Acceptance: invalid fields/dates/capacity write nothing; cancelled/completed events reject edits; create/edit survives reload with correct identity, associations and image.

### EVENT-03: Event cancellation

- [x] Cancellation opens a modal with a reason and explicit confirmation, Close/Keep event and Escape dismissal. Failed submissions retain the reason and submitted concurrency token. Require a trimmed 1-500 character reason. Cancel only unfinished Upcoming events. Repeated requests cannot overwrite the original reason.
- [x] Set event outcome/reason, retaining registrations, timestamps and CurrentRegistrations as history.
- [x] History includes cancelled events and opens their read-only details with the retained cancellation reason.
- [ ] Future registration/admission and passes must honor cancelled events and show the reason. Source sends no automatic emails on cancellation.

Acceptance: historical evidence/counts survive, admission/booking stop immediately, repeated/finished-event cancellation fails safely.

### EVENT-04: Audience, sponsors and images

- [x] Creation supports unrestricted audience dimensions, multiple known program codes, and optional year 1-5. College selection filters programs; the server rejects mismatched combinations. Eligibility enforcement for student booking remains in ELIGIBLE-01.
- [x] Creation stages sponsor add/preset/remove, caps input at 20 names of 150 characters, and removes case-insensitive duplicates at save. Sponsor rows have CreatedAt and identity insertion order; there is no global sponsor catalog. Editing uses the same validated input rules on the details page.
- [x] Creation and editing support upload/preview/remove with one EventPhotoPath. Editing retains the banner unless replaced or explicitly removed; replacement takes precedence over removal. Creation accepts PNG/JPEG/WEBP up to 5 MiB and validates/re-encodes pixels; see [database and security](DATABASE_AND_SECURITY.md).
- [x] Creation and editing save event/sponsors in one EF transaction. Sponsor replacement is atomic. Failed saves clean new uploads; obsolete banners are removed only after successful saves.

Acceptance: failure cannot leave partial sponsor replacement; valid associations persist; unsafe images fail; existing paths still resolve after migration.

Evidence: AdminEvents/CreateEvent/EventDetails.aspx/.cs; EventModel; EventCancellationModel/Repository; RegistrationDateTime; EventRepository/SponsorRepository.cs and related SQL.

## Students and CSV

### STUDENT-01: Student directory

- [ ] Search/filter/reset by available demographics/status, create/edit, linked account activate/deactivate and password reset. Department selection controls program choices.
- [ ] Add requires student ID, institutional email, first/last names and valid birthdate in the page flow; middle name is optional. Preserve gender/campus/department/program/state. Reject duplicate IDs/emails. Edit preserves the student identity and registration history.
- [ ] Year and section belong to event registrations, not current StudentTable. Student profile explains year is selected during event registration.
- [ ] Source default initial/reset password is uppercase middle-name initial (or X) plus MMddyyyy birthdate (or 01012000 if absent); admins may supply a reset password. Its predictability and display in success feedback need an explicit compatibility/security decision, not logging or exporting credentials.
- [ ] Create linked account/profile atomically. Coordinate full student email/state/password updates rather than allowing partial success from the separate source UI calls.

Acceptance: no orphan account/profile on failure; duplicate prevention survives concurrent requests; updated state/password is consistent across directory/account management; history remains linked.

### CSV-01: Roster exchange

- [ ] UTF-8 CSV upload/template uses StudentId,FirstName,MiddleName,LastName,Gender,CampusBranch,Department,Program,Email,BirthDate.
- [ ] Retain twelve-column legacy format: same first eight fields, then YearLevel,Section,Email,BirthDate. Ignore legacy year/section for profile persistence.
- [ ] Source skips header/blank lines, requires ID/first/last/email, rejects existing IDs/emails, and processes rows independently. Preserve valid rows when others fail; report accurate success/failure counts and row errors.
- [ ] Imported blank gender/campus defaults to Not Specified/San Bartolome. Source invalid/missing birthdate can become null and influence default credentials; explicitly report malformed dates rather than silently guessing identity data.
- [ ] Replace line.Split(',') with proper CSV parsing and header/schema validation; support accepted formats and quoted/Unicode content. Count success only when persistence succeeds.
- [ ] Filtered student export columns are StudentId,FullName,FirstName,MiddleName,LastName,Gender,CampusBranch,Department,Program,Email,BirthDate(MM/dd/yyyy),AccountStatus. This export differs from the import format; do not promise direct re-import.

Acceptance: template and both roster formats work; quotes/commas/Unicode, blank/malformed/duplicate rows and partial success are accurate; failed rows leave no orphan accounts.

Evidence: StudentList.aspx/.cs; StudentRepository.cs/.sql; account and student export handlers.

## Student discovery and reservations

### ELIGIBLE-01: Eligibility

- [ ] Every restricted dimension must match: trimmed campus/department, whole comma-delimited program token with spaces removed and known alias equivalences, and selected year. Blank/null target means unrestricted.
- [ ] Browse with omitted year; final commit must supply/recheck year 1-5. One matching dimension cannot bypass another restriction.
- [ ] Preserve supported program aliases: BSIT/BS Information Technology; BSCS/BS Computer Science; BSIS/BS Information Systems; BSIE/BS Industrial Engineering; BSECE/BS Electronics Engineering; BSA/BS Accountancy; BSBA/BS Business Administration; BSEntrep/BS Entrepreneurship; BECEd/Bachelor of Early Childhood Education. Only these nine programs are supported; event creation rejects other program codes. Check actual SQL collation before choosing case semantics.

Acceptance: independent dimension mismatches reject, unrestricted values allow, aliases match, substrings do not, and browse/preflight/commit use consistent predicates.

### DISCOVER-01: Dashboard

- [ ] Discover eligible Upcoming events with image/sponsors/venue/schedule/availability and category filter. Order Open, Soon, closed, preserving schedule order within groups.
- [ ] Non-cancelled owned registrations appear in My Registered Events rather than available catalog. Cancelled bookings allow rediscovery if otherwise eligible.
- [ ] Owned passes remain reachable when booking closes/full; represent ended/cancelled/checked-in states honestly. Missing profile/data does not produce sample events or bookings.

Acceptance: no duplicate current-pass/catalog item, cancelled bookings release visibility, filters work, and owned tickets remain accessible after deadline.

### REGISTER-01: Booking

- [ ] Preserve three functional steps: event information, read-only student profile, current year/section and terms confirmation. Presentation follows supplied references.
- [ ] Require terms, nonblank section within column length, year 1-5, real event/profile and authenticated student identity. Do not trust posted student IDs.
- [ ] Recheck Upcoming/unfinished event, inclusive registration window, capacity, eligibility and no active booking inside the commit operation. Existing active holders navigate to their pass even when closed/full.
- [ ] Insert NoShow with null CheckInTimestamp and increment CurrentRegistrations together. NoShow means reserved/unscanned here, not a finalized absence. Distinguish full/closed/ineligible/duplicate failures without side effects.
- [ ] Allow new booking after cancellation if still eligible; preserve old cancelled row and generate a new reference. Protect simultaneous submissions and final-seat races.

Acceptance: one registration/seat for concurrent duplicate submissions; never exceed capacity; no count change on failure; selected year/section persist per booking.

### CANCEL-01: Student cancellation

- [ ] Owner only, NoShow only, event Upcoming/unfinished and today's Philippine date <= RegEnd. Source does not separately require the opening date to have arrived; do not silently add that condition.
- [ ] Transition to Cancelled and decrement counter (never below zero) atomically. Reject Present/already-cancelled/expired/wrong-owner requests without another decrement.

Acceptance: deadline boundary honored, repeat requests release at most one seat, and forged ownership fails.

### CANCEL-02: Administrator void

- [x] Administrator voiding permits any non-Cancelled registration, including Present, regardless of deadline/event state. It retains CheckInTimestamp/history and decrements the event count once (never below zero). This is broader than student cancellation and changes historical cohort counts.
- [x] The service binds the registration to the selected event, requires explicit confirmation, and uses event/pass rowversions in one transaction. Missing/wrong-event, repeated, unconfirmed and stale submissions cannot partially update the pass/count.

Acceptance: correct attendee/event is confirmed, one transition/decrement occurs, cancelled rows stay stable and reports reflect the new status.

### PASS-01: Admission pass

- [ ] Resolve a real registration by ID/reference and enforce ownership. Source fallback chooses the student's first event-date-sorted registration if lookup fails; handle explicit invalid IDs deliberately rather than silently showing an unrelated pass. Never fabricate a pass.
- [ ] Reference/QR payload is TCK-{EventId:D4}-{EventRegistrationId:D5}. Redirect ticketId GUID and displayed SEC/GetHashCode token are not stored admission secrets. Query-string tokens/success flags cannot confer validity.
- [ ] Valid admission requires Upcoming/not-ended event and uncancelled/unconsumed registration. Checked-in passes remain visible as evidence with reuse warning; event cancellation invalidates admission and displays its reason.
- [ ] Show real student/program/registered year-section, event schedule/venue/reference. Preserve QR generation and PNG download functionality using compatible assets and the new design.

Acceptance: cross-student access fails; known reference resolves correctly; forged flags do not activate a pass; PNG QR decodes to same reference; inactive status is clear.

### PROFILE-01: Own profile/password

- [ ] Academic/account details are read-only; source offers no self-edit demographic save. Own password change requires correct current hash verification, matching confirmation and minimum length, then saves fresh salt/hash.

Acceptance: forged user IDs cannot affect another account; mismatch/wrong current password writes nothing; new password works and old fails after success.

Evidence: Dashboard/EventRegistration/EventPass/StudentProfile.aspx/.cs; EventRegistrationModel; RegistrationRepository; EventRepository eligibility procedures; local QR/PNG assets and scripts.

## Attendance and reporting

### ROSTER-01: Lists

- [x] `/admin/events/{id}/preregistered` includes all non-Cancelled registrations, including Present, with Cancelled in a separate cohort. Student ID/name search, college/program/registered-year filters, reset and 25-row paging combine on the server. Cohort counts/options cover the whole selected event. Rows show Ticket ref, Student name/ID, College & program, Year & section, Status, then View. NoShow displays as Reserved. The attendee modal adds campus and pass cancellation confirmation. Email is intentionally omitted from rows and modal per user direction. Registration year/section comes from the booking, not a current student year.
- [x] Attendance includes only current Present registrations for the selected event, ordered by check-in timestamp then registration ID descending. Student ID/full-name/ticket-reference search combines with college/program filters and 25-row paging. Event-wide checked-in count and options remain independent of filters. Rows follow Ticket ref, Student name/ID, College & program, registered Year & section, Check-in date/time (PHT), without email or actions. Latest check-in is the first row; turnout summaries belong to the later Analytics page. Available booking seats use CurrentRegistrations; physical occupancy uses Present counts.

Acceptance: check-in stays in preregistered and appears in attendance; void moves to revoked; counts and filter scope are correct, including empty lists.

### SCAN-01: Lookup/review

- [x] Admin selects a real event; the scanner provides operator-started QR camera capture and manual lookup by ticket reference, numeric registration ID, student ID or email. Search selected event first, then other events to identify wrong-event passes. Physical camera validation remains pending in TESTING.md.
- [x] Lookup is read-only and stages attendee details without email. Authentication protects both actions; NotFound/WrongEventWarning/CancelledWarning(inactive)/DuplicateWarning/ValidPending outcomes are represented. The operator reviews the university ID and explicitly clicks Confirm check-in; the proposed ID checkbox was removed per user instruction.
- [x] Validate encoded ticket event as well as registration ID, correcting the source parser gap. Legacy pipe/space suffixes remain accepted; malformed/mismatched references cannot authorize admission.

Acceptance: lookup never changes status/time; warnings block confirmation; manual/camera paths agree; expired auth blocks both endpoints; wrong-event matches are distinguishable.

### CHECKIN-01: Confirmation

- [x] Confirm only the selected event's real registration. The conditional write rechecks selected event, registration, both rowversions, lifecycle and eligibility server-side. The client confirmation button is not the security boundary.
- [x] Atomically transition eligible NoShow to Present and store the Philippine check-in time. Reject not found, duplicate, cancelled pass, inactive/cancelled/completed/ended event. Preserve the source policy allowing check-in before EventStart; no new door-opening restriction was added.
- [x] Return persisted time/reference/attendee and updated Present count. CurrentRegistrations is unchanged. Verification method/admin identity are not persisted; no stored attendance audit log is claimed.

Acceptance: concurrent confirmations yield one success and stable timestamp; selected-event tampering fails; changes after lookup are revalidated.

### REPORT-01: Analytics

- [ ] Reserved/preregistered = non-Cancelled (Present + NoShow), attended = Present, cancelled = Cancelled, unscanned = reserved - attended. No registration creation date exists; check-in time must not be used as ticket creation time.
- [ ] Turnout = attended/reserved; occupancy/saturation = attended/capacity; booking fill = CurrentRegistrations/capacity; attrition = cancelled/(reserved+cancelled). Zero denominators yield zero.
- [ ] Active-cohort department/program/campus/year distributions, cohort lists and demographic audit remain. Program chart groups top five plus Other Programs only if more than six groups; department audit ranks attendance rate then count.
- [ ] Confirmed-arrival intervals support 5/15/30 minutes (default 15), floor to bucket start, fill empty buckets between first/last, handle multiple days, and choose first tied peak label. No arrivals means awaiting/empty state.
- [ ] Retain useful chart inspection/filtering and history navigation. Do not invent registration-time series or persistent live push absent source/data support.

Acceptance: deterministic mixed-status/zero/multi-department/cross-midnight fixtures produce matching totals, denominators, cohorts and intervals.

### HISTORY-01: History

- [x] `/admin/events/history` shows stored/effective Completed and Cancelled events, newest EventStart then EventId first. Rows show outcome, title/target college and venue/event date. Global total/completed/cancelled counts remain independent of filters. Search matches title, venue or college without case sensitivity; academic year and outcome filters combine, with reset and 25-row pagination. Titles open read-only details. Semester filtering remains outside the visible UI, matching the source.
- [ ] The report View action stays disabled until analytics is implemented; it must eventually navigate to the event report with history return context.
- [x] Academic year starts August: August–December use the event year; January–July use the preceding year. Tests cover July/August, January and June. History retains cancellation reasons and counts. Semester categories remain defined for future reporting (Aug–Dec first, Jan–May second, Jun–Jul summer).

Acceptance: August/January/June fixtures and filters agree with reports and outcome totals.

### SHARED-01: Supporting behavior

- [ ] Preserve useful validation/toasts/navigation through MVC. Source diagnostics is Admin-protected; destination plan scopes it to development with retest/latency and safe errors, avoiding secrets/raw SQL exceptions.
- [ ] Replace Web Forms control IDs/postbacks/WebMethods with MVC forms and authorized JSON actions. Reuse compatible functional libraries/data only. Distinguish DB failure from truly empty lists; do not fabricate examples.

Acceptance: malformed IDs fail safely, modifying requests have CSRF protection, secrets never appear in client errors and unavailable data is represented accurately.

Evidence: EventPreRegistered/EventAttendance/AttendanceScanner/EventAnalytics/EventHistory/TestConnection.aspx/.cs; RegistrationRepository.sql; EventRepository.sql; source inline scripts.

## Migration gaps and remaining decisions

These are findings and implementation obligations, not completed fixes. Preserve intended functionality while correcting server integrity/security defects; record deliberate product changes in the owning rule.

| ID | Finding / required follow-up | Milestone |
| --- | --- | --- |
| GAP-01 | Source live schema/browser verification was unavailable in M1; old database adoption is no longer required | New M2 schema verified; full workflow checks remain M4-M11 |
| GAP-02 | Source duplicate booking check is outside SQL transaction | M2 adds verified filtered unique index/rowversion; M8 must still implement counter/booking transactions |
| GAP-03 | Gate commit/admin void do not bind the target registration to selected event server-side | M8/M9 |
| GAP-04 | Predictable source initial passwords, plaintext login fallback and inconsistent trim/reset checks | No imported users; M4/M6 use explicit new-account policy without plaintext fallback |
| GAP-05 | Event/sponsor and some student edits can partially succeed; upload/sponsor errors may be swallowed | M5/M6 |
| GAP-06 | CSV quoted fields/header/date handling and unchecked false return distort results | M6 |
| GAP-07 | Source app local time and SQL GETDATE may differ | M2 implements tested Asia/Manila EventClock; future services must use it |
| GAP-08 | Admin void can affect Present after deadline; no check-in start-time guard | Preserve unless user changes policy; M8/M9 verification |
| GAP-09 | Photo controls/security tokens not backed by persistence; student profile can survive Admin promotion | M2/M4/M6: avoid inventing fields/deleting history |
| GAP-10 | Source schema lacks email/student-user uniqueness | M2 enforces verified unique indexes in the NEW empty database; no old-data audit/import needed |

Reference readiness belongs in DESIGN.md. The source application is unchanged.
