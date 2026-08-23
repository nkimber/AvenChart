# COV-011 — Modern clinician interface and accessibility

## 1. Scope and coverage

- Primary coverage: `COV-011` modern clinician interface
- Supporting coverage: `COV-003`, `COV-004`, `COV-005`, `COV-006`, `COV-007`, `COV-014`
- Domains: 02, 03, 05, 07, 08, 09, 12
- Surfaces traced: clinician shell and navigation, dashboard, patient search and chart shell, schedule/calendar/flow, timeline and patient messaging, encounter and laboratory entry points, report execution, API transport, unit tests, browser/accessibility specifications, and CI workflow
- Exclusions: portal-specific workflows except where the shared response-ownership condition is already established; formal WCAG conformance, assistive-technology certification, clinical policy, and production deployment claims

## 2. Baseline and methods

- Fixed baseline: `d77a8320e6751a2deb2daf14cf1ac5d6b00cb989` (`phase-1-experimental^{}`)
- Review date: 2026-08-21
- Product boundary: read-only; no application, database, deployment, test, or runtime files were changed
- Environment during the original packet: .NET 10.0.400, Node 24.13.1, npm 11.8.0; Docker/PostgreSQL were unavailable at that time. The later [runtime-readiness packet](cov-014-019-runtime-readiness.md) supersedes that availability constraint.
- Evidence levels: repository/source inspection, route and component traces, existing test inventory, clean Release build, modern UI unit tests, and retained browser/accessibility specifications
- Product-integrity check: `git diff --name-only d77a8320e6751a2deb2daf14cf1ac5d6b00cb989 -- avenchart avenchart-ui infra` remained empty
- Verification commands and results:
  - `dotnet build .\avenchart\AvenChart.slnx -c Release --no-restore -v:minimal` — passed, 0 warnings/errors
  - `npm test -- --run` in `avenchart-ui` — passed, 31 files and 178 tests
  - `npm run build` in `avenchart-ui` — passed in the specialist pass
  - The original packet did not execute Playwright or assistive-technology runs. The later runtime pass executed the repository accessibility gate: 6 of 8 scenarios passed and two authorization-state fixtures returned HTTP 400 before scanning. No screen-reader session was performed.

The review traced real UI entry contracts rather than seeding downstream state. Existing browser specifications and axe checks were treated as evidence of their covered scenarios only, not as a conformance claim.

## 3. Material strengths

1. `ClinicianShell` has a coherent navigation surface, a skip link exists in the application entry document, the mobile navigation drawer traps focus and restores it to the trigger, and session verification provides an explicit Retry and Sign out state (`ClinicianShell.tsx:370-449,584-614`; `index.html:16`; `index.css:9413-9430`).
2. Shared API transport supplies cancellation, bounded timeout, session-invalid signaling, and normalized problem text (`api/transport.ts:41-148`). Several pages use `AbortController` correctly, including the clinician inbox, laboratory queue, report execution, appointment option dialogs, and the shell.
3. The dashboard preserves usable prior snapshots when individual refreshes fail and exposes `role="status"` plus a Retry action (`ClinicianDashboard.tsx:71-169,225-260,263-374`). Laboratory review also has announced loading/error states and retry controls (`LabQueue.tsx:187-256,674-704,971-993`).
4. The modern interface provides substantial semantic structure: named regions, labelled tables and controls, `aria-current` navigation state, status and alert roles in many workflow components, protected download wording, and a route-level skip target. The retained accessibility suite checks serious and critical axe findings across representative clinician and patient-chart surfaces and several modal/lifecycle states.
5. Existing clinical and data findings are surfaced rather than hidden by the UI: duplicate review is fail-closed, SOAP conflicts preserve the draft, laboratory queues expose review state and history, and report execution exposes durable lifecycle metadata. These controls remain important counterevidence even where recovery or response ownership is incomplete.

## 4. Candidate and validated findings

### P2-08-F005 — Failed flow-board refresh can retain an actionable board for a different selected date

