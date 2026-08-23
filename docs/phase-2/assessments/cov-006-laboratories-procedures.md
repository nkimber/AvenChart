# COV-006 assessment — laboratories, procedures, and result follow-up

- Status: in review
- Baseline: `phase-1-experimental` at `d77a8320e6751a2deb2daf14cf1ac5d6b00cb989`
- Observed on: 2026-08-21
- Primary reviewers: `phase2_clinical_safety`, `phase2_data`, `phase2_frontend_accessibility`
- Independent verification: retained Pilot C verifier evidence plus a separate correction-concurrency verifier pass and cross-specialist reproduction
- Primary coverage: `COV-006`
- Supporting coverage: `COV-003`, `COV-008`, `COV-009`, `COV-011`, `COV-012`, `COV-014`, `COV-019`
- Evidence level: source and retained-test trace, clean Release and modern UI builds, complete modern UI unit suite, focused laboratory tests; database interleavings, browser workflows, accessibility sessions, recovery exercises, and qualified policy decisions remain outstanding

## Assessment question

Does the fixed Phase 1 baseline preserve trustworthy order, specimen, report, result, review, correction, critical-result, patient-release, and follow-up state from local entry through downstream action and retained evidence?

This is an engineering-readiness assessment. It makes no clinical-adequacy, legal, certification, interoperability-conformance, or production-use claim. The current [HealthIT.gov SAFER Test Results Reporting and Follow-Up Guide](https://healthit.gov/resources/2025-safer-guide-test-results-reporting-and-follow-up/) was used as a recommendation benchmark, not as a universal requirement. It reinforces the importance of identifiable order ownership, complete status tracking, amended-result notification, and fail-safe critical-result follow-up, while leaving implementation and operating responsibility organization-dependent.

## Representative traces

### Local order, specimen, report, and result

1. Order creation resolves the patient and encounter, checks their relationship, and rejects a locking encounter signature before insertion.
2. Specimens have a constrained, versioned lifecycle with row locking and actor/reason events.
3. Reports do not resolve that specimen aggregate. They accept an order plus a free-text specimen/accession value, and results resolve only the report.
4. Result correction transactionally snapshots the prior value, but the request carries no expected content version and the row is neither locked before the snapshot nor conditionally updated.
5. Report and result writes do not consistently retain an authenticated actor, reason, or resource mutation event.

### Review, acknowledgement, and follow-up

1. Report assignment, sign, deny, reopen, and bulk sign use expected review versions, row locks, actors, reasons, events, and atomic transactions.
2. The report-review queue exposes patient, order, lab, provider, and review metadata, but no result values. It permits signing before a result exists.
3. Results can be created or corrected after review without changing the report's review status or version.
4. Critical acknowledgement similarly versions the acknowledgement state rather than the result content; later correction does not reopen or invalidate it.
5. The critical lifecycle ends at local acknowledgement. No linked accepted owner, patient or clinician communication, action, due time, escalation, coverage transfer, or follow-up closure exists in the application evidence.

### Critical classification, worklist, and portal

1. The supported local **Critical** option submits `C`, while display, queue, and acknowledgement logic recognize `critical`, `panic`, `hh`, and `ll`. The first-party value therefore misses the critical work queue.
2. The API returns every recognized open critical result, but the modern UI renders acknowledgement controls only for the first three.
3. The patient portal returns all patient-owned laboratory rows without a release predicate. It displays report status but omits report-review status, result status, and correction history.
4. The local UI accurately states that it does not prove partner receipt, provenance, delivery, interface correction acknowledgement, or external critical notification.

## Reproducible checks

| Check | Result |
| --- | --- |
| Resolve the Phase 1 tag and compare `avenchart/`, `avenchart-ui/`, and `infra/` with the baseline | Baseline resolved; product tree remained unchanged during assessment |
| Release API build | Passed with 0 warnings and 0 errors |
| Complete modern UI unit suite | 31 files and 178 tests passed |
| Focused Lab Queue and flag-normalization suite | 2 files and 18 tests passed |
| Extended focused API, Lab Queue, and flag suite | 3 files and 79 tests passed in the specialist pass |
| Modern UI production build and bundle budget | Passed; initial bundle 201,524 of 256,000 bytes, 128 chunks |
| PostgreSQL correction interleaving, result/review mutation, specimen mismatch, portal-state matrix, cascade/recovery, and browser flows | A nonexistent specimen/report/result and critical acknowledgement were replayed; correction interleaving, full portal-state matrix, and browser flows remain outstanding |

The green tests preserve useful API and error-state contracts. They do not exercise the real UI `C` value, review or acknowledgement followed by correction, more than three open critical results, overlapping corrections, mismatched or rejected specimens, patient release states, or closed-loop follow-up. The retained critical-result script inserts the downstream spelling `critical` directly and therefore bypasses the supported entry contract.

## Material strengths and counterevidence

- Patient and encounter identity are checked before an order is created.
- Report review transitions and bulk signing are transactionally versioned, locked, actor/reason attributed, and evented.
- Specimen lifecycle transitions are constrained, locked, version checked, actor/reason attributed, and evented.
- Critical acknowledgement state and its event commit atomically with server-derived actor, reason, and time.
- Result correction preserves a prior-value snapshot in the same transaction as the current-row update.
- Patient-lab and Lab Queue loading cancel obsolete requests and expose persistent loading, error, retry, and empty states.
- Unknown result flags remain visibly and textually marked rather than appearing normal.
- The generic integration inbox is idempotent by source/message ID and retains versioned reconciliation or rejection history.
- FHIR accurately advertises read/search rather than an unsupported laboratory write contract.
- Local UI boundaries are unusually explicit about the absence of partner transmission, provenance, delivery, barcode/courier integration, and external notification.

These controls materially narrow the findings. The conditions are not caused by choosing parameterized SQL instead of EF Core. Several of the strongest controls depend on SQL row locking and atomic event writes; the defects arise from inconsistent vocabularies, missing cross-aggregate invariants, incomplete version contracts, and unsupported workflow reach.

## Validated findings

| Finding | Condition | Severity | Reach | Production blocker |
| --- | --- | --- | --- | --- |
| [`P2-03-F025`](../findings/p2-03-f025-critical-result-vocabulary.md) | The supported Critical value is excluded from the critical-result workflow | High | Repeated | Yes |
| [`P2-03-F026`](../findings/p2-03-f026-result-correction-concurrency.md) | Concurrent result corrections can erase an intermediate correction from current state and history | High | Repeated | Yes |
| [`P2-03-F027`](../findings/p2-03-f027-critical-result-follow-up.md) | Critical-result evidence ends at local acknowledgement rather than accountable follow-up closure | High | Systemic | Unknown pending operating boundary |
| [`P2-03-F028`](../findings/p2-03-f028-specimen-result-lineage.md) | Report and result lineage is not bound to a governed specimen record | Medium | Repeated | Unknown pending laboratory scope |
| [`P2-03-F029`](../findings/p2-03-f029-portal-lab-release-context.md) | Portal laboratory release has no lifecycle predicate and omits material status context | Medium | Systemic | Unknown pending release policy |
| [`P2-08-F002`](../findings/p2-08-f002-critical-result-worklist-cap.md) | Only the three newest open critical results are actionable in the modern UI | High | Repeated | Yes |

High severity reflects the adopted future-production target; it does not assert that a critical result was missed, a correction was lost, a patient was misled, a follow-up failed, or harm occurred in the synthetic Phase 1 experiment.

## Existing findings broadened by this packet

- [`P2-03-F009`](../findings/p2-03-f009-encounter-signature-content.md) now covers the systemic content-binding cause across encounter signatures, laboratory report review, and critical-result acknowledgement. Review or acknowledgement workflow versions do not identify the result content they cover, and later result changes do not invalidate them.
- [`P2-03-F011`](../findings/p2-03-f011-clinical-record-hard-delete.md) already includes procedure-order deletion that removes specimens, reports, results, result versions, acknowledgement state, and their events.
- [`P2-05-F009`](../findings/p2-05-f009-workflow-mutation-provenance.md) now includes laboratory report/result mutations and specimen creation, which omit the authenticated actor/reason or hard-code `local-user`.
- [`P2-09-F002`](../findings/p2-09-f002-default-verification-gate.md) retains the missing real-entry and risk-shaped test evidence. It explains why green checks missed the conditions but does not replace their clinical/data findings.

## Narrowed or retained as unknown

- The specimen-lineage condition is Medium rather than High because externally collected specimens may legitimately lack a local specimen row. A laboratory owner must define when local specimen state is authoritative and how external provenance is represented.
- Portal status/release behavior is Medium rather than High because immediate patient access may be intentional. The verified condition is the absence of an explicit release predicate and the omission of available review/result/correction context, not a requirement to delay all results.
- Duplicate report, specimen, and result retries have no idempotency key or logical uniqueness, but valid duplicate identity for repeated panels and external messages is not yet defined. This remains a candidate for runtime and owner validation.
- The modern UI does not expose the backend's local order-transmit/status operations. Because the UI accurately disclaims transmission and supported ownership is unresolved, this remains a capability-reach question.
- Portal laboratory failure has no in-page retry or live error semantics. Manual accessibility and browser-failure evidence is required before a conformance or canonical finding conclusion.
- Signing a report assigned to another reviewer is permitted. Assignment may be informational; ownership policy is required before classifying that behavior.
- No supported external laboratory-result ingestion pipeline was found. `P2-D014` makes it required product scope, and synthetic inbox/apply evidence now records the absence as `P2-07-F005`.
- The critical acknowledgement endpoint uses `patients:lab:write`, while report signing uses `patients:sign:write`. The seeded doctor/admin groups receive both; no unauthorized fixture path was established. A laboratory/authorization owner must define whether acknowledgement is a laboratory editing act, a clinical acceptance act, or one step in a separate follow-up workflow.

## Required specialist decisions and remaining evidence

- Laboratory medicine and clinical informatics owners must define critical vocabulary, amendment/re-notification behavior, report-review meaning, acknowledgement meaning, and acceptable response times.
- Laboratory operations and interoperability owners must define specimen/accession authority, rejected/recollected handling, external provenance, duplicate identity, and COV-019 scope.
- Clinical operations must define responsible ownership, handoff acceptance, escalation, coverage, patient communication, follow-up action, and closure evidence for critical results.
- Patient-engagement, privacy, legal-policy, and clinical owners must approve portal release predicates and patient-facing preliminary, denied, reviewed, corrected, and amended-result context.
- Security and audit owners must define the actor, protected resource, prior/new state, reason, outcome, and retention required for laboratory mutations.
- Accessibility specialists and representative users must assess portal lab recovery, repeated control names, accordion semantics, keyboard behavior, screen-reader announcements, and 400% reflow.
- A disposable synthetic PostgreSQL runtime must reproduce the correction interleaving, review/acknowledgement followed by mutation, specimen mismatch, duplicate retry, cascade/recovery, and backup/restore behavior.
- Browser-backed tests must exercise the actual Critical selector, four-plus critical backlog, portal status matrix, and portal failure recovery.

`COV-006` remains **In review** because these human decisions and remaining runtime negative scenarios are outstanding. `COV-019` has evidence complete for the absent required boundary and is represented by `P2-07-F005`. The validated engineering conditions may support later recommendation analysis; they do not authorize product changes.
