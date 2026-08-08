# Third-Party Software Notices

The modernized applications use third-party packages under their respective licenses. The GNU GPL license applied to project-authored code does not replace or remove those terms.

The package lock files and restored package metadata are the authoritative version-specific inventories:

- `avenchart/frontend/package-lock.json`
- `avenchart-ui/package-lock.json`
- `avenchart/backend/src/AvenChart.Api/AvenChart.Api.csproj`
- The NuGet package metadata restored for the backend build

## Direct runtime dependencies

| Application | Package | License reported by package metadata |
| --- | --- | --- |
| AvenChart reference frontend | `dompurify` | MPL-2.0 or Apache-2.0 |
| AvenChart reference frontend | `lucide-react` | ISC |
| AvenChart reference frontend | `react` | MIT |
| AvenChart reference frontend | `react-dom` | MIT |
| AvenChart UI | `lucide-react` | ISC |
| AvenChart UI | `react` | MIT |
| AvenChart UI | `react-dom` | MIT |
| AvenChart UI | `react-router-dom` | MIT |
| AvenChart API | `Microsoft.AspNetCore.OpenApi` | MIT |
| AvenChart API | `Microsoft.OpenApi` | MIT |
| AvenChart API | `Npgsql` | PostgreSQL License |

Build and test dependencies additionally include software under MIT, ISC, Apache-2.0, BSD-2-Clause, BSD-3-Clause, 0BSD, BlueOak-1.0.0, MIT-0, MPL-2.0, CC0-1.0, and CC-BY-4.0 terms. Some are development-only and are not necessarily included in a production artifact.

Before publishing a release, generate or review the dependency inventory for the exact lock files and compiled artifact, include every license or attribution notice required by the packages actually distributed, and retain any notices shipped inside those packages. Do not infer that a package is covered by the project's GPL merely because it appears in the same repository, build, container, or dependency graph.

The original OpenEMR project is attributed separately in `NOTICE.md`. Any source, asset, schema expression, or other copyrightable material copied from OpenEMR must retain the original file-level notices and any separately stated license terms.
