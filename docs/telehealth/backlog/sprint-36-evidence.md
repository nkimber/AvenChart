# Sprint 36 read-only practice-review inbox evidence

Status: Bounded automated implementation evidence complete; independent clinical, legal, privacy, security, accessibility, interoperability, operational, and production approvals remain open  
Decision: [TH-DEC-0039](../decisions/0039-approved-sprint-36-read-only-practice-review-inbox.md)  
Plan: [Sprint 36 read-only practice-review inbox](sprint-36-read-only-practice-review-inbox.md)

## Implemented boundary

Authorized administrator and front-desk staff can now read a private, practice/facility-scoped inbox containing only the exact synthetic `PendingPracticeReview` work items produced in Sprint 35. Each item contains a bounded legal name and birth date, masked contacts, coarse residence region, controlled migraine-or-sleep purpose, passing safety result, server-owned review route, five coarse readiness sections, submission time, and explicit false-capability flags.

This is GET-only operational awareness. It is not assignment, priority, a response-time promise, staff or clinical review, accept/decline, patient contact, a telehealth request, a doctor search, a patient or clinician care queue, queue position, appointment, encounter, care, prescribing, billing, claim, integration, or external action.

## Evidence summary

| Evidence | Result |
|---|---:|
| Practice-review inbox policy tests | 13 passed |
| Full backend regression | 501 passed |
| Focused admin and API frontend tests | 43 passed |
| Full frontend regression, serial isolation | 53 files / 287 tests passed |
| Production frontend build | 137 chunks / 246,399 initial bytes of 256,000-byte budget |
| Frontend lint | Passed |
| Full browser accessibility and recovery | 52 passed across desktop Chromium, mobile Chromium, Firefox, and WebKit |
| Live GA/CA/FL practice-review inbox proof | 13 checks |
| Runtime safety | 37 checks |
| OpenAPI contract | 48 checks |
| Authorization matrix | 95 checks |
| Queue/lifecycle PostgreSQL concurrency | 134 checks / 20 callers |
| Base migration and recovery rehearsal | 268 migrations / 29 scenarios |
| Telehealth migration/schema integrity | 100 checks |
| Planning validator v3.3 | 81 checks / 138 Markdown files / 454 relative links / 0 failures or broken links |
| Controlled planning mutations | 3 rejected / restored positive pass |
| Graphify deterministic refresh | 6,742 nodes / 15,988 edges / 348 communities |
| Graph portability | 2 artifacts passed |

The generated bootstrap remained unchanged with SHA-256 `6a1a6ca3de61608654921edb843d48a4b07dcc8899d3e6ca4056cf8b838745a2`.

## Controls demonstrated

- Administrator/front-desk role, healthcare-operations purpose, `patients.demo.view` permission, and configured practice/facility isolation.
- Exact pending-case, submission, readiness, promotion, portal-disabled unmerged patient, controlled-purpose, and passing-safety provenance.
- Fail-closed exclusion when any copied promoted patient-shell field drifts from its applicant source.
- A masked minimized contract with no patient/applicant/source identifiers, raw contacts, access secret, payer/member/group data, detailed clinical selections, device values, narrative, free text, clinician identity, possible-match identity, or financial value.
- Deterministic bounded ordering, private no-store headers, GET-only routing, opaque PHI access auditing, and repeatable independent refresh/error recovery.
- Stable accessible items, keyboard focus, 320-pixel reflow, explicit no-action/no-queue language, and no browser persistence.
- An unchanged product-state fingerprint across repeated reads; only the expected PHI access-audit evidence was added.
- `staffReviewWorkItemExists=true`, while every assignment, priority, staff action, decision, acceptance, contact, clinical review, request, queue, appointment, encounter, care, prescribing, financial, integration, and external capability remained false.

## Environment boundary

The isolated readiness snapshot reported the synthetic telehealth dependency healthy with all 56 required tables present and all 268 packaged migrations through V0312 applied. The proof used only synthetic Georgia, California, and Florida fixtures. After verification, the exact `avenchart-api-sprint36-e2e` container and `avenchart_test_sprint36` database were removed. The normal database remained outside the Sprint 36 proof and was independently rechecked at 237 migrations through V0281 with 1,000 synthetic patients.

The deterministic Graphify refresh and two-artifact portability check passed. Review-delta received the nine core Sprint 36 code and proof paths, but reported zero changed or impacted graph nodes because the telehealth paths remain untracked relative to repository HEAD; its test-gap heuristic is therefore not treated as coverage evidence. Direct policy, frontend API/component, browser, live, authorization, OpenAPI, runtime, schema, migration, and full-regression suites provide the coverage evidence above.

## Remaining product and production gates

This evidence does not approve real patients, staff review decisions, assignment or priority, clinical review, acceptance or decline, patient communication, telehealth-request creation, patient or clinician queueing, queue estimates, scheduling, examination, consent, care, prescribing, pharmacy transmission, claims, standards serialization, external integration, or production use. Those require later bounded decisions plus the independent approvals in the master specification.
