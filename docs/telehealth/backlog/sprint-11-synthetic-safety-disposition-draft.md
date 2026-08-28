# Sprint 11: synthetic safety-disposition draft

Status: Approved for bounded implementation by [TH-DEC-0014](../decisions/0014-approved-sprint-11-synthetic-safety-disposition-draft.md)  
Scope: Owner-only, append-only structured safety-disposition draft during unfinished synthetic wrap-up; no signing, delivery, lifecycle transition, downstream action, external integration, production use, or patient care

## Outcome

Sprint 11 gives the treating physician a safe place to record a complete draft of the disposition and safety-net facts that must exist before later signing/finalization can even be considered. The physician—not the application—selects the disposition and authors instructions. The slice deliberately stops before legal signature, patient delivery, AVS generation, completion, clinician release, prescription, claim, or any assertion that an external handoff occurred.

## Stories

| Story | Acceptance boundary |
|---|---|
| `TH-SP11-001` | V0289 adds append-only, versioned disposition draft snapshots/events linked to the current consultation and encounter, with bounded vocabularies, `legal_effect=false`, conditional safety facts, idempotency, and no destructive/downstream SQL |
| `TH-SP11-002` | Owner-only GET/PUT APIs rebind the full current wrap-up relationship, require facility/treatment/staff/physician/chart view-write authority, return opaque not-found cross-scope, enforce common and disposition-specific completeness, and use no-store PHI audit |
| `TH-SP11-003` | Recording is transactionally versioned and exact-replay safe; stale/changed/concurrent writes fail without partial versions/events or lifecycle/downstream deltas |
| `TH-SP11-004` | The physician WrapUp UI makes unsigned/undelivered status unmistakable, contains no clinical defaults, exposes conditional fields, preserves entered data and retry identity, restores error focus, and passes keyboard/axe/320 px evidence without browser storage |
| `TH-SP11-005` | Runtime safety, OpenAPI, authorization, migration/recovery, real PostgreSQL, complete regressions, planning/safeguard, Graphify, and evidence packet prove the exact boundary |

## Data contract

Each immutable version contains: disposition code; adequate-evaluation boolean; follow-up owner and physician-authored timeframe; next-step instructions; warning/escalation instructions; communication method and completion boolean; location/callback reconfirmation boolean; emergency-instruction boolean; nullable emergency handoff state; nullable contact/safety-attempt summary; server physician/time; and `legal_effect=false`.

No patient, request, appointment, encounter, actor, author, timestamp, signature, delivery, diagnosis, order, medication, prescription, billing, claim, transfer, or external identifier is client-supplied.

## Done gate

The sprint is done only when all automated evidence passes and the evidence packet records every still-open independent review. It remains a disabled synthetic development slice regardless of test results.
