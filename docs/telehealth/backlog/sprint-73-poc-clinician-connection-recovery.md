# Sprint 73 plan: POC clinician connection recovery

Status: Implemented and staging-verified under [TH-DEC-0076](../decisions/0076-approved-poc-clinician-connection-recovery.md)

## Goal

Allow an owning physician to safely recover from a failed pre-consultation synthetic waiting-room attempt without leaving the patient unavailable until lease expiry.

## Delivery boundary

- Present the recovery action only after a waiting room exists and before a consultation starts.
- Require explicit no-consultation and synthetic-effect confirmations, exact versioning, physician ownership, facility scope, and idempotency.
- Reuse the existing queue entry and preserve its `ready_at`; release the reservation, revoke local grants, end the synthetic session, clear provisional assignment, and append lifecycle evidence in one transaction.
- Do not add patient messaging, queue reordering, clinician reassignment, clinical judgement, encounter creation, real media, billing, claims, integrations, or production recovery policy.

## Acceptance matrix

| Area | Required evidence |
| --- | --- |
| Guardrails | The command is unavailable before a connection or after a consultation, and requires both affirmative confirmations. |
| Ownership | Only the active owning physician at the scoped facility can act on the exact reservation version. |
| Lifecycle | A `Connecting` request atomically returns to `Queued`; the original queue entry is `Ready`; the reservation is `Released`. |
| Media boundary | Pending grants are revoked and the synthetic session is ended; no connection or media is created. |
| Audit | One append-only `connection-abandoned` event records the non-clinical recovery transition. |
| Consequence | No patient contact, clinical decision, care, financial, integration, external, or production effect. |
| Regression | Backend/frontend/OpenAPI/runtime/planning/staging/Graphify evidence passes. |

## Gate preserved

Production recovery ownership, patient communication, operational alerting, distributed coordination, clinical escalation, downstream financial handling, and all release gates remain separately governed work.
