# Rollout, metrics, risks, and approvals

## 1. Gate model

| Gate | Scope | Exit evidence |
|---|---|---|
| `G0 Specification approval` | Development baseline | Product, architecture, medical director, state counsel, privacy/security, billing, accessibility and operations approve scope/assumptions; open decisions have owner/date |
| `G1 Foundation complete` | No patient care | Domain schema/API/auth/audit/outbox/config/flags; migrations/rollback; synthetic environment; architecture and threat review |
| `G2 Clinical simulation` | Staff/clinicians, synthetic cases only | Triage safety cases/golden tests, new/existing intake, admin boundaries, queue/matcher, video simulator, accessible journeys |
| `G3 Integration certification` | Vendor/trading-partner test environments | BAAs/contracts, security/accessibility, eligibility/network/video/eRx/claims mappings and failure/reconciliation certification; production stub lockout |
| `G4 Controlled practice pilot` | One practice, limited hours/states/pathways/physicians/payers | Legal/clinical/config approvals, credentialing, dress rehearsal, on-call/runbooks, patient support, SLO/DR, explicit daily go/no-go and rollback |
| `G5 Expanded release` | Approved practices/states/pathways | Pilot safety/access/quality/financial evidence, defects/actions closed, capacity and vendor performance, equity/accessibility review |
| `G6 Marketplace discovery` | Future, separate specification | Marketplace governance, provider ranking/matching, cross-practice consent/enrollment, commercial/legal model; all practice workflow gates retained |

Development may begin on `G1` after `G0`. No real patient care may occur before `G4`, and no external live delivery before the relevant `G3` adapter passes.

### 1.1 Current gate status

On 2026-08-26, the project owner approved the product scope, architecture, recommended first vertical slice, provisional clinical framework, backlog creation, engineering-safeguard design and low-fidelity wireframes for development planning. [Decision 0001](decisions/0001-g0-development-baseline.md) is the controlling record.

This records the project decision at `G0`; it does not assert that the project owner substitutes for the qualified clinical, state-law, privacy, security, billing, accessibility, credentialing or vendor approvals required before the applicable `G2`–`G4` exit. It also does not close the repository's separate [Phase 2 exit gate](../phase-2/phase-2-exit-gate.md), which currently prevents application, database, deployment, test and runtime implementation. The backlog and Sprint 1 plan are therefore ready for estimation, but implementation remains blocked until that repository-level gate is closed or a compliant scoped exception is recorded.

## 2. Pilot controls

- One named practice/provider entity and approved branded domain.
- Adults only; physicians only; explicitly approved low-acuity pathways.
- Limited staffed hours and bounded queue/capacity with intake cutoff.
- State enabled separately only after its counsel/clinical/license/content pack is complete.
- Payers/products enabled only after exact network/financial workflows are verified; transparent self-pay may be the safer first financial route.
- Controlled substances, recording, autonomous diagnosis/coding/submission, and marketplace disabled by invariant plus configuration.
- Daily clinical/operational review during early pilot; low threshold to pause a pathway, state, clinician, payer or vendor.
- Practice downtime/telephone/emergency and in-person referral capacity confirmed for each operating window.

## 3. Success and guardrail metrics

All metrics use defined numerator/denominator, source, delay, exclusions, privacy threshold, owner, target, alert, and review cadence. They are segmented where safe by practice/state/pathway/patient type/language/accessibility need/network route and monitored for disparity.

### Access and operations

- eligible request completion rate and time by intake stage;
- practice review time, queue wait median/p90/p95, estimate accuracy band, abandonment/expiry;
- percentage of operating time with an eligible physician and reason for no capacity;
- reservation decline/reassignment/lease-expiry and patient reconnect rates;
- new-patient identity/manual-review/duplicate-link outcomes and redress time.

### Clinical safety and quality

- emergency and urgent-in-person routing rate;
- clinical-review rate/time/outcome and protocol reason distribution;
- deterioration while waiting, physician redirection, technical-abort and post-visit escalation;
- 24/72-hour ED/urgent-care report and adverse-event review where law/consent/data allow;
- follow-up/order closure, incomplete chart/AVS and prescription callback rates;
- antibiotic/prescribing/pathway quality measures approved by medical director;
- false-negative/false-positive findings from sampled clinical review, never inferred solely from utilization.

### Coverage/financial/integration

- eligibility and exact-network confirmation/manual/unknown rates and latency;
- estimate versus adjudicated patient responsibility where comparable;
- prescription and claim transport/business acceptance, retry, duplicate, rejection/denial, acknowledgment and reconciliation age;
- claims clean-acceptance and correction rate without incentivizing upcoding;
- patient financial complaints and incorrect network/coverage representations.

### Experience, accessibility, privacy and reliability

- task success, support contact, cancellation and satisfaction using non-coercive measures;
- accessibility defects/accommodation failures, device/browser/video failure and low-bandwidth completion;
- authorization denials/incidents, identity-link corrections, PHI exposure, audit gaps;
- SLO/error budget, outage/degraded-mode minutes, RPO/RTO exercises and runbook performance.

