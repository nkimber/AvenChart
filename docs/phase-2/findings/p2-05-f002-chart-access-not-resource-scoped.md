# P2-05-F002 — Ordinary professional chart and direct-report access is not resource-scoped

- Status: validated
- Domain(s): 03, 05, 07
- Coverage item(s): `COV-001`, `COV-002`, `COV-003`, `COV-007`, `COV-010`
- Severity: high
- Production blocker: yes
- Reach: systemic
- Confidence: high
- Reviewer: `phase2_security_privacy`
- Independent verifier: `phase2_verifier`
- Specialist validation: security/privacy, clinical, legal/compliance
- Baseline commit: `d77a8320e6751a2deb2daf14cf1ac5d6b00cb989`
- Observed on: 2026-08-21

## Condition

An authenticated professional with `patients:demo:view` can search the ordinary patient directory and request a chart by canonical, public, or legacy identifier. A professional with `patients:pat_rep:view` can also use direct practice-wide report families. These paths apply no patient relationship, assigned care team, facility, or purpose predicate, and the returned chart/export aggregates are broad.

## Evidence

- `Program.cs:962-963` applies only the patient-group capability filter; search and chart handlers are at `Program.cs:1019-1028` and `Program.cs:1721-1729`.
- `Data/PatientRepository.cs:18-340` performs unscoped search/chart reads and returns demographics, contact, guardian, employer, portal, facility/provider, insurance, care-team, history, duplicate, appointment, and encounter data.
- `PatientRepository.cs:649-760` includes sensitive social, substance-use, family mental-health, suicide, and surgical history.
- `Security/AuthorizationPolicyCatalog.cs:139-176` reports zero facility, patient/team, purpose, or exceptional-access rules for the current authorization catalog.
- The front-desk synthetic role receives the relevant patient capability, and the retained baseline test expects front-desk search and chart requests to succeed.
- Portal reads are patient-bound, and specialized governed-report paths apply narrower scope. Neither constrains this ordinary professional chart path.
- `Program.cs:8046-8056,8169-8186` gives direct operational and family reports only the broad report capability; `ReportRepository.cs:815-940` returns practice-wide patient, appointment, encounter, referral, chart-tracker, inventory, and clinical-form rows, including patient identity and contact fields.
- The governed report path at `ReportRepository.cs:550-710` is a positive counterexample: it receives a pinned facility or assigned-patient scope and applies row predicates.
- `Program.cs:896-960` applies the same broad local staff gate to FHIR Patient, Encounter, Observation, and SDOH reads/searches; `FhirRepository.cs:15-157` allows practice-wide queries when no subject is supplied and applies no facility, care-team, or purpose predicate.
- In the deterministic runtime, the front-desk fixture read the full synthetic chart without a purpose value. Adding fabricated purpose-of-use and facility headers returned the same `200` body, and the resolved session exposed no enforced facility, organization, team, patient, or purpose claim.
- Full trace and checks are in the [COV-002 assessment](../assessments/cov-002-identity-authorization-phi-audit.md).

## Consequence

Within the authenticated staff population, a user with the general capability can retrieve broad patient information unrelated to a demonstrated facility, assignment, care relationship, or current workflow purpose.

## Cause and reach

The local ACL model authorizes endpoint families, not resource instances. This is systemic across ordinary chart/search and direct-report boundaries.

## Risk calibration

- Impact: broad internal disclosure of patient information
- Likelihood or preconditions: valid professional session plus the general demographics-view capability
- Detectability: endpoint audit events exist, but patient identity is absent from those events
- Reversibility: authorization behavior can change; information already disclosed cannot be recalled
- Severity rationale: high and production-blocking against the adopted least-privilege and minimum-necessary engineering target because the omission is systemic and the data aggregate is broad

## Uncertainty and counterevidence

The application currently models one local organization. Portal reads are patient-bound, and governed-report/configuration paths prove scoped authorization is possible elsewhere. A qualified clinical/privacy decision that unrestricted ordinary chart access is necessary and proportionate could change the required target, but no such policy was supplied.

## Validation record

- Independent method: separate route-to-repository trace, role/seed inspection, authorization-catalog comparison, and counterexample search
- Result: corroborated statically and reproduced with a synthetic front-desk session
- Reviewer agreement or dispute: agreement on condition, high/systemic severity, and target production-blocker status
- Specialist conclusion or outstanding need: clinical operations and privacy/legal owners must define assignment, facility, purpose, and exceptional-access rules; synthetic cross-scope runtime tests remain outstanding

## Disposition

Validated as an engineering-readiness condition. `P2-D014` resolves the target-policy uncertainty: multi-facility and purpose-of-use authorization are required. COV-007 broadened its reach to the direct report families and COV-010 to FHIR. The distinct report-lifecycle bypass is tracked separately as `P2-05-F011`. No statutory conclusion or implementation recommendation is made.
