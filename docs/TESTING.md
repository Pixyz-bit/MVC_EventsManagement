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
| Legacy password fixtures, invalid hashes, input whitespace, salt regeneration, self password change | AUTH-02, PROFILE-01 | Focused credential tests and account integration |
| Last-admin/self-change and account/profile rollback | ACCOUNT-01 | SQL Server transactions and simultaneous administrator operations |
| Event field/schedule validation and state transitions | EVENT-01 through EVENT-04 | Deterministic clock tests, SQL persistence and form integration |
| Eligibility, program aliases, unrestricted dimensions and submitted year | ELIGIBLE-01, DISCOVER-01 | Rule fixtures and equivalent browse/preflight/commit query outcomes |
| Final-seat race, simultaneous same-student booking, cancellation/rebooking and counter invariants | REGISTER-01, CANCEL-01, CANCEL-02 | Separate SQL Server connections/contexts executing concurrent operations |
| CSV legacy/current template, quoted content, row failures and no orphan accounts | STUDENT-01, CSV-01, ACCOUNT-02 | SQL integration, fixture files and download/import browser checks |
| Owned pass, invalid references, decoded QR payload and actual PNG download | PASS-01 | Authorization integration and browser/download decoding |
| Read-only lookup, wrong gate, confirmation race, stale lookup state and duplicate timestamp | SCAN-01, CHECKIN-01, ROSTER-01 | Endpoint plus SQL concurrency tests; real camera check separately |
| Mixed cohorts, zero denominators, cross-midnight/empty/tied arrival intervals, academic-year boundaries | REPORT-01, HISTORY-01 | Deterministic reporting fixtures and cross-page totals |
| Missing IDs, service failure, CSRF, unsafe uploads and safe diagnostics | SHARED-01 and security rules | Integration and negative browser checks |
| Clean restore/build/publish with generated Tailwind CSS; fresh/existing database paths | Setup and database specifications | Disposable environments and reviewed SQL |

## Checks actually performed

2026-11-21, M1 static inspection only: counted 17 Frontend ASPX pages plus separate Default.aspx; enumerated 67 procedure definitions in six repository SQL files; checked C# repository referenced procedure names against those definitions (none missing); checked the procedure names against combined installer (none missing); checked declared OnClick/OnItemCommand/OnSelectedIndexChanged/OnTextChanged portal handlers against code-behind (none unresolved in this audit). This is a bounded static name audit, not compilation or proof of runtime behavior. No source SQL, application workflow, camera, or database integration tests have been run. No application implementation changes were made in M1.
