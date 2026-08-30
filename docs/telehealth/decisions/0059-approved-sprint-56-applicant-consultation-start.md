# Decision 0059: Sprint 56 applicant clinician connection and consultation start

Status: Approved — active for the exact disabled synthetic slice below

Approved date: 2026-08-29

Decision owner: AvenChart program owner

Implementation owner: Codex delivery agent under AvenChart program-owner direction

Review/expiry: 2026-10-31, or immediately when superseded by a later gate decision

## 1. Authorized outcome

Permit the exact reservation-owning physician to enter the existing applicant waiting room and, after both short-lived participant grants are current and every start-checklist item is affirmative, begin the existing bounded synthetic consultation lifecycle. The transaction may move `Connecting` to `InConsultation`, mark the appointment `In encounter`, remove the queue entry, release the reservation, make the physician shift busy, end and revoke the synthetic waiting-room artifacts, create one canonical synthetic encounter, and expose the established audited bounded workspace and explicit unsigned SOAP draft.

This is lifecycle and chart-workspace evidence for synthetic data only. It is not media or communication, legal identity proofing or consent, real coverage verification, a payment guarantee, diagnosis, treatment, prescribing, billing, claims, completion, or external integration.

## 2. Applicant financial-evidence rule

The established-patient coverage gate remains unchanged. For an applicant-originated request only, consultation start may instead accept the still-current exact Sprint 52 queue authorization when it binds the same applicant, patient shell, request, practice, facility, and reservation-owning Sprint 48 candidate. Its synthetic-only, no-coverage-guarantee, queue-is-not-care, no-real-network, no-consent, no-care, no-prescribing, no-billing, no-claim, no-integration, and no-external-action facts must all remain intact.

The alternative gate must never populate canonical coverage or report `coverageVerified`. Expiry, candidate mismatch, applicant drift, appointment-owner drift, inactive/deceased/merged/underage patient state, stale participant grants, or changed reservation/session state fails closed.

## 3. Required controls

1. The feature remains disabled by default, rejected in Production, synthetic-only, and scoped to the configured branded practice, facility, GA/CA/FL, and adult patient shell.
2. Only the eligible active physician who owns the exact active reservation and shift may receive the physician-role grant or start the lifecycle.
3. Both patient- and physician-role grants must be distinct, short lived, current, hashed at rest, bound to the same capture-disabled `NON_PRODUCTION` waiting room, and revoked atomically at start.
4. Start requires exact request version, idempotency key, matching fresh patient location, and affirmative identity discussion, callback, privacy, telehealth-consent discussion, symptom-change, emergency-plan, communication-sufficiency, and synthetic-data confirmations.
5. The consultation transaction rechecks appointment patient/facility/provider ownership and active, unmerged, living adult patient state under database locks.
6. Applicant financial evidence requires the full current negative-consequence flag set described above and the exact queue-authorization candidate must equal the reservation-owning physician.
7. Concurrent commands produce one consultation and one encounter. Exact replay is stable; changed, stale, incomplete, expired, foreign, or drifted commands fail without partial mutation.
8. The bounded workspace exposes only patient/callback facts, current visit facts, active allergies/medications/problems, and an explicit unsigned SOAP draft. General chart navigation stays unavailable.
9. Empty clinical lists are labeled unconfirmed, every item must be verbally reconciled, and applicant-collected information is not treated as clinically verified merely because it was promoted.
10. Applicant status may advance to `Consultation` but must not expose physician identity, chart data, encounter IDs, participant credentials, insurance identifiers, or a real coverage/consent claim.
11. No media, signaling, communication channel, diagnosis, order, signature, prescription, pharmacy transmission, billing, claim, patient message, integration, external call, completion, or clinician release is created.
12. Unit, authorization, OpenAPI, runtime, migration, GA/CA/FL live concurrency/replay, bounded-workspace, established-patient regression, browser/accessibility, planning, and Graphify evidence are required.

## 4. Normalized contract

| Field | Rule |
|---|---|
| Policy | `SYNTHETIC_APPLICANT_CONSULTATION_START` version 1 in `NON_PRODUCTION` mode. |
| Physician connection | Existing POST `/api/telehealth/v1/clinician/reservations/{reservationId}/connection-grants`; exact reservation owner only. |
| Consultation command | Existing POST `/api/telehealth/v1/clinician/reservations/{reservationId}/consultations/start`; exact version and idempotency key required. |
| Entry state | Applicant request `Connecting`; current exact candidate reservation/shift; same arrived appointment; capture-disabled waiting room; both grants current. |
| Financial gate | Current exact applicant queue authorization and candidate binding with every real-coverage/care/downstream flag false; never canonical coverage. |
| Atomic result | One `InConsultation` request, in-encounter appointment, removed queue entry, released reservation, busy shift, ended session/revoked grants, consultation context, encounter, and events. |
| Workspace | Existing physician-owned bounded projection plus explicit unsigned SOAP draft only. |
| Outstanding gates | Real media/communication, legal consent, real coverage and financial clearance, diagnosis/treatment, prescribing, claims, integrations, wrap-up/completion for the applicant path, independent review, and production. |

## 5. Explicit exclusions

This decision does not authorize WebRTC, WebSocket, SignalR, SIP, telephony, chat, recording, transcription, vendor media, real identity proofing, legally effective consent, canonical coverage, real network or financial clearance, advice, diagnosis, treatment, orders, signing, prescribing, pharmacy transmission, billing, claims, FHIR/X12/NCPDP messages, payer or pharmacy calls, real people or PHI, visit completion, or production enablement.

## 6. Stop conditions and rollback

Stop if a non-owner physician can enter or start; if an expired or candidate-mismatched applicant authorization is accepted; if missing negative-consequence flags are ignored; if appointment or patient state is not rebound; if a partial queue/reservation/session/encounter mutation occurs; if duplicate encounters are created; if the applicant receives chart, clinician, credential, or insurance identifiers; or if media, communication, real consent/coverage, prescribing, billing, claim, integration, external, completion, or production consequence occurs. Rollback restores the applicant-specific financial-evidence branch while leaving the established-patient path and governed synthetic evidence intact.

## 7. Approval record

The program owner approved all current decisions and authorized uninterrupted implementation. This record applies that standing authority only to the disabled synthetic applicant clinician-connection and consultation-start boundary above.

## References

- [Consultation and clinical documentation](../09-consultation-documentation-and-follow-up.md)
- [Video, waiting room, and realtime communication](../10-video-realtime-and-communications.md)
- [Security, privacy, consent, and audit](../16-security-privacy-consent-and-audit.md)
- [Decision 0058](0058-approved-sprint-55-applicant-request-connection-room.md)
- [Sprint 56 plan](../backlog/sprint-56-applicant-consultation-start.md)