Guardrails override growth/conversion metrics. No team is rewarded for lowering appropriate emergency/in-person routing, issuing prescriptions, raising billed level, reducing manual identity review, or claiming network confirmation without evidence.

## 4. Risk register

| Risk | Consequence | Preventive controls | Trigger/response owner |
|---|---|---|---|
| Under-triage of emergency/complex illness | Patient harm/delay | Universal screen, unknown->review, protocol safety cases, deterioration path, clinician authority, monitoring | Any credible case pauses pathway; medical director |
| Admin clinical override | Unsafe/illegal decision | Permission separation, immutable assessments, server readiness gates, audit | Attempt/defect disables authorization action; clinical/compliance |
| Wrong-state or unlicensed treatment | Regulatory/patient harm | Dual location confirmation, effective credential facts, atomic matcher/start gate | Mismatch stops consult/new matches; credentialing/legal |
| Wrong patient/duplicate chart link | Privacy/clinical harm | Prospective record, independent proofing, hidden duplicate review, atomic promotion, redress | Suspected mislink revokes access/freezes promotion; HIM/privacy |
| Coverage/network misrepresentation | Financial harm/trust | Two gates, exact product/entity evidence, timestamp/expiry, no-guarantee/GFE | Incorrect confirmation disables source/rule; billing/compliance |
| Queue unfairness/cherry-picking | Access disparity | FIFO ready time, no browse/pick, reasoned priority/decline, disparity review | Threshold breach prompts staffing/clinical review; operations/medical director |
| Video failure/privacy/recording | Incomplete care/PHI exposure | BAA/provider review, no recording, grants, waiting-room isolation, fallback/disposition | Outage/privacy signal stops joins; security/clinical operations |
| Unsafe/duplicate prescription | Medication harm | Non-controlled block, reconciliation/alerts, signature, idempotency, pharmacy confirmation | Controlled/duplicate attempt pages clinical/security; pharmacy owner |
| Wrong/duplicate claim or false status | Financial/compliance harm | Signed source, human approval, X12 semantics, idempotency/version/reconciliation | Submission source disabled/quarantined; revenue cycle |
| Cross-practice/IDOR exposure | Breach | Server tenant/resource auth, DB constraints, opaque IDs, tests/alerts | Confirmed exposure invokes incident/breach process; security/privacy |
| Vendor outage/bad response | Stuck/incorrect workflow | Adapter semantics, timeout/bulkhead, durable state, degraded mode, reconciliation | SLO/semantic threshold trips circuit/kill switch; integration owner |
| Regulatory/payer rule drift | Noncompliant care/billing | Quarterly monitoring, versioned rules/sources/review dates, deny-by-default | Material change pauses affected scope; legal/billing/clinical |
| Accessibility/digital exclusion | Denied/delayed access | WCAG 2.2 AA, user studies, alternatives/accommodations, slow-network support | Critical journey barrier blocks/pauses release; accessibility owner |
| Insufficient capacity/long wait | Deterioration/abandonment | Hours/cutoff/capacity, wait bands, stale rescreen, safe alternatives | Queue/wait threshold closes intake/alerts staffing; operations |
| Incomplete documentation/follow-up | Clinical/legal harm | completeness gate, wrap-up lock, follow-up work queue/escalation | Overdue critical work escalates and limits new work; medical director |
| Data loss/audit gap | Unsafe recovery/noncompliance | transactions/outbox, backups, integrity monitoring, restore tests | Invariant/audit gap stops affected writes; engineering/security |

## 5. Open decisions and owners

These do not prevent foundation engineering where interfaces remain configurable, but must close by the named gate.

| Decision | Default in this spec | Owner | Required by |
|---|---|---|---|
| Exact initial pathways and wording | Candidate list only | Medical director | G2 |
| Physician qualifications per pathway | Active state authority + practice privileges | Medical director/credentialing | G2 |
| Patient IAL/AAL and proofing vendor | Risk-assessed provisional AAL2/IAL2-compatible approach | Security/privacy/HIM | G1 design; G3 vendor |
| Practice legal structure and state wording | Practice is provider; AvenChart technology/admin | State counsel/practice | G0/G4 per state |
| Florida provider type (licensed vs registered) | Both modeled, individually enabled | Practice/legal/credentialing | G4 FL |
| Retention matrix, especially Florida/exceptions | Longest applicable, configurable | Counsel/HIM/privacy | G1 schema; G4 values |
| Audio-only fallback | Deny by default | Medical director/legal/payer | G4 |
| Live video/identity/eligibility/network/eRx/claim vendors | Vendor-neutral ports only | Procurement/security/engineering | G3 |
| Initial payer/product/network set | Deny by default | Practice contracting/billing | G4 |
| Prices/GFE/cancellation/refund policy | Configurable, no payment in baseline | Practice/billing/legal | G4 |
| Pilot scale/SLO/capacity envelope | Provisional NFR targets | Product/operations/engineering | G0/G4 |
| Interpreter/multi-party video | Need captured; join workflow separately approved | Medical director/accessibility/privacy | G4 if offered |

## 6. Go/no-go approval matrix

