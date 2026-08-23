# P2-03-F029 — Portal laboratory release has no lifecycle predicate and omits material status context

- Status: validated condition
- Domain(s): 03, 05, 07, 08, 09
- Coverage item(s): `COV-006`, `COV-012`, `COV-014`
- Severity: medium
- Production blocker: unknown pending detailed clinical/legal acceptance evidence
- Reach: systemic across patient-portal laboratory display
- Confidence: high for source behavior; medium for consequence
- Reviewers: `phase2_frontend_accessibility`, `phase2_data`, `phase2_clinical_safety`
- Independent verification: independent cross-specialist repository and rendering reproduction
- Specialist validation: clinical informatics, laboratory medicine, patient engagement, privacy, and legal-policy review outstanding
- Baseline commit: `d77a8320e6751a2deb2daf14cf1ac5d6b00cb989`
- Observed on: 2026-08-21

## Condition

The patient portal returns every patient-owned laboratory order, report, and result without filtering by order, report, review, or result state. The presentation omits report-review status, result status, and correction history.

## Evidence

- `PatientPortalRepository.cs:543-616,3506-3607` loads rows using patient/order/report identity only.
- Backend portal models retain report review and result status at `PatientPortalDtos.cs:338-356`.
- `PortalRecords.tsx:123-213` renders report status, value, range, and abnormal flag but not review status, preliminary/final/corrected result status, or prior versions.
- `PatientLabs.tsx:449-482` provides the clinician-side counterexample by rendering result status and correction history.

## Consequence

Received, assigned, denied, preliminary, final, and corrected content follows the same patient presentation path. Patients cannot tell from the result view whether content was unreviewed, denied, preliminary, or later corrected.

## Cause and reach

Portal access is correctly patient-bound but is independent of the laboratory review, result-status, amendment, and release lifecycles.

## Risk calibration

The presentation can be clinically ambiguous, but immediate patient access may be intentional. `P2-D016` requires visible preliminary/corrected/critical context and permits delay or exception only under an approved rule. Medium severity records the verified missing predicate and context while detailed timing and exception evidence remain open.

## Uncertainty and counterevidence

Report status and abnormal flags remain visible, and patient binding is strong. The target context rule is approved; clinical, patient-engagement, privacy, and legal owners must still validate release timing, exceptions, and explanatory language.

## Validation record

Three specialist passes independently reproduced the query and render behavior. A browser/database matrix for received, assigned, reviewed, denied, preliminary, final, and corrected states remains outstanding.

## Disposition

Validated engineering condition under an approved status-rich portal-release target. Blocker status remains unknown pending detailed specialist evidence. It is distinct from PHI read-audit findings. No implementation recommendation is made.
