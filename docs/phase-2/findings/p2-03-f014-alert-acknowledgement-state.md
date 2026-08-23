# P2-03-F014 — Allergy-review acknowledgement is not bound to the condition or rule revision

- Status: validated condition
- Domain(s): 03, 04, 09
- Coverage item(s): `COV-004`, `COV-014`
- Severity: medium
- Production blocker: unknown pending clinical policy
- Reach: repeated within the allergy-review rule
- Confidence: high behavior confidence; medium consequence confidence
- Reviewers: `phase2_clinical_safety`, `phase2_data`
- Independent verifier: separate `phase2_verifier` pass
- Specialist validation: clinician, pharmacist, and clinical-informatics review outstanding
- Baseline commit: `d77a8320e6751a2deb2daf14cf1ac5d6b00cb989`
- Observed on: 2026-08-21

## Condition

An allergy-review acknowledgement is keyed only by encounter and rule key. It is not bound to the evaluated allergy-list state, rule revision, severity, or message.

## Evidence

- Alert evaluation suppresses the rule whenever an acknowledgement remains open at `ClinicalAlertEvaluationRepository.cs:15-49`.
- Acknowledge and reopen mutate the same row at `ClinicalAlertEvaluationRepository.cs:52-95`.
- The schema stores no rule revision or condition snapshot in `V0019__encounter_clinical_alert_acknowledgments.sql:1-13`.
- Rule revisions exist separately in `V0046__clinical_alert_rule_revisions.sql:1-20`.

## Consequence

After acknowledge → add allergy → deactivate or delete all allergies, the historical acknowledgement can suppress the newly recurring “no active allergy” condition. A revised rule under the same key can likewise inherit the old acknowledgement.

## Cause and reach

Acknowledgement is modeled as durable rule-key state rather than evidence of one evaluated condition and rule version.

## Risk calibration

The behavior is deterministic but narrow. Because the intended meaning may be “reviewed once per encounter,” severity is held at medium and blocker status remains unknown pending clinical policy.

## Uncertainty and counterevidence

Acknowledgement is authenticated, timed, transactional, accepted only while the condition is active, and explicitly reopenable. These controls do not decide the intended recurrence semantics.

## Validation record

All passes reproduced the state sequence. No live reordered scenario or approved clinical policy was available.

## Disposition

Validated engineering condition; production-readiness decision deferred to the clinical owner. No implementation recommendation is made.
