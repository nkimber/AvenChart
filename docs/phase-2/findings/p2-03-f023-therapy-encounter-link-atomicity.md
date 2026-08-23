# P2-03-F023 — Therapy encounter creation is not atomic with session linkage

- Status: validated condition
- Domain(s): 03, 04, 07, 09, 10
- Coverage item(s): `COV-005`, `COV-008`, `COV-009`, `COV-014`
- Severity: high
- Production blocker: yes
- Reach: repeated across completed group-session encounter generation
- Confidence: high static confidence
- Reviewers: `phase2_clinical_safety`, `phase2_data`
- Independent verifier: separate `phase2_verifier` pass
- Specialist validation: clinician, therapy workflow, and database-recovery review outstanding
- Baseline commit: `d77a8320e6751a2deb2daf14cf1ac5d6b00cb989`
- Observed on: 2026-08-21

## Condition

Group-session encounter generation holds a therapy transaction and session lock, but each patient encounter is created and committed on another connection before its therapy-session link is inserted.

## Evidence

- The therapy workflow opens its outer transaction, locks the completed session, loops participants, and inserts links at `TherapyGroupRepository.cs:375-496`.
- Inside that loop, `EncounterRepository.CreateAsync` opens its own connection and commits independently at `EncounterRepository.cs:256-374`.
- The therapy workflow checks existing links before creation, so an independently committed encounter without a link is not recognized on retry.

## Consequence

A failure after encounter commit but before link or outer-transaction commit can leave an orphan chart encounter. Retrying can create a second encounter for the same patient and group session.

## Cause and reach

One clinical aggregate is split across two independently committing connection and transaction owners.

## Risk calibration

The condition can create duplicate or untraceable chart encounters and is difficult to repair reliably after a partial failure. It supports high severity and future-production blocker status.

## Uncertainty and counterevidence

The outer session lock serializes competing batch attempts, and already linked retries are idempotent. A controlled fault between encounter and link commits is still required to confirm recovery behavior.

## Validation record

Specialist and independent passes reproduced the cross-connection commit order and retry gap from the fixed source. No database fault injection was available.

## Disposition

Validated source-level condition and future-production blocker. No implementation recommendation is made.
