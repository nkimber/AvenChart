# P2-03-F021 — Legacy message mutations bypass the governed history and version boundary

- Status: validated
- Domain(s): 03, 04, 07, 09, 10
- Coverage item(s): `COV-005`, `COV-008`, `COV-009`, `COV-014`
- Severity: high
- Production blocker: yes
- Reach: repeated across status, content, and reply mutations
- Confidence: high
- Reviewers: `phase2_clinical_safety`, `phase2_data`, `phase2_frontend_accessibility`
- Independent verifier: separate `phase2_verifier` pass
- Specialist validation: clinical communication and records-management review outstanding
- Baseline commit: `d77a8320e6751a2deb2daf14cf1ac5d6b00cb989`
- Observed on: 2026-08-21

## Condition

Older staff-message status, content, and reply operations mutate current message state without the expected versions, authenticated attribution, and immutable events used by assignment, correction, forwarding, escalation, and archive workflows.

## Evidence

- Status and content operations replace the body, use no expected version or event, and hard-code `updated_by = 1` at `MessageRepository.cs:249-317`.
- Reply appends body text but can change `assigned_to` without incrementing `assignment_version` or recording an assignment event at `MessageRepository.cs:736-766`.
- Assignment and forwarding use row locking, expected versions, actors, and events at `MessageRepository.cs:329-469`; correction has its own locked evented path at `MessageRepository.cs:624-700`.
- Scheduling Operations invokes the legacy status-plus-body operation for deferral at `SchedulingOperations.tsx:98-113`.

## Consequence

A stale status or content write can remove a later appended correction from the current body while the correction event remains. A reply can make assignment state disagree with assignment history and version evidence. Current communication and retained evidence can therefore contradict one another.

## Cause and reach

New governed lifecycles were added beside, rather than around, older whole-record mutation contracts.

## Risk calibration

The condition affects patient communication content, ownership, and follow-up state. It is reachable through ordinary product operations and supports high severity and future-production blocker status.

## Uncertainty and counterevidence

Reply uses SQL concatenation, reducing concurrent append loss. Corrections, assignment, forwarding, escalation, and reversible archive are strong counterexamples with retained evidence. A fault and stale-client matrix remains outstanding.

## Validation record

All three specialist perspectives and the independent verifier reproduced the mixed lifecycle and the contradictory stale-write/assignment-history outcomes from source.

## Disposition

Validated engineering condition and future-production blocker. No implementation recommendation is made.
