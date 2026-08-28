# Sprint 15 synthetic prospective-applicant safety-triage evidence packet

Status: Bounded automated evidence passing; independent reviews and program-owner packet review pending  
Decision: [TH-DEC-0018](../decisions/0018-approved-sprint-15-prospective-safety-triage.md)  
Scope: Disabled, synthetic-only, applicant-owned emergency-first universal safety screen after no-candidate staff review; no identity proofing, patient promotion/linkage, complaint, complete triage, insurance, consent, request, queue, clinician review, care, downstream action, external integration, production use, or real PHI

## 1. Implementation trace

| Item | Implementation | Evidence |
|---|---|---|
| `TH-SP15-001` | V0292 adds one append-only applicant safety-evaluation table, four constrained terminal states, immutable protocol/answer provenance, and hard-false consequential flags | Empty/populated migration proof, live constraints, outcome priority, append-only rejection, contention, and zero-delta checks |
| `TH-SP15-002` | `TelehealthProspectiveSafetyTriagePolicy`, service, and repository require applicant access, a supported current location, every explicit nullable answer, and the current no-candidate approval before server-only evaluation | Thirteen focused policy cases plus missing/access/stale/state/exact-replay/changed-content/12-writer live evidence |
| `TH-SP15-003` | A typed private/no-store applicant POST publishes applicant access-key security, semantic idempotency, version conflicts, opaque not-found, and no staff/patient session substitution | OpenAPI, authorization, API tests, live headers, and public minimization evidence |
| `TH-SP15-004` | The prospective entry supplies an immediate 911 action, explicit yes/no radio groups, supported-state location confirmation, fixed results, focus recovery, reload, and stable ambiguous retry without answer persistence | Component/API tests and 52-case cross-browser accessibility/recovery suite |
| `TH-SP15-005` | Applicant resume exposes only a coarse safety state and fixed next action; the command response explicitly denies identity, clinical review, patient, coverage, request, queue, and care consequences | Live response inspection, contract tests, and session-storage assertions |
| `TH-SP15-006` | Safeguard `TH-SG-020`, Decision 0018, planning validation, runtime CI, runbook, migration recovery, Graphify review, and full regressions close the bounded loop | Automated results and open gates below |

## 2. Automated results

| Gate | Result |
|---|---|
| Focused telehealth backend tests | 135 passed, 0 failed, including 13 prospective safety policy cases |
| Focused telehealth frontend tests | 10 files, 45 tests passed, including required answers, immediate emergency direction, stable ambiguous retry, focus recovery, terminal result, and no answer persistence |
| Live prospective safety-triage proof | 12 checks passed: five prioritized outcomes, missing/access/stale rejection, exact replay, changed-content conflict, 12-way one-winner contention, append-only rejection, public minimization, hard-false consequences, and zero canonical/downstream delta |
| ASP.NET Core Release build | Passed with 0 warnings and 0 errors |
| Full backend tests | 188 passed, 0 failed, 0 skipped |
| C# format verification | Passed with no changes required |
| Frontend lint and TypeScript | Passed |
| Full frontend unit/component tests | 53 files, 251 tests passed in the authoritative one-worker run |
| Production frontend build and budget | Passed; 246,395 / 256,000 initial bytes; 137 JavaScript chunks checked |
| Cross-browser telehealth accessibility and recovery | 52 passed across desktop/mobile Chromium, Firefox, and WebKit |
| Full 248-migration empty/populated/interruption/recovery rehearsal | Passed 29 scenarios, including checkpoints 1, 64, and 127, bootstrap catch-up, idempotent replay, missing migration, checksum-drift rejection, and unexpected-ledger rejection |
| Telehealth migration/schema regression | 47 passed; V0282–V0292, all 32 telehealth tables, 25 append-only triggers, and every earlier control passed |
| Telehealth authorization proof | 42 passed, including absent applicant access-key denial and all earlier role/resource boundaries |
| Telehealth OpenAPI proof | 23 passed, including the typed applicant-only safety command, required idempotency, bounded failures, minimized input, and explicit no-consequence output |
| Telehealth runtime-safety proof | 16 top-level checks passed; no downstream/outbound source path and 32-table synthetic readiness remained healthy |
| Prospective identity and identity-review regressions | 11 and 14 passed respectively, including contention, privacy, append-only evidence, and zero canonical deltas |
| Real-PostgreSQL end-to-end concurrency proof | 134 passed, preserving all prior workflow, ownership, exact-replay, append-only, contention, lifecycle, privacy, and zero-downstream controls |
| Generated empty bootstrap verification | Passed unchanged; packaged bootstrap SHA-256 `6a1a6ca3de61608654921edb843d48a4b07dcc8899d3e6ca4056cf8b838745a2` |
| Planning/backlog validator | 60 passed across 75 Markdown files and 243 relative links; all three controlled negative mutations returned nonzero |
| Graphify code index | Refreshed to 6,742 nodes, 15,988 edges, and 348 communities; portable-artifact check passed |

