# Sprint 8 backlog: consultation documentation draft

Status: Approved for bounded implementation by [TH-DEC-0011](../decisions/0011-approved-sprint-08-consultation-documentation-draft.md)  
Mode: Disabled and synthetic only; no database migration

| ID | Deliverable | Acceptance evidence |
|---|---|---|
| `TH-SP8-001` | Extend the owner-bound workspace with a current SOAP draft projection | current version/author/time/lock and four sections only; no identifiers or version-history content; prior read allowlist preserved |
| `TH-SP8-002` | Add an owner-scoped explicit draft-save application boundary | physician/facility/purpose/staff/active-consultation/adult gates; opaque route; canonical encounter reuse; non-owner and stale-scope denial |
| `TH-SP8-003` | Reuse canonical append-only SOAP versions and signature locking | expected-version zero/current behavior; no-change and empty rejection; 10,000-character limits; authenticated author/server time; signed-encounter rejection |
| `TH-SP8-004` | Preserve PHI audit, privacy, and synthetic-only controls | permitted/denied audit rows on opaque resource; no-store headers; no PHI in URLs/logs/storage/events; Production configuration rejection unchanged |
| `TH-SP8-005` | Render an accessible explicit-save draft editor | empty initial state, SOAP labels, no clinical defaults, saved/dirty/error/conflict status, deliberate reload, keyboard/axe/320 px/three-engine evidence, no autosave or browser persistence |
| `TH-SP8-006` | Extend typed contracts, OpenAPI, authorization/runtime proofs, planning, CI, runbook, and regressions | owner save/reload, stale writer conflict, non-owner zero-delta, excluded downstream mutations, migration/bootstrap invariants, complete evidence packet |

## Exit boundary

Sprint 8 ends with the owning physician able to explicitly save and reload an unsigned SOAP draft on the existing synthetic encounter through an opaque consultation route. Each change is a canonical conflict-safe version and every draft is visibly incomplete. Templates, autosave, diagnosis/coding, orders, medication changes, prescribing/pharmacy, disposition, signing/finalization, AVS, completion, claims, billing, real media, external integrations, and patient care remain unavailable.
