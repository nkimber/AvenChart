# P2-03-F027 — Critical-result evidence ends at local acknowledgement rather than accountable follow-up closure

- Status: validated condition
- Domain(s): 03, 07, 08, 09, 10
- Coverage item(s): `COV-006`, `COV-011`, `COV-014`
- Severity: high
- Production blocker: yes
- Reach: systemic within critical-result follow-up
- Confidence: high for the application boundary and approved operating target; medium for clinical timing details
- Reviewers: `phase2_clinical_safety`, `phase2_data`, `phase2_frontend_accessibility`
- Independent verifier: Pilot C verifier pass
- Specialist validation: laboratory director, clinical operations, clinical informatics, and patient-communication governance outstanding
- Baseline commit: `d77a8320e6751a2deb2daf14cf1ac5d6b00cb989`
- Observed on: 2026-08-21

## Condition

Critical-result state has only open and acknowledged lifecycle evidence. It does not link the result to accepted responsibility, clinician or patient communication, follow-up action, due time, overdue escalation, coverage transfer, or closure.

## Evidence

- `V0214__critical_lab_result_acknowledgements.sql:1-24` models only open and acknowledged state plus its event.
- `ProcedureRepository.cs:217-277` implements queue and acknowledge operations only.
- `ProcedureDtos.cs:310-328` contains result and acknowledgement fields but no accountable follow-up owner or action state.
- `LabQueue.tsx:492-497` accurately states that acknowledgement is local and sends no external notification.
- Messaging, recall, review, and integration capabilities are not linked to the critical result.

## Consequence

Application evidence can show that a user clicked acknowledgement, but not that responsibility was accepted, a person was reached, a clinical action occurred, or follow-up completed.

## Cause and reach

Acknowledgement is the terminal application lifecycle even though recognition, responsibility, communication, action, and closure can be distinct operating events.

## Risk calibration

The application boundary is systemic and may be safety significant, supporting high severity. `P2-D016` requires AvenChart evidence for named ownership, due time, escalation, clinical action, communication, closure, and reopened review after correction. The present acknowledgement-only boundary therefore blocks the adopted production target.

## Uncertainty and counterevidence

Orders retain provider context, report assignment exists, and separate message and recall tools could support parts of the approved workflow. Exact timing, escalation, release exceptions, and qualified clinical acceptance evidence remain to be defined and tested.

## Validation record

Three specialist traces and Pilot C independently reproduced the application boundary. The program owner approved AvenChart's required evidence boundary in `P2-D016`; laboratory/clinical owners must still validate the detailed timing, escalation, and recovery acceptance criteria.

## Disposition

Validated High production blocker against the approved target. This does not claim that harm occurred or that the future workflow is clinically validated.
