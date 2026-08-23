# P2-03-F012 — Several prescription mutations are not atomic with correctly attributed audit

- Status: validated
- Domain(s): 03, 04, 05, 09
- Coverage item(s): `COV-004`, `COV-008`, `COV-014`
- Severity: high
- Production blocker: yes
- Reach: repeated across prescription create, deactivate, and direct refill
- Confidence: high
- Reviewers: `phase2_clinical_safety`, `phase2_data`
- Independent verifier: separate `phase2_verifier` pass
- Specialist validation: pharmacy, clinical informatics, and database review outstanding
- Baseline commit: `d77a8320e6751a2deb2daf14cf1ac5d6b00cb989`
- Observed on: 2026-08-21

## Condition

Prescription create, deactivate, and direct-refill paths commit the clinical mutation and its audit as separate autocommit operations and do not reliably pass the authenticated actor.

## Evidence

- Create mutates and then separately audits at `ClinicalListRepository.cs:331-398`.
- Deactivate does the same at `ClinicalListRepository.cs:621-677`; direct refill at `680-739`.
- The audit helper defaults the actor to `admin` at `ClinicalListRepository.cs:1942-1983`.
- The affected routes do not resolve and pass the authenticated session actor.

## Consequence

Interruption or audit failure can leave a committed prescription change without its evidentiary event. Successful changes can also be attributed to a generic actor rather than the user who performed them.

## Cause and reach

Clinical state, audit state, and authenticated attribution are not one transaction contract on these older paths.

## Risk calibration

Prescriptions are clinically material, and missing or incorrect provenance is difficult to reconstruct. The repeated condition supports high severity and blocker status.

## Uncertainty and counterevidence

Prescription content edit and portal refill approval use stronger transactions, versions, row locks, and actor attribution. Fault injection was unavailable, so the interruption consequence is established from transaction semantics rather than observed failure.

## Validation record

Specialist and independent passes reproduced statement order, transaction absence, and actor fallback.

## Disposition

Validated engineering condition and future-production blocker. No implementation recommendation is made.
