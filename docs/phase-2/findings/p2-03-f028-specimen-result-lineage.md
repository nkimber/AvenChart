# P2-03-F028 — Report and result lineage is not bound to a governed specimen record

- Status: validated condition
- Domain(s): 03, 04, 07, 09
- Coverage item(s): `COV-006`, `COV-008`, `COV-014`, `COV-019`
- Severity: medium
- Production blocker: unknown pending detailed laboratory validation
- Reach: repeated across locally captured reports and results
- Confidence: high for implementation; medium for consequence
- Reviewers: `phase2_frontend_accessibility`, `phase2_data`, `phase2_clinical_safety`
- Independent verification: independent cross-specialist schema, repository, and UI reproduction
- Specialist validation: laboratory operations and interoperability review outstanding
- Baseline commit: `d77a8320e6751a2deb2daf14cf1ac5d6b00cb989`
- Observed on: 2026-08-21

## Condition

Specimens have a governed lifecycle, but report capture accepts an order plus a free-text specimen/accession value. No relationship identifies which specimen row produced a report or requires that specimen to be received and non-rejected.

## Evidence

- `lab_reports` stores `order_id` and `specimen_number text`, while `lab_specimens` independently stores identifiers against the order at `generate-postgres-seed.mjs:1583-1640`.
- `V0234__procedure_specimen_lifecycle.sql:1-34` governs specimen state but adds no report/result link.
- `CreateReportAsync` checks only order existence and a nonblank text value at `ProcedureRepository.cs:1126-1165`.
- `CreateResultAsync` checks only report existence at `ProcedureRepository.cs:1580-1623`.
- `LabReportAndResultCapture.tsx:29-38,69` types a specimen/accession rather than selecting or resolving a governed specimen.

## Consequence

The application can retain a report/result against an invented, mistyped, rejected, recollected, or otherwise ambiguous specimen reference and cannot reconstruct which governed specimen produced it.

## Cause and reach

The newer specimen lifecycle was added beside the inherited report text field without a shared identity or explicit external-specimen model.

## Risk calibration

The implementation condition is deterministic. `P2-D016` approves a profiled FHIR laboratory bundle with governed specimen lineage and an explicit later adapter boundary for external HL7 v2 messages. External reports can still legitimately describe specimens not collected within AvenChart, so representation and reconciliation details require laboratory validation; Medium severity and blocker-unknown status remain appropriate.

## Uncertainty and counterevidence

Order foreign keys preserve the patient/order relationship, and specimen lifecycle itself is strongly governed. A laboratory owner must define when local specimen state is authoritative and how unmatched external provenance is represented.

## Validation record

All three specialist passes independently reproduced the UI, repository, and schema boundary. Synthetic PostgreSQL runtime then accepted a final report using `NO-SUCH-SPECIMEN-P2` and accepted an atomic result beneath it, confirming the missing resolver against a nonexistent specimen. Rejected-specimen, recollection, and legitimate external-specimen policy remain outstanding.

## Disposition

Validated engineering condition under an approved laboratory target; severity and blocker status may change after detailed laboratory lineage and unmatched-specimen validation. No implementation recommendation is made.
