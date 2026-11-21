# Verification and definition of done

Last updated: 2026-11-21 (Asia/Manila).

Use business-rule tests for dates/eligibility/lifecycle/authorization; SQL Server integration tests for mapping, transactions, constraints, concurrency and migrations; browser tests for complete workflows, redesigned pages, downloads, console errors and responsiveness. In-memory EF tests alone cannot establish SQL Server correctness. Verify camera scanning with a real device/browser where available and identify it as unverified until performed.

End-to-end acceptance: administrator creates an eligible event -> student discovers/registers -> student opens/downloads pass -> administrator scans or manually looks up attendee -> reviews and confirms once -> lists, counts, history and analytics reflect the result.

Negative/concurrency cases: unauthenticated and wrong-role direct URLs, another student's pass, inactive users, full events, closed periods, duplicate simultaneous registration, duplicate check-in, cancelled registration, wrong-event pass, invalid uploads, malformed/partially invalid CSV, expired sessions, and conflicting edits.

A feature is done only when required behavior is implemented, its reference-led UI is consistent and accessible, server authorization/validation is enforced, relevant checks pass, affected documentation files are current, and work is committed. Report any checks not run and why. Final release checks include clean dependency restore, .NET build/test/publish, frontend production build and generated CSS inclusion, and validated database setup for both supported paths.

## Acceptance coverage from M1

Detailed expected business outcomes have one owner: [functional specification](FUNCTIONAL_SPEC.md). Use its rule IDs in test names/results rather than duplicating changing requirements here.

| Coverage | Rule IDs | Verification method |
| --- | --- | --- |
| Both login identifiers, roles, inactive accounts, logout, local return URLs, forged IDs | AUTH-01, ACCOUNT-01 | Integration plus browser; include JSON/export endpoints |
| New-account password policy, invalid hashes, input whitespace, salt regeneration, self password change | AUTH-02, PROFILE-01 | Focused credential tests and account integration; no imported users |
| Last-admin/self-change and account/profile rollback | ACCOUNT-01 | SQL Server transactions and simultaneous administrator operations |
| Event field/schedule validation and state transitions | EVENT-01 through EVENT-04 | Deterministic clock tests, SQL persistence and form integration |
| Eligibility, program aliases, unrestricted dimensions and submitted year | ELIGIBLE-01, DISCOVER-01 | Rule fixtures and equivalent browse/preflight/commit query outcomes |
| Final-seat race, simultaneous same-student booking, cancellation/rebooking and counter invariants | REGISTER-01, CANCEL-01, CANCEL-02 | Separate SQL Server connections/contexts executing concurrent operations |
| CSV legacy/current template, quoted content, row failures and no orphan accounts | STUDENT-01, CSV-01, ACCOUNT-02 | SQL integration, fixture files and download/import browser checks |
| Owned pass, invalid references, decoded QR payload and actual PNG download | PASS-01 | Authorization integration and browser/download decoding |
| Read-only lookup, wrong gate, confirmation race, stale lookup state and duplicate timestamp | SCAN-01, CHECKIN-01, ROSTER-01 | Endpoint plus SQL concurrency tests; real camera check separately |
| Mixed cohorts, zero denominators, cross-midnight/empty/tied arrival intervals, academic-year boundaries | REPORT-01, HISTORY-01 | Deterministic reporting fixtures and cross-page totals |
| Missing IDs, service failure, CSRF, unsafe uploads and safe diagnostics | SHARED-01 and security rules | Integration and negative browser checks |
| Clean restore/build/publish with generated Tailwind CSS; fresh empty database setup | Setup and database specifications | Reviewed migration SQL and startup checks; old-database adoption is out of scope |

## Checks actually performed

2026-11-21, M1 static inspection: counted 17 Frontend ASPX pages plus Default.aspx; enumerated 67 procedure definitions in six repository SQL files; checked C# repository referenced names and combined installer coverage (none missing); checked declared OnClick/OnItemCommand/OnSelectedIndexChanged/OnTextChanged portal handlers against code-behind (none unresolved in this bounded audit). No source SQL or old application workflow was executed.

2026-11-21, M2 foundation verification:

- Original starter and EF-enabled Release solution build succeeded with zero warnings/errors; release publish succeeded.
- InitialCreate SQL reviewed before applying; created only MVC_EventsManagement on LocalDB, five application tables and EF history. Reapplying migration reported up-to-date; pending-model check reported no changes.
- 17 SQL-backed xUnit cases passed: graph round-trip and DateOnly/null handling; no-cascade FK protection; duplicate email/profile rejection; cancelled history plus active rebooking and duplicate rejection; invalid statuses/timestamps/years/section; event capacity/schedule/status/cancellation reason checks; stale event RowVersion conflict; retained timestamp on admin void; Philippine clock independent of machine timezone.
- Every test uses a rollback transaction. Post-test SQL counts were zero in all five application tables. SQL metadata confirmed ten enabled/trusted CHECKs, five NO ACTION FKs and three unique indexes.
- HTTP startup smoke: existing root and Privacy routes responded successfully. Production/missing-connection startup fails clearly; deliberately configured old-catalog startup was rejected before any connection.
- No data was imported or seeded. No camera, redesigned UI, full booking/cancellation/check-in services, or authentication workflows are implemented/verified yet. RowVersion and uniqueness checks do not replace the future transaction/concurrency workflow tests.

