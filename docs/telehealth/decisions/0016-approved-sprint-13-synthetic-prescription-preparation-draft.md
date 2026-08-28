# Decision 0016: Sprint 13 synthetic prescription-preparation draft authorization

Status: Approved — active for the exact disabled synthetic slice below  
Approved date: 2026-08-27  
Decision owner: AvenChart program owner  
Implementation owner: Codex delivery agent under AvenChart program-owner direction  
Review/expiry: 2026-10-31, or immediately when superseded by a later gate decision

## 1. Authorized outcome

Permit only the physician who owns a current synthetic consultation in unfinished wrap-up to search the existing deterministic medication vocabulary and append one versioned prescription-preparation draft. The slice is a physician-authored planning aid only. It may snapshot one explicitly selected active non-controlled catalog entry, manual structured directions, the current patient-confirmed pharmacy-choice version, and explicit medication-list, allergy-list, and adequate-evaluation acknowledgments.

The draft is not a prescription, medication-list entry, order, recommendation, safety check, signature, transmission request, pharmacy claim, or evidence of patient counseling. It has no legal effect and cannot be sent, signed, finalized, delivered, or used to complete the consultation.

## 2. Required controls

1. The feature remains disabled by default, rejected in Production, synthetic-only, physician-only, treatment-purpose/facility scoped, private/no-store, and audited against only the opaque consultation resource.
2. The server rebinds the consultation, request, released reservation, wrap-up shift, ended synthetic room, in-progress appointment, open unsigned encounter, active adult patient, physician, practice, and facility in one transaction before every read or write.
3. Search is neutral and deterministic, returns at most 20 active catalog entries in stable display-name/code order, exposes catalog facts rather than clinical recommendations, and returns no patient, pharmacy, payer, encounter, appointment, request, or actor identifier.
4. Unknown catalog codes and every nonblank controlled-substance schedule fail closed in both service and database constraints. Free-text drug creation and controlled-substance override are unavailable.
5. Drug, strength, form, route, and classification snapshots come from the selected catalog row. Dose amount/unit, frequency, quantity/unit, duration, refills, indication, and directions require explicit physician entry; the product supplies no dosing or diagnosis default.
6. Recording requires an affirmative current medication-list review, allergy-list review, adequate-evaluation acknowledgment, current patient-confirmed pharmacy choice, expected draft version, stable idempotency key, and explicit synthetic-data confirmation.
7. The append-only version and event preserve authenticated physician identity, server/database time, catalog dataset/source/version, intended canonical model version, intended `NCPDP SCRIPT 2017071` seam, and referenced pharmacy-choice version. Prior versions remain immutable.
8. `legalEffect`, `signed`, `safetyChecked`, `transmissionQueued`, `transmitted`, `patientDelivered`, and `completionEnabled` remain false. The UI and API explicitly state that interaction/contraindication knowledge checking and prescriber signing are not implemented.
9. The operation creates no canonical `prescriptions` or `medications` row, prescription audit event, encounter signature, AVS, billing/claim record, message, task, notification, integration outbox/inbox, lifecycle transition, clinician release, external call, or browser-storage record.
10. Exact retry converges on one version/event, changed-key reuse and stale writers fail, concurrent first writes produce one winner, a canonical locking signature removes eligibility, and all earlier safeguards remain intact.
11. The UI has no drug/dose default, distinguishes catalog metadata from entered directions, retains an ambiguous failed command for explicit retry, focuses errors, supports keyboard/screen readers and 320 px reflow, and persists no clinical content.
12. Unit, API, authorization, live PostgreSQL constraints/contention/no-delta, accessibility/recovery, migration/bootstrap, planning, Graphify, and full regression evidence is required without weakening Sprints 1–12.

## 3. Explicit exclusions

This decision does not authorize medication advice, a drug recommendation, interaction or contraindication adjudication, allergy inference, diagnosis, a canonical medication or prescription, EPCS, a controlled substance, prescription signature, NewRx mapping, transmission, vendor/network call, pharmacy acknowledgment, dispense status, drug claim, AVS, patient delivery, encounter/request/appointment completion, clinician release, billing, professional claim, production enablement, real people/PHI, or patient care.

## 4. Stop conditions and rollback

Stop if a non-owner can read or write the draft; a controlled or unknown catalog item passes; catalog facts appear as a dosing recommendation; a draft is represented as signed, checked, transmitted, or legally effective; any canonical prescription/medication/signature/downstream/lifecycle row is created; content enters browser storage or ordinary logs; or an earlier safeguard regresses. Rollback disables/removes the routes and panel; the additive append-only tables remain as inert evidence and require a separately reviewed forward migration for schema correction.

## 5. Approval record

The program owner directed complete implementation of the approved telehealth goal, approved all decisions, and authorized uninterrupted work. This record applies that standing authority only to the bounded disabled synthetic prescription-preparation draft above. It does not substitute for independent clinical, pharmacy/e-prescribing, legal, privacy/security, accessibility, data, operational, interoperability, or production review.

## References

- [Prescribing and pharmacy](../11-prescribing-and-pharmacy.md)
- [Consultation, documentation, and follow-up](../09-consultation-documentation-and-follow-up.md)
- [Decision 0015](0015-approved-sprint-12-completion-prerequisites-review.md)
- [Sprint 13 plan](../backlog/sprint-13-synthetic-prescription-preparation-draft.md)
