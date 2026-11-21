# Project working rules and documentation index

Last updated: 2026-11-21 (Asia/Manila).

## Documentation ownership

Documentation is divided into files by topic. Each topic has one authoritative file. Read this file and the documents relevant to the task before making changes. User instructions take precedence; update the affected documents when decisions change.

| File | Authoritative contents | Update when |
| --- | --- | --- |
| [Project scope](docs/PROJECT_SCOPE.md) | Source/destination, migration boundaries, project context | Scope or source/destination changes |
| [Architecture](docs/ARCHITECTURE.md) | Folder structure, responsibilities, MVC conventions | Files, layers, routing, or conventions change |
| [Database and security](docs/DATABASE_AND_SECURITY.md) | EF mapping/migration strategy, data integrity, authentication and security rules | Schema, persistence, permissions, or security behavior changes |
| [Design](DESIGN.md) | Tailwind conventions, shared design decisions, page references and visual verification state | References, tokens, components, layouts, or frontend styling conventions change |
| [Skills](docs/SKILLS.md) | Skill research, selection, installation state, technical reference links | Skills or reference guidance changes |
| [Milestones](docs/MILESTONES.md) | Milestone status, remaining work, current handoff | Meaningful progress, blockers, priorities, or completion changes |
| [Functional specification](docs/FUNCTIONAL_SPEC.md) | Feature inventory, business rules, parity requirements | Functionality, validation, lifecycle, or workflows change |
| [Testing](docs/TESTING.md) | Verification strategy, acceptance scenarios, definition of done | Test requirements or acceptance criteria change |
| [Setup](docs/SETUP.md) | Verified prerequisites, tooling versions, run/build/test/publish/database setup | Dependencies, configuration, commands, or deployment setup changes |

- Update each affected existing document in the same commit as its related implementation change. A change can affect several documents: for example, a new registration rule belongs in the functional specification, related constraints belong in database documentation, and completion belongs in milestones.
- Replace outdated statements in place. Do not append contradictory instructions or create a new file covering an existing document's topic.
- Cross-link to the authoritative document instead of duplicating detailed rules. Summaries and milestone acceptance descriptions may refer to the topic document; keep those references consistent.
- The milestone plan is the progress tracker and links to topic documents; it is not a duplicate full specification. Keep completed work separate from planned or unverified work.
- Add a new topic document only when an existing owner does not fit, then register its ownership here. Dependency licenses, generated files, installed skill instructions, test output, and reference assets are not duplicate authored documentation.

## Development and Git rules

- Commit every meaningful, reviewable increment, including documentation-only work. Implement, run relevant checks, update affected documents, review the diff/staged files, then commit. Do not wait for a large module to finish before checkpointing progress.
- Preserve user work and stage only intended files. Exclude IDE state, build output, database backups, credentials, private imports/exports, and personal data.
- Follow the established architecture and design. Use the document index above to find the applicable rules. Avoid unnecessary layers, dependencies, and competing component systems.
- Run tests appropriate to consequential behavior; avoid tests that only mirror trivial implementation details. Do not mark milestones complete merely because files exist.
- Report changes, verification, limitations, and commit ID. Distinguish implemented from verified work.
- Local commits are authorized. Remote pushes, publication, and production database changes are separate actions.
