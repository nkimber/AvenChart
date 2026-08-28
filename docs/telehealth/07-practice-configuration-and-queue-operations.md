# Practice configuration and queue operations

## 1. Practice-branded entry

A `PracticeBrand` is resolved from an allowlisted, verified host or signed embed configuration. It contains practice legal/display name, logo/theme assets, contact methods, privacy/terms links, accessibility contact, emergency content, supported languages, service states/hours, pricing presentation, and an explicit platform attribution. Brand configuration is presentation data; it cannot change semantic safety colors, hide mandated content, reduce contrast, or alter clinical/legal text outside approved slots.

Custom domains require ownership verification, managed TLS, anti-takeover controls, CSP/frame-ancestor configuration, cookie isolation, and a controlled disabled/error page. An embed must bind the parent origin and practice and must not allow a query string to select another practice.

## 2. Governed practice configuration

| Configuration | Required scope/evidence |
|---|---|
| Service catalog | Complaint pathway, state, patient type, modality, physician qualifications, hours, capacity, price route, follow-up, effective dates, medical-director approval |
| Clinician pool | Practice relationship, privileges, state authority, payer/network participation, service competency, availability, effective dates, credentialing evidence |
| Clinical protocols | Versioned content/rules per triage specification and approval |
| Operating hours/capacity | Time zone, holidays, intake cutoff, max queue, max expected wait, clinician capacity, closure message, graceful drain |
| Financial policy | Payers/products/networks, billing/rendering entity, price, self-pay/GFE, manual exceptions, refund/cancellation terms, payer-rule versions |
| Communications | Approved SMS/email/push templates, sender identity, opt-in, language, PHI minimization, retry/escalation |
| Pharmacy/prescribing | Allowed non-controlled prescription services, formulary display policy, pharmacy directory sources, follow-up |
| Emergency/referral directory | 911/988 content, local ED/urgent care/practice contacts where approved, state/location applicability, last validation |
| Feature/kill switches | Global/practice/state/service/protocol/vendor scope, reason, owner, start/end, drain behavior |

Publishing configuration requires schema validation, semantic validation, impact preview, two-person approval for clinical/legal/financial controls where specified, effective scheduling, immutable history, and rollback to a previously approved version.

## 3. Administrative work queue

The queue displays only what the administrator needs:

- request age and safe contact indicator;
- state/service/patient type and clinical outcome (not editable);
- identity/link/duplicate state;
- demographic/contact confirmation state;
- consent and notice state;
- eligibility, network, estimate, and acknowledgment state with evidence time;
- technology/accessibility/interpreter readiness;
- blocking reasons, owner, next action, freshness/expiry; and
- patient contact history and approved actions.

Actions are `RequestInformation`, `PlaceOperationalHold`, `ReleaseHold`, `AuthorizeForQueue`, `DeclineOperationally`, `CancelForPracticeClosure`, and `EscalateToClinicalReview`. Every action uses a reason code, optional bounded note, optimistic concurrency, resource authorization, and audit. Notes must not be used as an unstructured workaround for medical decision-making.

## 4. Clinician queue and matching

The patient is already scoped to a practice. The matcher selects the oldest eligible request by ready time after applying:

1. patient current state and confirmed location;
2. request/practice/service/protocol availability and freshness;
3. clinician active shift/presence and single-active-request limit;
4. active license/registration/credential/privilege for patient state and service;
5. language/interpreter and accessibility capability where required;
6. payer/product/network participation when the request depends on in-network service, or an accepted alternate financial route;
7. clinical conflict/restriction, such as self/family or practice-defined continuity restriction; and
8. deterministic ordering and atomic reservation.

The physician sees a next-patient summary only after reservation. The initial release does not provide a browse-and-pick list. A decline releases the lease using an approved reason and is monitored for cherry-picking, access disparities, and capacity issues.

## 5. Patient queue experience

Patient-facing states use calm, precise language:

