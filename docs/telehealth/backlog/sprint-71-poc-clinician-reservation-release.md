# Sprint 71 plan: POC clinician reservation release

Status: Implemented and staging-verified under [TH-DEC-0074](../decisions/0074-approved-poc-clinician-reservation-release.md)

## Goal

Allow the exact physician who reserved an unconnected synthetic request to safely return it to the same clinician queue, avoiding a stalled lease while preserving queue fairness and an auditable lifecycle.

## Delivery boundary

- Restrict release to the owning physician during an active shift, before any video-session or consultation context exists.
- Require a current request version, an idempotency key, and two explicit UI/server confirmations.
- Atomically release the reservation, restore the original queue entry to `Ready`, restore the request to `Queued`, and remove the provisional synthetic provider assignment while retaining the original queue `ready_at`.
- Record the transition through the append-only request event log and make only exact replays semantic-idempotent.
- Do not add a clinical decline reason, patient-facing disposition, notification, care action, media, integration, billing, claims, or production behavior.

## Acceptance matrix

| Area | Required evidence |
| --- | --- |
| Authorization | Exact physician, active shift, practice/facility, reservation ownership, and current version are required. |
| Lifecycle | Only `Reserved -> Queued` before connection/consultation is permitted; stale, expired, connected, or consulted work fails closed. |
| Queue fairness | The existing queue entry is returned with unchanged `ready_at`; no new queue entry is created. |
| Audit | An append-only `reservation-released` request event records actor, version, idempotency key, and command fingerprint. |
| UX | The action is hidden after connection/consultation and disabled until both explicit confirmations are supplied. |
| Consequence | No clinical declination, patient contact, care, financial, media, integration, external, or production effect. |
| Regression | Backend/UI/API/runtime/staging/Graphify evidence passes. |

## Gate preserved

Production staffing rules, physician declination policy, clinical appropriateness and reassignment, patient communication, monitoring, escalation, availability, financial effect, documentation, media recovery, and all production-release gates remain separately governed work.
