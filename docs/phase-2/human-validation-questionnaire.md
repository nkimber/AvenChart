# Phase 2 human validation questionnaire

**Status:** all recommended defaults approved by the program owner; no gate closed
**Assessment date:** 2026-08-21
**Fixed baseline:** `phase-1-experimental^{}` = `d77a8320e6751a2deb2daf14cf1ac5d6b00cb989`

This questionnaire contains the remaining decisions that materially change the Phase 3 target or acceptance evidence. Engineering conditions are already recorded in the canonical findings; the questions below ask for clinical, operational, policy, or product judgments that source inspection cannot supply.

## Decisions already fixed by P2-D014

- Target a production-worthy US ambulatory EHR.
- Support only the modern `avenchart-ui` clinician and portal experience; the reference UI is excluded.
- Require standards-conformant FHIR, an external laboratory-result API exercised with a synthetic laboratory, multi-facility and purpose-of-use authorization, vendor-neutral SSO, and a first-party test identity provider.
- Keep every implementation gate open until the program owner explicitly closes it.

## Approved decisions

### HV-01 — Identity and SSO protocol floor

- **Question:** Approve standards-based OpenID Connect discovery/JWKS, Authorization Code with PKCE, explicit issuer/audience/signature/expiry validation, MFA/assurance claims, logout/session revocation, and workload identity as the mandatory provider contract for Auth0, Okta, Entra ID, and similar vendors? Should SAML 2.0 be required in the first production release or remain a later adapter when a customer requires it?
- **Recommended default:** approve the OpenID Connect contract; do not make SAML a first-release blocker; implement the first-party test IdP as a non-production OpenID Connect conformance fixture, not a separate application authentication model.
- **Linked findings:** `P2-05-F001`, `P2-05-F005`, `P2-07-F002`
- **Answer:** approved as recommended. OpenID Connect is the first-release provider contract; SAML is deferred to a customer-driven adapter; the first-party test IdP implements the same non-production OpenID Connect contract.

### HV-02 — Facility, patient, purpose, and emergency access policy

- **Question:** For ordinary treatment access, should an authenticated professional be limited to permitted facilities plus an active treatment purpose, with patient/team relationship required where the workflow supports assignment? Should exceptional access require a reason, elevated re-authentication, short expiry, prominent disclosure, and retrospective review?
- **Recommended default:** yes. Deny absent facility/purpose context, apply assignment/team constraints where available, and provide a separately governed break-glass path rather than silently broad access.
- **Linked findings:** `P2-05-F002`, `P2-05-F003`, `P2-05-F008`, `P2-05-F009`
- **Answer:** approved as recommended.

### HV-03 — Patient lifecycle and immutable clinical evidence

- **Question:** Should merged source charts always redirect or reject new action; retired/deceased charts remain readable but block ordinary scheduling and encounter creation; and any exceptional/postmortem documentation use an explicit reasoned workflow? Once an encounter is locking-signed, should every later change be a content/version-bound amendment rather than an ordinary mutation?
- **Recommended default:** yes to all. Preserve the original signed content, bind signatures to exact versions, and make exceptions attributable and visible.
- **Linked findings:** `P2-03-F004` through `P2-03-F006`, `P2-03-F009`, `P2-03-F010`, `P2-03-F016` through `P2-03-F018`
- **Answer:** approved as recommended. This resolves the policy dependency for `P2-03-F006` and makes it a production blocker.

### HV-04 — Deletion, correction, and retention

- **Question:** Should ordinary users be prohibited from physically deleting clinical, follow-up, appointment, medication, prescription, result, and financial evidence, using reversible status/correction/version history instead? If any destructive deletion is allowed, which record classes, authorities, reasons, retention periods, and legal-hold controls apply?
- **Recommended default:** prohibit ordinary hard deletion across these classes; permit only policy-approved administrative disposition with immutable evidence and recovery.
- **Linked findings:** `P2-03-F011`, `P2-03-F015`, `P2-03-F021`, `P2-03-F024`, `P2-04-F005`, `P2-04-F007`
- **Answer:** approved as recommended. Exact retention periods, legal-hold obligations, and exceptional disposition authorities still require qualified legal/HIM validation before production.

### HV-05 — Critical results, corrections, and portal release

- **Question:** Should every critical result have a named owner, due time, escalation path, documented clinical action, patient/recipient communication, and explicit closure; and should a corrected critical result reopen review/acknowledgement? For portal release, should preliminary/corrected/critical status always be visible, with delay or exception only where an approved rule permits it?
- **Recommended default:** yes. Keep release rules explicit and status-rich; never equate local acknowledgement with completed clinical follow-up.
- **Linked findings:** `P2-03-F025` through `P2-03-F029`, `P2-08-F002`
- **Answer:** approved as recommended. This resolves the operating-boundary dependency for `P2-03-F027` and makes it a production blocker; clinical timing and release-exception details remain acceptance-evidence tasks.

### HV-06 — FHIR and external laboratory contract

