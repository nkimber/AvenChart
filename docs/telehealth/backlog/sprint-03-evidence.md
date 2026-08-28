# Sprint 3 patient queue transparency evidence packet

Status: Bounded automated evidence passing; independent reviews and program-owner packet review pending  
Decision: [TH-DEC-0006](../decisions/0006-approved-sprint-03-patient-queue-transparency.md)  
Evidence date: 2026-08-27

This packet describes only the disabled, local, synthetic patient queue-transparency increment. It is not production authorization, a service-level promise, a wait-time guarantee, a realtime-service claim, or patient-care acceptance. Runtime JSON evidence is generated under `avenchart/artifacts/telehealth/` and is intentionally not a durable source artifact.

## Deliverable mapping

| Item | Implementation | Automated evidence |
|---|---|---|
| `TH-SP3-001` | `TelehealthPatientQueueStatusResponse`, patient status endpoint, service authorization, and patient-owned repository query | OpenAPI contract, cross-patient denial, pure projection tests, and real API status checks |
| `TH-SP3-002` | `GetPatientQueueStatusAsync` counts current earlier `Ready` entries only in the same practice/facility using the existing ready-time/request-ID order | real PostgreSQL fixture proves one same-facility entry is counted while an earlier different-facility entry is excluded; removed fixtures no longer count |
| `TH-SP3-003` | `TelehealthPatientQueueStatusProjector` maps every current request state to bounded calm content and fails closed when position evidence is unavailable | queued zero/one/many/unavailable plus review/reserved/redirected unit matrix |
| `TH-SP3-004` | `polling.ts` and `PatientTelehealthWorkspace.tsx` use server cadence, exponential backoff, bounded jitter, visibility pause/resume, manual retry, and HTTP reconciliation | polling unit tests and desktop/mobile hidden/resume/interruption browser path |
| `TH-SP3-005` | accessible queue-status card exposes approximate position, last confirmed time, connection state, no-estimate/realtime limitations, worsening direction, and emergency direction | keyboard, automated WCAG, 320 px reflow, and failed-refresh snapshot-preservation checks |
| `TH-SP3-006` | telehealth runtime scripts, OpenAPI/authorization matrices, planning decision/validator, and this packet | full results below |

All backend implementation paths above are relative to `avenchart/backend/src/AvenChart.Api/Features/Telehealth/`; frontend paths are relative to `avenchart-ui/src/features/telehealth/` unless an exact repository path is named.

## Recorded automated results

| Evidence | Result |
|---|---:|
| Repository backend Release build/tests | Build 0 warnings and 0 errors; 104 tests passed, 0 failed |
| Focused telehealth backend tests | 51 passed, 0 failed |
| Existing migration/schema checks | 10 passed, 0 failed; no Sprint 3 migration was added |
| Authorization runtime checks | 12 passed, 0 failed, including cross-patient status denial |
| OpenAPI runtime checks | 6 passed, 0 failed; read-only status route has patient portal/OIDC security and no mutation header |
| Runtime-safety checks | 5 passed, 0 failed; 8 production-rejection policy tests passed |
| Readiness/coverage/queue/concurrency/status runtime checks | 28 passed, 0 failed; scoped position, no-wait semantics, reservation reconciliation, and 20-caller one-winner behavior passed |
| Focused frontend unit/component tests | 8 passed, 0 failed |
| Repository frontend lint/types/tests/build | Lint and TypeScript passed; 214 tests passed; production build and 246,113/256,000-byte initial bundle budget passed |
| Desktop/mobile telehealth browser checks | 16 passed, 0 failed, including visibility pause/resume, manual failure recovery, keyboard, automated WCAG, and 320 px reflow |
| C# formatting | Clean |
| Planning validation | 48 structural checks passed; all 3 controlled negative mutations rejected |
| Graphify changed-file review | The committed code-only graph returned no changed nodes for the untracked telehealth feature paths; direct source review and the test layers above supplied the bounded evidence instead |

The isolated runtime used only deterministic synthetic fixtures. The proof introduced one earlier same-facility and one earlier different-facility queue fixture, verified that only the same-facility request affected position, and retired both fixtures with non-active states before clinician reservation. No row was deleted, no live destination was contacted, and no production environment or real patient data was used.

## Demonstrated fail-closed and privacy behavior

- A different authenticated patient receives `404` and no request or queue information.
- Position is computed inside one authoritative database statement from the request owner's current record and current queue rows.
- Only the same practice, same facility, `Ready` queue status, `Queued` request status, and earlier deterministic order affect the count.
- Missing/inconsistent queue evidence yields position-unavailable content rather than a fabricated number.
- The contract never provides a wait-duration estimate and reports realtime delivery unavailable.
- The response and UI expose no other patient, physician identity, physician workload, complaint details, coverage details, or internal queue-entry identifier.
- A reservation changes the patient phase to `PhysicianPreparing` and removes queue position.
- A failed manual refresh preserves the last confirmed snapshot, marks the connection interrupted, and backs off while keeping retry available.
- Polling pauses while the page is hidden and immediately reconciles through HTTP when visible again.

## Open closure criteria

- Independent clinical-safety/content review of patient-facing queue, worsening-symptom, emergency, redirect, and uncertainty language.
- Independent security/privacy review of resource authorization, projection minimization, timing/traffic exposure, and anti-enumeration behavior.
- Independent data/performance review of snapshot semantics, queue ordering, query plan/index needs, concurrency, and production-scale polling load.
- Independent accessibility review of announcements, interruption behavior, motion/cognitive load, reflow, and assistive-technology use.
- Program-owner review of this complete evidence packet before 2026-10-31.
- Hosted CI evidence through the repository's normal review workflow.

Until those reviews are recorded, Sprint 3 remains in implementation verification. It authorizes neither production deployment nor an exact wait promise, SignalR/realtime service, notification delivery, or patient care, and later rollout gates remain closed.

