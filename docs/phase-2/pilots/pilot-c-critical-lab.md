# Pilot C — Critical laboratory result acknowledgement and follow-up

## Packet

- Baseline: `d77a8320e6751a2deb2daf14cf1ac5d6b00cb989`
- Coverage sampled: COV-006, COV-008, COV-009, COV-010, COV-011, COV-014
- Reviewers: clinical-safety specialist; coordinator acting in the quality/operations role
- Required human validation: practicing ambulatory clinician or clinical informaticist; database/operations and records-management input for retention and recovery

Both reviewers inspected the slice independently before exchanging conclusions. Review 1 also ran a focused 79-test set covering the laboratory API, flag normalization, and queue component. Review 2 ran the four focused shell/queue tests, the production UI build, and the .NET solution build. All executed checks passed. No database-changing workflow was run during this read-only pilot.

## Material strengths

- laboratory routes apply authenticated view/write permissions and the shared staff access-decision audit;
- acknowledgement state and its event are committed in one transaction with actor, reason, time, and an expected acknowledgement version;
- a stale or duplicate acknowledgement is rejected;
- reasons are required and bounded;
- result corrections retain a prior result snapshot;
- queue failure is explicit and tells the user not to rely on the unavailable queue;
- the interface accurately says that the acknowledgement is local and sends no external notification;
- the existing synthetic script proves the direct `critical` happy path, stale conflict, and event write.

## Reconciled candidate conditions

| Candidate | Reviewer agreement | Initial severity | Confidence | Key evidence | Validation still needed |
| --- | --- | --- | --- | --- | --- |
| The supported local “Critical” option sends and stores `C`, while the acknowledgement queue accepts `critical`, `panic`, `hh`, and `ll` but not `C`. The current end-to-end proof inserts `critical` directly and bypasses the real entry path. | Independently found by both | High | High | `LabReportAndResultCapture.tsx:47,70`; `ProcedureRepository.cs:234,265,1616`; `Test-CriticalLabResultAcknowledgement.ps1:33` | Synthetic runtime reproduction; clinician consequence |
| Acknowledgement versioning is separate from result-content versioning, so correction does not invalidate or reopen the acknowledgement and the event cannot prove which result version was reviewed. | Clinical reviewer; supported by coordinator trace after disclosure | High | High for implementation | `ProcedureRepository.cs:226,263-279,1647-1675`; `V0214__critical_lab_result_acknowledgements.sql:10-21` | Barrier-controlled correction scenarios; clinician judgment |
| The API returns every open result newest-first, but the UI exposes actionable rows only for the first three and provides no full-list path; older items can remain hidden. | Independently found by both | High | High | `ProcedureRepository.cs:223-236`; `LabQueue.tsx:492-516` | Four-or-more-result browser exercise; clinician usability review |
| The application lifecycle ends at local acknowledgement and cannot itself establish responsible-recipient acceptance, communication, follow-up, overdue escalation, coverage transfer, or reopening. | Independently identified as a boundary gap | High, systemic | Medium | `V0214__critical_lab_result_acknowledgements.sql`; complete critical-acknowledgement repository references; `LabQueue.tsx:492-498` | Map approved organizational controls; clinician/operations decision |
| Deleting an order hard-deletes its results, acknowledgement state, and acknowledgement events through cascading foreign keys. | Independently found by both | High | High | `Program.cs:5539-5551`; `ProcedureRepository.cs:1680-1722`; `V0214__critical_lab_result_acknowledgements.sql:1-23` | Disposable-data reproduction; records-management and recovery review |

## Agreement and uncertainty

The two passes followed the same material execution path and independently agreed on the highest-detectability failures: vocabulary mismatch, hidden queue items, loss of acknowledgement evidence on deletion, and the absence of closed-loop follow-up inside the application boundary. The result-version linkage condition was found by one reviewer and accepted as source-supported by the coordinator only after the independent pass had been completed; it therefore still requires a separate verifier.

No reviewer claimed that the application alone must implement every notification or follow-up activity. A documented external process could be a compensating control. No such approved process was present in the repository evidence. Likewise, engineering evidence establishes the behavior but does not establish the clinical consequence, acceptable timeliness, or production-blocker decision.

## Test-shape gap demonstrated by the pilot

The strongest calibration result is methodological: green focused tests and a green build coexisted with a deterministic cross-layer mismatch. The entry interface emits `C`, persistence retains `C`, and the queue filters it out, while the workflow proof seeds `critical` directly. Full Phase 2 packets must therefore trace representative values from user entry or integration input through persistence and downstream behavior; endpoint-only happy paths are not sufficient for safety-relevant workflows.

## Verification status

The independent verifier, who did not author either pass, corroborated all five clusters:

| Cluster | Verifier disposition | Reconciled severity/confidence |
| --- | --- | --- |
| `C` vocabulary mismatch | Corroborated across UI, create/update persistence, queue, acknowledgement predicate, display normalizer, and bypassing smoke fixture | High; high confidence |
| Acknowledgement not bound to result-content version | Corroborated from separate histories and correction behavior | High; high implementation confidence |
| Only three newest items actionable | Corroborated; API and total retain the full backlog as counterevidence | High; high confidence |
| Lifecycle ends at local acknowledgement | Corroborated as an application boundary; separate messaging, recall, and report-review capabilities are not linked | High systemic candidate; high source confidence, medium consequence confidence |
| Deletion removes acknowledgement evidence | Corroborated through explicit result deletion and cascading state/event relationships | High; high confidence |

Material independent agreement is acceptable. The technical conditions are corroborated, but clinical consequence, acceptable workflow boundary, records-retention policy, and recovery expectations remain `needs-specialist-validation`. The next full-assessment evidence should include disposable UI/API reproduction with `C`, acknowledge/correct interleavings, a four-plus-result browser exercise, closed-loop workflow mapping, and post-deletion evidence/recovery inspection.
