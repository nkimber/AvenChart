# Sprint 5 backlog: connection-room shell

Status: Approved for bounded implementation by [TH-DEC-0008](../decisions/0008-approved-sprint-05-connection-room-shell.md)  
Mode: Disabled and synthetic only

| ID | Deliverable | Acceptance evidence |
|---|---|---|
| `TH-SP5-001` | Add `Connecting` and a constrained, provider-neutral session/grant/evidence model | State-machine tests; V0285 schema, constraint, append-only, migration and recovery proof |
| `TH-SP5-002` | Implement `ITelehealthVideoProvider` and an ephemeral `NON_PRODUCTION` simulator | Adapter contract tests; opaque/minimal payload proof; Production rejection; no network/media dependencies |
| `TH-SP5-003` | Issue patient grants only to the request owner after an active reservation | Portal ownership, request state/version, lease, practice/facility, expiry, replay, and cross-patient denial tests |
| `TH-SP5-004` | Issue physician grants only to the active reservation owner | Role, permission, facility, staff, shift, reservation-owner, expiry, replay, and cross-clinician denial tests |
| `TH-SP5-005` | Add user-initiated browser device preflight and isolated waiting-room UI | Track-stop tests, no device-label persistence, keyboard/screen-reader/axe/320 px evidence, honest simulator wording |
| `TH-SP5-006` | Extend OpenAPI, runtime safety, health, CI, planning, generated bootstrap, and regression evidence | Typed contracts; separate auth semantics; 21-table health; full backend/frontend/browser/migration suite |

## Exit boundary

Sprint 5 ends with both authorized participants able to hold independent, short-lived simulator grants for one opaque session and the request in `Connecting`. No media is transported and no consultation, encounter, chart note, prescription, claim, or clinical completion exists.
