# P2-05-F003 — PHI access evidence cannot correlate ordinary reads to the protected patient resource

- Status: validated
- Domain(s): 05, 10
- Coverage item(s): `COV-002`, `COV-007`, `COV-010`, `COV-012`
- Severity: high
- Production blocker: yes
- Reach: systemic
- Confidence: high
- Reviewer: `phase2_security_privacy`
- Independent verifier: `phase2_verifier`
- Specialist validation: security/privacy, legal/compliance, database/operations
- Baseline commit: `d77a8320e6751a2deb2daf14cf1ac5d6b00cb989`
- Observed on: 2026-08-21

## Condition

The central professional PHI audit records actor/session, endpoint, required permission, allow/deny, and a response-status value but intentionally excludes patient, report-family output, or resource identity. Ordinary portal home, profile, appointment, clinical-summary, laboratory, and document reads do not enter that audit or an equivalent general portal-read audit.

## Evidence

- `database/migrations/V0004__phi_access_audit.sql:1-19` defines no patient, resource, facility, purpose, or correlation field.
- `Data/PhiAuditRepository.cs:12-98` writes and exports the limited event dimensions.
- `Program.cs:8827-8900` couples the event to professional capability evaluation.
- Ordinary portal reads under `Program.cs:437-880` use repository session checks outside that filter.
- `PatientPortalRepository` contains dedicated message and generated-report audit events, providing a positive counterexample but not ordinary-read coverage.
- The deliberate omission of bodies, queries, and patient identifiers reduces secondary PHI in the audit store; it also prevents patient-resource reconstruction.
- Direct practice-wide family exports at `Program.cs:8169-8186` create no governed run or download event. Central audit may identify the route, but cannot identify the patients/rows disclosed; the governed path at `ReportExecutionRepository.cs:860-922` provides stronger protected-download evidence.
- FHIR routes at `Program.cs:896-960` enter the same endpoint-level filter, but `FhirRepository.cs:15-157` reads/searches patient-linked data without passing a patient/resource identifier to `PhiAuditRepository`; FHIR audit evidence therefore has the same correlation gap.
- Full trace and checks are in the [COV-002 assessment](../assessments/cov-002-identity-authorization-phi-audit.md).

## Consequence

An investigation cannot reliably determine which patient record a professional read, which patient rows a direct report exported, or reconstruct a complete cross-boundary history of ordinary portal PHI reads.

## Cause and reach

The general audit is designed around endpoint capability decisions rather than completed protected-resource access. The condition spans ordinary professional chart access and ordinary portal reads.

## Risk calibration

- Impact: impaired incident investigation, privacy review, and validation of access-control effectiveness
- Likelihood or preconditions: any inquiry requiring patient-level correlation or complete portal-read history
- Detectability: the missing dimensions are visible in schema review but cannot be recovered from the audit itself
- Reversibility: past missing correlation cannot be reconstructed reliably
- Severity rationale: high and production-blocking because reliable traceability of sensitive access is a required production safeguard and the gap is systemic

## Uncertainty and counterevidence

Audit minimization is a genuine privacy control, and specialized portal message/report actions retain richer context. The correct pseudonymous correlation, retention, access, and minimization design requires qualified privacy and operations decisions. No legal retention or disclosure-accounting conclusion is made.

## Validation record

- Independent method: separate schema, writer, filter, portal-route, and dedicated-audit inventory
- Result: corroborated
- Reviewer agreement or dispute: agreement after separating resource correlation/portal coverage from response-status timing and retention-policy uncertainty
- Specialist conclusion or outstanding need: privacy/legal and operations owners must approve the audit subject and lifecycle model; runtime row inspection remains outstanding

## Disposition

Validated. COV-007 broadened the condition to direct report downloads and COV-010 to FHIR reads/searches. Response-status fidelity is tracked separately as `P2-05-F004`; the wider report-governance bypass is tracked as `P2-05-F011`, and retention remains an evidence gap.
