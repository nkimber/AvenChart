# Sprint 56 plan: applicant clinician connection and consultation start

Status: Implemented and automated verification complete under [TH-DEC-0059](../decisions/0059-approved-sprint-56-applicant-consultation-start.md); independent review and every production gate remain open

## Goal

Let the exact reservation-owning physician enter the applicant's existing private synthetic waiting room, execute the guarded consultation-start handoff, and use the existing bounded chart workspace and explicit unsigned SOAP draft without claiming real media, consent, coverage, or care.

## Delivery boundary

- Reuse the established physician connection endpoint after exact applicant candidate reservation; retain distinct role-scoped hashed short-lived grants and capture-disabled session behavior.
- Add an applicant-specific financial-evidence alternative at consultation start without changing the established-patient coverage gate.
- Require the current exact queue authorization to match applicant, patient, request, practice, facility, and reservation-owning candidate while every real-coverage/care/downstream flag remains false.
- Rebind appointment patient/facility/provider and active, living, unmerged adult patient state in the locked start query.
- Reuse the existing affirmative start checklist, exact location/version/idempotency gates, atomic encounter/lifecycle handoff, bounded workspace, and unsigned SOAP draft.
- Present explicit new-patient limitations stating that the evidence is synthetic, not real coverage verification or a payment guarantee.
- Prove single-winner concurrency, replay, lifecycle atomicity, workspace minimization, applicant status minimization, no downstream consequence, established-patient regression, and cross-layer safety.

## State and evidence contract

```text
SyntheticRequestCreated v26 (unchanged)
  + request Connecting v17+
  + exact current Sprint 52 queue authorization
  + authorization candidate = reservation-owning physician
  + every real coverage/care/downstream flag false
  + active reservation and shift
  + arrived appointment owned by same patient/facility/physician
  + active living unmerged adult patient shell
  + capture-disabled WaitingRoom
  + current patient and physician grants
  + fresh matching GA/CA/FL location
  + every start checklist item affirmative
  -> request InConsultation v+1
  -> appointment In encounter
  -> queue Removed; reservation Released; shift Busy
  -> grants Revoked; session Ended
  -> one synthetic encounter and consultation context
  -> bounded chart projection and explicit unsigned SOAP draft
```

No real coverage result, media, communication, legally effective consent, diagnosis, treatment, order, signature, prescription, claim, integration, message, external action, completion, or clinician release is created.

## Acceptance matrix

| Area | Required evidence |
|---|---|
| Clinician ownership | Only the exact reservation-owning eligible physician receives the role-scoped grant and starts the lifecycle. |
| Applicant provenance | Exact applicant/patient/request/authorization/candidate/reservation/shift/appointment/practice/facility chain is current at database time. |
| Financial honesty | Synthetic eligibility/candidate evidence is accepted only with no-coverage-guarantee and all real-coverage, consent, care, prescribing, billing, claim, integration, and external flags false. |
| Start safety | Both grants, fresh matching location, active-adult patient state, exact appointment owner, version, idempotency, and every checklist item are required. |
| Atomicity/concurrency | Twenty competing starts create one consultation/encounter and one lifecycle transition; replay is stable and losers leave no duplicate evidence. |
| Workspace | Owner-only minimized visit/chart projection and explicit unsigned SOAP draft; empty lists are unconfirmed and general chart access is absent. |
| Applicant status | Consultation phase only; no clinician identity, chart facts, encounter/credential/insurance identifiers, coverage verification, or legal-consent claim. |
| No downstream action | Prescriptions, billing, claims, outbox/inbox, messages, integrations, and external actions remain unchanged. |
| Regression | Backend, frontend, browser/accessibility, authorization, OpenAPI, runtime, migrations/recovery, established lifecycle, planning, Graphify, and cleanup. |

## Gate preserved

Sprint 57 must separately authorize any applicant-path wrap-up, pharmacy choice, prescription-preparation draft, safety-disposition draft, or completion-prerequisite review. Real media/communication, legal consent, real coverage and financial clearance, diagnosis/treatment, signing, prescribing, claims, integrations, patient delivery, completion, cancellation, independent review, and production remain open.
