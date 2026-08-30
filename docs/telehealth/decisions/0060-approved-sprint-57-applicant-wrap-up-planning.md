# Decision 0060: Sprint 57 applicant wrap-up and bounded planning

Status: Approved — active for the exact disabled synthetic slice below

Approved date: 2026-08-29

Decision owner: AvenChart program owner

Implementation owner: Codex delivery agent under AvenChart program-owner direction

Review/expiry: 2026-10-31, or immediately when superseded by a later gate decision

## 1. Authorized outcome

Permit the exact physician who owns an applicant-originated synthetic consultation to end the synthetic session lifecycle, enter unfinished `WrapUp`, continue the explicit unsigned SOAP draft, record one or more patient-confirmed neutral synthetic pharmacy-destination versions, record non-controlled catalog-bound prescription-preparation draft versions, record physician-authored safety-disposition draft versions, and read the existing structural completion-prerequisites review.

These are physician-owned planning records for synthetic data only. They are not a signed clinical record, after-visit summary, medication order, prescription, transmission, patient instruction delivery, claim, completed visit, clinician release, external action, or production care.

## 2. Applicant provenance and continuity rule

The applicant path must arrive through the exact immutable applicant, promoted patient shell, request, queue authorization, rendering candidate, reservation, session, appointment, encounter, and consultation-start chain proven by Decision 0059. Every downstream operation must then rebind the configured practice/facility, exact consultation-owning physician, released reservation, ended capture-disabled session, in-encounter appointment, active unmerged living adult patient, unsigned encounter, `MediaEnded` consultation, `WrapUp` request, and `WrapUp` shift.

An expired applicant access session or expired pre-start queue-authorization validity window must not prevent the owning physician from recording safety and unfinished-work evidence after the consultation has started. Applicant polling remains access-key protected and expires normally. This continuity rule does not revive, replace, or claim real identity, consent, coverage, network, or financial clearance.

## 3. Required controls

1. The feature remains disabled by default, rejected in Production, synthetic-only, and scoped to the configured branded practice, facility, GA/CA/FL, and adult patient shell.
2. Only the eligible exact consultation-owning physician may enter wrap-up or access any downstream planning workspace or mutation; other clinicians receive opaque not-found behavior.
3. Wrap-up requires exact consultation version, idempotency key, and affirmative acknowledgment that the synthetic session ended, documentation remains unfinished, and physician responsibility continues.
4. The atomic transition changes only consultation `Started` to `MediaEnded`, request `InConsultation` to `WrapUp`, and shift `Busy` to `WrapUp`; appointment remains in encounter and the physician remains unavailable for new work.
5. Documentation remains an append-only unsigned SOAP draft in the existing canonical encounter; a locking signature fails subsequent planning access closed.
6. Pharmacy search uses only the versioned neutral `NON_PRODUCTION` directory. Approximate distance requires an explicitly acknowledged entered postal origin. A destination is patient-confirmed planning evidence, not an endorsement, network/availability guarantee, prescription, or transmission.
7. Prescription preparation is restricted to the bounded non-controlled synthetic catalog, requires medication/allergy review and adequate-evaluation acknowledgments, binds the current pharmacy-choice version, and remains unchecked, unsigned, non-legal, unqueued, untransmitted, and undelivered.
8. Safety disposition is physician-authored, conditionally validates emergency/interrupted workflows, and remains unsigned, unfinalized, undelivered, and non-legal. The application supplies no clinical recommendation.
9. Completion review reports structural field presence only and always retains final-review, signature/finalization, and atomic downstream-ownership blockers. It is side-effect free and cannot complete or release the visit.
10. Applicant polling may advance from `Consultation` to minimized `WrapUp`, then stops automatic polling. It exposes no physician identity, chart content, encounter/appointment identifiers, insurance identifiers, pharmacy facts, prescription facts, or care/coverage/consent claim.
11. No canonical medication, prescription, signature, AVS, patient message, billing, claim, outbox/inbox, integration, external call, lifecycle completion, cancellation, or clinician release is created.
12. Unit, authorization, OpenAPI, runtime, migration, GA/CA/FL live applicant flow, established-patient regression, browser/accessibility, planning, and Graphify evidence are required.

## 4. Normalized contract

| Field | Rule |
|---|---|
| Policy | Existing governed wrap-up and planning policies reused only after the Decision 0059 applicant consultation chain. |
| Entry state | Applicant request `InConsultation` v16+, exact owned consultation v1, busy owner shift, ended synthetic room, in-encounter appointment, unsigned encounter. |
| Wrap-up result | Request `WrapUp` v17+, consultation `MediaEnded` v2, shift `WrapUp`; appointment/encounter remain unfinished and physician remains unavailable. |
| Documentation | Existing append-only canonical unsigned SOAP draft only. |
| Pharmacy | Versioned neutral synthetic directory and patient-confirmed destination draft; no prescription or transmission. |
| Prescription preparation | Versioned non-controlled catalog-bound preparation draft; no safety check, signature, canonical prescription, transmission, or delivery. |
| Safety disposition | Versioned physician-authored conditional draft; no signature, finalization, delivery, or external handoff. |
| Completion review | Read-only structural presence plus permanent product blockers; no lifecycle or downstream mutation. |
| Applicant status | Minimized `WrapUp` projection only; no protected clinical, professional, financial, or credential identifiers. |
| Outstanding gates | Real media/communication, legal consent, real coverage/financial clearance, diagnosis/treatment authorization, signing, prescribing, patient delivery, completion/release, claims, integrations, independent review, and production. |

## 5. Explicit exclusions

This decision does not authorize WebRTC, WebSocket, SignalR, SIP, telephony, chat, recording, transcription, vendor media, real identity proofing, legally effective consent, canonical coverage, real network or financial clearance, application-authored advice, diagnosis, treatment, orders, signing, prescribing, pharmacy transmission, AVS/patient delivery, billing, claims, FHIR/X12/NCPDP messages, payer or pharmacy calls, real people or PHI, visit completion, clinician release, or production enablement.

## 6. Stop conditions and rollback

Stop if a non-owner physician can read or write; if wrap-up partially mutates lifecycle state; if applicant status exposes protected or consequential facts; if controlled catalog content is accepted; if a planning draft gains legal, signed, finalized, transmitted, delivered, completed, or external effect; if permanent completion blockers disappear; if the physician is released for new work; or if canonical medication, prescription, signature, billing, claim, communication, integration, completion, or production state changes. Rollback removes the applicant `WrapUp` projection and applicant-path proof while leaving established-patient planning behavior and prior applicant lifecycle evidence intact.

## 7. Approval record

The program owner approved all current decisions and authorized uninterrupted implementation. This record applies that standing authority only to the disabled synthetic applicant wrap-up and bounded planning boundary above.

## References

- [Consultation and clinical documentation](../09-consultation-documentation-and-follow-up.md)
- [Prescribing and pharmacy](../11-prescribing-and-pharmacy.md)
- [Security, privacy, consent, and audit](../16-security-privacy-consent-and-audit.md)
- [Decision 0059](0059-approved-sprint-56-applicant-consultation-start.md)
- [Sprint 57 plan](../backlog/sprint-57-applicant-wrap-up-planning.md)
