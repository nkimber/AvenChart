# Phase 2 specialist validation plan

**Status:** Prepared for Phase 3 authorization; no validation conclusion, recommendation acceptance, or gate closure is implied.
**Baseline:** `phase-1-experimental^{}` = `d77a8320e6751a2deb2daf14cf1ac5d6b00cb989`
**Decision context:** Target policies are approved in [P2-D016](decision-log.md). All Phase 2 exit gates remain open in the [exit gate](phase-2-exit-gate.md).

## Purpose and boundary

This plan converts the specialist needs identified in the Phase 2 finding register into reviewable Phase 3 inputs. It asks people with accountable domain knowledge to choose and validate semantics that engineering evidence cannot safely infer: clinical exceptions, retention, identity proof, facility/purpose rules, laboratory interpretation, accessibility conformance, operational reliance, and recovery policy.

It is not a certification, legal opinion, production-readiness attestation, or a replacement for independent implementation verification. It neither authorizes code changes nor closes a gate.

## Required review model

Before a recommendation is accepted for Phase 3, its decision owner must name the accountable role(s), select the relevant rows below, and record the decision, evidence standard, exceptions, and recovery owner. Before a related release gate can close, the same role(s) must review the delivered evidence and explicitly accept or reject the observed behavior.

Use synthetic data for all exercises. Preserve the baseline finding IDs, test scenarios, results, exceptions, and decision date in the Phase 3 packet. A role may be filled by more than one person, but a person may not silently stand in for an unassigned discipline.

| Review area | Decision to validate before implementation | Minimum evidence to review | Accountable role(s) | Primary recommendations and finding families |
| --- | --- | --- | --- | --- |
| Identity, privacy, and HIM | OIDC/SSO claim mapping; facility, patient/team, purpose, exceptional-access, session-revocation, audit, minimum-necessary, report-artifact, and retention rules | Claim matrix; synthetic cross-facility/purpose/disabled-session/stolen-token scenarios; audit and retention/recovery examples | Identity/security owner, privacy officer, HIM/records owner, infrastructure owner | `P2-R001`; `P2-R006`; `P2-05-*` |
| Clinical record and patient identity | Duplicate/merge rules; merged/deceased/retired actionability; amendment/correction versus deletion; signature/content relationship; record recovery | Two-editor/fault scenarios; lifecycle/merge decision tables; before/after evidence and forward-correction examples | Clinical informatics lead, patient identity lead, HIM/records owner | `P2-R002`; `P2-03-F001`–`F018`, `F025`–`F029` |
| Laboratory and interoperability | Required FHIR R4/SMART profiles; inbound lab identity, specimen/order association, review/correction/critical-result behavior; synthetic-lab reconciliation | Profile selection; validator output; synthetic ServiceRequest/Specimen/DiagnosticReport/Observation create/correct/replay/duplicate cases; FHIR error/content-negotiation tests | Laboratory director or delegate, clinical informatics, FHIR/interoperability lead, HIM, security owner | `P2-R002-D`; `P2-R006-B/C`; `P2-03-F016`–`F018`; `P2-07-*` |
| Pharmacy and controlled inventory | Medication/prescription correction/deletion policy; controlled-count independent attestation; discrepancy closure and evidence retention | Actor/reason/content history; independent-attestation interaction; reversal/reconciliation scenarios | Pharmacy/controlled-substance operations, clinical informatics, HIM, security owner | `P2-R002-C`; `P2-R003-D`; `P2-03-F011`–`F013`; `P2-05-F009` |
| Scheduling, communications, referrals, recalls, and therapy | Overbooking/override; terminal appointment correction; delivery/outcome vocabulary; referral acknowledgement; recall closure; attendance correction; therapy encounter recovery | State/exception matrices; two-actor race and partial-failure tests; closed-loop outcome samples; recovery drills | Scheduling lead, clinical operations, communications/referral/recall owners, therapy lead, HIM | `P2-R003-A/B/C/E`; `P2-03-F019`–`F024`; `P2-08-*` |
| Billing and financial operations | Ledger/reversal/provenance policy; EOB/ERA import semantics; collections and remittance error recovery | Mutation provenance, duplicate/replay, reversal, reconciliation, and retention scenarios | Revenue-cycle/finance owner, HIM, security/operations | `P2-R003-D`; related `P2-03-*` and `P2-08-*` |
| Data platform and recovery | Schema authority; data remediation; constraints; performance service levels; migration/backup/restore response | Empty-DB bootstrap; schema fingerprint/drift; query plans/locks/timings; migration failure and PITR rehearsal | PostgreSQL/database operations lead, data migration owner, performance engineer | `P2-R004`; `P2-04-*` |
| Modern UI and accessibility | Supported Claude UI workflow safety; stale/error/retry behavior; WCAG 2.2 AA outcomes | Controlled browser interleavings; keyboard, screen reader, focus, zoom/reflow, contrast, and clinical workflow evidence | Frontend lead, independent accessibility specialist, clinical workflow representative | `P2-R005`; `P2-08-*`; `P2-09-F002` |
| API, reports, configuration, and integration operations | API compatibility policy; governed export/report lifecycle; configuration separation-of-duties policy; transport/retry ownership | OpenAPI/FHIR contract tests; least-privilege reports; artifact expiry/restore; same-actor approval and retry/dead-letter scenarios | API/interoperability lead, security/privacy, report owner, configuration-governance owner, operations | `P2-R006`; `P2-07-*`; `P2-10-*` |
| Verification and release | Risk tiers; parity statement disposition; suite freshness; release evidence, deployment, incident, and rollback readiness | Verification manifest; synthetic suite results; SBOM/provenance; deployment/rollback and recovery rehearsal | Quality lead, release engineering, operations, security, clinical owners | `P2-R007`; `P2-09-*`; COV-014–COV-019 evidence gaps |

## Review deliverable for each packet

Each Phase 3 packet must contain:

1. The baseline finding/recommendation IDs and the exact behavioral decision being made.
2. Named accountable reviewers, decision owner, implementation owner, operations owner, and rollback owner.
3. A concise rule/exception matrix, including what happens during emergency, correction, retry, failure, and recovery paths.
4. Synthetic test scenarios with expected results, the accepted residual risk, and observable evidence.
5. A migration/compatibility plan, patient/PHI handling boundary, and forward-recovery plan where records can change.
6. Reviewer disposition: approved, approved with tracked condition, rejected, or needs further evidence. A condition cannot be converted into a silent exception.

## Independent validation requirements

- Every High or systemic finding retains independent verification after implementation; implementation authors do not self-certify the result.
- Accessibility requires an independent specialist review and browser/assistive-technology evidence. Phase 2 makes no conformance claim.
- FHIR requires profile/validator and contract evidence. Real partner certification/testing is later work; the approved current requirement is a standards-conformant API verified with a synthetic laboratory.
- Privacy, regulatory, retention, controlled-substance, and clinical-policy decisions require accountable human review. Engineering may propose controls but may not infer approval from a source-code trace.
- Any production deployment evidence must come from the deployed, approved configuration, not the fixed experimental baseline or a local fixture.

## Gate relationship

The program owner may accept an individual recommendation only after the relevant pre-implementation decisions above are recorded. The Phase 2 exit gate remains **Open** until accepted recommendations have delivery owners, detailed scope, dependencies, rollback plans, specialist outcomes, and current deployment evidence. No approval in this plan closes a gate automatically.
