# Product scope and requirements

## 1. Problem statement

Patients seeking timely help for a simple, low-acuity problem often face an avoidable scheduling delay. Practices need a safe way to accept immediate requests from established and new patients, determine whether video care is appropriate, verify operational prerequisites, assign an eligible physician, conduct and document care, and prepare downstream prescription and claim transactions.

The product must improve access without presenting emergency triage as a diagnosis, allowing administrators to practice medicine, weakening state licensure or corporate-practice boundaries, or representing payer information as a payment guarantee.

## 2. Goals

- Offer a trusted, practice-branded path from request to synchronous physician consultation during configured service hours.
- Support both existing adult patients and new adult applicants with the minimum data needed for safe care, identity resolution, coverage checks, billing, and recordkeeping.
- Restrict service to medical-director-approved, low-acuity presentations using deterministic, versioned triage.
- Give patients honest, accessible queue and failure-recovery information.
- Give practice staff an auditable operational work queue and physicians a sequenced clinical work queue.
- Produce a complete encounter, optional non-controlled prescription, after-visit summary, and claim-ready record.
- Establish vendor-neutral integration seams aligned with adopted healthcare standards.
- Preserve a clean seam for a later AvenChart marketplace.

## 3. Non-goals for the initial release

- Pediatric or guardian-consent care.
- Scheduled telehealth, asynchronous e-visits, remote patient monitoring, group visits, or multi-party specialty consults.
- Behavioral-health crisis treatment, longitudinal psychotherapy, pregnancy-related urgent care, abortion services, or cancer treatment.
- Emergency, urgent in-person, procedure-dependent, imaging-dependent, or palpation-dependent care.
- Controlled-substance prescribing, including electronic prescribing for controlled substances.
- Patient selection of a particular clinician.
- Cross-practice pooling, marketplace discovery, marketplace ranking, or marketplace payment settlement.
- Live payer, clearinghouse, pharmacy, e-prescribing, or video vendor integrations in the first engineering increment.
- Automated diagnosis, AI triage, AI medical advice, autonomous coding, or autonomous claim submission.
- Guaranteeing clinician response time, treatment, prescription issuance, coverage, network status, cost, or claim payment.

## 4. Functional requirements

