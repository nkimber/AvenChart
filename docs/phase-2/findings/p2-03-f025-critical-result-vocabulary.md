# P2-03-F025 — The supported Critical value is excluded from the critical-result workflow

- Status: validated
- Domain(s): 03, 04, 07, 08, 09
- Coverage item(s): `COV-006`, `COV-011`, `COV-014`
- Severity: high
- Production blocker: yes
- Reach: repeated across locally captured critical results
- Confidence: high
- Reviewers: `phase2_clinical_safety`, `phase2_data`, `phase2_frontend_accessibility`
- Independent verifier: Pilot C verifier pass
- Specialist validation: laboratory medicine and clinical-informatics review outstanding
- Baseline commit: `d77a8320e6751a2deb2daf14cf1ac5d6b00cb989`
- Observed on: 2026-08-21

## Condition

The modern result form's supported **Critical** option submits `C`, but critical display, queue, and acknowledgement logic recognize `critical`, `panic`, `hh`, and `ll` instead.

## Evidence

- `LabReportAndResultCapture.tsx:47,70` emits `C` for Critical.
- `ProcedureRepository.cs:1580-1677` persists create and correction flags unchanged.
- The critical queue and acknowledgement predicate exclude `C` at `ProcedureRepository.cs:223-236,263-267`.
- `labResultFlag.ts:28-67` maps `C` to the unknown label “Review flag,” not Critical.
- Focused tests cover `panic` and `HH`, but not the first-party value `C`, at `labResultFlag.test.ts:19-39`.
- `Test-CriticalLabResultAcknowledgement.ps1:27-47` inserts `critical` directly and bypasses the entry contract.

## Consequence

A result deliberately recorded as Critical through the supported UI remains visible elsewhere but does not enter the dedicated acknowledgement queue and cannot be acknowledged through that workflow.

## Cause and reach

Safety-relevant classification is independently encoded in the form, display normalizer, SQL predicates, and retained fixture rather than normalized at one authoritative boundary.

## Risk calibration

The exclusion is deterministic for a first-party entry path, affects a safety-significant queue, and is not obvious from green tests. This supports high severity and future-production blocker status.

## Uncertainty and counterevidence

Unknown nonempty values remain visibly flagged rather than appearing normal. Direct callers that use `critical`, `panic`, `hh`, or `ll` enter the queue. Qualified owners must approve the authoritative vocabulary and response expectations.

## Validation record

Three specialist traces and the independent Pilot C pass reproduced the complete entry-to-storage-to-queue mismatch. A browser/database run using the actual selector remains outstanding.

## Disposition

Validated engineering condition and future-production blocker. `P2-09-F002` records why the default evidence did not detect it; no implementation recommendation is made.
