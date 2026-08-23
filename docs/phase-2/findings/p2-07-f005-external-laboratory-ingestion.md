# P2-07-F005 — No supported external laboratory-result ingestion contract applies results to the clinical record

- Status: validated
- Domain(s): 03, 04, 05, 07, 09, 10, 11
- Coverage item(s): `COV-006`, `COV-010`, `COV-014`, `COV-019`
- Severity: high
- Production blocker: yes
- Reach: cross-cutting across external laboratory intake, correction, duplicate handling, provenance, reconciliation, and follow-up
- Confidence: high static and synthetic runtime
- Reviewer: Phase 2 coordinator with COV-006 clinical/data/frontend and COV-010 architecture/security evidence
- Independent verifier: prior COV-006/COV-010 static verifier passes; synthetic runtime reproduced by the coordinator
- Specialist validation: laboratory medicine, interoperability, security/privacy, and clinical operations remain outstanding
- Baseline commit: `d77a8320e6751a2deb2daf14cf1ac5d6b00cb989`
- Observed on: 2026-08-21

## Condition

`P2-D014` makes external laboratory-result integration required production scope, but the fixed Phase 1 baseline has no supported external contract that authenticates a laboratory source, validates a standards-based result payload, resolves the patient/order/specimen, applies new or corrected results idempotently, and retains reconciliation and notification evidence.

## Evidence

- `/api/integrations/inbox` accepts a generic source, source message ID, message type, and arbitrary JSON payload, then stores the payload only in integration inbox state (`Program.cs:5630-5669`; `IntegrationRepository.cs:168-256`). No reviewed consumer applies it to laboratory clinical tables.
- The only registered transport is local and deterministic; no laboratory adapter, source registry/signature, schema/profile registry, terminology-normalization boundary, or partner acknowledgement contract is registered (`Program.cs:42`; `LocalDeterministicIntegrationTransport.cs`).
- `/api/procedures` supplies authenticated internal staff APIs for orders, specimens, reports, and results. Those APIs are not an external laboratory trust contract and do not consume FHIR `DiagnosticReport`/`Observation`, HL7 v2, or another selected partner schema (`Program.cs:5015-5548`).
- A synthetic `lab.result.v1` inbox message returned `201`; replaying the same source/message ID with a different result value returned `200` with `duplicate = true`. The `lab_results` row count remained 2,400 before and after both receipts.
- In the same disposable runtime, the internal procedure APIs successfully created an order, report, and result with `201`, proving that local clinical persistence exists. The report also accepted a nonexistent specimen identifier, which independently reproduces `P2-03-F028` rather than establishing external lineage.
- HL7 FHIR R4 models laboratory reports with `DiagnosticReport` and atomic results with referenced `Observation` resources. A selected implementation guide and validation boundary are absent: [FHIR R4 DiagnosticReport](https://hl7.org/fhir/R4/diagnosticreport.html), [FHIR R4 Observation](https://hl7.org/fhir/R4/observation.html).

## Consequence

A laboratory cannot currently deliver a result or correction through a supported production API and have AvenChart safely establish source identity, patient/order/specimen lineage, duplicate equivalence, clinical state, provenance, notification, and reconciliation. The synthetic inbox can acknowledge receipt while leaving the clinical record unchanged.

No lost or misapplied real result is claimed: no real partner or real patient data was used.

## Cause and reach

Phase 1 built useful local laboratory workflow and a generic integration foundation without selecting or implementing the external laboratory message contract. The gap reaches initial results, amendments/corrections, critical-result signaling, terminology mapping, duplicates/replays, failures, reconciliation, and accountable follow-up.

## Risk calibration

The condition is High and a production blocker because external laboratory integration is now an explicit production requirement, and treating generic message receipt as clinical ingestion would create an unsafe trust and data-integrity boundary. Internal staff entry and generic inbox durability are meaningful countercontrols but do not meet the required external contract.

## Uncertainty and counterevidence

- The generic inbox has durable identity, versioned reconcile/reject decisions, actor/reason events, and exact-key deduplication.
- Internal laboratory review, specimen lifecycle, result correction history, and critical acknowledgement provide reusable domain mechanisms.
- A real laboratory partner is unavailable by approved scope, so protocol-specific certification, connectivity, and acknowledgement behavior cannot yet be tested.
- The exact target may be FHIR, HL7 v2, or a constrained partner API; `P2-D014` requires a standards-based API but does not yet select the laboratory transport/profile.

## Validation record

Static reviewers independently found no supported inbound laboratory apply path. The coordinator then reproduced generic receipt, divergent replay acceptance, unchanged clinical result count, and successful internal order/report/result creation against PostgreSQL 17.10 with synthetic data.

## Disposition

Validated production blocker. The implementation gate remains open; no Phase 3 change is authorized until the program owner explicitly closes it.
