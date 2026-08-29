# Sprint 45 applicant request insurance-source confirmation evidence

Status: Bounded automated implementation evidence complete; independent clinical, legal, privacy, security, accessibility, interoperability, operational, and production approvals remain open

Decision: [TH-DEC-0048](../decisions/0048-approved-sprint-45-applicant-request-insurance-source-confirmation.md)

Plan: [Sprint 45 applicant request insurance-source confirmation](sprint-45-applicant-request-insurance-source-confirmation.md)

## Implemented boundary

The access-key owner of one unexpired synthetic prospective applicant can, after the exact Sprint 44 intake receipt, review a server-owned masked insurance-source projection and make seven explicit confirmations. The projection identifies payer and product, masked member and optional group suffixes, subscriber relationship, and the primary source while keeping the prior synthetic eligibility and practice-network evidence visibly historical, expired, non-reusable, and without a rendering-physician network check. The atomic command revalidates the complete applicant, patient shell, request, intake, protected member-detail, eligibility, network, promotion, and practice-review provenance chain; references but never copies or decrypts the protected payload; inserts one applicant-protected receipt; advances only the request from `Verification` version 5 to the same `Verification` status at version 6; and records one request event. The applicant remains `SyntheticRequestCreated` version 26.

The response records only an intent to obtain fresh verification. It does not perform eligibility or network verification, select or create canonical coverage, calculate benefits or patient responsibility, create operational review, accept or contact the patient, search for a doctor, enter a patient or clinician care queue, assign a queue position, schedule an appointment, create an encounter or consent, authorize care, enable prescribing or billing, create a claim, call an integration, or communicate externally.

## Evidence summary

| Evidence | Result |
|---|---:|
| Applicant insurance-source policy tests | Included in 653 passing backend tests |
| Full backend regression | 653 passed |
| Backend formatting verification | Passed with zero changes |
| Full frontend regression | 53 files / 304 tests passed |
| Production frontend build | 137 chunks / 246,399 initial bytes of 256,000-byte budget |
| Frontend lint | Passed |
| Four-engine applicant insurance-source flow | 4 passed across desktop Chromium, mobile Chromium, Firefox, and WebKit |
| Full telehealth browser accessibility and recovery | 76 passed in serial runs across desktop Chromium, mobile Chromium, Firefox, and WebKit |
| Live GA/CA/FL applicant insurance-source proof | 6 checks covering minimization, seven confirmations, stable replay, contention, source binding, immutability, and zero downstream action |
| Runtime safety | 46 checks |
| OpenAPI contract | 66 checks |
| Authorization matrix | 125 checks |
| Telehealth migration/schema integrity | 129 checks |
| Isolated migration ledger/readiness | 276 migrations through V0320 / 64 required tables |
| Full migration and recovery rehearsal | 276 migrations / 29 scenarios |
| Queue and consultation lifecycle regression | 134 checks / 20 concurrent callers |
| Planning and governance validation | 90 checks / 165 Markdown files / 555 relative links / 3 rejected mutations |
| Deterministic code graph | 9,718 nodes / 21,737 edges / 546 communities / 2 portable artifacts passed |
| Generated bootstrap fingerprint | Unchanged SHA-256 `6a1a6ca3de61608654921edb843d48a4b07dcc8899d3e6ca4056cf8b838745a2` |

## Controls demonstrated

