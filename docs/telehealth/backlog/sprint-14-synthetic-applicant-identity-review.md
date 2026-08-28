# Sprint 14: synthetic prospective-applicant identity review

Status: Approved for bounded implementation by [TH-DEC-0017](../decisions/0017-approved-sprint-14-synthetic-applicant-identity-review.md)  
Scope: Staff-only review and one deterministic append-only decision for a contact-verified synthetic prospective applicant; no identity proofing, patient creation/linkage, portal, intake completion, coverage, request, queue, care, downstream action, external integration, production use, or real PHI

## 1. Outcome

Close the current Sprint 4 dead end without crossing the patient-promotion boundary. An authorized administrator can see a bounded identity-review queue and record the only outcome supported by the server-held duplicate disposition. The applicant remains prospective after either decision.

## 2. Stories

| ID | Story |
|---|---|
| `TH-SP14-001` | Add append-only review decisions/events and constrained terminal applicant review states with staff, provenance, replay, and synthetic-only database invariants. |
| `TH-SP14-002` | Add a practice/facility-scoped PHI-minimized review queue that exposes no matching-patient candidate or canonical identifier. |
| `TH-SP14-003` | Add an administrator-only service that derives the allowed outcome, requires reason/version/idempotency/synthetic confirmation, and performs no promotion or linkage. |
| `TH-SP14-004` | Publish typed private/no-store GET and PUT routes with treatment-purpose, demographic view/write permissions, PHI audit context, opaque not-found, and bounded Problem Details. |
| `TH-SP14-005` | Add an accessible administration panel with explicit limitations, outcome-specific language, manual retry/reload, focus recovery, 320 px reflow, and no browser persistence. |
| `TH-SP14-006` | Prove exact replay, changed-key/stale/conflicting/concurrent behavior, role/access/facility isolation, append-only evidence, audit/cache/privacy, zero patient/downstream delta, and full regression/Graphify/planning evidence. |

## 3. Contract boundary

The list response may contain opaque applicant ID, version/status, legal name, date of birth, masked email/phone, residence state/postal code, contact-verification time, duplicate disposition, derived allowed decision, created/expiry time, and stable limitations. It never contains an access key, evidence fingerprint, possible matching patient, canonical patient/chart/request/actor identifier, or raw comparison data.

The PUT request contains expected version, the server-derived allowed decision, a bounded reason, and explicit synthetic confirmation. Administrative actor/role, optional staff binding, facility, policy, evidence provenance, and time are server derived.

The decision response contains the opaque applicant/decision IDs, resulting status/version, decision/reason, policy/evidence provenance, decision time, and false identity/promotion/downstream capability flags.

## 4. Acceptance evidence

1. Only configured-facility administrators with `patients/demo/view` can list and `patients/demo/write` can decide; physician, patient, missing-session, wrong-purpose, denied-permission, and cross-facility paths fail without disclosure.
2. `NoCandidate` accepts only `ApprovedForProspectiveIntake`; `PossibleMatchManualReview` accepts only `ManualReviewRequired`. No override or candidate data exists.
3. A valid first write creates one decision/event and advances one applicant version; exact retry returns it; changed-key reuse, stale or conflicting commands fail; concurrent first writes produce one winner.
4. Decision and event rows are append-only, retain server-derived staff/provenance/time, and contain no real-proofing assertion.
5. The public applicant response remains coarse, states that the applicant is not a patient, and never reveals staff identity, review reason, possible candidate, or internal evidence.
6. Recording changes only the applicant aggregate plus the two bounded evidence rows and required PHI audit; patient, portal, request, queue, intake, coverage, consent, encounter, appointment, claim, prescription, task/message/notification, and integration rows remain unchanged.
7. API, component, and four-browser tests cover queue semantics, deterministic decisions, validation, ambiguous failure/retry, reflow, serious automated WCAG findings, and no browser persistence.

## 5. Exit boundary

Sprint 14 ends with a reviewed prospective applicant. Identity proofing, patient matching resolution, canonical patient creation/linkage, portal enrollment, consent, insurance/network verification, clinical triage, practice acceptance, request reassociation/creation, and queue entry remain unavailable and require a separately authorized atomic workflow and evidence.