- Status: validated condition
- Domain(s): 03, 08, 09
- Coverage item(s): `COV-003`, `COV-005`, `COV-011`, `COV-014`
- Severity: medium (high boundary if the board is relied upon as the live room-control surface)
- Production blocker: unknown
- Reach: repeated across selected-day flow-board refreshes
- Confidence: high static; runtime deferred-response and clinical-operations validation outstanding
- Reviewer: `phase2_quality_operations`, coordinator frontend trace
- Independent verifier: required if severity is raised to high or blocker
- Specialist validation: scheduling/clinical operations, clinical informatics, accessibility
- Baseline commit: `d77a8320e6751a2deb2daf14cf1ac5d6b00cb989`
- Observed on: 2026-08-21

#### Condition

Changing the selected date starts a new flow-board request, but the previous board remains in state while the new request is pending or fails. A prior response can also complete after the newer request and replace the board. The page keeps the old board's Arrive, Room, and Complete actions enabled while the date control shows the new date.

#### Evidence

- `FlowBoard.tsx:22-32` stores one `board` value, starts a load for the initial date, and does not cancel or identify requests.
- `FlowBoard.tsx:55-67` calls `setDate` and `load(event.target.value)` for every date change; the load catch sets only `error` and does not clear or associate `board`.
- `FlowBoard.tsx:75-109` renders the board whenever `board` is non-null and leaves lane actions enabled; the date input remains bound to the newer `date` state.
- A controlled deferred-response test was not present. The source path is deterministic; a stale response or a failed new request can therefore display old actionable cards under the new date.

Expected behavior is a coherent date/board identity, with stale responses ignored and failed refreshes clearly separated from actionable current data. Whether the flow board is a safety-critical control surface requires an accountable scheduling owner.

#### Consequence

A scheduler or clinical staff member can see an appointment lane and act on it while believing it belongs to the selected day. The likely consequence is a wrong status transition or delayed room movement; no actual mutation or clinical harm was demonstrated.

#### Cause and reach

The selected date and fetched board are separate state values with no request epoch, cancellation, or failure invalidation. The same response-ownership pattern appears elsewhere and is retained under `P2-03-F007`; this finding is narrower because it concerns the board's failure state retaining enabled actions.

#### Risk calibration

- Impact: medium correctness and workflow impact; potentially high if the board is used as the authoritative live-room board
- Likelihood or preconditions: ordinary rapid date changes, a slow response, or a failed refresh
- Detectability: visual date/board mismatch may be subtle; action success can make it look normal
- Reversibility: status transitions may be reversible, but wrong-room actions require reconciliation
- Severity rationale: medium pending operating reliance; do not treat as a blocker without clinical/scheduling validation

#### Uncertainty and counterevidence

The board is a local UI projection, appointments are still validated by the API, and a full page reload recovers. Other pages such as the dashboard and laboratory queue clear or preserve snapshots with explicit error and retry semantics. No database interleaving or browser fault injection was run.

#### Validation record

- Independent method: separate quality-operations source trace and coordinator reproduction of the state transitions
- Result: condition corroborated statically; runtime ordering remains outstanding
- Reviewer agreement or dispute: quality-operations review agrees; no contrary specialist result received
- Specialist conclusion or outstanding need: scheduling/clinical operations and accessibility review required before high/blocker calibration

#### Disposition

No Phase 3 implementation is authorized. Link any accepted future recommendation to this finding only after the program owner accepts the evidence and the required specialists define the intended flow-board operating contract.

### P2-08-F006 — Representative clinician load failures are visual-only and inconsistently recoverable

- Status: validated condition
- Domain(s): 08, 09, 12
- Coverage item(s): `COV-011`, `COV-014`
- Severity: medium
- Production blocker: unknown
- Reach: repeated across clinician schedule, calendar, patient search, patient timeline, patient messaging, and flow-board loads
- Confidence: high static; browser and assistive-technology validation outstanding
- Reviewer: `phase2_quality_operations`, coordinator frontend trace
- Independent verifier: not required at current medium severity; required if raised to high/systemic
- Specialist validation: accessibility specialist and representative clinician users; clinical operations for queue reliance
- Baseline commit: `d77a8320e6751a2deb2daf14cf1ac5d6b00cb989`
- Observed on: 2026-08-21

