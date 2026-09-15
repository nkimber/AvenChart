# Office telehealth demonstration — 14 September 2026

## Acceptance status

The established-patient/physician workflow passes repeated complete, real-API
local browser rehearsals. The release is deployed and healthy on Azure. The
clean Azure calling rehearsal is blocked by three pre-existing unfinished
requests for the default demo patient; those requests have not been changed.
Physical-camera acceptance is also pending. This is a **synthetic demonstration**, not authorization for
patient care, legal signing, prescribing, patient delivery, billing or claims.

## Presenter script

Use [the telehealth entry](https://avenchart.kimber.dev/telehealth). Allow about
8–12 minutes for a narrated demonstration; rehearse on the office network first.

1. From the entry page, expand **Demo accounts and scenarios** when the audience
   needs the test credentials and purpose of each account. Open **Open physician
   workspace in a new tab**. In the original tab choose **Sign in as an existing
   patient**. Both role-specific demonstration credentials are prefilled; sign
   in without changing them. Each tab keeps its own session. Use fictional
   information only.
2. Patient: **Start sleep demo**, confirm the displayed current location, review
   the safety questions and **Evaluate synthetic triage**. Review the existing
   details, clinical-list summary and coverage; make the five explicit
   confirmations. **Confirm details and join demo queue** performs the allowed
   verification and synthetic queue steps together. The patient sees a progress
   indicator and “You're in line”; no admin tab is required for this fixture.
3. Physician: **See next patient** starts availability if needed and reserves
   the request. The patient receives the changed status automatically. Review
   the reserved patient and reason before proceeding.
4. **Complete the device check in both tabs before either tab joins a call.**
   In each tab, **Check camera and microphone**, allow browser permissions,
   then enter that role's waiting room. Choose a **different named camera** in
   each tab. For one laptop, enable the same-laptop silent-audio option in both
   tabs before **Join video visit**. Wait for **Patient and physician are
   connected** and inspect both previews. A tab waiting alone is not sufficient
   to start the consultation. The local fallback test transport has different
   POC button names; Azure uses internet calling.
5. Physician: conduct the seven explicit opening checks, then **Start synthetic
   lifecycle**. Show both sides of the short synthetic transcript. Type a brief
   fictional SOAP note and **Save unsigned draft**. Explain that this is not
   real clinical documentation. Do not reload during a live video demonstration:
   saved chart work is recoverable, but a browser reload ends that tab's media.
6. Make the three end-session confirmations and **End synthetic session and
   enter wrap-up**. The camera is released and the patient sees wrap-up status.
   Author the safety disposition, follow-up owner/timeframe, next steps and
   warning/escalation text. Use an honest communication selection; do not mark
   something discussed or delivered when it was not. **Record safety draft**.
7. Review the exact current evidence and make the final-review confirmations;
   **Record final clinical-review evidence**. Make the two lock confirmations;
   **Record synthetic encounter lock**. The note becomes read-only. Make the
   two closure confirmations and **Close synthetic visit and return to
   availability**. The patient receives the synthetic receipt and authored plan
   preview automatically. These are not a completed appointment or real delivery.
8. To repeat, start the next fictional patient request and use **See next
   patient**. A patient who is still waiting can expand the visible **Cancel
   telehealth visit** control, confirm cancellation and return to the main
   telehealth entry; the ready queue entry and provisional synthetic appointment
   are cancelled together. To stop, expand **End availability**, make its two
   confirmations and **End idle telehealth shift**. No database reset is necessary.

Optional prescribing/pharmacy, completion and claim preparation are collapsed
and loaded only when opened. Leave these out of the core office script; they
are separate governed demonstrations, not shortcuts to real-world actions.

## Before the meeting

- Connect both cameras before opening the rooms; close competing camera apps.
  Confirm the actual camera name and image in each tab. Test on the exact laptop,
  browser, USB connections and office network. Physical-device/driver contention
  cannot be established by mocked-camera tests.
- Keep the same-laptop audio option on in both tabs to avoid speaker feedback.
  To demonstrate two-way audio, use a second device with headphones instead.
- Finish any earlier owned visit or use its normal release/connection-recovery
  controls. Do not delete records or reset the shared Azure database.
- If a selected camera is missing, reconnect it, refresh available devices and
  select it explicitly. The internet-call implementation refuses to silently
  substitute the first camera. Leave and rejoin to change devices.
- A denied/stalled permission request is recoverable: check site permissions,
  then retry. Preflight times out after 30 seconds and releases late-approved
  streams. A lost connection needs an explicit rejoin and verified peer presence.
- Save notes before navigation. Reload restores owned consultation/wrap-up and
  locked-note state. Unsaved notes are not guaranteed across browser closure.

## First-time/new-patient branch

The public **Start as a new patient** route collects only fictional identity
details first. The visible demonstration contact code verifies synthetic contact
control, not identity. The default configuration intentionally stops at
**Stopped safely at identity review** without creating a chart or joining the
queue. Present this as a short optional identity-intake demonstration, not as the
fast path into the physician call. Do not bypass approved identity review.

## Evidence — 12 September 2026

| Check | Result and boundary |
| --- | --- |
| Full established-patient + physician workflow | Passed in Chromium, separate tabs, real API and PostgreSQL, no API mocks: intake → queue → two-peer local media → bidirectional transcript → saved SOAP/reload → wrap-up/reload → safety draft → final review → lock/reload → closure → automatic receipt/plan → end shift |
| Accessibility/laptop layout | Same real-API rehearsal: no serious/critical axe findings at the final patient/physician states; no horizontal page overflow at 1280×720. This is not a whole-product WCAG conformance claim. |
| Frontend telehealth regression suite | 178 tests passed across 25 files, including selected-camera, denied permission, remote-peer presence, interrupted command retry, recovery and polling tests. |
| Backend telehealth suite | 758 tests passed. Real SQL behavior was additionally exercised in the browser rehearsal. |
| Production frontend build | TypeScript, Vite and initial bundle budget passed (246,548 / 256,000 bytes at the recorded build). The lazy ACS SDK chunk remains large; warm the room before presenting. |
| Browser accessibility/recovery regressions | All 22 existing telehealth browser scenarios passed in desktop Chromium across the validation runs: public/new-patient entry, governed applicant handoffs, patient/physician/admin screens, optional drafting, and failure recovery. These use mocked feature APIs, unlike the separate real-API office rehearsal. Other browser projects were not rerun in this task. |
| New-patient real API manual check | Created a fictional applicant and verified its displayed contact code; reached IdentityReviewPending with masked contact information and no canonical patient/queue entry. |
| Physical cameras | Not yet verified. ACS camera-selection regressions use a mocked SDK; the real local media rehearsal uses Chromium synthetic hardware. |
| Azure deployment | Healthy release `avce82741ad-app--telehealth-4f80bf24`, one active revision, 100% traffic. Live/ready/telehealth endpoints return HTTP 200; role-specific patient sign-in reaches the deployed workspace. |
| Azure internet calling | Clean rehearsal stopped before creating a request: the default patient has three unfinished requests from August 31 (Verification, OperationalReview and Queued). Approval requested to cancel these through normal UI while preserving history, or use a separate test patient. No ACS call or physical-camera proof is claimed. |

Actual blockers found and corrected by the rehearsal include discarded camera
selection, lost physician active-work recovery, nullable PostgreSQL conversation
parameters, malformed final-review SQL, missing JSON headers on lock/close,
an open reader preventing shift-end commit, and patient polling stopping before
the receipt could arrive. Background polling no longer clears command errors or
reloads the transcript on every parent render. Unchanged lock/close retries keep
their idempotency key; changed source versions get a new one.

The final combined local repeat passed 22 of 23 scenarios under an intentionally
short 60-second command-line cap; the long applicant operational-review scenario
hit that cap near its end. It then passed in 1.2 minutes with the repository's
normal 300-second timeout. No timeout setting or test assertion was weakened in
the repository. The complete real-API two-tab scenario passed repeatedly,
including the final combined run (39.4 seconds).

## Reproduce the isolated browser rehearsal

Use the synthetic Docker Compose project `avenchart-telehealth-demo-qa`, never
the shared Azure database, for this local test. Its fresh database was seeded
once; repetitions complete and end work through normal UI operations.

```powershell
# Match the existing Azure limit for this two-tab accelerated rehearsal.
# The default local limit remains 120; request limiting is not disabled.
$env:AVENCHART_STAGING_RATE_LIMIT_PERMITS = '300'
docker compose --env-file .env.staging -f docker-compose.staging.yml -p avenchart-telehealth-demo-qa up -d --no-deps --wait api
# Run the source UI on port 3100 with VITE_API_BASE_URL=http://127.0.0.1:8088.
Set-Location avenchart-ui
$env:MODERN_UI_BASE_URL = 'http://127.0.0.1:3100'
$env:TELEHEALTH_AZURE_DEMO_E2E = $null
$env:TELEHEALTH_DEMO_E2E = '1'
node node_modules/@playwright/test/cli.js test e2e/telehealth-office-demo.spec.ts --project=desktop-chromium --workers=1 --reporter=list
```

The local test is opt-in and loopback-only. The separate explicit
`TELEHEALTH_AZURE_DEMO_E2E=1` opt-in permits only HTTPS on `avenchart.kimber.dev`
and selects the actual ACS path, including remote-video rendering, camera
controls and leave/rejoin. It uses synthetic browser hardware, not physical
cameras. It refuses to start over an unfinished request or enter a different
queued patient's consultation. Diagnostic resume flags do not constitute a
fresh end-to-end pass. Do not edit source files while it runs against Vite.
A transcript GET can return 404 after wrap-up has begun but before the patient's
status poll; this intentional closed-transcript race is handled quietly and is
the only such response allowed by the test. Other HTTP errors fail acceptance.

## Deployment record

- Application commit: `4f80bf2494a3974aa48e56474addf72df6f1563d`, pushed to GitHub.
- Deployment: 12 September 2026, approximately 21:37 UTC.
- Container App: `avce82741ad-app`, resource group `rg-avenchart-demo-e82741ad`.
- Revision: `avce82741ad-app--telehealth-4f80bf24`; predecessor
  `avce82741ad-app--0000017` drained normally and is inactive.
- ACR builds `ch15` (UI) and `ch16` (API), both Succeeded.
- UI digest: `sha256:56f64d1ff8dd4181daa5b1ef9515934761932f86aee334686044288619b65171`.
- API digest: `sha256:1a7c3d8960619db5259e0f4416573d71a6e2c029b36fe6e10c9b3b2127475372`.
- Both image digests updated atomically with an ARM merge patch. An initial
  request was rejected for the CLI-only `imageType` metadata field; no revision
  changed on that attempt. The corrected patch was accepted with HTTP 202.
- Configuration/template SHA-256 excluding images and revision suffix is
  unchanged: `65c83e8ceaeb0335e2f8c94ff4cab6a729249e7c0885e8709408e80abaf23185`.
  Existing secret references, single-revision mode, pool/rate limits, scaling,
  startup seed/reset disabled flags, and branded host settings were preserved.
- Build context came from the committed Git archive, with all 293 migration SQL
  files copied byte-identically from the verified live-ledger baseline. Manifest
  SHA-256: `4c63a1b4ebf3ddaecba528ad282189849142e9c7a3d7d740d2f0f34cf9cd4685`.
  No migration, seed reset, domain, secret or infrastructure changes were made.
