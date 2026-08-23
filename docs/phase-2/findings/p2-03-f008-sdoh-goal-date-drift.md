# P2-03-F008 — Generated SDOH target dates move forward on every read

- Status: validated
- Domain(s): 03, 04, 07, 09
- Coverage item(s): `COV-003`, `COV-004`, `COV-014`
- Severity: medium
- Production blocker: no
- Reach: repeated
- Confidence: high
- Reviewers: `phase2_clinical_safety`, `phase2_data`
- Independent verifier: separate `phase2_quality_operations` pass
- Specialist validation: clinician, care management, clinical informatics
- Baseline commit: `d77a8320e6751a2deb2daf14cf1ac5d6b00cb989`
- Observed on: 2026-08-21

## Condition

Every response for a positive SDOH assessment recalculates generated goal dates as the current UTC date plus 90 days. The date is not persisted or anchored to the assessment date, but the UI displays it as a concrete Target.

## Evidence

- `PatientSdohRepository.cs:242-250` computes the due date from `DateTime.UtcNow` during serialization.
- The same response includes the persisted assessment date at `PatientSdohRepository.cs:255-281`.
- `PatientSdoh.tsx:607-615` displays each generated goal and target date.
- Existing tests check goal count/description, not temporal stability.

## Consequence

An unchanged assessment receives a later target every day and can never naturally age toward overdue, so the displayed date cannot serve as durable follow-up evidence.

## Cause and reach

A time-sensitive workflow fact is derived at read time rather than represented as stable assessment- or task-state.

## Risk calibration

The output is explicitly labeled generated, and no evidence establishes it as an order or authoritative work queue. That counterevidence keeps severity medium and production-blocker status no pending clinical policy.

## Validation record

All three passes corroborated the deterministic computation. A controlled-date retrieval and clinician/care-management interpretation remain outstanding.

## Disposition

Validated engineering-readiness condition. No implementation recommendation is made.