#### Condition

Several clinician pages catch request failures into a plain `.error-banner` without a live-region or alert role and without a dedicated in-place Retry action. Other pages implement stronger `role="alert"`/`role="status"` and retry patterns, so the failure experience is inconsistent across equivalent data-loading workflows.

#### Evidence

- `ClinicianSchedule.tsx:50-60,160-169` loads the selected date without an abort signal and renders a plain error banner with no retry control.
- `ClinicianCalendar.tsx:74-94,292-294` handles a month-load failure with the same visual-only banner and no retry action.
- `PatientSearch.tsx:26-32,71-73` catches search failures and renders a plain banner; the user must submit the search again or navigate away.
- `PatientMessages.tsx:45-66,79-87` and `PatientTimeline.tsx:100-126,175-183` use the same visual-only pattern in patient-chart workflows.
- `FlowBoard.tsx:22-27,67-75` sets an error but retains the old board and has no retry action.
- Counterexamples demonstrate that the repository already knows how to make recovery explicit: `ClinicianDashboard.tsx:225-260,263-374`, `ClinicianMessages.tsx:158-174,700-709`, and `LabQueue.tsx:187-256,688-704`.
- The retained `accessibility.spec.ts` axe checks cover representative initial and modal states, but do not force these request failures, verify announcement timing, or test keyboard recovery.

#### Consequence

Screen-reader users may not be told that a queue, search, schedule, or patient timeline failed. Keyboard users may have no focused recovery target. Clinicians can also infer an empty or unchanged workspace from a failed request, especially where no retry action is present. No actual missed care event was established.

#### Cause and reach

Loading/error behavior is implemented locally rather than through a shared clinician data-state contract. The result is repeated semantic and recovery drift across otherwise similar pages.

#### Risk calibration

- Impact: medium accessibility, usability, and failure-recovery impact
- Likelihood or preconditions: any network, timeout, authorization, or backend failure on an affected page
- Detectability: visible to some users, silent or ambiguous to others; page telemetry is not a substitute for user feedback
- Reversibility: reload, repeat search, or navigation may recover, but the page does not consistently expose that path
- Severity rationale: medium under the adopted WCAG 2.2 AA project target; no production blocker is asserted without manual and operating evidence

#### Uncertainty and counterevidence

Some clinician pages have strong status and retry behavior, and the global toast component provides polite announcements for completed actions. A browser with a screen reader, keyboard-only navigation, zoom/reflow, and forced network failures was not available in this packet. This is not a WCAG conformance or legal-compliance conclusion.

#### Validation record

- Independent method: quality-operations inventory plus coordinator source trace across six pages and counterexamples
- Result: repeated condition corroborated statically
- Reviewer agreement or dispute: no contrary result received
- Specialist conclusion or outstanding need: accessibility specialist and representative clinician user validation remain required

#### Disposition

No Phase 3 implementation is authorized. Any future recommendation must define announcement, focus, retry, stale-data, and telemetry acceptance criteria and must be evaluated against the existing successful patterns.

## 5. Existing findings broadened and deduplicated

### `P2-03-F007` — response ownership

COV-011 broadens the existing systemic finding with these additional paths:

- `ClinicianSchedule.tsx:50-60` — selected-date responses are not cancelled or epoch-checked.
- `ClinicianCalendar.tsx:74-94` — month responses are not associated with the current month.
- `PatientMessages.tsx:45-66,74-86` — patient-chart message responses can complete after a chart switch.
- `PatientTimeline.tsx:100-126` — four patient-scoped requests are combined without response ownership.
- `FlowBoard.tsx:22-32,55-67` — selected-date responses can replace the current board; the failed-refresh/actionable-state consequence is separately recorded as `P2-08-F005`.

