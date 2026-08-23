# P2-03-F011 — Ordinary APIs physically delete clinical, follow-up, and financial records and supporting evidence

- Status: validated
- Domain(s): 03, 04, 07, 08, 09, 10
- Coverage item(s): `COV-004`, `COV-005`, `COV-006`, `COV-007`, `COV-008`, `COV-009`, `COV-011`, `COV-014`
- Severity: high
- Production blocker: yes
- Reach: cross-cutting across clinical lists, procedure orders, appointments, recalls, billing lines, claims, and payments
- Confidence: high
- Reviewers: `phase2_clinical_safety`, `phase2_data`, `phase2_frontend_accessibility`
- Independent verifier: separate `phase2_verifier` pass
- Specialist validation: clinician, HIM, billing/revenue-cycle, retention/legal, privacy, and recovery review outstanding
- Baseline commit: `d77a8320e6751a2deb2daf14cf1ac5d6b00cb989`
- Observed on: 2026-08-21

## Condition

Ordinary authenticated product routes physically delete problems, allergies, immunizations, prescriptions, procedure orders, appointments, recalls, billing lines, claims, payment activities, and related supporting evidence rather than retaining a tombstone, correction, or immutable history.

## Evidence

- Allergies, problems, and immunizations use `ExecuteDeleteAsync` at `ClinicalListStateRepository.cs:81-90,152-161,443-446`.
- Prescription deletion removes audit events and then the prescription in separate autocommit statements at `ClinicalListRepository.cs:1120-1144`.
- Order deletion transactionally removes specimens, results, reports, and the order at `ProcedureRepository.cs:1680-1736`.
- These routes are exposed at `Program.cs:3147-3155,3253-3261,3405-3413,3438-3446,5539-5548`.
- Appointment deletion physically removes the root at `Program.cs:2265-2274` and `AppointmentRepository.cs:1210-1226`; portal appointment requests and their events cascade through `V0211__portal_appointment_request_history.sql:1-29`.
- Recall deletion uses `ExecuteDeleteAsync` at `RecallRepository.cs:63-68`, and its outreach activities cascade through `V0031__recall_activity.sql:1`.
- The patient appointment UI deliberately confirms permanent deletion, while the recall board exposes a direct trash action at `PatientAppointments.tsx:264-269` and `RecallBoard.tsx:176-183`.
- Billing lines and claims are physically deleted at `BillingRepository.cs:1375-1401,1947-1962`; payment activity and an orphaned payment session can be removed at `BillingRepository.cs:2490-2550` even though a separate soft-void path exists.
- The ordinary financial delete routes are exposed at `Program.cs:6519-6535,6626-6635,6741-6750`.

## Consequence

Clinically material history, appointment-request history, follow-up obligations, outreach evidence, and financial posting evidence can disappear from normal application and database history. A failed prescription delete can erase its audit while leaving the prescription itself.

## Cause and reach

Fixture-cleanup-style physical deletion is part of ordinary product behavior across multiple clinical, follow-up, and financial aggregates.

## Risk calibration

Loss is not reversible through the application and may require broad backup restoration. The condition therefore supports high severity and future-production blocker status.

## Uncertainty and counterevidence

The order cascade and payment hard-delete transaction are internally transactional, and appointment deletion requires deliberate confirmation. Payment void, deactivation, entered-in-error, medication lifecycle, encounter archive, and governed message archive demonstrate preservation patterns elsewhere. The independent recall lifecycle finding `P2-03-F024` records why deletion is currently its only exit. Exact clinical and financial retention obligations require qualified policy decisions.

## Validation record

COV-004, COV-005, and COV-007 specialist and independent passes reproduced route-to-storage deletion. Pilot C and COV-006 independently confirmed that procedure-order deletion also removes result versions and critical acknowledgement state/events through their cascading foreign keys. Destructive runtime, cascade verification, and selective-recovery tests were intentionally not performed.

## Disposition

Validated engineering condition and future-production blocker. No implementation recommendation is made.
