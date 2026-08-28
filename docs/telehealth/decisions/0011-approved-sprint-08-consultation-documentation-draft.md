# Decision 0011: Sprint 8 consultation documentation draft authorization

Status: Approved — active for the exact disabled synthetic slice below  
Approved date: 2026-08-27  
Decision owner: AvenChart program owner  
Implementation owner: Codex delivery agent under AvenChart program-owner direction  
Review/expiry: 2026-10-31, or immediately when superseded by a later gate decision

## 1. Authorized outcome

Permit only the physician who owns an active synthetic telehealth consultation to explicitly save a bounded SOAP documentation draft into the consultation's existing canonical AvenChart encounter:

```text
opaque consultation ID + authenticated owning physician + selected treatment facility
  -> re-verify active consultation/request/encounter ownership and adult patient
  -> require a client expected version and at least one clinician-entered SOAP section
  -> append one canonical clinical_notes version with server-derived author/time
  -> return only the current bounded draft projection
```

This is synthetic documentation-development evidence. It is not signed, final, coded, billed, transmitted, patient-visible, or usable for real patient care.

## 2. Authorized implementation surfaces

Changes may use the existing canonical encounter SOAP-note/version/signature boundary and existing telehealth paths plus:

```text
docs/telehealth/decisions/0011-approved-sprint-08-consultation-documentation-draft.md
docs/telehealth/backlog/sprint-08-consultation-documentation-draft.md
docs/telehealth/backlog/sprint-08-evidence.md
```

The smallest backend, frontend, OpenAPI, PHI-audit, authorization, runtime-evidence, planning-validation, CI, runbook, and test edits needed to connect and prove this no-migration slice are authorized.

## 3. Required controls

1. The feature remains disabled by default, synthetic-only, and rejected in Production.
2. The write route uses only an opaque consultation ID and requires physician role, treatment purpose, selected facility, staff identity, `patients:demo view`, `encounters:auth view`, `encounters:auth write`, and ownership of the active consultation's encounter.
3. The server rebinds consultation, request, reservation, shift, session, appointment, encounter, physician, practice, facility, and active adult patient before every save. A non-owner, administrator, cross-scope identity, missing/stale consultation, or ineligible patient receives the same not-found or forbidden boundary used by Sprint 7.
4. The draft reuses canonical `clinical_notes`; telehealth does not create a second legal-chart store. Each successful change appends a version linked through `supersedes_note_id`, records authenticated `saved_by` and server time, and increments the encounter version.
5. Each save requires `ExpectedVersion >= 0`. The first version expects zero. A stale save fails with conflict and does not overwrite or merge another version.
6. The four SOAP fields are optional individually, but at least one must contain clinician-entered text. Whitespace is normalized, each field is limited to 10,000 characters, and no default/template may assert symptoms, observations, examination, diagnosis, decision-making, or treatment.
7. Draft timestamps are server-derived. The client cannot choose author, patient, encounter, appointment, note identifier, evidence source, saved time, or note time.
8. A locking encounter signature rejects all further draft writes through both application prechecks and the existing database serialization trigger. This slice exposes no signing or amendment action.
9. Workspace retrieval returns only the current draft version, saved timestamp/author, lock state, and four SOAP sections. It does not expose note identifiers, prior-version content, encounter/patient keys, signatures, diagnoses, orders, or other chart domains.
10. Every read/write response remains no-store/private and passes through the existing permitted/denied PHI audit boundary bound to the opaque consultation resource. Draft content is excluded from URLs, ordinary logs, telemetry, browser storage, and telehealth evidence events.
11. The UI provides explicit Save draft and Reload current draft actions, semantic field labels, save/loading/error/conflict status, keyboard operation, 320 px reflow, and unsaved-change protection when replacing a local draft. It performs no background autosave.
12. Draft content is visibly labeled synthetic, incomplete, unsigned, and not patient-visible. Diagnosis/coding, orders, medication changes, prescribing/pharmacy, disposition, signature/finalization, AVS, completion, claims, billing, external integrations, and real media remain unavailable.
13. Unit, contract, authorization, real-PostgreSQL owner/non-owner/concurrency/lock/audit/privacy evidence, accessibility, failure recovery, and full regressions must pass without weakening Sprints 1–7.

## 4. Explicit exclusions

This decision does not authorize:

- autosave, offline/local persistence, multi-author merge, collaborative editing, templates, copied-forward findings, or generated clinical text;
- diagnosis/problem-list mutation, medication reconciliation, order/referral, prescription, pharmacy search/transmission, claim, billing, payment, disposition, AVS, signing, amendment, finalization, or encounter completion;
- patient access to the draft, general chart navigation, prior-note history, export, printing, messaging, recording, transcription, or any external vendor;
- real consent, identity proofing, minors/proxies/guardians, real people, real PHI, production enablement, patient care, or closure of any independent review gate.

## 5. Stop conditions and rollback

Stop if a non-owner or cross-scope identity can read or write the draft; a stale version overwrites current content; a signed encounter accepts an ordinary draft; author/time can be supplied by the client; draft PHI reaches logs, URLs, browser storage, evidence events, or cacheable responses; a default asserts care not entered by the physician; any excluded clinical/financial action becomes reachable; or prior safeguards regress. Rollback removes/disables the telehealth draft route and editor while retaining canonical synthetic note versions and audit evidence; clinical/audit history is never destructively removed.

## 6. Approval record

The program owner directed complete implementation of the approved telehealth goal, approved all decisions, and authorized uninterrupted work and the changes needed to continue the long-running job. This record applies that authority only to the bounded disabled synthetic draft slice above. It does not broaden authority to production, real care, signing, prescribing, billing, completion, or external vendors.

## References

- [Decision 0010](0010-approved-sprint-07-read-only-consultation-workspace.md)
- [Consultation specification](../09-consultation-documentation-and-follow-up.md)
- [API and event contracts](../15-api-events-and-integration-contracts.md)
- [Security, privacy, consent, and audit specification](../16-security-privacy-consent-and-audit.md)
- [Sprint 8 plan](../backlog/sprint-08-consultation-documentation-draft.md)
