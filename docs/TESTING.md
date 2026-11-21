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

2026-11-21, split-screen login preview: Tailwind compiled and the Release publish served `/login` and the QCU campus image with HTTP 200. Edge checks verified the image panel at 1366px, the form-only layout at 390px, no horizontal overflow, and visible keyboard focus on both fields. Sign-in is disabled for the design preview.
