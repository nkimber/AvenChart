# Phase 2 calibration evidence

This directory preserves the evidence used to decide whether the Phase 2 assessment method is ready to scale. The calibration evaluates the review contract; it is not a complete assessment of the application and does not establish production readiness, clinical safety, legal compliance, accessibility conformance, or certification readiness.

## Fixed baseline

- Product baseline: `phase-1-experimental`
- Resolved commit: `d77a8320e6751a2deb2daf14cf1ac5d6b00cb989`
- Calibration date: 2026-08-20
- Data boundary: repository evidence and deterministic synthetic checks only; no real patient data or production credentials
- Mutation boundary: no application, test, migration, infrastructure, or deployment implementation was changed

The Phase 2 and workbench files are newer than the fixed product baseline. Reviewers confirmed that the `avenchart/` and `avenchart-ui/` product trees match the tagged baseline before drawing product conclusions.

## Records

| Record | Purpose |
| --- | --- |
| [pilot-a-identity-phi.md](pilot-a-identity-phi.md) | Authentication, authorization, staff and portal scope, patient-data access, audit, and failure behavior |
| [pilot-b-encounter-lifecycle.md](pilot-b-encounter-lifecycle.md) | Encounter creation, clinical documentation, versioning, signatures, concurrency, audit, UI recovery, and downstream visibility |
| [pilot-c-critical-lab.md](pilot-c-critical-lab.md) | Critical classification, queue visibility, acknowledgement, amendment, follow-up, evidence retention, and recovery |
| [pilot-d-accessibility-recovery.md](pilot-d-accessibility-recovery.md) | Clinician and portal keyboard behavior, semantics, focus, dynamic state, recovery, responsive behavior, and automated accessibility evidence |
| [calibration-report.md](calibration-report.md) | Cross-pilot agreement, disagreements, rubric changes, inventory changes, limitations, and launch recommendation |

## Interpretation

Each pilot record distinguishes:

- conditions independently observed by both reviewers;
- conditions found by one reviewer and then independently corroborated or challenged;
- open uncertainty that needs a focused experiment;
- conclusions reserved for qualified clinical, security/privacy, legal/compliance, accessibility, certification, or operations specialists.

Canonical finding IDs are assigned only after coordinator reconciliation. A candidate can be technically corroborated while its clinical, legal, or conformance consequence remains pending specialist validation.
