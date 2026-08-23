# P2-03-F015 — Medication edits cannot reconstruct the prior clinical content

- Status: validated
- Domain(s): 03, 04, 09
- Coverage item(s): `COV-004`, `COV-008`, `COV-014`
- Severity: high
- Production blocker: yes
- Reach: repeated across medication-content edits
- Confidence: high
- Reviewers: `phase2_clinical_safety`, `phase2_data`
- Independent verifier: separate `phase2_verifier` pass
- Specialist validation: clinician, pharmacy informatics, and HIM review outstanding
- Baseline commit: `d77a8320e6751a2deb2daf14cf1ac5d6b00cb989`
- Observed on: 2026-08-21

## Condition

Medication title, diagnosis, dates, and comments are overwritten in place. The accompanying lifecycle event records that an edit occurred but retains neither the prior nor resulting clinical content.

## Evidence

- Content columns are mutated directly at `ClinicalListStateRepository.cs:276-319`.
- The event retains action, activity, actor, reason, expected/resulting version, and time at `ClinicalListStateRepository.cs:517-537`.
- The event schema and response model have no content snapshots or digest in `V0215__medication_list_lifecycle_history.sql:4-14`.
- `V0216__medication_list_content_edit_history.sql:1-6` adds the `edited` action without adding prior/current content.

## Consequence

After a medication correction, the prior clinical statement cannot be reconstructed through the reviewed application or lifecycle history.

## Cause and reach

Lifecycle evidence and clinical-content version evidence are treated as the same concern, while only the former is retained.

## Risk calibration

Medication history is clinically material and later reconstruction can be important for reconciliation and incident review. The repeated loss of prior content supports high severity and blocker status.

## Uncertainty and counterevidence

Expected versions, EF concurrency, actor, reason, and mutation/event atomicity are strong. The finding is limited to content reconstruction and does not allege lost updates or missing lifecycle evidence.

## Validation record

All passes independently reproduced the persistence and DTO boundary. Qualified owners must still define the exact medication-correction record.

## Disposition

Validated engineering condition and future-production blocker. No implementation recommendation is made.
