# AvenChart UI

AvenChart UI is the independent React and TypeScript interface for the AvenChart API. It includes staff and patient-portal entry flows, clinician workspaces, patient-chart views, responsive navigation, component tests, and browser workflow coverage.

Start the API stack in [`avenchart/`](../avenchart/) first, then run:

```powershell
docker compose up -d --build
```

The UI is available at <http://localhost:3100/> and uses the API at <http://localhost:5001/>.

For a host build:

```powershell
npm ci
npm test
npm run build
```

AvenChart UI is licensed under GPL-3.0-or-later. It is independent and was developed with reference to the [original OpenEMR project](https://www.open-emr.org/); see the repository [notice](../NOTICE.md) for complete attribution.
