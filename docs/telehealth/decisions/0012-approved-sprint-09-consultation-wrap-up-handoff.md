# Decision 0012: Sprint 9 consultation wrap-up handoff authorization

Status: Approved — active for the exact disabled synthetic slice below  
Approved date: 2026-08-27  
Decision owner: AvenChart program owner  
Implementation owner: Codex delivery agent under AvenChart program-owner direction  
Review/expiry: 2026-10-31, or immediately when superseded by a later gate decision

## 1. Authorized outcome

Permit only the physician who owns a current synthetic telehealth consultation to explicitly declare the synthetic session ended and move the unfinished visit into physician-owned wrap-up:

```text
opaque consultation ID + authenticated owning physician + selected treatment facility
  -> re-verify current consultation/request/shift/session/appointment/encounter/adult-patient binding
  -> require expected consultation version, idempotency key, and affirmative unfinished-work acknowledgments
  -> atomically move consultation Started -> MediaEnded, request InConsultation -> WrapUp,
     and clinician shift Busy -> WrapUp
  -> append consultation and request lifecycle events
  -> keep the appointment/encounter open and the unsigned draft editable by the same physician
```

This is a synthetic lifecycle-development handoff. It is not a final disposition, clinical completion, encounter closure, signed record, availability release, patient delivery, billing event, or evidence that any media or care occurred.

## 2. Authorized implementation surfaces

Changes may add one additive migration after V0286 and use the existing telehealth consultation, request, shift, event, workspace, and documentation paths plus:

```text
docs/telehealth/decisions/0012-approved-sprint-09-consultation-wrap-up-handoff.md
docs/telehealth/backlog/sprint-09-consultation-wrap-up-handoff.md
docs/telehealth/backlog/sprint-09-evidence.md
```

The smallest backend, frontend, OpenAPI, PHI-audit, authorization, runtime-evidence, migration/bootstrap, planning-validation, CI, runbook, and test edits needed to connect and prove this disabled synthetic slice are authorized.

## 3. Required controls

1. The feature remains disabled by default, synthetic-only, and rejected in Production.
2. The command route uses only an opaque consultation ID and requires physician role, treatment purpose, selected facility, staff identity, `patients:demo view`, `encounters:auth view`, `encounters:auth write`, and ownership of the current consultation encounter.
3. The server rebinds and locks consultation, request, released reservation, clinician shift, ended synthetic session, appointment, encounter, physician, practice, facility, and active adult patient in one transaction. A non-owner, administrator, cross-scope identity, missing/stale consultation, or ineligible patient receives the established opaque boundary.
4. The request requires `ExpectedVersion >= 1`, a semantic idempotency key, `SyntheticSessionEndedConfirmed = true`, `DocumentationStillIncompleteAcknowledged = true`, and `WrapUpResponsibilityAcknowledged = true`. The client cannot supply state, time, actor, patient, request, shift, appointment, encounter, or event identifiers.
5. One transaction changes only the consultation lifecycle from `Started` to `MediaEnded`, request from `InConsultation` to `WrapUp`, and shift from `Busy` to `WrapUp`, with monotonic versions and server time. The appointment remains in progress, the encounter remains open, and the physician remains unavailable for new work.
6. Exact replay returns the original result; reuse of a key with different content, a stale expected version, or a second competing transition fails without a partial state/event change.
7. Immutable consultation-start evidence remains immutable. The database may permit only the one named status/version/server-time transition; consultation/request events remain append-only and uniquely versioned.
8. The workspace remains accessible only to the owning physician in `Started/InConsultation/Busy` or `MediaEnded/WrapUp/WrapUp` and exposes the opaque consultation status/version. The canonical unsigned SOAP draft remains explicitly saveable during wrap-up with all Sprint 8 conflict/signature protections.
9. Patient status distinguishes wrap-up from completion: it says the physician is finishing the synthetic record, makes no delivery or care promise, and retains safety guidance. It exposes no physician, encounter, draft, or internal lifecycle identifier.
10. Every read/write response remains no-store/private and passes through the existing permitted/denied PHI audit boundary bound to the opaque consultation resource. No draft or hidden identifier enters URLs, ordinary logs, telemetry, browser storage, or lifecycle event payloads.
11. The UI presents an explicit, consequential wrap-up action with the required acknowledgments, loading/error/conflict state, keyboard operation, focus recovery, 320 px reflow, and clear wording that the visit remains unfinished and the physician remains responsible.
12. Final clinical disposition, emergency/in-person instructions, follow-up, signature/finalization, clinician availability release, encounter/appointment completion, AVS, diagnosis/coding, orders, medication changes, prescribing/pharmacy, claims, billing, payment, external integrations, and real media remain unavailable.
13. Unit, contract, authorization, real-PostgreSQL owner/non-owner/idempotency/concurrency/rollback/audit/privacy evidence, migration recovery, accessibility, failure recovery, and full regressions must pass without weakening Sprints 1–8.

## 4. Explicit exclusions

This decision does not authorize:

- a final disposition or any assertion that technical failure, patient departure, urgent need, emergency transfer, successful treatment, reassurance, testing, referral, or follow-up occurred;
- signing, finalization, amendment, AVS, patient draft access, encounter or appointment completion, clinician release to new work, or request completion;
- diagnosis/problem-list mutation, medication reconciliation, order/referral, prescription, pharmacy search/transmission, claim, billing, payment, or external vendor action;
- media transport, recording, transcription, chat retention, patient notification, callback/contact attempt, or a claim that a real session ended; or
- real consent, identity proofing, minors/proxies/guardians, real people, real PHI, production enablement, patient care, or closure of any independent review gate.

## 5. Stop conditions and rollback

Stop if a non-owner or cross-scope identity can enter or read wrap-up; a stale/competing command partially changes lifecycle state; the physician becomes available before a future safe disposition/closure command; the appointment or encounter is completed; unsigned documentation becomes signed/final/patient-visible; event or start evidence can be rewritten; PHI reaches logs, URLs, browser storage, or cacheable responses; or any prior safeguard regresses. Rollback disables/removes the wrap-up command and UI while retaining the additive schema and immutable synthetic lifecycle/audit evidence.

## 6. Approval record

The program owner directed complete implementation of the approved telehealth goal, approved all decisions, and authorized uninterrupted work and the changes needed to continue the long-running job. This record applies that authority only to the bounded disabled synthetic wrap-up handoff above. It does not broaden authority to real care, disposition, signing, completion, clinician release, prescribing, billing, or external vendors.

## References

- [Decision 0011](0011-approved-sprint-08-consultation-documentation-draft.md)
- [Workflow specification](../03-workflows-and-state-machines.md)
- [Consultation specification](../09-consultation-documentation-and-follow-up.md)
- [Sprint 9 plan](../backlog/sprint-09-consultation-wrap-up-handoff.md)
