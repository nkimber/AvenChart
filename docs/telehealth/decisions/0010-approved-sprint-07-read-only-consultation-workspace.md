# Decision 0010: Sprint 7 read-only consultation workspace authorization

Status: Approved — active for the exact disabled synthetic slice below  
Approved date: 2026-08-27  
Decision owner: AvenChart program owner  
Implementation owner: Codex delivery agent under AvenChart program-owner direction  
Review/expiry: 2026-10-31, or immediately when superseded by a later gate decision

## 1. Authorized outcome

Give only the physician who owns an active synthetic telehealth consultation a least-privilege, read-only workspace projection:

```text
opaque consultation ID + authenticated physician + selected treatment facility
  -> verify physician role, staff identity, practice/facility, own encounter,
     active consultation/request, and adult patient
  -> return current identity/callback, confirmed visit intake/location,
     and active allergy/medication/problem summaries
  -> record the read through the existing PHI access-audit boundary
```

This is synthetic chart-read evidence only. It is not a clinical assessment, medication reconciliation, diagnosis, note, order, prescription, completed encounter, or permission to navigate the patient's broader chart.

## 2. Authorized implementation surfaces

Changes may use the existing telehealth paths plus:

```text
docs/telehealth/decisions/0010-approved-sprint-07-read-only-consultation-workspace.md
docs/telehealth/backlog/sprint-07-read-only-consultation-workspace.md
docs/telehealth/backlog/sprint-07-evidence.md
```

The smallest backend, frontend, OpenAPI, PHI-audit, authorization, runtime-evidence, planning-validation, CI, runbook, and test edits needed to connect and prove this no-migration slice are authorized.

## 3. Required controls

1. The feature remains disabled by default, synthetic-only, and rejected in Production.
2. The established-patient entry path accepts only an active, unmerged, portal-enabled adult age 18 through 120; an ineligible identity fails with the existing scope-hiding boundary before a request is created.
3. The workspace route uses only an opaque consultation ID. It requires physician role, treatment purpose, selected facility, staff identity, `patients:demo view`, `encounters:auth view`, and ownership of the consultation's encounter.
4. The repository binds consultation, request, appointment, encounter, physician, practice, facility, and patient in one query. Non-owner, cross-facility, cross-practice, non-current, missing, or non-adult work returns the same not-found boundary.
5. The projection is an explicit allowlist: display name, date of birth, age, recorded sex, one callback number, confirmed physical-location state, complaint category, patient-entered visit summary, symptom-duration bucket, triage outcome, and active allergy/medication/problem summaries.
6. The projection excludes canonical/legacy patient IDs, encounter/appointment IDs, address, email, insurance identifiers, financial data, employer, guardian, portal credentials, care-team data, documents, messages, labs, prior notes, diagnoses from prior encounters, free-text list comments, and inactive list entries.
7. Each clinical list is bounded to 20 stable, current entries. The response states that it is read-only, current only as of its timestamp, and requires physician verification; absence of entries is never represented as a confirmed negative history.
8. Every response uses `Cache-Control: no-store` and related private-cache protections. Neither the UI nor API stores the projection in browser storage, URLs, logs, telemetry, or telehealth evidence events.
9. The existing staff access filters record permitted and denied PHI access outcomes. The endpoint sets the audited resource to the opaque telehealth consultation before returning any result.
10. The UI exposes no general patient-chart navigation. It renders the allowlisted projection within the owned consultation workspace with semantic headings, explicit empty states, loading/error recovery, keyboard operation, and 320 px reflow.
11. Documentation, diagnosis, orders, signing, medication reconciliation changes, prescribing, pharmacy, claims, billing, completion, and all external integrations remain unavailable and visibly disabled.
12. Unit, contract, authorization, real-PostgreSQL owner/non-owner/facility/audit, privacy/no-cache, accessibility, failure-recovery, and full regression evidence must pass without weakening Sprints 1–6.

## 4. Explicit exclusions

This decision does not authorize:

- general chart access, chart search, chart navigation, bulk patient access, prior note/document/lab/message access, or export;
- clinical documentation, diagnosis, coding, orders, medication changes, reconciliation attestation, prescription, pharmacy selection/transmission, claim, billing, payment, AVS, disposition, signature, amendment, or completion;
- real consent, identity proofing, minors/proxies/guardians, live media, recording, transcription, or any external vendor;
- use of the projection as a source-of-truth replacement for clinician verification; or
- production enablement, real people, real PHI, patient care, or closure of any independent review gate.

## 5. Stop conditions and rollback

Stop if a non-owner, administrator, different facility/practice, inactive consultation, or underage patient can receive the projection; an excluded identifier or chart domain appears; a read lacks a PHI audit result; the response is cacheable; browser storage contains the projection; the endpoint permits chart mutation/navigation; or prior safeguards regress. Rollback disables/removes the workspace route and UI. Existing PHI audit evidence is retained and never destructively removed.

## 6. Approval record

The program owner directed complete implementation of the approved telehealth goal, approved all decisions, and authorized uninterrupted work and the changes needed to continue the long-running job. This record applies that authority only to the bounded disabled synthetic read-only slice above. It does not broaden authority to production, real care, clinical output, or external vendors.

## References

- [Decision 0009](0009-approved-sprint-06-consultation-start-handoff.md)
- [Consultation specification](../09-consultation-documentation-and-follow-up.md)
- [Security, privacy, consent, and audit specification](../16-security-privacy-consent-and-audit.md)
- [Sprint 7 plan](../backlog/sprint-07-read-only-consultation-workspace.md)
