# Verification and definition of done

Last updated: 2026-11-21 (Asia/Manila).

Use business-rule tests for dates/eligibility/lifecycle/authorization; SQL Server integration tests for mapping, transactions, constraints, concurrency and migrations; browser tests for complete workflows, redesigned pages, downloads, console errors and responsiveness. In-memory EF tests alone cannot establish SQL Server correctness. Verify camera scanning with a real device/browser where available and identify it as unverified until performed.

End-to-end acceptance: administrator creates an eligible event -> student discovers/registers -> student opens/downloads pass -> administrator scans or manually looks up attendee -> reviews and confirms once -> lists, counts, history and analytics reflect the result.

Negative/concurrency cases: unauthenticated and wrong-role direct URLs, another student's pass, inactive users, full events, closed periods, duplicate simultaneous registration, duplicate check-in, cancelled registration, wrong-event pass, invalid uploads, malformed/partially invalid CSV, expired sessions, and conflicting edits.

A feature is done only when required behavior is implemented, its reference-led UI is consistent and accessible, server authorization/validation is enforced, relevant checks pass, affected documentation files are current, and work is committed. Report any checks not run and why. Final release checks include clean dependency restore, .NET build/test/publish, frontend production build and generated CSS inclusion, and validated database setup for both supported paths.