Machine-readable runtime results are under `avenchart/artifacts/telehealth/`, including `latest-telehealth-prospective-safety-triage.json`, with recovery evidence under `avenchart/artifacts/migration-resilience/`. They contain deterministic synthetic identifiers and bounded facts only.

After evidence capture, the exact labeled Sprint 15 API/PostgreSQL containers, network, isolated volume, and disposable synthetic dataset were removed. That data is intentionally not recoverable. No `avenchart-sprint15` Docker resource remains. The pre-existing default PostgreSQL service stayed healthy and was verified unchanged at 237 migrations, 1,000 synthetic patients, and latest migration `V0281__index_flow_board_appointments_by_date`.

## 3. Clinical-safety, ownership, privacy, and UX results

The evidence demonstrates that:

- only the access-key owner of an unexpired `IdentityReviewApproved`, `NoCandidate`, `ApprovedForProspectiveIntake` synthetic applicant can submit;
- current physical location is explicit and limited to configured Georgia, California, or Florida service states; residence does not become an implicit current-location answer;
- every clinical answer is explicitly yes or no, missing answers write no evidence, and the immutable priority is emergency, urgent in-person, hands-on in-person, clinical review, then pass;
- emergency direction and the 911 action exist before submission; outputs are fixed and non-diagnostic and say no clinician reviewed the answers;
- exact retry converges; changed, stale, second, and losing concurrent commands fail; 12 concurrent first writers produce one evaluation and event; and both evidence types reject update/delete;
- even `SafetyScreenPassed` means only that a later separately authorized intake step may exist: every identity-proofing, clinical-review, patient/chart/portal, complete-intake, coverage, request, queue, care, and downstream capability remains false;
- recording changes only the applicant aggregate plus one evaluation and one event; patient, portal, insurance, intake, coverage, request, queue, appointment, encounter, prescription, claim, message/integration, and external-action counts remain unchanged; and
- the browser retains one ambiguous command identity for explicit retry but persists only applicant ID/access key, never location or clinical answers.

## 4. Boundary refinements found by the evidence gate

The initial runtime source assertion expected the route-group prefix and endpoint suffix to occur together in source. ASP.NET Core composes the `/applicants` group with the `/{applicantId}/safety-triage` mapping; the proof now recognizes that structure without weakening its route requirement. The authoritative rerun passed all 16 checks.

The first focused browser invocation used a label query broad enough to match both the location select and its confirmation checkbox. The test now selects the semantic combobox role. The unchanged product behavior then passed the focused case and all 52 cross-browser cases.

The authoritative frontend suite used one Vitest worker, consistent with the repository's prior evidence practice, and passed all 251 tests without timing noise.

## 5. Graph and change-impact review

The deterministic code-only Graphify index was rebuilt to 6,742 nodes, 15,988 edges, and 348 communities, and its portability check passed. `review-delta` reported zero changed or impacted nodes for the eight principal Sprint 15 backend, migration, contract, and frontend files because the telehealth feature tree remains new and untracked relative to the current commit. Its generic missing-test hints were treated only as navigation prompts. Direct source review plus unit, component, API, browser, real-database, authorization, OpenAPI, runtime-safety, migration, recovery, and no-delta evidence covers those exact boundaries.

## 6. Negative assertions and exclusions

Sprint 15 does not authorize or claim NIST identity proofing, patient matching resolution, patient promotion/linkage, canonical patient/chart or portal creation, complaint/purpose collection, diagnosis, complete clinical triage or telehealth eligibility, clinician review, insurance eligibility/network participation, consent, practice acceptance, request/queue creation, appointment, encounter, care, prescription, claim, billing, communication, notification, integration, external call, production enablement, real people, real PHI, or patient care.

## 7. Open review gates

Independent identity, clinical, security/privacy, data, accessibility, interoperability, legal/compliance, operational, and program-owner review remain open. Until those reviews are recorded, Sprint 15 remains a disabled synthetic development slice and every production, identity-proofing, patient-promotion, request/queue, downstream, and patient-care gate remains closed.
