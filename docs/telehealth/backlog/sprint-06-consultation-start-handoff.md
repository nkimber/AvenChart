# Sprint 6 backlog: consultation-start handoff

Status: Approved for bounded implementation by [TH-DEC-0009](../decisions/0009-approved-sprint-06-consultation-start-handoff.md)  
Mode: Disabled and synthetic only

| ID | Deliverable | Acceptance evidence |
|---|---|---|
| `TH-SP6-001` | Link one scheduled existing-system appointment during operational authorization and assign it during reservation | transactional replay/concurrency proof; one-to-one request/appointment constraint; scheduled/assigned lifecycle assertions |
| `TH-SP6-002` | Add `InConsultation` and a constrained telehealth consultation context/start-event model | V0286 schema, state-machine, append-only/no-delete, migration/recovery, and one-to-one linkage proof |
| `TH-SP6-003` | Enforce the physician-owned consultation start gate | role, purpose, facility, staff, shift, reservation, request/version, location, checklist, both-participant grant, expiry, replay, and concurrency tests |
| `TH-SP6-004` | Reuse the existing AvenChart encounter foundation in the same transaction | one appointment-linked encounter; no duplicate; zero notes/signatures/prescriptions/claims/billing deltas; opaque public response |
| `TH-SP6-005` | Add an accessible physician synthetic start checklist and patient in-consultation projection | explicit non-clinical wording; fail/retry focus; keyboard, axe, 320 px, Firefox/WebKit/Chromium evidence; no encounter-key disclosure |
| `TH-SP6-006` | Extend OpenAPI, authorization, migration, concurrency, health, runtime safety, planning, CI, runbook, and full regressions | typed contract; 23-table health; 242-migration recovery; complete evidence packet |

## Exit boundary

Sprint 6 ends with one synthetic request, existing-system appointment, existing-system encounter, reservation, video session, and telehealth consultation context transactionally linked in `InConsultation`. The public result exposes only an opaque consultation ID. Chart access, clinical documentation, diagnosis, orders, prescribing, pharmacy, claims, billing, completion, and real media remain unavailable.
