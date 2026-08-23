# P2-03-F004 — Merge execution is not bound to the identity state that was reviewed

- Status: validated
- Domain(s): 03, 04, 07, 09
- Coverage item(s): `COV-003`, `COV-014`
- Severity: high
- Production blocker: yes
- Reach: repeated
- Confidence: high
- Reviewers: `phase2_clinical_safety`, `phase2_data`
- Independent verifier: separate `phase2_quality_operations` pass
- Specialist validation: HIM/patient identity, clinician, database operations
- Baseline commit: `d77a8320e6751a2deb2daf14cf1ac5d6b00cb989`
- Observed on: 2026-08-21

## Condition

An audited merge plan records patient IDs, legacy IDs, match score, reasons, rationale, actor, and time, but not the reviewed identity snapshot, patient version, count version, or expiry. Execution accepts the audit ID and does not recompute the identity match after intervening corrections.

## Evidence

- `PatientMergeAuditRepository.cs:11-49` and `V0020__patient_merge_execution.sql:5-17` define the stored evidence.
- `Program.cs:1634-1653` sends only the audit ID to execution.
- `PatientMergeExecutionRepository.cs:56-110,372-390` reloads merge identity/markers and blockers, not the reviewed demographics.
- `Test-AvenChartBaseline.ps1:2194-2209` executes immediately after planning and does not interleave an identity change.

## Consequence

A correction can invalidate the evidence used to decide that two records belong to the same person while the old approval remains executable.

## Cause and reach

The plan attests a decision summary rather than exact reviewed content. Every deferred merge approval shares this condition.

## Risk calibration

Execution-time locks, structural blockers, single use, atomic movement, manifests, and rollback substantially reduce interruption and recovery risk. They do not prove freshness of the patient-identity decision, supporting a narrowed high finding rather than a general merge indictment.

## Validation record

The independent pass partially corroborated the broader candidate and specifically confirmed the identity-evidence gap. A plan-correct-execute synthetic interleaving remains outstanding.

## Disposition

Validated narrowly as a future-production identity safeguard. No implementation recommendation is made.
