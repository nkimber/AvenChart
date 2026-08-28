# Decision 0015: Sprint 12 synthetic completion-prerequisites review authorization

Status: Approved — active for the exact disabled synthetic slice below  
Approved date: 2026-08-27  
Decision owner: AvenChart program owner  
Implementation owner: Codex delivery agent under AvenChart program-owner direction  
Review/expiry: 2026-10-31, or immediately when superseded by a later gate decision

## 1. Authorized outcome

Permit only the physician who owns a current synthetic consultation in unfinished wrap-up to read a minimized, server-derived review of evidence relevant to eventual finalization. The projection may report lifecycle facts, current SOAP draft version and section-presence flags, current safety-disposition version/code and structured confirmation state, optional pharmacy-destination version, and explicit product blockers.

The projection is a navigation and structural-review aid only. It does not determine clinical completeness, approve a disposition, sign or finalize any content, satisfy a co-signature or compliance policy, create after-visit or financial work, change lifecycle state, or release the physician.

## 2. Required controls

1. The route remains disabled by default, rejected in Production, synthetic-only, physician-only, treatment-purpose/facility scoped, private/no-store, and audited against only the opaque consultation resource.
2. The server rebinds the consultation, request, released reservation, wrap-up shift, ended synthetic room, in-progress appointment, open unsigned encounter, active adult patient, physician, practice, and facility in one repeatable read.
3. The response exposes no patient/request/appointment/encounter/note/disposition-version row/actor identifiers and no SOAP, instruction, warning, contact-attempt, insurance, pharmacy-address, or other clinical free text.
4. The product may report whether each SOAP section contains nonblank content but may not assert that any section is clinically adequate, applicable, accurate, reviewed, or complete. Empty and missing remain distinct only where useful for physician navigation.
5. The product may report only the current physician-selected disposition code and structured confirmation flags already enforced by Decision 0014. It may not generate a disposition, clinical advice, diagnosis, order, referral, prescription, follow-up, warning, or handoff claim.
6. Optional pharmacy choice never becomes a completion prerequisite and must remain explicitly unrelated to whether a prescription is clinically indicated.
7. `signingEnabled`, `completionEnabled`, `patientDeliveryEnabled`, and `downstreamCreationEnabled` are always false in this slice. The projection always includes product blockers for unavailable final clinical review/signature and atomic after-visit/financial ownership.
8. GET is side-effect free. Repeated and concurrent reads create no database, audit-excluded, browser-storage, lifecycle, signing, clinical, financial, communication, integration, notification, task, or external-call delta.
9. The UI clearly labels the projection as not a clinical-completeness decision, supports manual reload, focuses errors, works with keyboard/screen readers and 320 px reflow, and persists no response data.
10. Unit, API, authorization, live PostgreSQL owner/non-owner/audit/cache/privacy/no-delta, accessibility/recovery, planning, Graphify, and full regression evidence is required without weakening Sprints 1–11.

## 3. Explicit exclusions

This decision does not authorize a final clinical checklist; an affirmative “ready to sign” decision; clinical inference; generated chart content; signature; co-signature; final disposition; encounter/request/appointment transition; clinician release; AVS; patient delivery; order/referral; medication or prescription; billing/claim; message/task/notification/outbox; real integration; production enablement; real people/PHI; or patient care.

## 4. Stop conditions and rollback

Stop if a non-owner can read the projection; authored free text or canonical identifiers are exposed; an optional pharmacy is treated as required; a presence flag is represented as clinical sufficiency; any read changes durable/lifecycle/downstream state; data enters browser storage or ordinary logs; or an earlier safeguard regresses. Rollback disables/removes the route and panel; no schema rollback is required.

## 5. Approval record

The program owner directed complete implementation of the approved telehealth goal, approved all decisions, and authorized uninterrupted work. This record applies that standing authority only to the bounded disabled synthetic read projection above. It does not substitute for independent clinical, legal, privacy/security, accessibility, data, operational, or production review.

## References

- [Consultation, documentation, and follow-up](../09-consultation-documentation-and-follow-up.md)
- [Workflow state machines](../03-workflows-and-state-machines.md)
- [Decision 0014](0014-approved-sprint-11-synthetic-safety-disposition-draft.md)
- [Sprint 12 plan](../backlog/sprint-12-completion-prerequisites-review.md)
