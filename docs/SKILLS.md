# Skills and technical references

Last updated: 2026-11-21 (Asia/Manila).

ASP.NET Core and EF Core skills are now installed under C:/Users/arlan/.codex/skills and their relevant guidance has been read for M2; they become discoverable in the next turn. Browser testing remains deferred until needed. Existing design skills: Frontend God Mode, Impeccable, UI/UX Pro Max. Use focused relevant guidance rather than loading every overlapping skill. User architecture, Tailwind, references, and documentation ownership in AGENTS.md override generic defaults, including React/Next.js or duplicate documents.

| Candidate | Source | Intended use | State |
| --- | --- | --- | --- |
| aspnet-core | https://github.com/openai/skills/tree/main/skills/.curated/aspnet-core | MVC, DI, routing, authentication, configuration, testing | Installed via Skill Installer; relevant guidance read |
| efcore-patterns | https://github.com/aaronontheweb/dotnet-skills/tree/master/skills/efcore-patterns | Supplementary queries, tracking, migrations, transaction guidance | Installed via Skill Installer (master); relevant guidance read |
| webapp-testing | https://github.com/anthropics/skills/tree/main/skills/webapp-testing | Browser flows, screenshots, console errors | Recommended; not installed |

Skills.sh research on 2026-11-21 showed approximately 4.3K, 1.6K, and 210.6K installs respectively; counts are historical selection signals, not quality guarantees. Source instructions were reviewed. Adapt restrictive/community defaults to actual requirements; Microsoft guidance is authoritative for framework behavior. Review install compatibility and supporting scripts when installation begins. Install only selected skills, not entire unrelated packs.

Previously researched installation commands (not executed):
```powershell
npx skills add https://github.com/openai/skills --skill aspnet-core
npx skills add https://github.com/aaronontheweb/dotnet-skills --skill efcore-patterns
npx skills add https://github.com/anthropics/skills --skill webapp-testing
```

Technical references to verify during relevant work:
- ASP.NET Core authentication migration: https://learn.microsoft.com/en-us/aspnet/core/migration/fx-to-core/areas/authentication
- EF reverse engineering: https://learn.microsoft.com/en-us/ef/core/managing-schemas/scaffolding/
- EF migrations: https://learn.microsoft.com/en-us/ef/core/managing-schemas/migrations/
- EF concurrency: https://learn.microsoft.com/en-us/ef/core/saving/concurrency
- Tailwind CLI/build/source detection: https://tailwindcss.com/docs/installation/tailwind-cli