The coordinator does not create separate findings for each route because the common cause is unowned asynchronous response state. Patient chart, portal, therapy, billing, inventory, and clinician routes remain one systemic root with route-specific consequences.

### `P2-09-F002` — default verification gate

COV-011 adds that the repository-visible workflow does not execute the 19 Playwright specs or force clinician route failures, deferred response ordering, keyboard recovery, or assistive-technology announcements. The modern unit suite is valuable but contains only a small number of clinician component tests and no deferred-response race tests. This broadens the existing gate finding rather than creating a separate test-count finding.

## 6. Unknowns, counterevidence, and exclusions

- No formal WCAG conformance, screen-reader, keyboard-only, 400% zoom/reflow, contrast, target-size, or authentication-timing conclusion is made.
- No browser interception or deferred-promise runtime reproduction was available; source paths are deterministic but frequency and user impact remain bounded by timing and deployment.
- The API can reject stale or unauthorized mutations in several workflows; UI response ownership and recovery evidence are separate concerns.
- The dashboard, laboratory queue, clinician inbox, mobile drawer, portal appointment modal, and report execution page supply positive patterns that constrain the review and show the condition is not universal.
- `P2-05-F006` remains the canonical browser-persistence finding for saved SOAP templates; COV-011 does not create another browser-storage root.
- `P2-08-F001` through `P2-08-F004` remain the canonical therapy, critical-result, report-poll, and collections-queue UI findings; COV-011 does not duplicate them.
- Appointment dialogs and patient-appointment inline modals declare `aria-modal` but were not given a dedicated focus-containment test in this packet. This remains an accessibility evidence gap, not a validated finding.
- No product code, test, database, infrastructure, or configuration change is authorized by this assessment.

## 7. Specialist validation and scorecard impact

- Accessibility specialist and users with disabilities: manually exercise modal focus containment/restoration, error announcements, keyboard recovery, zoom/reflow, contrast, and screen-reader state changes.
- Clinician/clinical informatics and scheduling operations: decide whether flow-board actions are operationally authoritative and calibrate wrong-day action impact.
- Frontend/API owner: define the intended shared loading/error/retry contract and response-ownership boundary.
- Quality/operations: run deferred-response, forced 4xx/5xx, timeout, offline, and route-recovery browser scenarios; add results to the default-gate reconciliation.
- Program owner: accept or defer the two medium findings and any linked future recommendation; no scorecard domain may be marked complete from this packet alone.

`COV-011` remains **In review**. Domain 08 gains two medium UI/recovery conditions and broader systemic response-ownership evidence, but remains capped by existing high findings and the absence of manual accessibility evidence. Domain 09 gains a further gap in risk-shaped browser and accessibility coverage. Domains 03 and 07 receive supporting workflow-context evidence; no new clinical or API conformance claim is made. Domain 12 receives a repeated consistency/documentation concern about the absence of a shared UI state contract, not a separate architecture finding.

## 8. Recommended next evidence (not fixes)

1. Run a controlled browser test that selects Flow Board dates A→B with deferred responses in both orders; force B to fail and verify that A's actions cannot remain actionable under B.
2. Force 4xx, 5xx, timeout, offline, and session-invalid responses on Schedule, Calendar, Patient Search, Patient Messages, Patient Timeline, Flow Board, Lab Queue, and Reports; record visual state, accessible-tree announcements, focus destination, retry behavior, and stale-data visibility.
3. Run keyboard-only and screen-reader sessions through clinician appointment dialogs and patient-chart modals, including Tab wrap, Escape, trigger-focus restoration, and zoom/reflow.
4. Execute the Playwright accessibility and workflow suites in a disposable supported runtime and include route-interception/failure cases in the repository-visible gate.
5. Obtain scheduling and clinician-owner decisions about Flow Board authority, status-transition reversal, and acceptable stale-data messaging.
6. Reconcile these results into the findings register and scorecard before creating any Phase 3 recommendation.
