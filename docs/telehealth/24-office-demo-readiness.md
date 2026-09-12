# Office telehealth demonstration — 14 September 2026

## Acceptance status

The established-patient/physician workflow passes a complete, real-API local
browser rehearsal. Azure deployment and internet-calling acceptance are recorded
separately below. This is a **synthetic demonstration**, not authorization for
patient care, legal signing, prescribing, patient delivery, billing or claims.

## Presenter script

Use [the telehealth entry](https://avenchart.kimber.dev/telehealth). Allow about
8–12 minutes for a narrated demonstration; rehearse on the office network first.

1. From the entry page, open **Open physician workspace in a new tab**. In the
   original tab choose **Sign in as an existing patient**. Both role-specific
   demonstration credentials are prefilled; sign in without changing them.
   Each tab keeps its own session. Use fictional information only.
2. Patient: **Start sleep demo**, confirm the displayed current location, review
   the safety questions and **Evaluate synthetic triage**. Review the existing
   details, clinical-list summary and coverage; make the five explicit
   confirmations. **Confirm details and join demo queue** performs the allowed
   verification and synthetic queue steps together. The patient sees a progress
   indicator and “You're in line”; no admin tab is required for this fixture.
3. Physician: **See next patient** starts availability if needed and reserves
   the request. The patient receives the changed status automatically. Review
   the reserved patient and reason before proceeding.
4. In each tab, **Check camera and microphone**, allow browser permissions,
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
   patient**. To stop, expand **End availability**, make its two confirmations
   and **End idle telehealth shift**. No database reset is necessary.

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
| Public/new-patient browser regressions | Four existing mocked-API browser tests passed: branded keyboard/accessibility entry, narrow reflow, identity-review failure/retry and masked session resume. |
| New-patient real API manual check | Created a fictional applicant and verified its displayed contact code; reached IdentityReviewPending with masked contact information and no canonical patient/queue entry. |
| Physical cameras | Not yet verified. ACS camera-selection regressions use a mocked SDK; the real local media rehearsal uses Chromium synthetic hardware. |
| Azure internet calling | Pending deployment acceptance; local WebRTC does not prove the ACS transport or office firewall. |

Actual blockers found and corrected by the rehearsal include discarded camera
selection, lost physician active-work recovery, nullable PostgreSQL conversation
parameters, malformed final-review SQL, missing JSON headers on lock/close,
an open reader preventing shift-end commit, and patient polling stopping before
the receipt could arrive. Background polling no longer clears command errors or
reloads the transcript on every parent render. Unchanged lock/close retries keep
their idempotency key; changed source versions get a new one.

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
$env:TELEHEALTH_DEMO_E2E = '1'
node node_modules/@playwright/test/cli.js test e2e/telehealth-office-demo.spec.ts --project=desktop-chromium --workers=1 --reporter=list
```

The test is opt-in and loopback-only. Diagnostic resume flags do not constitute
a fresh end-to-end pass. Do not edit source files while it runs against Vite.
A transcript GET can return 404 after wrap-up has begun but before the patient's
status poll; this intentional closed-transcript race is handled quietly and is
the only such response allowed by the test. Other HTTP errors fail acceptance.

## Deployment record

Pending. Preserve Azure's single-revision mode, existing secret references,
disabled startup seed/reset and byte-identical migration history. Update both
container images in one revision; verify health, traffic and image digests.
