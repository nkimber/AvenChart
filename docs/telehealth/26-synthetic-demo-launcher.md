# Synthetic two-session demo launcher

Authorized by the project owner's October 16, 2026 instruction to implement the
demo launcher, active-call recovery, and acceptance tests, keeping all demos
synthetic. This does not authorize production patient care or deployment.

Open `/telehealth/demo`, confirm fictional use, and choose **Create synthetic
demo**. Open the patient and physician invitations in separate tabs, browser
profiles, or devices. Check devices in each session, then join in either order.
Use headphones on separate devices. On one laptop, enable the silence option in
both media panels to prevent feedback.

The launcher displays each participant's latest reported state. An invitation
activates one role in one browser session; a lost response can be retried by that
same session. Invitations are URL fragments, removed after successful activation.
Refreshing a participant tab uses its room-scoped session and requires an explicit
device check and join again. Refreshing the launcher recovers its original room
through an idempotent create request.

Either participant or the launcher can select **End this demo**. Both browser
sessions release media on the next successful status check. Rooms expire after
30 minutes, and the browser also stops media at the original expiry even while
offline. Create another pair without resetting the database or another demo.

## Configuration and boundaries

The default `Telehealth:DemoLauncherEnabled` is `false`. The synthetic staging
Compose file enables it. All demo routes additionally require telehealth enabled,
`Mode=Synthetic`, a configured branded host, and an environment other than
`Production`. Enable either the local WebRTC POC or the internet ACS Calling POC.
Apply migration `V0338` and provision the configured facility using the existing
synthetic fixtures. The launcher requires no patient or physician sign-in.

The Azure application template exposes `telehealthDemoLauncherEnabled`, default
`false`, alongside the existing synthetic internet calling opt-in. This change
does not enable or deploy the launcher on the hosted site.

Only `telehealth_demo_runs` and `telehealth_demo_participants` are written. No
canonical patient, account, clinical sign-in, request, appointment, encounter,
consent, prescription, payment, or claim is created. Random scoped capabilities
are hashed in the database. Observer capabilities cannot join as a participant;
participant capabilities cannot join another role or room. The browser stores
room session keys in session storage; vendor tokens are held in memory. Treat
invitations and browser session storage as temporary credentials.

There are at most 50 simultaneously active rooms per practice/facility. Expired
rows are retained; no automated record deletion is introduced. Normal API request
limits still apply. Heartbeat labels are browser reports, not proof that another
person can hear the call. API restart preserves room/session authorization; local
signaling is process-local and reconnects with fresh negotiation. Multiple API
replicas still require shared signaling infrastructure.

The existing ACS group-call POC issues vendor credentials that are not scoped by
the provider to a single group. Ending or expiring a room denies new AvenChart
admission and tells cooperative browsers to stop; it cannot forcibly revoke an
already-issued ACS token or terminate a hostile vendor client. This remains a
synthetic demonstration, unsuitable for clinical care or a production isolation
claim. No recording or transcription is added.

## Acceptance checks

Run against an explicitly isolated synthetic staging environment:

```powershell
./avenchart/scripts/Test-TelehealthDemoLauncher.ps1 `
  -ApiBaseUrl http://127.0.0.1:8088 -UseDisposableSyntheticEnvironment `
  -DatabaseContainer avenchart-telehealth-demo-ci-postgres-1

$env:MODERN_UI_BASE_URL='http://127.0.0.1:8088'
$env:TELEHEALTH_API_BASE_URL='http://127.0.0.1:8088'
$env:TELEHEALTH_DEMO_E2E='1'
$env:TELEHEALTH_QA_API_CONTAINER='avenchart-telehealth-demo-ci-api-1'
Set-Location avenchart-ui
npx playwright test e2e/telehealth-demo-launcher.spec.ts `
  e2e/telehealth-office-demo.spec.ts --project=desktop-chromium --workers=1
Set-Location ..
./avenchart/scripts/Test-TelehealthActiveCallRecovery.ps1 `
  -ApiBaseUrl http://127.0.0.1:8088 -UseDisposableSyntheticEnvironment `
  -FixtureDatabaseContainer avenchart-telehealth-demo-ci-postgres-1
```

The launcher API check covers confirmation, idempotent retry, independent roles,
invitation replay, wrong-role and cross-room denial, paired signaling, ending,
expiry, and unchanged canonical clinical/account/sign-in row counts. Its optional
expiry fault changes only a newly created demo room in a guarded QA/CI database.
It never resets a schema or changes a clinical table. The recovery API check
creates a full synthetic visit and leaves it in wrap-up; use a disposable fixture.

The browser checks use real APIs and PostgreSQL, independent browser contexts,
and Chromium synthetic cameras/microphones. They check playing remote video,
inbound synthetic audio packets, patient-first joining, both-role refresh,
network interruption, disconnected-track recovery, guarded API restart, ending,
media release, and native permission denial/retry. The office test also checks
rejoining after the consultation starts without duplicating its encounter.
Tests disable traces/video capture to avoid retaining invitation capabilities.

The dedicated GitHub workflow provisions its own disposable staging project and
runs these checks. Physical USB devices, audible speech on two physical machines,
Safari/Firefox, mobile devices, and live ACS remain separate manual acceptance
work; synthetic Chromium RTP does not establish those results.

## Implementation checkpoint, October 16, 2026

- Backend build and 772 telehealth tests passed, including the relay restart
  cursor regression and synthetic/production feature-gate checks.
- Real PostgreSQL/API launcher acceptance passed, including server expiry,
  unchanged clinical/account/sign-in counts, replay and cross-room denial.
- Frontend type checking, production build, bundle budget (247,002 / 256,000
  initial bytes), and ESLint passed. The 198-case frontend batch passed 196
  cases; two existing timing failures passed focused reruns without changing
  their assertions or time limits. The updated room permission-error persistence
  and server-revocation tests also passed separately (3 / 3).
- The Azure application template compiled and the workflow YAML parsed.
- Browser acceptance remains pending a clean Linux run. The Windows browser
  runner hung during shutdown, including after a successful saved-wrap-up UI
  scenario; those process failures are not reported as passing acceptance.
- No hosted deployment, live ACS call, physical-device, or mobile acceptance
  was executed.
