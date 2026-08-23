# P2-03-F024 — Recall follow-up has no durable terminal lifecycle

- Status: validated
- Domain(s): 03, 04, 07, 08, 09, 10
- Coverage item(s): `COV-005`, `COV-008`, `COV-009`, `COV-011`, `COV-014`
- Severity: high
- Production blocker: yes
- Reach: repeated across patient recalls
- Confidence: high
- Reviewers: `phase2_clinical_safety`, `phase2_data`, `phase2_frontend_accessibility`
- Independent verifier: separate `phase2_verifier` pass
- Specialist validation: clinical operations, HIM, retention, and care-coordination review outstanding
- Baseline commit: `d77a8320e6751a2deb2daf14cf1ac5d6b00cb989`
- Observed on: 2026-08-21

## Condition

Recall records can be active, receive phone/postcard/label activities, or be physically deleted. They have no completed, deferred, cancelled, unsuccessful, escalated, or other durable terminal state with an actor, outcome, and reason.

## Evidence

- Listing returns active recalls, creation sets active, and the only exit is `ExecuteDeleteAsync` at `RecallRepository.cs:13-68`.
- Activity records retain type, note, and time but no actor or outreach outcome at `RecallRepository.cs:88-133`.
- The modern recall board exposes outreach activities and a direct trash action, with no closure workflow or confirmation, at `RecallBoard.tsx:140-184`.
- Deleting a recall cascades its activities through `V0031__recall_activity.sql:1`.

## Consequence

The system cannot distinguish successfully completed follow-up from cancellation, failure, deferral, or accidental removal. Deletion can remove both the obligation and the only outreach history.

## Cause and reach

Recall was implemented as an active work item plus activity log, without a closed-loop state model.

## Risk calibration

Patient follow-up can disappear without a durable outcome or accountable transition. This supports high severity and future-production blocker status for a production recall workflow.

## Uncertainty and counterevidence

Activity types do not falsely claim delivery, and write permission limits who can mutate recalls. Qualified owners must define intended recall outcomes, escalation timing, retention, and exceptional deletion policy.

## Validation record

The clinical, data, frontend, and independent passes reproduced the lifecycle boundary. The physical-delete symptom is also incorporated into `P2-03-F011` rather than counted twice.

## Disposition

Validated engineering condition and future-production blocker; human workflow and retention policy remain outstanding. No implementation recommendation is made.
