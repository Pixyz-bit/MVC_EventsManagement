# Functional specification and parity checklist

Last updated: 2026-11-21 (Asia/Manila).

Initial inventory below is not yet a verified exhaustive specification. During M1 expand each capability with precise inputs, allowed roles, conditions, state transitions, database effects, failure cases, and testable acceptance criteria. Record the discovered rules here once; tests reference those rules.

- [ ] Account access: login, logout, role-specific entry routing, access denial, inactive accounts.
- [ ] Administrator events: create/update/detail/list, registration periods, capacity, campus/department/program/year restrictions, sponsors, image uploads, cancellations, event outcomes/history.
- [ ] Student administration: maintain records and linked accounts, CSV roster import, exports, password resets, transaction/failure behavior.
- [ ] Account administration: administrator/student account maintenance, roles, activation, passwords, profile photos where supported.
- [ ] Student portal: discover eligible events, filter/view details, profile information and password changes.
- [ ] Registration: steps and year/section capture, duplicates, capacity, eligibility, date windows, eligible cancellation, ticket-reference generation.
- [ ] Passes: owned registrations only, QR payload compatibility, downloads and responsive ticket content.
- [ ] Attendance: preregistered lists, manual/QR lookup, attendee review, confirmation, rejected passes, one-time attendance recording.
- [ ] Analytics/history: registration, attendance, cancellation and check-in counts/charts/filters with source-equivalent semantics.
- [ ] Shared: validation, notifications, error handling, navigation, development diagnostics, preserved date and image behavior.
