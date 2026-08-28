# Sprint 9 backlog: consultation wrap-up handoff

Status: Approved for bounded implementation by [TH-DEC-0012](../decisions/0012-approved-sprint-09-consultation-wrap-up-handoff.md)  
Mode: Disabled and synthetic only; additive V0287 migration

| ID | Deliverable | Acceptance evidence |
|---|---|---|
| `TH-SP9-001` | Add the monotonic consultation/request/shift wrap-up lifecycle boundary | V0287 permits only `Started -> MediaEnded`, adds request/shift `WrapUp`, preserves immutable start evidence and append-only events, and keeps active-shift uniqueness |
| `TH-SP9-002` | Add an owner-scoped idempotent enter-wrap-up command | opaque route; physician/facility/purpose/staff/owner/current/adult gates; required acknowledgments; expected consultation version; exact replay and changed-key/stale conflict |
| `TH-SP9-003` | Atomically hand unfinished work to physician-owned wrap-up | consultation/request/shift and both events change together; appointment/encounter remain open; reservation/session stay released/ended; physician cannot reserve new work |
| `TH-SP9-004` | Preserve the owner workspace and unsigned draft during wrap-up | workspace exposes status/version only; same owner can reload/save canonical draft; non-owner remains opaque; no signing/finalization/downstream action |
| `TH-SP9-005` | Add honest patient and physician wrap-up UX | patient sees unfinished synthetic record state without identifiers or completion promise; physician uses explicit acknowledged action with keyboard/focus/reflow/error/conflict recovery |
| `TH-SP9-006` | Extend typed contracts, OpenAPI, authorization/runtime/concurrency proofs, migration/bootstrap, planning, CI, runbook, and regressions | real PostgreSQL contention/atomicity/event/immutability/audit assertions; migration recovery; full backend/frontend/browser gates; evidence packet |

## Exit boundary

Sprint 9 ends with an owning physician able to move one active synthetic consultation into unfinished wrap-up while retaining exclusive ownership and draft access. The patient sees an accurate not-complete status. A future separately governed slice must supply a clinically safe final disposition, follow-up/safety communication, signing/finalization, and only then clinician release or downstream work. No such action is part of Sprint 9.
