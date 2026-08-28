# Sprint 7 backlog: read-only consultation workspace

Status: Approved for bounded implementation by [TH-DEC-0010](../decisions/0010-approved-sprint-07-read-only-consultation-workspace.md)  
Mode: Disabled and synthetic only; no database migration

| ID | Deliverable | Acceptance evidence |
|---|---|---|
| `TH-SP7-001` | Enforce adult-only established-patient request entry | repository predicate; underage/overage zero-request proof; prior adult journey regression |
| `TH-SP7-002` | Add an owner-bound read-only consultation workspace repository and response allowlist | consultation/request/appointment/encounter/physician/practice/facility binding; active/adult predicates; bounded active clinical lists; excluded-field contract tests |
| `TH-SP7-003` | Add physician-only workspace retrieval with own-encounter and patient-view permissions | role, facility, purpose, staff, non-owner, administrator, stale/inactive, and cross-scope denial evidence |
| `TH-SP7-004` | Reuse the existing PHI audit boundary and enforce no-store responses | permitted/denied audit rows bound to opaque consultation resource; cache-header assertions; no logs/storage/URL exposure |
| `TH-SP7-005` | Render an accessible consultation workspace inside the physician telehealth route | identity/callback, visit context, allergies, medications, and problems; explicit read-only/verify wording; retry, keyboard, axe, 320 px, Firefox/WebKit/Chromium evidence |
| `TH-SP7-006` | Extend OpenAPI, authorization, runtime safety, queue proof, planning, CI, runbook, and full regressions without a migration | typed contract; 23-table/242-migration invariants unchanged; privacy negative assertions; complete evidence packet |

## Exit boundary

Sprint 7 ends with the reservation-owning physician able to retrieve and render only a bounded current synthetic chart projection through an opaque consultation ID. It does not expose a patient or encounter key and does not enable broader chart navigation or any clinical/financial mutation. Documentation, diagnosis, orders, prescribing, pharmacy, claims, billing, completion, real media, and patient care remain unavailable.
