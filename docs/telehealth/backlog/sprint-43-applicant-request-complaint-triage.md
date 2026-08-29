# Sprint 43: applicant request complaint triage

Status: Approved for bounded implementation by [TH-DEC-0046](../decisions/0046-approved-sprint-43-applicant-request-complaint-triage.md)  
Scope: One applicant-owned complaint-specific synthetic assessment from a universally safe `SafetyScreening` version 3 request to one exact version 4 disposition; no medically approved protocol, clinical-review work item, contact, queue, appointment, encounter, consent, care, financial, integration, external, or production consequence

## 1. Outcome

Exercise deterministic complaint-specific triage for the server-owned migraine or sleep category, preserve every protective outcome, retain ordered rule evidence, and make the absent medical-director approval/golden-case/publication package an explicit fail-closed production gate.

## 2. Stories

| ID | Story |
|---|---|
| `TH-SP43-001` | Add an additive migration for `Unsupported`, version 4 complaint dispositions, one immutable answer/rule-evidence receipt, exact source guards, append-only enforcement, and a false-only clinical-publication gate. |
| `TH-SP43-002` | Add typed migraine/sleep synthetic fixtures and a deterministic evaluator that records all fired rules in priority order, checksum-bound answers, exact outcomes, and no opaque/generative behavior. |
| `TH-SP43-003` | Add an access-key-bound repository transaction that revalidates the complete request/source chain, universal-pass and location freshness, server-owned category, fixture, replay/contention, and exact version 3 to 4 transition. |
| `TH-SP43-004` | Add private/no-store applicant GET/POST endpoints with an opaque snapshot, exact category-specific coded answers, safe state-change handling, applicant request correlation, minimized response, and idempotency. |
| `TH-SP43-005` | Add an accessible category-specific form with no defaults, immediate emergency actions, explicit `Not sure`, stable retry, outcome-specific projection, focus recovery, and no browser persistence. |
| `TH-SP43-006` | Prove ordered rule/outcome fixtures, publication blocking, exact success/replay/contention, changed-key/stale/expired/foreign/category/source/patient/protocol drift denial, immutable evidence, unchanged upstream records, zero downstream action, migration recovery, and full regression. |

## 3. Entry gate

The applicant remains unexpired and `SyntheticRequestCreated` version 26; the request creation and location receipts agree; the request is `SafetyScreening` version 3; exactly one current passing request universal assessment exists; the request category exactly matches the controlled earlier visit-purpose category (`migraine` or `sleep`); the current GA/CA/FL location and callback route remain fresh and unchanged; the canonical patient remains portal-disabled and unmerged; and every clinical-review work item, contact, queue, appointment, encounter, consent, care, financial, integration, and external consequence remains absent.

## 4. Outcome boundary

| Deterministic outcome | Request state at version 4 | Meaning in this slice |
|---|---|---|
| `Emergency` | `EmergencyRedirected` | Terminal for this request; show direct 911 and, for self-harm warning, 988 direction without claiming dispatch. |
| `UrgentInPerson` or `InPersonRequired` | `InPersonRecommended` | Terminal for this request; show prompt/in-person direction. |
| `Unsupported` | `Unsupported` | Terminal for this request; the synthetic presentation is outside the demonstrated telehealth scope. |
| `ClinicalReview` | `ClinicalReview` | A qualified clinical review is required, but no reviewer, assignment, or work item exists yet. |
| `TelehealthEligible` | `Intake` | Synthetic workflow demonstration only; no production clinical approval, intake snapshot, acceptance, or care authority exists. |

The applicant and all prior evidence remain unchanged. Exactly one new generic assessment, one complaint assessment receipt, and one request event are appended. No patient contact, doctor search, care queue, queue position, appointment, encounter, consent, media, care, prescribing, financial, integration, or external action exists.

## 5. Verification matrix

| Area | Required evidence |
|---|---|
| Policy/evaluator | Missing/malformed/category-mismatch denial, explicit unknown-to-review behavior, complete migraine/sleep priority tables, every fired rule in stable order, exact answer/protocol hashes, six outcomes, and false-only production publication. |
| Data | Full provenance under locks, database-clock freshness, exact universal-pass and protocol validation, one request transition/assessment/receipt/event, replay/contention, immutable ordered evidence, and no downstream mutation. |
| HTTP | Access-key ownership, configured host/practice/facility, private/no-store, safe errors, minimized outcome/governance state, request correlation, idempotency, and prohibited-input absence. |
| UI | No default answers, explicit `Not sure`, emergency action before submission, 988 direction for the relevant result, changed-context stop guidance, stable retry, result recovery, reflow, keyboard/focus, and no persistence. |
| Regression | Backend/frontend/lint/build/E2E, live GA/CA/FL plus outcome/rule cases, migration/recovery, runtime/authorization/OpenAPI, planning validator, Graphify portability/review, bootstrap, and exact cleanup. |

## 6. Clinical content gate

The bundled fixture is intentionally incomplete and unapproved. It must remain `UNAPPROVED_SYNTHETIC` with `medicalDirectorApprovalRecorded=false`, `clinicalGoldenCasePackApproved=false`, and `productionPublicationAllowed=false`. A later clinical-governance slice must own the exact medical content, evidence review, golden cases, independent red-team review, approval identities, dates, scope, and publication lifecycle before any production use.