| ID | Requirement | Acceptance evidence |
|---|---|---|
| TEL-PROD-001 | The system MUST resolve one active practice and its approved brand/configuration before collecting PHI. Unknown, disabled, or ambiguous host mappings MUST fail closed without revealing tenant data. | Host-resolution integration tests and cross-practice isolation tests. |
| TEL-PROD-002 | The entry page MUST name the medical practice as provider of care, identify AvenChart as the technology platform, show supported states/hours, and display emergency guidance before intake. | Content review and accessible UI test. |
| TEL-PROD-003 | The system MUST support authenticated established patients and prospective new adult patients without requiring staff to pre-create a chart. | End-to-end tests for both journeys. |
| TEL-PROD-004 | The system MUST collect and confirm current physical location and a callback number before clinical triage and again at consultation start. | State-machine and encounter-start tests. |
| TEL-PROD-005 | Universal emergency screening MUST precede coverage, price, payment, practice queue, and clinician matching. | Ordered journey test proving no downstream call occurs after an emergency result. |
| TEL-PROD-006 | Only a versioned clinical outcome of `TelehealthEligible`, or a physician-reviewed `ClinicalReview` converted to eligibility with a reason, MAY continue to operational authorization. | Rule and authorization tests. |
| TEL-PROD-007 | The system MUST collect confirmations for demographics, consent, notices, identity, coverage selection, financial acknowledgment, technology readiness, and intake completeness before queue acceptance. | Readiness-gate matrix tests. |
| TEL-PROD-008 | Practice staff MUST have a work queue that distinguishes safety-blocked, verification-pending, operationally-ready, held, accepted, and declined requests. | Staff journey and authorization tests. |
| TEL-PROD-009 | An operational acceptance MUST place the request in a practice-scoped clinician queue ordered by clinically ready time unless an authorized, reasoned clinical priority changes it. | Queue-ordering and audit tests. |
| TEL-PROD-010 | Patients MUST receive realtime status changes and an approximate position or wait band only after acceptance; the system MUST offer a polling fallback. | SignalR/polling contract and accessibility tests. |
| TEL-PROD-011 | Physicians MUST explicitly enter an available telehealth shift, and the matcher MUST validate practice, state authority, service, credentialing, payer/network constraints, and current availability before assignment. | Eligibility matrix and concurrency tests. |
| TEL-PROD-012 | Reservation MUST be atomic and leased so that one request cannot be actively claimed by two physicians and a failed client cannot hold a request forever. | Transaction and lease-recovery tests. |
| TEL-PROD-013 | The physician MUST be able to review the chart, intake, triage evidence, coverage evidence, current location, consents, allergies, medications, and relevant history before joining. | Role/context authorization and clinical workspace tests. |
| TEL-PROD-014 | The product MUST provide a private waiting room and synchronous video visit with device preflight, short-lived authorization, reconnection, and a governed fallback. It MUST NOT record media. | Video adapter contract, security configuration, and recovery tests. |
| TEL-PROD-015 | Every started consultation MUST produce an encounter disposition; every completed consultation MUST contain required documentation and a signed/finalized record or a documented pending-signature exception. | Encounter completeness validation. |
| TEL-PROD-016 | The physician MAY prescribe only after medication/allergy review and pharmacy selection; the initial release MUST prevent controlled-substance orders and preserve physician autonomy to issue no prescription. | Medication safety and negative-path tests. |
| TEL-PROD-017 | The patient MUST be free to choose a pharmacy, with preferred and nearby options clearly presented as choices rather than endorsements or restrictions. | State-specific content/behavior tests. |
| TEL-PROD-018 | The system MUST create a claim-ready professional-service record and route it only through an explicit human-controlled submit workflow. Stub transport MUST be visibly distinguishable from live delivery. | Billing workflow and environment-safety tests. |
| TEL-PROD-019 | The system MUST deliver an accessible after-visit summary containing disposition, instructions, medications/orders, warning signs, escalation guidance, and follow-up. | Clinical-content and portal test. |
| TEL-PROD-020 | All material state, clinical, consent, administrative, assignment, chart, prescription, claim, and external-delivery actions MUST be attributable and auditable. | Audit completeness test and sampled audit report. |
| TEL-PROD-021 | A practice MUST be able to disable telehealth globally or by state, service, payer, protocol, clinician, or operating interval without corrupting in-progress encounters. | Kill-switch and graceful-drain tests. |
| TEL-PROD-022 | All external capabilities MUST sit behind adapters with deterministic stubs, contract tests, correlation IDs, idempotency, timeouts, retry policy, and dead-letter/manual recovery behavior appropriate to the transaction. | Adapter certification suite. |
| TEL-PROD-023 | The design MUST preserve a future marketplace boundary before practice enrollment; marketplace identity or discovery MUST NOT grant access to a practice chart or queue. | Architecture review and tenant-isolation test. |
| TEL-PROD-024 | Initial-release production enablement MUST remain blocked until clinical, legal, privacy/security, billing, accessibility, operations, and practice owners approve the readiness gates. | Signed release evidence package. |

## 5. Capability boundaries

### 5.1 AvenChart responsibilities

- Consumer and workforce identity integration, authorization enforcement, tenant/practice context, intake workflows, configurable clinical-protocol execution, request and queue coordination, recordkeeping, audit, integration adapters, operational telemetry, and accessible user experiences.
- Administrative tooling and evidence that allow a practice to apply its own clinical, licensure, contracting, pricing, and operational policies.
- Explicit platform status and failure/recovery handling.

### 5.2 Practice responsibilities

- Practice-of-medicine decisions, medical director oversight, protocol approval, standard of care, clinician credentialing/privileging/licensure, payer and pricing configuration, operational staffing, clinical documentation, prescribing, follow-up, billing review, and patient communications.
- Contracting with video, e-prescribing, clearinghouse, eligibility, and other vendors, including required business associate agreements.
- Legal validation of state-specific wording, consent, retention, and corporate-practice arrangements.

### 5.3 Vendor responsibilities

Vendors perform bounded capabilities under contract. A successful HTTP response or transport acknowledgment is not equivalent to clinical success, network participation, prescription acceptance, claim acceptance, or payment. AvenChart retains each business acknowledgment separately.

## 6. Product assumptions requiring approval

| Assumption | Default | Owner |
|---|---|---|
| Patient population | Adults 18+ only | Medical director and legal |
| Clinician population | Physicians only | Practice credentialing |
| Visit modality | Video-first; governed audio fallback | Medical director, legal, payer operations |
| Queue discipline | FIFO by ready time, reasoned safety priority only | Medical director and operations |
| New patient identity | Risk-based proofing before chart promotion; stronger recovery than intake | Security/privacy and HIM |
| Price route | Insurance when confirmed or pending with disclosed risk; optional self-pay | Billing/legal |
| Recording | No recording of any media | Privacy/security and medical director |
| External integrations | Stubs until separate vendor certification | Engineering and compliance |

