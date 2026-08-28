# Sprint 3: patient queue transparency

Status: Bounded automated evidence passing; independent reviews and program-owner packet review pending  
Authorization: [Decision 0006](../decisions/0006-approved-sprint-03-patient-queue-transparency.md)
Evidence: [Sprint 3 evidence packet](sprint-03-evidence.md)

## Objective

Add a disabled, synthetic-only patient status projection and resilient HTTP polling for `OperationalReview`, `Queued`, `Reserved`, and `Redirected`. Provide calm, honest status and an approximate same-practice/facility queue count without exposing other patients, clinician workload, a physician identity, or an exact wait promise.

## Committed items

| Item | Deliverable |
|---|---|
| `TH-SP3-001` | Patient-scoped authoritative status contract and endpoint with version and snapshot time |
| `TH-SP3-002` | Deterministic same-practice/facility approximate requests-ahead query with unavailable fallback |
| `TH-SP3-003` | Pure status-content projector and state/position unit matrix |
| `TH-SP3-004` | Visibility-aware polling with bounded exponential backoff/jitter, manual retry, and authoritative reconciliation |
| `TH-SP3-005` | Accessible patient status card with last update, connection state, emergency/worsening guidance, and no exact wait promise |
| `TH-SP3-006` | Authorization, runtime, OpenAPI, unit, browser, planning, and durable evidence updates |

## Exit criteria

- A different patient receives no resource existence or queue information.
- Queue position counts only current earlier `Ready` entries in the same practice and facility.
- Every displayed position is approximate and no wait-duration estimate is invented.
- Missing queue evidence produces explicit position-unavailable content.
- Polling pauses while hidden, resumes visibly, backs off after errors, and never changes domain state without HTTP reconciliation.
- Manual refresh/retry, connection state, last update, worsening-symptoms action, and emergency direction remain keyboard and screen-reader usable at 320 px.
- Existing Sprint 1 and Sprint 2 automated gates remain green; feature defaults off and Production rejection remain unchanged.
- Independent clinical-safety, security/privacy, data, accessibility, and program-owner packet review remain open and are not self-certified.

All automated exit criteria above are demonstrated in the [Sprint 3 evidence packet](sprint-03-evidence.md). The open independent reviews prevent production or patient-care use.