- **Question:** Approve FHIR R4 plus the current US Core implementation guide as the US interoperability floor, with SMART App Launch for external client authorization? For initial synthetic laboratory acceptance, should AvenChart receive a FHIR transaction/message bundle containing profiled `DiagnosticReport`, `Observation`, `Specimen`, `ServiceRequest`, patient, and laboratory identity, while preserving an adapter seam for HL7 v2 `ORU^R01` rather than requiring v2 in the first release?
- **Recommended default:** yes. Pin exact package versions in the implementation packet, validate resources with the official validator, and treat HL7 v2 as a later/partner-driven adapter unless the program owner marks it first-release scope.
- **Linked findings:** `P2-03-F025` through `P2-03-F029`, `P2-07-F001` through `P2-07-F005`
- **Standards anchors:** [US Core 9.0.0](https://hl7.org/fhir/us/core/STU9/), [FHIR R4 DiagnosticReport](https://hl7.org/fhir/R4/diagnosticreport.html), [FHIR R4 Observation](https://hl7.org/fhir/R4/observation.html), [SMART App Launch](https://hl7.org/fhir/smart-app-launch/STU2.2/app-launch.html)
- **Answer:** approved as recommended. The first-release target is FHIR R4, US Core 9.0.0, and SMART App Launch with a profiled FHIR laboratory bundle; HL7 v2 `ORU^R01` is a later partner-driven adapter.

### HV-07 — Scheduling, communication, and follow-up workflow policy

- **Question:** Should conflict prevention be atomic when enabled, with attributable reasoned overrides; and should messages, reminders, referrals, recalls, and therapy sessions have owned, versioned, recoverable lifecycles with delivery/outcome/closure evidence rather than mutable status or deletion?
- **Recommended default:** yes. Define the few valid overbooking and correction exceptions explicitly.
- **Linked findings:** `P2-03-F019` through `P2-03-F024`, `P2-05-F009`, `P2-08-F001`
- **Answer:** approved as recommended.

### HV-08 — Billing and remittance scope

- **Question:** Is production billing, adjudication, payment allocation, and electronic remittance processing part of the first production-worthy target? If yes, must the acceptance contract include a real clearinghouse/ERA adapter later, while synthetic adjudication remains test-only and every posting is balanced, idempotent, attributable, and reversible?
- **Recommended default:** keep a production-grade internal financial ledger in scope now; make external clearinghouse certification partner-dependent, and prohibit fixed synthetic outcomes in production paths.
- **Linked findings:** `P2-04-F005`, `P2-04-F006`, `P2-05-F009`
- **Answer:** approved as recommended. The internal production-grade ledger remains in scope; external clearinghouse/ERA certification is partner-dependent and synthetic adjudication is test-only.

### HV-09 — Controlled inventory scope and dual attestation

- **Question:** Is controlled inventory a supported production workflow? If yes, must counters and witnesses independently authenticate, possess the required facility/role authority, review the exact content being attested, and confirm it through their own interaction rather than transfer a live session credential?
- **Recommended default:** yes if the feature remains available; otherwise remove it from production scope. Never use a transferable bearer credential as attestation.
- **Linked findings:** `P2-04-F004`, `P2-05-F010`
- **Answer:** approved as recommended. Controlled inventory may remain in production scope only with independent, authorized, content-bound attestation; otherwise it must be excluded before release.

### HV-10 — Reports, retention, and configuration approval

- **Question:** Should all production exports use the governed purpose/recipient/scope/artifact/download lifecycle, with direct legacy exports disabled? Should retention, legal hold, backup disposition, and configuration separation-of-duty rules be approved before production?
- **Recommended default:** yes. Preserve direct exports only in explicitly non-production fixtures, and require independent approval for configuration classes that can affect clinical alerts, access, integrations, or evidence.
- **Linked findings:** `P2-04-F007`, `P2-05-F011`, `P2-10-F001`
- **Answer:** approved as recommended.

### HV-11 — Accessibility validation authority

- **Question:** Can the program owner personally validate the modern clinician and portal interface against WCAG 2.2 AA using keyboard-only navigation, 200%/400% zoom and reflow, contrast, NVDA or JAWS on Windows, and VoiceOver/Safari for representative success, error, timeout, and recovery flows? If not, should an independent accessibility specialist be required?
- **Recommended default:** require an independent specialist unless the validator has current assistive-technology and WCAG evaluation competence.
- **Linked findings:** `P2-08-F005`, `P2-08-F006`, `P2-09-F002`
- **Answer:** approved as recommended. Independent accessibility-specialist validation is required; the program-owner approval does not constitute a WCAG conformance result.

### HV-12 — Production operations and release floor

- **Question:** Approve a production floor requiring a deployment built from the assessed release commit; high availability or an explicitly accepted downtime objective; tested point-in-time restore and regional/failover procedure; alerting/on-call/incident ownership; capacity and performance limits; security headers and TLS policy; secrets/key lifecycle; SBOM, dependency/license/vulnerability evidence; signed artifact provenance; and rollback rehearsal?
- **Recommended default:** yes. The existing Azure demo is useful counterevidence but cannot close this gate because it predates the baseline and lacks several of these controls.
- **Linked findings:** `P2-04-F001`, `P2-09-F001`, `P2-09-F002`, residual `COV-015` through `COV-018`
- **Answer:** approved as recommended. The Azure demo remains evidence only and does not satisfy this production floor.

## Recording answers

The approved defaults establish the target policy used to evaluate recommendations and acceptance evidence. They do not assert legal compliance, clinical safety, accessibility conformance, interoperability certification, or production readiness. Qualified validation remains required where stated. These answers do not authorize product changes; recommendation acceptance, sequencing, ownership, rollback, and explicit implementation-gate closure remain separate decisions.
