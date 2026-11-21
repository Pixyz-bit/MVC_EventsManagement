# UI design, Tailwind, and page references

Last updated: 2026-11-21 (Asia/Manila).

The user will supply a design reference for each page. Reproduce the supplied visual direction while retaining the source system's required functionality. Do not copy the old Web Forms UI or invent final page designs before their references arrive.

For each page:
1. Record the supplied reference identifier/link/local asset and intended scope in the register below. If the user supplies a local-copyable asset, place it in DesignReferences under its portal; this folder is outside wwwroot and is not served publicly. Do not copy private reference data into public assets.
2. Inspect layout, spacing, typography, colors, navigation, components, responsive behavior, and interactions. Identify functional controls/states absent from the reference.
3. Reuse established shared patterns. If a new reference conflicts with existing shared navigation/tokens, resolve the intended exception or shared update explicitly; do not silently create inconsistent components.
4. Build with Razor, Tailwind, shared partials, and only necessary JavaScript. Reference matching must retain accessibility and required business functionality.
5. Verify desktop/mobile presentation, keyboard use, focus, contrast, form labels, validation, empty/loading/error states, and long/realistic content. Compare against the supplied reference.
6. Update the page register, shared design decisions, milestone status in docs/MILESTONES.md, and relevant verification records; commit the increment.

Tailwind implementation conventions:
- Install Tailwind through local frontend build tooling, not a production CDN runtime. Select the supported version and compatible CLI/build integration during M3 after checking official documentation.
- Keep authored Tailwind source, imports, tokens, and minimal shared custom CSS in Styles/app.css. Compile to wwwroot/css/app.css. Do not hand-edit generated CSS.
- Keep npm dependencies and build scripts in the web application's package.json, beside its csproj, and commit its lockfile. No extra frontend application.
- Ensure Tailwind discovers Razor views and JavaScript source. Avoid dynamically concatenated utility classes that the scanner cannot discover; use complete class mappings or the selected version's supported explicit inclusion.
- Provide development watch and production build scripts; integrate production CSS generation with the documented .NET build/publish workflow. Verify a clean checkout can generate CSS with no watch process running.
- Link generated CSS from shared layouts with cache busting. Replace Bootstrap starter styling during M3 rather than mixing competing layout frameworks. Retain unrelated validation dependencies only when needed.
- Keep page interaction scripts in wwwroot/js/admin or student and shared scripts in site.js. Keep production images/fonts in their wwwroot folders. Create per-page CSS only when utilities/shared styles cannot reasonably express a requirement.
- Generated CSS is build output; its final tracking policy must be settled in M3 together with build integration. Regardless of tracking, publishing must reliably include the generated file.

### Page reference and verification register

All design references are pending. The source page/action inventory is recorded in [the functional specification](docs/FUNCTIONAL_SPEC.md); this register owns reference readiness and visual verification only. Status progresses through Awaiting reference -> Reference reviewed -> Implementing -> Implemented -> Verified.

| Portal | Page/workflow | Source reference | Design reference | Status |
| --- | --- | --- | --- | --- |
| Account | Login | Frontend/Login/Login.aspx | Pending | Awaiting reference |
| Shared | Access denied / entry routing | Frontend/AccessDenied.aspx; Default.aspx | Pending | Awaiting reference |
| Admin | Event list | AdminEvents.aspx | Pending | Awaiting reference |
| Admin | Create event | CreateEvent.aspx | Pending | Awaiting reference |
| Admin | Event details/edit/sponsors/cancellation | EventDetails.aspx | Pending | Awaiting reference |
| Admin | Student directory/import/export | StudentList.aspx | Pending | Awaiting reference |
| Admin | Account management | AccountManagement.aspx | Pending | Awaiting reference |
| Admin | Preregistered students | EventPreRegistered.aspx | Pending | Awaiting reference |
| Admin | Attendance list | EventAttendance.aspx | Pending | Awaiting reference |
| Admin | QR/manual attendance terminal | AttendanceScanner.aspx | Pending | Awaiting reference |
| Admin | Analytics | EventAnalytics.aspx | Pending | Awaiting reference |
| Admin | History | EventHistory.aspx | Pending | Awaiting reference |
| Admin | Connection diagnostics (development scope) | TestConnection.aspx | Pending | Awaiting reference |
| Student | Dashboard/discovery | Dashboard.aspx | Pending | Awaiting reference |
| Student | Registration/cancellation | EventRegistration.aspx | Pending | Awaiting reference |
| Student | Pass/QR/download | EventPass.aspx | Pending | Awaiting reference |
| Student | Profile/password | StudentProfile.aspx | Pending | Awaiting reference |

Admin filenames above are under source Frontend/Admin; Student filenames are under Frontend/User. Shared layouts/partials are derived from supplied page references rather than assumed to require separate screens. Backend work may proceed while references are pending; final page design must wait for its reference or an explicit user decision to reuse an established pattern.

### Established design decisions

- Framework: Tailwind CSS with server-rendered Razor MVC.
- Visual direction: page-specific user references; none supplied yet.
- Typography, palette, spacing scale, breakpoints, component rules, and motion: pending reference review. Record actual shared decisions here as they are established.
- Accessibility and responsive behavior are required for all implemented pages.
