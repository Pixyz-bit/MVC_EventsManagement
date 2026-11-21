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
- Commit the compiled `wwwroot/css/app.css` with its source changes so .NET builds and publishes include styling without requiring Node. Regenerate it with the commands in [setup](docs/SETUP.md).

### Page reference and verification register

The login fills the viewport edge to edge. On desktop the seal stays at the top left and the form is vertically centered. The white background behind the form fades left to right into the QCU campus photo. The form uses softly filled fields, a blue sign-in button, and a keyboard-accessible Show/Hide password control. The solid white background fills the screen below the large breakpoint and on short landscape screens. Spacing responds to viewport height, and the page can scroll when the available height is too small to show every control. Plus Jakarta Sans is served locally. Validation appears by the affected field or above the form for failed credentials.

The source page/action inventory is recorded in [the functional specification](docs/FUNCTIONAL_SPEC.md). This register tracks design references and visual verification.

The white admin sidebar is 16rem wide on desktop. It follows the supplied Web Forms reference for the Core Management and Attendance & Users groups, line icons, and active row with a light-blue fill and blue left edge. The header adapts the first MVC sidebar's QCU seal, QCU Events name, and Administrator portal label. The white account footer contains the signed-in user's initial, role, and icon-only sign-out. Navigation rows and sign-out retain 44px minimum click targets. On smaller screens, the navigation opens under a compact Menu control. Event Directory and sign-out are available now. The other visible destinations are indicated as unavailable until implemented. Attendance-specific workflows remain in the functional specification.

| Portal | Page/workflow | Source reference | Design reference | Status |
| --- | --- | --- | --- | --- |
| Account | Login | Frontend/Login/Login.aspx | [Supplied layout reference](MVC_QCUEventsManagement/DesignReferences/Account/login-layout.png) with the original MVC white fade | See [testing](docs/TESTING.md) |
| Shared | Access denied / entry routing | Frontend/AccessDenied.aspx; Default.aspx | Shared account styling for initial access feedback | Functional page and entry routing verified |
| Admin | Shared sidebar | Admin.Master | [White sidebar reference](MVC_QCUEventsManagement/DesignReferences/Admin/sidebar-white.png) and [header detail](MVC_QCUEventsManagement/DesignReferences/Admin/sidebar-white-header.png), with the first MVC branding | See [testing](docs/TESTING.md) |
| Admin | Event Directory | AdminEvents.aspx | User-directed redesign using the shared white QCU admin styling | Search and registration tabs share the results panel. The active tab is underlined. Counts show on the unsearched directory, and the results show the matching count. Desktop results align in columns and stack on smaller screens. See [testing](docs/TESTING.md). |
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
- Visual direction: page-specific references. The account layout and color tokens are established for login. The admin and student portal design decisions follow their own references.
- Accessibility and responsive behavior are required for all implemented pages.