- Configured branded host, practice and facility isolation; constant-time applicant access-key ownership; database-clock expiry; exact applicant and request state/version; exact Sprint 44 intake provenance; and an unexpired bounded context.
- A server-owned no-edit projection containing only payer and product labels, masked member and optional group suffixes, subscriber relationship, and `Primary` coverage priority. No policy number, full member or group identifier, insurance edits, free text, clinical content, or protected payload is returned or accepted.
- Explicit presentation of prior synthetic eligibility and practice-network results as historical-only, non-reusable evidence; their original check and expiry times; the absence of a rendering-physician network check; and seven required confirmations, including fresh-verification intent and evidence-limit acknowledgment.
- Full source-provenance revalidation under locks, including the applicant, portal-disabled and unmerged synthetic patient shell, request creation, intake, protected member-detail receipt, prior eligibility and practice-network results, promotion, and practice-review authorization. The protected payload is referenced by immutable identifiers and fingerprints without being copied, returned, or decrypted.
- One protected applicant receipt, one same-status request-version advance, and one event. Applicant state, patient data, intake, source insurance evidence, promotion, and practice-review evidence remain unchanged.
- Exact semantic replay returns the original result; changed-content key reuse, another command after success, stale version, expired or foreign access, source drift, missing or false confirmation, and duplicate writers fail closed.
- Private/no-store responses, safe Problem Details, applicant correlation without a staff-session PHI-audit claim, stable retry with one idempotency key, keyboard operation, automated WCAG checks, result focus recovery, 320-pixel reflow, and no answer, receipt, checksum, or result persistence in browser storage.
- Clinical content remains `UNAPPROVED_SYNTHETIC`; medical-director approval is required but unrecorded, the clinical golden-case pack is unapproved, and production publication is false and database-guarded.

## Defects and evidence-environment findings

The first live invocation used an empty migrated database and correctly failed at the inherited synthetic-promotion foreign key because that workflow requires the deterministic practice and facility fixtures. The exact disposable database was reset, loaded with the gold synthetic dataset, and all 276 migrations were reapplied before product evidence was collected.

The first seeded live run exposed that the PostgreSQL provider returned the new `confirmed_at` `timestamptz` scalar as `DateTime` in this command path, while the repository assumed `DateTimeOffset`. The repository now handles both provider representations and assigns UTC explicitly for `DateTime`; the complete workflow passed on a clean rerun. An intermediate rerun against the already-used database reached the inherited Sprint 43 exact-six-row assertion and was discarded; the final proof ran once from a clean seeded baseline.

The recovery companion rendered two valid independent alerts quickly enough in Chromium and Firefox that its unqualified `getByRole('alert')` locator became ambiguous. The assertion was narrowed to the intended synthetic-queue alert without changing product behavior, and all four browser engines passed.

## Environment boundary

The live proof ran against the exact disposable `avenchart_test_sprint45_schema` database and a local API process with synthetic Georgia, California, and Florida fixtures. No real person, PHI, credential, payer, pharmacy, provider directory, notification, media, clearinghouse, or other external destination was used. The normal database remained outside the proof and was verified unchanged at 237 recorded migrations, maximum numeric migration version 281, and 1,000 patients. The generated bootstrap verified unchanged with the recorded fingerprint.

The deterministic graph was rebuilt from 1,036 code files into 9,718 nodes, 21,737 edges, and 546 communities. Its two durable artifacts passed the repository portability check. The Sprint 45 review delta identified 34 changed files and 447 changed nodes, with 80 capped impacted nodes across 15 surfaced files. The endpoint group, shared frontend transport and applicant headers, insurance-source repository and database provenance guard are the principal review surfaces. Direct backend, frontend, policy, browser, schema, live replay/isolation/source-binding, OpenAPI, runtime, authorization, migration, and queue-concurrency coverage addresses the graph's conservative test-gap warnings.

The exact disposable Sprint 45 API process was stopped and the exact disposable database was removed after every API-dependent verification completed. Its absence and the unchanged normal-database baseline were then confirmed. This synthetic proof environment is intentionally not recoverable.

## Remaining product and production gates

This evidence does not approve real patients or PHI, the synthetic migraine or sleep rules as medical content, medical-director or golden-case approval, production publication, comprehensive clinical collection or reconciliation, current eligibility or coverage verification, exact practice or rendering-clinician network confirmation, canonical coverage, benefits or patient-responsibility calculation, operational review, practice acceptance, patient communication, final clinical eligibility, doctor search, patient or clinician care queueing, queue estimates, scheduling, examination, consent, media, care, prescribing, pharmacy transmission, claims, standards serialization, external integration, or production use. Those require later bounded decisions plus the independent approvals in the master specification.
