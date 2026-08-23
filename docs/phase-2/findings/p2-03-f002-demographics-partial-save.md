# P2-03-F002 — One demographics Save can commit only its contact portion

- Status: validated
- Domain(s): 03, 04, 08, 09
- Coverage item(s): `COV-003`, `COV-011`
- Severity: medium
- Production blocker: no
- Reach: repeated
- Confidence: high
- Reviewers: `phase2_data`, `phase2_clinical_safety`
- Independent verifier: separate `phase2_quality_operations` pass
- Specialist validation: clinical operations, privacy
- Baseline commit: `d77a8320e6751a2deb2daf14cf1ac5d6b00cb989`
- Observed on: 2026-08-21

## Condition

One user-visible Save action sends contact and demographics as two sequential requests and independent transactions. If the second request fails, contact information and communication preferences remain committed while the interface reports only that demographics could not be saved.

## Evidence

- `PatientSummary.tsx:573-587` awaits contact, then demographics, and uses one undifferentiated error message.
- `api.ts:10394-10419` defines separate mutation calls.
- `PatientRepository.cs:1068-1125,1128-1198` opens and commits separate transactions.
- The retained demographics smoke calls only the demographics endpoint and does not inject failure between the browser calls.

## Consequence

A user can reasonably believe nothing changed when contact destinations or communication preferences did change, leaving an unintended partial state if the action is abandoned.

## Cause and reach

A single UI aggregate has no shared commit boundary or explicit partial-success outcome across its two server mutations.

## Risk calibration

Individual writes remain atomic and audited, and a retry normally converges. The misleading partial state is material but generally recoverable, supporting medium severity.

## Validation record

Three read-only passes reproduced the source path. A browser failure-injection scenario remains outstanding.

## Disposition

Validated separately from stale-write concurrency because its failure mode and closure evidence differ. No implementation recommendation is made.
