# P2-08-F001 — Therapy sessions with members cannot be completed through the modern UI

- Status: validated
- Domain(s): 03, 07, 08, 09
- Coverage item(s): `COV-005`, `COV-011`, `COV-014`
- Severity: medium
- Production blocker: unknown pending accepted therapy-workflow scope
- Reach: repeated across therapy sessions with members
- Confidence: high static confidence
- Reviewers: `phase2_frontend_accessibility`, `phase2_clinical_safety`
- Independent verifier: separate `phase2_verifier` pass
- Specialist validation: clinician, therapy-workflow, and usability review outstanding
- Baseline commit: `d77a8320e6751a2deb2daf14cf1ac5d6b00cb989`
- Observed on: 2026-08-21

## Condition

Scheduling a therapy session creates an `unrecorded` attendance row for every member, and completion correctly rejects unrecorded attendance. The modern UI exposes scheduling and completion but no attendance retrieval or recording control.

## Evidence

- The therapy page exposes member management, scheduling, completion, and encounter creation at `TherapyGroups.tsx:58-154,226-373`.
- No attendance API client or UI use exists under `avenchart-ui/src`.
- Scheduling creates unrecorded attendance at `TherapyGroupRepository.cs:199-223`; completion refuses it at `TherapyGroupRepository.cs:283-311`.
- Attendance GET and PUT routes exist at `Program.cs:8504-8518`, so the missing step is specifically the modern UI boundary.

## Consequence

Any non-empty session reaches a deterministic dead end: staff cannot complete it or proceed to chart-encounter generation through the modern application.

## Cause and reach

The backend lifecycle was implemented beyond the UI client and route workflow.

## Risk calibration

The server fails safely instead of completing without attendance, which materially limits harm. The result is still a repeated functional failure in a scoped workflow. Severity is medium and blocker status remains unknown until the intended therapy capability and operating model are approved.

## Uncertainty and counterevidence

Empty sessions may complete, and a direct API client can record attendance. The later modern-UI browser gates ran against the synthetic database, but the material-workflow gate failed in an unrelated medication fixture before it established this complete therapy path; the exact user-visible member-session error therefore remains unverified.

## Validation record

The frontend specialist and independent verifier reproduced the static UI-to-server dead end. Browser/API runtime confirmation and therapy-owner validation remain outstanding.

## Disposition

Validated engineering condition. Production-blocker status is unknown; no implementation recommendation is made.
