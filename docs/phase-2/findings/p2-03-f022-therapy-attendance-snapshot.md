# P2-03-F022 — Therapy attendance can diverge from the completed-session snapshot

- Status: validated condition
- Domain(s): 03, 04, 07, 09
- Coverage item(s): `COV-005`, `COV-008`, `COV-009`, `COV-014`
- Severity: high
- Production blocker: yes
- Reach: repeated across therapy sessions with participants
- Confidence: high static confidence
- Reviewers: `phase2_clinical_safety`, `phase2_data`, `phase2_frontend_accessibility`
- Independent verifier: separate `phase2_verifier` pass
- Specialist validation: clinician, therapy workflow, and database-concurrency review outstanding
- Baseline commit: `d77a8320e6751a2deb2daf14cf1ac5d6b00cb989`
- Observed on: 2026-08-21

## Condition

Attendance recording checks that a therapy session is scheduled and later updates an attendance row, while session completion separately snapshots present participants. The two operations do not serialize through one session aggregate lock or version.

## Evidence

- Attendance loads the scheduled session and later writes only the attendance row at `TherapyGroupRepository.cs:245-280`.
- Completion checks for unrecorded attendance, snapshots present participants, and marks the session completed in its own transaction at `TherapyGroupRepository.cs:283-340`.
- Attendance entities have neither a concurrency token nor a session-wide version in `TherapyGroupSessionAttendanceConfiguration.cs:14-27` and `TherapyGroupSessionAttendance.cs:6-14`.

## Consequence

An attendance writer can load while the session is scheduled, completion can snapshot that participant as present, and the writer can then commit an absent status. The completed session's attendance and participant snapshot can permanently disagree.

## Cause and reach

Session status and attendance are related clinical facts but use independent concurrency boundaries.

## Risk calibration

The race can affect group-session evidence and downstream participant encounter generation. It supports high severity and future-production blocker status against the adopted target.

## Uncertainty and counterevidence

Completion correctly refuses unrecorded attendance, uses a transaction, and protects session status with a concurrency token. Database constraints and primary keys preserve row shape. A two-session PostgreSQL reproduction remains required.

## Validation record

The data specialist and independent verifier reproduced the non-conflicting transaction boundaries and reachable interleaving. Live runtime confirmation was unavailable.

## Disposition

Validated source-level condition and future-production blocker; clinical and runtime validation remain outstanding. No implementation recommendation is made.
