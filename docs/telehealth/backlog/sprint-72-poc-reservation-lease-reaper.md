# Sprint 72 plan: POC reservation lease reaper

Status: Implemented and staging-verified under [TH-DEC-0075](../decisions/0075-approved-poc-reservation-lease-reaper.md)

## Goal

Ensure a synthetic clinician reservation that expires without further clinician activity is restored to the existing queue promptly and audibly.

## Delivery boundary

- Register one disabled-by-default background worker with the synthetic telehealth feature.
- Reuse the existing expiration transaction rather than introducing a second state transition or queue ordering rule.
- Reconcile only the configured practice/facility every 15 seconds while enabled.
- Do not add patient messaging, staff assignment, reordering, clinical judgement, media recovery, integration, or production scheduling.

## Acceptance matrix

| Area | Required evidence |
| --- | --- |
| Activation | The worker exits immediately when telehealth is disabled. |
| Lifecycle | Existing active leases expire atomically even when no clinician attempts a new reservation. |
| Queue fairness | The original `ready_at` and queue entry are retained. |
| Audit | Existing append-only `reservation-expired` events remain the sole lifecycle evidence. |
| Consequence | No patient contact, care, media, financial, integration, external, or production effect. |
| Regression | Backend/runtime/planning/staging/Graphify evidence passes. |

## Gate preserved

Production scheduler ownership, distributed coordination, outage recovery, patient communication, telemetry thresholds, on-call response, clinical ownership, and all release gates remain separately governed work.
