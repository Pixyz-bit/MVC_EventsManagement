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