- `Reviewing your request`
- `Your practice accepted your request`
- `You're in line — approximately N requests ahead` or an approved wait band
- `A physician is getting ready`
- `Check your camera and microphone`
- `Your visit is ready`
- `Reconnect to your visit`
- `Your visit is complete`

The screen includes last-updated time, connection state, refresh/retry, notification preference, cancel/leave action, worsening-symptoms action, emergency guidance, and what the patient should do if the estimate exceeds service hours. It never exposes other patients, physician workload, or a physician identity until assignment policy allows it.

## 6. Practice and queue requirements

| ID | Requirement | Acceptance evidence |
|---|---|---|
| TEL-PRA-001 | Host/embed resolution MUST bind exactly one enabled practice before PHI and fail closed for unverified or ambiguous mappings. | Domain/origin/tenant tests. |
| TEL-PRA-002 | Brand customization MUST be constrained so required provider, emergency, privacy, cost, state, and accessibility content and WCAG semantics cannot be removed. | Brand-schema and visual/accessibility tests. |
| TEL-PRA-003 | Practice configuration MUST be typed, scoped, versioned, approved, effective-dated, audited, impact-previewed, and safely rollable back. | Configuration publication tests. |
| TEL-PRA-004 | The system MUST prevent configuration combinations that have no safe clinician, protocol, financial route, emergency content, or operating policy. | Semantic validation tests. |
| TEL-PRA-005 | The administrative queue MUST show gate evidence/freshness and permitted actions without enabling clinical override. | Role/UI/API tests. |
| TEL-PRA-006 | `AuthorizeForQueue` MUST revalidate all gates server-side; client checkmarks or staff assertions are not authority. | Tampering and stale-state tests. |
| TEL-PRA-007 | Clinician matching MUST be practice-scoped, deterministic, eligibility-aware, atomic, and privacy-preserving. | Match matrix/concurrency/tenant tests. |
| TEL-PRA-008 | Default queue order MUST be FIFO by `ready_at`; clinical priority changes require a qualified actor, approved code, rationale, and audit. | Ordering/authorization tests. |
| TEL-PRA-009 | Patient position/wait information MUST be approximate, versioned, explainable, and replaced with honest unavailable/delayed content when confidence is inadequate. | Queue calculation/content tests. |
| TEL-PRA-010 | Realtime delivery MUST have polling/resume fallback and reconcile by server aggregate version. | Disconnect/out-of-order tests. |
| TEL-PRA-011 | Queue capacity, hours, cutoff, expected-wait limit, stale-safety interval, abandonment, and closure behavior MUST be configured per practice/service/state. | Boundary-time and closure tests. |
| TEL-PRA-012 | Patients MUST be notified before foreseeable service closure or excessive delay and offered safe alternatives/cancellation; they MUST not remain indefinitely in a false searching state. | Time-travel patient journey tests. |
| TEL-PRA-013 | Staff contact attempts MUST use approved channels/templates, minimum necessary content, consent/preferences, rate limits, and delivery status. | Communication contract/privacy tests. |
| TEL-PRA-014 | Clinician declines, staff holds, manual exceptions, out-of-order moves, reassignments, cancellations, and queue aging MUST be measurable and reviewed for bias/abuse. | Audit analytics review. |
| TEL-PRA-015 | A kill switch MUST define intake behavior, active-queue behavior, active-consult behavior, patient/staff content, owner, expiry, and recovery plan. | Kill-switch exercise. |
| TEL-PRA-016 | Practice queues, brands, configuration, clinicians, patient relationships, metrics, and events MUST be isolated from every other practice. | Multi-tenant isolation suite. |

## 7. Operational exception policy

Allowed manual exceptions are explicitly enumerated. An exception cannot bypass emergency, jurisdiction, licensure, identity-to-chart authorization, controlled-substance, or consent gates. Financial evidence may be moved to `manual-confirmed` or self-pay only by an authorized role with source, timestamp, scope, and patient acknowledgment. Technology may use an approved fallback only when clinical/state/payer policy permits. All other failures remain blocked.

