# Sprint 2 established-patient readiness evidence packet

Status: Bounded automated evidence passing; independent reviews and program-owner packet review pending  
Decision: [TH-DEC-0005](../decisions/0005-approved-sprint-02-established-patient-readiness.md)  
Evidence date: 2026-08-27

This packet describes only the disabled, local, synthetic established-patient readiness increment. It is not a payer response, an in-network guarantee, a clinical-protocol approval, production authorization, or patient-care acceptance. Runtime JSON evidence is generated under `avenchart/artifacts/telehealth/` and is intentionally not a durable source artifact.

## Deliverable mapping

| Item | Implementation | Automated evidence |
|---|---|---|
| `TH-SP2-001` | `TelehealthDomain.cs` adds explicit `Intake` and `Verification` states and fail-closed transitions | state-machine and focused telehealth tests; runtime eligible/unknown/emergency paths |
| `TH-SP2-002` | `TelehealthRepository.cs` and `TelehealthContracts.cs` provide a patient-scoped, current server projection with masked coverage and opaque request-bound tokens | authorization denial across patients; masking, token, ownership, and changed-source runtime assertions |
| `TH-SP2-003` | `TelehealthService.cs` and repository transactions capture exact versioned confirmations, bounded complaint intake, coverage choice, and a non-legal demonstration acknowledgment | unit validation plus idempotent replay, content-conflict, stale-version, and recovery runtime checks |
| `TH-SP2-004` | `ITelehealthCoverageGateway` and `SyntheticTelehealthCoverageGateway` keep eligibility, network, and financial-route results separate in `NON_PRODUCTION` mode | deterministic gateway tests; confirmed-network and network-unknown runtime paths; no-guarantee limitations asserted |
| `TH-SP2-005` | `V0283__telehealth_established_patient_readiness.sql` adds five append-only evidence tables, ownership constraints, state constraints, fingerprints, and freshness evidence | full 239-migration empty/replay/interruption/recovery rehearsal plus ten focused schema assertions |
| `TH-SP2-006` | `PatientTelehealthWorkspace.tsx`, telehealth API contracts, and telehealth CSS expose review, explicit affirmations, masked coverage, verification, and correction/reconfirmation | four focused frontend tests plus desktop/mobile keyboard, semantics, automated WCAG, masking, and 320 px reflow checks |
| `TH-SP2-007` | endpoint/OpenAPI changes, health readiness, proof scripts, planning authorization, and this packet | authorization, OpenAPI, runtime-safety, queue/concurrency, migration, full repository, and planning-validation results below |

All backend implementation paths above are relative to `avenchart/backend/src/AvenChart.Api/Features/Telehealth/`; frontend paths are relative to `avenchart-ui/src/features/telehealth/` unless an exact repository path is named.

## Recorded automated results

| Evidence | Result |
|---|---:|
| Repository backend Release build/tests | Build 0 warnings and 0 errors; 97 tests passed, 0 failed |
| Focused telehealth backend tests | 44 passed, 0 failed |
| Telehealth additive migration/schema checks | 10 passed, 0 failed |
| Full migration resilience | All 239 migrations passed from empty and seeded databases, including replay, three interruption checkpoints, missing-ledger reapply, drift rejection, unexpected-ledger rejection, readiness, and downstream EF scenarios |
| Authorization runtime checks | 11 passed, 0 failed, including cross-patient readiness denial |
| OpenAPI runtime checks | 5 passed, 0 failed |
| Runtime-safety checks | 5 passed, 0 failed; feature remains disabled by default and rejected in Production |
| Readiness/coverage/queue/concurrency runtime checks | 25 passed, 0 failed; 20 reservation callers produced exactly one winner |
| Focused frontend unit/component tests | 4 passed, 0 failed |
| Repository frontend lint/types/tests/build | Lint and TypeScript passed; 210 tests passed; production build and 246,113/256,000-byte initial bundle budget passed |
| Desktop/mobile telehealth browser checks | 14 passed, 0 failed |
| C# formatting and planning validation | Formatting clean; 47 planning checks passed; 3 controlled mutations rejected |
| Generated bootstrap authority | `--verify-bootstrap` passed; deterministic SHA-256 remains `6a1a6ca3de61608654921edb843d48a4b07dcc8899d3e6ca4056cf8b838745a2` |
| Graphify changed-file review | The committed code-only graph returned no nodes for the untracked feature paths; direct source review, unit tests, real PostgreSQL transactions, OpenAPI inspection, and browser tests supplied the bounded evidence instead |

The isolated runtime used only deterministic synthetic fixtures. The synthetic gateway deliberately reports that no payer or provider directory was contacted and that its result is not a guarantee of coverage, payment, benefits, network participation, or patient responsibility. No live external integration, production environment, or real patient data was used.

## Demonstrated fail-closed behavior

- Unknown branded hosts and cross-patient access are denied.
- Raw insurance row identifiers, policy numbers, and group numbers are not exposed to the browser.
- Active coverage with unknown exact network participation remains in `Verification` and is invisible to the administrator review queue.
- Administrators cannot override emergency triage or an unknown exact-network result.
- A source insurance change after verification fails the administrator's final gate.
- The patient can reconfirm changed source data and obtain new current evidence before staff review.
- Readiness and verification commands replay exactly but reject idempotency-key reuse with changed content.
- Concurrent clinician reservation retains one database-enforced winner.

## Open closure criteria

- Independent clinical-safety review of the readiness boundary, language, transition effects, and fail-closed coverage gating.
- Independent security/privacy review of projection minimization, opaque tokens, identity/resource authorization, fingerprints, audit, and non-PHI evidence boundaries.
- Independent data review of V0283 ownership, immutability, transaction, concurrency, staleness, and recovery behavior.
- Independent accessibility review of the patient readiness and recovery experience.
- Program-owner review of this complete evidence packet before 2026-10-31.
- Hosted CI evidence through the repository's normal review workflow.

Until those reviews are recorded, Sprint 2 remains in implementation verification. It authorizes neither production deployment nor real eligibility/network decisions, and later rollout gates remain closed.
