# Sprint 57 plan: applicant wrap-up and bounded planning

Status: Implemented under [TH-DEC-0060](../decisions/0060-approved-sprint-57-applicant-wrap-up-planning.md); independent review and every production gate remain open

## Goal

Let the exact physician who owns an applicant-originated synthetic consultation enter unfinished wrap-up and use the existing documentation, neutral pharmacy, prescription-preparation, safety-disposition, and completion-prerequisite planning tools without signing, prescribing, delivering, completing, releasing, billing, claiming, integrating, or calling anything external.

## Delivery boundary

- Reuse the established exact-owner wrap-up endpoint with version, idempotency, acknowledgments, atomic request/consultation/shift transition, and unfinished appointment/encounter state.
- Preserve physician safety-documentation continuity after consultation start even when the applicant access session or pre-start authorization validity window later expires.
- Reuse the bounded chart workspace and append-only unsigned SOAP draft during wrap-up.
- Reuse the neutral versioned synthetic pharmacy directory and patient-confirmed destination draft.
- Reuse the non-controlled catalog-bound prescription-preparation draft with all legal, safety, signature, transmission, delivery, and completion flags false.
- Reuse the physician-authored conditional safety-disposition draft with signing, finalization, delivery, and external handoff disabled.
- Reuse the side-effect-free completion-prerequisites review with permanent final-review, signature/finalization, and downstream-ownership blockers.
- Add fail-closed applicant `WrapUp` provenance projection and continue patient polling only from `InConsultation` until that terminal unfinished state.
- Prove GA/CA/FL applicant flow, owner isolation, replay, append-only evidence, minimized status, no downstream consequence, and established behavior regression.

## State and evidence contract

```text
SyntheticRequestCreated v26 (unchanged)
  + request InConsultation v16+
  + exact Decision 0059 applicant consultation lineage
  + owner physician + busy shift
  + released reservation + ended capture-disabled room
  + in-encounter appointment + active adult patient + unsigned encounter
  + exact version/idempotency + all wrap-up acknowledgments
  -> request WrapUp v17+
  -> consultation MediaEnded v2
  -> shift WrapUp; physician remains unavailable
  -> appointment and encounter remain unfinished
  -> unsigned SOAP/pharmacy/preparation/disposition planning evidence
  -> structural completion-prerequisite review with permanent blockers
  -> minimized applicant WrapUp status
```

No signed/final record, canonical medication or prescription, transmission, patient delivery, message, billing, claim, integration, external action, visit completion, cancellation, clinician release, or production behavior is created.

## Acceptance matrix

| Area | Required evidence |
|---|---|
| Ownership and provenance | Exact consultation owner only; immutable applicant/patient/request/authorization/candidate/reservation/session/appointment/encounter/start lineage and current wrap-up state remain consistent. |
| Atomic wrap-up | Exact replay is stable; request, consultation, and shift transition once while appointment/encounter stay unfinished and physician stays unavailable. |
| Documentation | Append-only canonical unsigned SOAP draft; no signature/finalization and no general chart expansion. |
| Pharmacy | Neutral synthetic directory facts and patient-confirmed versioned destination; no endorsement, network guarantee, prescription, or transmission. |
| Preparation draft | Non-controlled catalog only; required review/evaluation acknowledgments; pharmacy-version bound; every consequential flag false. |
| Safety disposition | Conditional clinical-safety fields validated; physician-authored, unsigned, unfinalized, undelivered, non-legal, and no external handoff claim. |
| Completion review | Structural presence only; stable permanent blockers; repeat reads create no mutation. |
| Applicant status | Polls through consultation to minimized terminal `WrapUp`; no physician/chart/credential/insurance/pharmacy/prescription identifiers or care claim. |
| No downstream action | Canonical medication/prescription, signature, AVS, messages, billing, claims, inbox/outbox, integrations, completion, and release remain unchanged. |
| Regression | Backend, frontend, browser/accessibility, authorization, OpenAPI, runtime, migrations/recovery, established lifecycle, planning, Graphify, and cleanup. |

## Gate preserved

Sprint 58 must separately authorize any applicant-path signing/finalization, canonical prescription creation, safety checking, patient delivery/AVS, visit completion, clinician release, billing, claim preparation, integration, or cancellation. Real media/communication, legal consent, real coverage and financial clearance, diagnosis/treatment authorization, external action, independent review, and production remain open.