2026-11-21, SQL Server switch: InitialCreate applied on `localhost` to the new `MVC_EventsManagement` catalog. All 17 integration suite cases passed with the localhost default. A post-test query confirmed the migration history and zero rows in all five application tables. The development app returned HTTP 200 on `/`.

2026-11-21, white login preview: Tailwind compiled and the Release publish served `/login`. Edge checks at 390px, 1024px, and 1366px verified the white background, image visibility at desktop widths, no horizontal overflow, visible keyboard focus, and password reveal and masking using Space. Sign-in is disabled for the design preview.

2026-11-21, outer margin removal: Edge verified that the published login reaches all four viewport edges at 390 × 800 and 1366 × 900, with no horizontal overflow.

2026-11-21, viewport fit: Edge verified the published login fits without scrolling at 1366 × 900, 1024 × 480, 390 × 800, 320 × 568, 667 × 375, and 568 × 320. At 320 × 240, scrolling kept the bottom of the form reachable.

2026-11-21, M4: all 26 tests passed, including nine authentication cases through the MVC pipeline and localhost SQL Server. Coverage includes login by student ID or email, local return URLs, redirects by role, wrong-role denial, antiforgery on login and logout, rejection of invalid credentials, and cookie revocation after role changes or deactivation. The published app returned generic feedback after an invalid sign-in. The first-admin command rejected missing arguments and redirected password input. Test accounts rolled back. The first local administrator was then provisioned explicitly, and live sign-in and access to `/admin` succeeded.

2026-11-21, login centering: Edge measured the form centered in the viewport at 1366 × 900 and 1024 × 480. Both desktop sizes and the 390 × 800 and 568 × 320 mobile sizes fit without scrolling.

2026-11-21, M5 event list: all 27 tests passed. SQL Server fixtures covered inclusive opening and deadline, full and expired registration, excluded cancelled and ended events, title-family metadata search through venue and department, chronological order, and pagination. HTTP tests covered authentication and the protected event list alias. The published app showed the empty list after administrator sign-in, and Edge verified the active filter and responsive layout at 1366px and 390px. Test data rolled back.

2026-11-21, M5 admin sidebar: all 27 tests passed, and the Tailwind build and Release publish succeeded. In the published app, Edge verified the sticky desktop sidebar at 1366 × 900, the active Event list link, and the mobile Menu at 390 × 800. Space opened the menu and Tab reached the Event list link. Neither viewport had horizontal overflow.

2026-11-21, sidebar reference and login fade: all 27 tests passed, and the Tailwind build and Release publish succeeded. Edge verified the blue photo fade and viewport fit at 1366 × 900 and the photo-free layout and viewport fit at 390 × 800. After sign-in, Edge verified the navy sidebar, active Event Directory link, bottom account footer, and mobile Menu opening with Space. Neither sidebar viewport had horizontal overflow.

2026-11-21, horizontal blue login fade: all 27 tests passed, and the Tailwind build and Release publish succeeded. Edge verified the left-to-right fade and checked viewport fit at 1366 × 900, 1024 × 480, 568 × 320, and 390 × 800.

2026-11-21, white sidebar and restored white login: all 27 tests passed, and the Tailwind build and Release publish succeeded. Edge verified the desktop white login fade and the mobile login fit, the white sidebar with the restored QCU header and active Event Directory link, and the keyboard-operated mobile Menu. Neither 1366 × 900 nor 390 × 800 had horizontal overflow.

2026-11-21, compact sidebar: the Tailwind build and Release publish succeeded. Edge measured the sidebar at 256px with a 36px seal and 44px Event Directory and sign-out targets at 1366 × 900. At 390 × 800, the mobile Menu opened with Space and the page had no horizontal overflow.

2026-11-21, Event Directory redesign: the Tailwind build and Release publish succeeded. MVC integration coverage renders a matching event and a filtered empty state using a rolled-back SQL Server fixture. Edge verified the active tab, preservation of the search query when switching status, keyboard activation of Clear filters, and no horizontal overflow at widths of 390px and 320px. The local preview was checked with an empty event catalog.

2026-11-21, compact Event Directory: all 28 tests passed. Edge verified a 28px title and an 864px-wide panel at 1366px, with tabs and search side by side. At 320px the toolbar stacked with no horizontal overflow. Search and filter controls retained 44px targets.

2026-11-21, full-width Event Directory: all 28 tests passed. With Soon selected, Edge verified that the panel used the available width beside the sidebar, the title stayed at 28px, and all four tabs, search, and Clear filters shared one row at 1280px, 1536px, and 1920px. The toolbar stacked without horizontal overflow on mobile.
