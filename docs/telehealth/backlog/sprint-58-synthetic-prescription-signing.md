# Sprint 58 plan: synthetic prescription safety gate and signing

Status: Implemented under [TH-DEC-0061](../decisions/0061-approved-sprint-58-synthetic-prescription-signing.md); independent review and every production gate remain open

## Goal

Turn the exact owner's current non-controlled prescription-preparation draft into one immutable signed synthetic prescription after a conservative, server-verified empty-list safety gate, while preserving a standards-oriented but uncertified and non-transmitting e-prescription seam.

## Delivery boundary

- Rebind the entire unfinished wrap-up ownership chain in a serializable transaction.
- Require the current draft and unchanged patient-confirmed pharmacy-choice versions.
- Require explicit no-current-medication, no-known-allergy, adequate-evaluation, and synthetic-only attestations.
- Verify that canonical active medication and allergy counts are both zero; otherwise fail closed.
- Create one canonical prescription, one immutable telehealth order/signature record, and one audit event atomically.
- Hash signed content, preserve exact idempotent replay, reject conflicts and duplicate consultation prescriptions, and lock the canonical row against mutation.
- Prepare an uncertified `NewRx` seam targeting NCPDP SCRIPT 2023011 with explicit 2017071 transition metadata through 2027-12-31.
- Keep transmission, pharmacy contact, patient delivery, lifecycle completion, clinician release, billing, claims, and every external consequence disabled.

## Acceptance matrix

| Area | Required evidence |
|---|---|
| Ownership | Exact current physician, practice, facility, patient, encounter, wrap-up, draft, and pharmacy lineage only. |
| Safety | Zero active canonical medications and allergies plus four explicit attestations; any other state fails closed. |
| Atomicity | One prescription, order evidence, and audit event or no durable change. |
| Immutability | Signed content hash and database-enforced append-only order/canonical prescription. |
| Idempotency | Exact retry returns the same IDs and signature time; conflicting reuse or second order is rejected. |
| Standards seam | Canonical model v1, `NewRx`, target SCRIPT 2023011, transition-only 2017071 label, `PreparedOnly`, uncertified. |
| No external effect | No gateway call, network destination, outbox, pharmacy acknowledgment, dispense, delivery, billing, claim, or lifecycle transition. |
| Accessibility | Explicit consequence language, labeled attestations, disabled-until-complete action, focused failure recovery, and persisted-result summary. |
| Regression | Backend, frontend, OpenAPI, authorization, runtime, migration/recovery, browser/accessibility, planning, Graphify, and cleanup. |

## Gate preserved

Sprint 59 must separately authorize an approved medication-knowledge source and alert lifecycle, certified NCPDP mapping/vendor connectivity, transmission/outbox ownership and recovery, pharmacy business acknowledgments, corrections/cancel/change flows, patient delivery/AVS, final documentation signature, visit completion and clinician release, billing, or claims. Production and real patient care remain unavailable.
