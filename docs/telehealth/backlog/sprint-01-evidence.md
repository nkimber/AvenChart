# Sprint 1 synthetic-foundation evidence packet

Status: Bounded automated evidence passing; independent reviews and program-owner packet review pending  
Decision: [TH-DEC-0003](../decisions/0003-proposed-sprint-01-synthetic-foundation.md)  
Evidence date: 2026-08-26

This packet describes only the disabled, local synthetic Sprint 1 slice. It is not production, clinical-protocol or patient-care acceptance. Runtime JSON evidence is generated under `avenchart/artifacts/telehealth/` and is intentionally not a durable source artifact.

## Deliverable mapping

| Item | Implementation | Automated evidence |
|---|---|---|
| `TH-SP1-001` | `Features/Telehealth/TelehealthOptions.cs`, `Program.cs`, base/Development configuration | API Release build; `TelehealthRuntimeSafetyPolicyTests`; runtime-safety script |
| `TH-SP1-002` | `TelehealthService.cs`, existing portal identity adapter and staff access filters | `TelehealthAuthorizationTests`; authorization runtime matrix |
| `TH-SP1-003` | `V0282__telehealth_foundation.sql` with eight additive tables, append-only triggers and partial unique indexes | full empty/populated/interruption/recovery rehearsal plus six telehealth schema assertions |
| `TH-SP1-004` | `TelehealthDomain.cs` explicit transition table | `TelehealthStateMachineTests` |
| `TH-SP1-005` | `TelehealthEndpoints.cs`, `TelehealthContracts.cs`, `TelehealthOpenApi.cs` | OpenAPI runtime contract script |
| `TH-SP1-006` | branded-host allowlist and public context projection in `TelehealthService.cs` | unknown-host authorization denial; public OpenAPI projection assertion |
| `TH-SP1-007` | `SyntheticTelehealthTriageEvaluator.cs`; immutable protocol evidence | `TelehealthProtocolEvaluatorTests`; queue test requires `TelehealthEligible` |
| `TH-SP1-008` | `TelehealthLanding.tsx`, `PatientTelehealthWorkspace.tsx` | focused Vitest API tests; desktop/mobile public-entry and authenticated-patient browser checks |
| `TH-SP1-009` | administrator operational-review/authorize service and transaction | wrong-role runtime denials; end-to-end authorize-to-queue assertion |
| `TH-SP1-010` | shift, database-time lease and `FOR UPDATE SKIP LOCKED` reserve-next transaction | 20 concurrent callers: one HTTP 200, nineteen HTTP 409; database uniqueness assertions |
| `TH-SP1-011` | patient, administrator and physician route shells with telehealth CSS module | focused administrator component test; desktop/mobile keyboard, semantics, 320px reflow, automated WCAG, and failure-recovery checks across all bounded shells |
| `TH-SP1-012` | five telehealth proof scripts and mandatory verification/runtime workflow steps | all five scripts pass against isolated PostgreSQL/API; CI definitions present |
| `TH-SP1-013` | `TelehealthReadinessHealthCheck.cs` and [runbook](sprint-01-runbook.md) | `/health/ready` exposes only enabled/mode/table-count capability data; runtime-safety assertion |
| `TH-SP1-014` | [release manifest](sprint-01-release-manifest.json) | manifest fixes API v1, migration V0282, synthetic protocol v1 and keeps G2–G4 false |

All implementation paths above are relative to `avenchart/backend/src/AvenChart.Api/Features/Telehealth/`, `avenchart-ui/src/features/telehealth/`, or the explicitly named repository path.

## Recorded automated results

| Evidence | Result |
|---|---:|
| Repository backend Release build/tests | Build 0 warnings; 87 tests passed, 0 failed |
| Focused telehealth backend tests | 33 passed, 0 failed |
| Runtime-safety policy/startup tests | 8 passed, 0 failed |
| Telehealth additive migration/schema checks | 6 passed, 0 failed |
| Authorization runtime checks | 10 passed, 0 failed |
| OpenAPI runtime checks | 4 passed, 0 failed |
| Queue/idempotency/emergency/concurrency runtime checks | 12 passed, 0 failed; 20 callers, one winner |
| Focused frontend unit/component tests | 3 passed, 0 failed |
| Repository frontend lint/tests/build | Lint passed; 209 tests passed; bundle budget passed |
| Desktop/mobile public, patient, administrator, physician and recovery browser checks | 12 passed, 0 failed |
| C# formatting and planning validation | Formatting clean; 46 planning checks passed; 3 mutations rejected |
| Graphify changed-file review | Earlier 26-file slice reviewed composition hubs; focused bootstrap/migration/test delta reviewed 5 files and 93 changed nodes; untracked feature/test files required and received direct source/test validation |

Repository-wide Release build, backend tests, format verification, frontend lint/test/build, planning validation and Graphify delta review passed locally. Local results are not hosted-CI evidence.

[Decision 0004](../decisions/0004-proposed-bootstrap-schema-reconciliation.md) authorized deterministic regeneration of `database/bootstrap/base-schema.sql`. The resulting SHA-256 is `6a1a6ca3de61608654921edb843d48a4b07dcc8899d3e6ca4056cf8b838745a2`; `git diff --ignore-space-at-eol` confirms no logical SQL change. `--verify-bootstrap` passes. The full repository rehearsal then passed all 238 migrations from an empty database, idempotent replay, seeded resets interrupted after migrations 1, 64, and 127 with recovery, missing-ledger replay, checksum-drift rejection, unexpected-ledger rejection, readiness gating, and downstream EF scenarios. The rehearsal exposed that `V0282` needed the repository's established idempotent DDL form; the uncommitted migration was corrected with `IF NOT EXISTS`, `CREATE OR REPLACE`, and deterministic trigger recreation before the passing run. The isolated Docker projects, diagnostic database, networks, containers, and volumes were removed; the pre-existing `avenchart_avenchart-postgres` volume was preserved.

## Open closure criteria

- Independent clinical-safety review of the synthetic evaluator and fail-closed transitions.
- Independent security/privacy review of host, identity, patient, facility, purpose, role, audit and non-PHI evidence boundaries.
- Independent data review of migration, transaction, append-only and recovery behavior.
- Independent accessibility review of the bounded route shells.
- Program-owner review of the complete evidence packet before 2026-10-31.

Until these are recorded, Sprint 1 remains in implementation verification and no later gate, deployment or patient-care use is authorized.