| Owner | Must approve |
|---|---|
| Product executive | Scope, non-goals, patient promise, pilot practices/states, success/guardrails |
| Practice medical director | Protocols/safety cases, clinician scope, clinical review, exam sufficiency, emergency/follow-up, quality monitoring |
| Georgia/California/Florida counsel | Licensure/registration, consent/content, corporate practice, prescribing, website, record retention, consumer/financial rules |
| Credentialing | Primary-source evidence, restrictions, privileges, expiry monitoring, coverage |
| Privacy/security officer | HIPAA roles/BAAs/risk analysis, identity, authorization, data flows, vendors, audit, incident/continuity |
| Billing/compliance | Payers/networks, estimates/GFE, coding/POS/modifiers, X12/trading partner, reconciliation |
| Accessibility owner | WCAG 2.2 AA report, documents/video/brand variants, user research and accommodations |
| Engineering/architecture/data | Architecture/API/data/migrations/concurrency/adapters/telemetry/DR, no high defects |
| Operations/support | Hours/capacity/staffing, runbooks/on-call/vendors, patient contact, degraded/downtime mode |
| Practice executive/owner | Provider responsibility, contracts, staff, configuration, financial policy, pilot acceptance |

Approvals reference immutable release/configuration/protocol/content/vendor versions and expire or require re-review after material change.

## 7. Rollback and pause

Pause scope is the narrowest safe of global, practice, state, service/pathway, clinician, payer/product, patient type, or adapter. The response:

1. stops affected new intake/authorization/reservation/transmission;
2. identifies every active request/consultation/work item;
3. assigns clinical continuity and patient communication;
4. preserves records/audit/outbox and prevents unsafe automatic replay;
5. rolls configuration/code back only to a compatible approved version;
6. reconciles vendor and internal state; and
7. requires owner approval and monitoring before reopening.

Code rollback never deletes new clinical records or reverts a completed external transaction. Schema changes remain backward compatible through the rollout window.

## 8. Rollout requirements

| ID | Requirement | Acceptance evidence |
|---|---|---|
| TEL-ROL-001 | Development MUST begin from an approved `G0` baseline with named owners, open-decision dates, and controlled requirement changes. | G0 signatures/change log. |
| TEL-ROL-002 | Real patient care MUST remain disabled until all G4 owner approvals and release evidence reference the exact deployed/configured versions. | Production gate test and signed pack. |
| TEL-ROL-003 | Each state, practice, pathway, payer/product, clinician pool, patient type, modality, and adapter MUST be independently deny-by-default and enableable/pausable. | Configuration/kill-switch tests. |
| TEL-ROL-004 | Clinical/patient safety and privacy/accessibility/financial guardrails MUST take precedence over conversion, queue throughput, prescription, and revenue metrics. | KPI governance/scorecard review. |
| TEL-ROL-005 | Pilot scope MUST be bounded by hours/capacity/staffing and have an explicit intake cutoff, daily review, escalation and pause authority. | Pilot operating plan/drill. |
| TEL-ROL-006 | No external adapter may become live before environment isolation, BAA/security, standard/trading-partner, semantic, failure and reconciliation certification. | G3 per-adapter evidence. |
| TEL-ROL-007 | No clinical protocol may become live before medical-director safety case, fixtures, content/accessibility, state applicability, monitoring and rollback approval. | Protocol publication gate. |
| TEL-ROL-008 | Go-live MUST have current credential/license, practice contract, emergency/referral, consent/content, payer/network/price and vendor evidence. | Freshness report. |
| TEL-ROL-009 | Metrics MUST have governed definitions, privacy thresholds, disparity segments, owners, targets and actions; raw counts without denominators cannot establish safety. | Metric catalog validation. |
| TEL-ROL-010 | Adverse events, emergency under-triage, identity mislink, cross-tenant exposure, controlled-drug path, duplicate external transaction, false network/claim status, and critical accessibility barrier MUST have immediate pause criteria. | Trigger/alert/runbook exercises. |
| TEL-ROL-011 | Patients and staff MUST receive accurate degraded/paused/rollback communication and safe alternatives for every affected active state. | Continuity communication test. |
| TEL-ROL-012 | Expansion requires evidence from prior stage plus revalidation of capacity, disparities, legal/payer/configuration, vendor performance, support, and unresolved actions. | G5 review package. |
| TEL-ROL-013 | Feature success MUST include clinical safety/quality, access, equity/accessibility, privacy/security, reliability, financial accuracy and patient experience—not volume alone. | Executive scorecard. |
| TEL-ROL-014 | The future marketplace MUST be a separately approved specification and MUST reuse, not bypass, practice enrollment, identity, triage, licensure, consent, financial and queue gates. | G6 architecture/product review. |
| TEL-ROL-015 | All temporary waivers/exceptions MUST name scope, risk acceptance owner, compensating controls, expiry and removal plan; none may waive a core safety invariant. | Exception register audit. |
| TEL-ROL-016 | After pilot, an independent review MUST verify outcomes, incidents, disparities, complaints, vendor/financial semantics and requirement evidence before expansion. | Independent G5 recommendation. |
