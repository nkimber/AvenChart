# COV-005 assessment — scheduling and communications

- Status: in review
- Baseline: `phase-1-experimental` at `d77a8320e6751a2deb2daf14cf1ac5d6b00cb989`
- Observed on: 2026-08-21
- Primary reviewers: `phase2_clinical_safety`, `phase2_data`, `phase2_frontend_accessibility`
- Independent verification: separate read-only `phase2_verifier` pass
- Primary coverage: `COV-005`
- Supporting coverage: `COV-003`, `COV-008`, `COV-009`, `COV-011`, `COV-012`, `COV-014`
- Evidence level: source and retained-test trace, clean Release and modern UI builds, complete modern UI unit suite, focused scheduling tests; database interleavings, injected faults, controlled browser races, accessibility sessions, and qualified workflow decisions remain outstanding

## Assessment question

Does the fixed Phase 1 baseline preserve safe, attributable, closed-loop scheduling and communication state as appointments, messages, referrals, recalls, therapy sessions, reminders, and bulk outputs are created, changed, completed, corrected, and retained?

This is an engineering-readiness assessment. Clinical, care-coordination, health-information-management, privacy, retention, accessibility, integration, and legal conclusions remain subject to appropriately qualified validation. It makes no compliance, certification, delivery, or production-use claim. The review is consistent with the current [HealthIT.gov SAFER clinical-communication guidance](https://healthit.gov/resources/2025-safer-guide-clinical-communication/), which treats reliable electronic communication and follow-up as sociotechnical workflows rather than message creation alone.

## Representative traces

### Appointments and portal requests

1. Appointment status changes lock the current row and apply an explicit transition policy.
2. Conflict-enforced creation checks availability and then creates on a separate connection; the checked decision is not guaranteed at commit.
3. Recurring-series edits and exception operations replace a caller-loaded exception set without a caller version or consistent shared lock.
4. Appointment deletion physically removes the appointment; portal appointment-request and event history cascades with it.
5. Portal appointment requests are otherwise a strong counterexample: portal identity is patient-bound, appointment and staff reminder creation share a transaction, and trigger-maintained request history exposes lifecycle events to the patient.

### Staff and portal messages, referrals, and recalls

1. Message assignment, forwarding, correction, escalation, and archive use stronger versions, actors, reasons, row locks, and events.
2. Older message status, content, and reply routes bypass those controls, allowing current content or ownership to disagree with retained history.
3. Referral creation and transitions are another strong counterexample: EF transactions, expected workflow versions, explicit transition policy, accountable ownership, due dates, reasons, and immutable events operate together.
4. Recalls have active work and outreach activities but no durable completion, deferral, cancellation, failure, or escalation lifecycle. Physical deletion is their only exit and cascades the outreach history.
5. Portal message and therapy-group selections permit obsolete responses to replace current detail, broadening the response-ownership condition already recorded in `P2-03-F007`.

### Therapy, reminders, and batch communication

1. Session completion correctly refuses unrecorded attendance, snapshots present participants, and protects session status with optimistic concurrency.
2. Attendance updates do not participate in the same session concurrency boundary, so a preloaded update can commit after completion and contradict the snapshot.
3. Group encounter generation holds a therapy transaction but creates each chart encounter through another autocommitting connection before the link is retained.
4. The modern therapy UI provides no attendance step, making completion unreachable for a session with members even though attendance APIs exist.
5. Reminder selection respects configured communication preferences and retains local dispatch records. Reminder and batch UIs explicitly disclose that no external delivery provider is connected; delivery, failure, acknowledgement, and external referral exchange therefore remain scope questions rather than findings.

## Reproducible checks

| Check | Result |
| --- | --- |
| Resolve the Phase 1 tag and compare `avenchart/`, `avenchart-ui/`, and `infra/` with the baseline | Baseline resolved; product tree remained unchanged during assessment |
| Release API build | Passed with 0 warnings and 0 errors |
| Complete modern UI unit suite | 31 files and 178 tests passed |
| Focused appointment status, scheduling-entry, clinician schedule, portal appointment, and portal API tests | 5 files and 19 tests passed |
| Modern UI production build and bundle budget | Passed; initial bundle 201,524 of 256,000 bytes, 128 chunks |
| PostgreSQL concurrency, fault injection, cascade/recovery, and browser response-order scenarios | Not run: Docker/PostgreSQL was unavailable; no disposable browser-backed application runtime could be established |

The green tests establish valuable appointment and portal contracts. They do not exercise two-writer conflict enforcement, recurrence lost updates, stale message/correction interleavings, therapy attendance/completion races, encounter/link fault recovery, recall closure and deletion, obsolete portal-thread responses, or the missing therapy attendance UI. No page-specific unit tests were located for staff/portal messaging, referrals, recalls, therapy groups, batch communication, or Scheduling Operations beyond the focused set above.

## Material strengths and counterevidence

- Referral lifecycle is transactionally versioned, policy controlled, owner and due-date aware, actor/reason attributed, and evented.
- Portal appointment requests are patient-bound, atomic with a staff reminder, trigger-versioned, and expose patient-visible history.
- Appointment status transitions use row locks and explicit allowed transitions.
- Governed message assignment, forwarding, correction, escalation, and reversible archive retain substantially better history than legacy routes.
- Therapy completion refuses unknown attendance, snapshots participants, and protects session status with optimistic concurrency.
- Therapy membership capacity is serialized through a group lock, and already linked encounter-generation retries are idempotent.
- Reminder eligibility respects inactive appointments and communication preferences.
- Reminder and batch-communication screens accurately state that they create local evidence or output and do not prove external delivery.
- Portal appointment UI has strong loading, retry, empty, history, modal focus, Escape, focus restoration, and focused automated evidence.
- Referral and clinician-message root loaders demonstrate correct cancellation patterns that could be shared by affected selection views.

These controls materially narrow the findings. The assessment does not attribute the conditions to EF Core, SQL, Minimal APIs, or React as technologies. Strong and weak examples exist in each layer; the deciding factors are shared transaction scope, caller-visible versions, response ownership, retained resource evidence, and complete workflow reach.

## Validated findings

| Finding | Condition | Severity | Reach | Production blocker |
| --- | --- | --- | --- | --- |
| [`P2-03-F019`](../findings/p2-03-f019-appointment-conflict-atomicity.md) | Enforced appointment-conflict checks are not atomic with creation | High | Repeated | Yes |
| [`P2-03-F020`](../findings/p2-03-f020-recurring-appointment-concurrency.md) | Recurring appointment mutations can lose exceptions and bypass conflict validation | High | Repeated | Yes |
| [`P2-03-F021`](../findings/p2-03-f021-legacy-message-history-bypass.md) | Legacy message mutations bypass the governed history and version boundary | High | Repeated | Yes |
| [`P2-03-F022`](../findings/p2-03-f022-therapy-attendance-snapshot.md) | Therapy attendance can diverge from the completed-session snapshot | High | Repeated | Yes |
| [`P2-03-F023`](../findings/p2-03-f023-therapy-encounter-link-atomicity.md) | Therapy encounter creation is not atomic with session linkage | High | Repeated | Yes |
| [`P2-03-F024`](../findings/p2-03-f024-recall-terminal-lifecycle.md) | Recall follow-up has no durable terminal lifecycle | High | Repeated | Yes |
| [`P2-05-F009`](../findings/p2-05-f009-workflow-mutation-provenance.md) | Scheduling and communication mutations lack consistent resource-scoped provenance | High | Cross-cutting | Yes |
| [`P2-08-F001`](../findings/p2-08-f001-therapy-attendance-ui.md) | Therapy sessions with members cannot be completed through the modern UI | Medium | Repeated | Unknown pending scope |

The engineering conditions were independently reproduced from the fixed source. High severity reflects the adopted future-production target; it does not assert that a real scheduling collision, lost message, inconsistent therapy record, missed recall, patient harm, or unauthorized action occurred in the synthetic Phase 1 experiment.

## Existing findings broadened by this packet

- [`P2-03-F007`](../findings/p2-03-f007-patient-route-response-inversion.md) now includes the same unowned-response cause in portal message threads and therapy-group selections. It remains one finding rather than three UI-race entries.
- [`P2-03-F011`](../findings/p2-03-f011-clinical-record-hard-delete.md) now includes appointment deletion that cascades portal request history and recall deletion that cascades outreach evidence. The distinct absence of a recall terminal lifecycle remains `P2-03-F024`.
- [`P2-05-F003`](../findings/p2-05-f003-phi-audit-resource-correlation.md) and [`P2-05-F004`](../findings/p2-05-f004-phi-audit-result-status.md) cannot substitute for resource-level mutation history and are cross-referenced by `P2-05-F009` rather than duplicated.

## Narrowed or retained as unknown

- Appointment overlap is sometimes explicitly permitted. `P2-03-F019` concerns requests that opt into enforcement; it does not declare every overlap invalid.
- Appointment status-transition policy was reproduced as a strong control. The rejected lifecycle-bypass subclaim is not included in `P2-03-F020`.
- Therapy session-status concurrency is not absent. The finding is the separate attendance boundary and its participant snapshot.
- Reminder dispatch currently records local queued evidence only, and the UI accurately discloses that no external provider is connected. External delivery and acknowledgement remain a product/operations decision.
- Referral workflow is locally well governed. Binding it to an external transmitted artifact, recipient acknowledgement, and automated overdue escalation remains an integration and operating-scope question.
- Clinician inbox reply may intentionally leave a message open, and governed archive is available. A communication owner must define New, read, Done, deferred, escalated, and archived semantics before the absence of a Done control can be a finding.
- Appointment-dialog focus containment/restoration, 400% reflow, screen-reader behavior, URL-carried search terms, and bulk-download governance remain evidence gaps pending manual accessibility and privacy evaluation.

## Required specialist decisions and remaining evidence

- Scheduling and clinical operations owners must define overlap enforcement, override evidence, recurrence exception semantics, stale-editor behavior, and appointment deletion policy.
- Clinical communication and HIM owners must define message correction, assignment, closure, retention, and resource-history requirements.
- Care-coordination, recall, and retention owners must define terminal states, escalation, failed outreach, cancellation, reopening, and exceptional deletion.
- Therapy clinicians and operations owners must define attendance correction, completion snapshots, encounter-generation idempotency, and whether the therapy UI is supported production scope.
- Security/privacy and audit owners must define authenticated actor, protected resource, prior/new state, reason, outcome, retention, and minimum-necessary evidence for mutations.
- Integration and product owners must decide reminder delivery, failure/acknowledgement, referral artifact exchange, and overdue-escalation scope.
- Accessibility specialists and representative keyboard/screen-reader users must evaluate appointment dialogs and the broader scheduling/communication surfaces at the adopted WCAG 2.2 AA target.
- A disposable synthetic PostgreSQL runtime must exercise appointment check/create and recurrence interleavings, stale message/correction paths, therapy attendance/completion ordering, encounter/link fault injection and retry, and destructive cascade/recovery.
- Controlled deferred-response component/browser tests must reproduce patient, portal-message, and therapy-group response inversion.

`COV-005` remains **In review** because these human decisions and runtime negative scenarios are outstanding. The validated engineering conditions may support later recommendation analysis; they do not authorize product changes.
