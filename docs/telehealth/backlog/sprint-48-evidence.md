# Sprint 48 evidence: applicant request rendering-candidate selection

Status: Bounded automated implementation evidence complete; independent clinical, legal, privacy, security, accessibility, interoperability, operational, and production approvals remain open

Decision: [TH-DEC-0051](../decisions/0051-approved-sprint-48-applicant-request-rendering-candidate-selection.md)

Plan: [Sprint 48 applicant request rendering-candidate selection](sprint-48-applicant-request-rendering-candidate-selection.md)

## Implemented boundary

- Applicant-private GET/POST route at `/api/telehealth/v1/applicants/{applicantId}/telehealth-request/rendering-candidate`.
- Exact request, applicant, patient-shell, eligibility, practice-network, facility, state, service, and roster provenance is rebound under locks.
- One server-owned GA/CA/FL synthetic candidate is exposed by display name and masked provider reference for network evaluation only.
- One immutable selection and event advance only pending `Verification` version 8 to version 9.
- Clinician assignment, network participation, exact network, canonical coverage, financial, operational, queue, appointment, encounter, consent, care, integration, and external action remain false.
- The UI requires four unchecked acknowledgments, provides stable retry and focus recovery, and persists no evidence.

## Automated evidence

| Evidence | Result |
|---|---:|
| Rendering-candidate policy tests | 15 passed |
| Live applicant rendering-candidate proof | 7 checks passed across GA, CA, and FL boundaries, including completed-result roster-drift denial and fixture restoration |
| Fresh seeded migration | 279 migrations applied, including V0323 |
| Backend regression | Release build clean; 689 tests passed; formatting clean |
| Frontend regression | 307 tests passed; both frontend lint/build pipelines passed; bundle budget passed |
| Browser accessibility and recovery | 76 telehealth cases passed across desktop/mobile Chromium, Firefox, and WebKit; the final rendering-candidate case passed again in all four projects after provenance hardening |
| Runtime contracts and concurrency | Authorization, OpenAPI, runtime safety, migration resilience, and 20-caller queue/consultation concurrency proofs passed |
| Planning controls | Validator v3.15 passed 93 checks across 174 Markdown files and 590 relative links; all three controlled negative mutations were rejected |
| Graphify maintenance | Deterministic code graph refreshed to 9,964 nodes and 22,288 edges; portability check and changed-code delta review passed mechanically without treating graph output as readiness evidence |

## Open gates

The synthetic candidate roster is not evidence of real licensure, credentialing, availability, payer contracting, exact participation, or suitability for treatment. A separately approved effective-dated contract/authority matrix and exact network gate are required before this request can progress toward canonical coverage or operational review. Independent review and every production gate remain open.
