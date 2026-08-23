# P2-03-F003 — Stale full-record demographic edits silently overwrite intervening corrections

- Status: validated
- Domain(s): 03, 04, 07, 09
- Coverage item(s): `COV-003`, `COV-012`, `COV-014`
- Severity: high
- Production blocker: yes
- Reach: repeated
- Confidence: high
- Reviewers: `phase2_data`, `phase2_clinical_safety`
- Independent verifier: separate `phase2_quality_operations` pass
- Specialist validation: clinical informatics, HIM, privacy
- Baseline commit: `d77a8320e6751a2deb2daf14cf1ac5d6b00cb989`
- Observed on: 2026-08-21

## Condition

Full contact and demographic contracts carry no expected patient version. A row lock serializes each request, but a stale form submitted later replaces all represented fields and receives success instead of a conflict. Delayed portal approval can similarly apply a stored full snapshot over intervening staff corrections.

## Evidence

- `PatientDtos.cs:328-353` and `api.ts:10365-10419` contain no expected version.
- `PatientRepository.cs:1090-1109,1155-1182` performs full-area updates without a version predicate.
- `PatientRepository.cs:2453-2494` locks the current row for audit; it does not compare the client's baseline.
- `PatientPortalRepository.cs:347-447` constructs a full proposed snapshot, and `AdministrationRepository.cs:2128-2281` applies it without comparing a patient version.
- Atomic before/after audit is useful counterevidence and supports later investigation.

## Consequence

Names, DOB, contact destinations, language/interpreter needs, or other patient-identification facts can silently revert after another user corrected them.

## Cause and reach

Pessimistic database locking is used as if it also detected stale clients. The write contract does not express which patient state the user reviewed.

## Risk calibration

The lost update is silent and affects identity and communication fields. Audit makes correction possible but does not prevent use of stale data before discovery.

## Validation record

Clinical, data, and independent traces agreed on the deterministic interleaving. A two-client PostgreSQL/browser reproduction remains outstanding.

## Disposition

Validated engineering-readiness condition and future-production blocker. No implementation recommendation is made.
