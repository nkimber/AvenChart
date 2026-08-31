# Workflows and state machines

## 1. Design rules

State transitions are server-authoritative domain commands, not arbitrary status updates. Every transition records aggregate ID, prior/new state, reason code, actor/service identity, practice context, patient location state, correlation ID, timestamp, and aggregate version. Invalid transitions return a stable Problem Details response and do not partially mutate related aggregates.

## 2. Telehealth request lifecycle

### 2.1 States

| State | Meaning | Owner/next action |
|---|---|---|
| `Draft` | Request shell exists; required data incomplete | Patient |
| `SafetyScreening` | Current location/callback captured; emergency and complaint screening active | Patient/system |
| `EmergencyRedirected` | Emergency rule matched or patient requested emergency help | Terminal for telehealth; patient receives emergency route |
| `InPersonRecommended` | Presentation requires timely physical evaluation | Terminal for this request; provide level/time guidance |
| `Unsupported` | Service/protocol does not support the presentation | Terminal; show alternative care route |
| `ClinicalReview` | Deterministic rules require physician judgment before eligibility | Qualified clinical reviewer |
| `Intake` | Clinically eligible; confirmations, consent, history, and technology pending | Patient |
| `Verification` | Identity, duplicate, eligibility/network, or price evidence pending | System/practice staff |
| `OperationalReview` | All automatable checks returned; staff must resolve exceptions/authorize | Practice administrator |
| `Ready` | Clinical and operational gates passed in one transaction; `ready_at` set | Queue coordinator |
| `Queued` | Visible in clinician queue and patient queue status | Matcher |
| `Reserved` | One eligible physician holds a time-limited lease | Physician |
| `Connecting` | Parties are in waiting-room/device/join flow | Patient/physician/video adapter |
| `InConsultation` | Physician has started the clinical encounter | Physician |
| `WrapUp` | Media ended; documentation/orders/disposition incomplete | Physician |
| `PostVisit` | Signed/finalized; after-visit and financial work being delivered | System/billing |
| `Completed` | Required patient delivery and work-item creation succeeded or has accepted recoverable ownership | Terminal success |
| `Declined` | Practice declined for an approved non-clinical or clinical-review reason | Terminal; notify with alternative path |
| `Canceled` | Patient or authorized practice action canceled before completion | Terminal |
| `Abandoned` | Draft expired without required completion | Terminal |
| `Expired` | Time-sensitive accepted request exceeded policy and requires a new request | Terminal |
| `Failed` | Unrecoverable platform failure after compensation/manual ownership | Terminal exception; never silent |

### 2.2 Permitted transitions

```text
Draft -> SafetyScreening
SafetyScreening -> EmergencyRedirected | InPersonRecommended | Unsupported
SafetyScreening -> ClinicalReview | Intake
ClinicalReview -> EmergencyRedirected | InPersonRecommended | Unsupported | Intake | Declined
Intake -> Verification -> OperationalReview -> Ready -> Queued
Queued -> Reserved -> Connecting -> InConsultation -> WrapUp -> PostVisit -> Completed

Draft/SafetyScreening/Intake/Verification/OperationalReview/Ready/Queued/Reserved/Connecting
  -> Canceled | Abandoned | Expired (where policy allows)

Reserved/Connecting -> Queued (lease release, still fresh and safe)
InConsultation -> WrapUp (including incomplete/aborted clinical dispositions)
PostVisit -> Completed | Failed
```

An emergency or in-person condition discovered after intake does not mutate the historical triage result. A new assessment is appended and the request transitions to the appropriate safe disposition. Once `InConsultation` begins, the encounter must be closed with a clinical disposition even if media fails.

## 3. Readiness gates

The `AuthorizeForQueue` command re-reads all authoritative values in one transaction. It succeeds only if:

- practice, state, service, protocol, and operating window are enabled;
- age and confirmed current location are supported;
- latest triage outcome is eligible and fresh;
- no unresolved red-flag or clinical-review item exists;
- identity/contact verification and duplicate resolution meet policy;
- required demographic, medication, allergy, and history confirmation is complete;
- all current notice/consent versions are accepted;
- technology requirements or a permitted alternative are satisfied;
- one financial route exists: sufficiently confirmed insurance/network status with acknowledgment, authorized manual exception, or accepted self-pay estimate;
- a canonical patient link/enrollment exists or can be promoted atomically; and
- the request version matches the version reviewed by the administrator.

### 3.1 Scheduling projection

`TelehealthRequest` owns intake, safety, and queue state; it is not a replacement for the EHR appointment. When operational authorization succeeds, the same transaction creates or links one same-day, immediate-telehealth appointment in the existing scheduling system, initially unassigned to a physician. Reservation assigns the eligible physician and scheduled/service timestamps; consultation start marks the scheduling record in progress/arrived according to the approved local status mapping; completion fulfills it. Pre-consult cancellation, redirect, expiry, abandonment, or practice decline sets the corresponding canceled/not-completed reason without mislabeling the patient as a no-show. The request, appointment, and eventual encounter are one-to-one for the initial release and retain independent lifecycle histories.

## 4. Queue and reservation lifecycle

Queue entries are ordered by `clinical_priority`, then `ready_at`, then a stable opaque request key. The default priority is equal for all eligible low-acuity requests. Only a qualified clinical rule/reviewer may set a different priority, with an approved code and rationale.

`Queued -> Reserved` uses an atomic conditional update (or `SELECT ... FOR UPDATE SKIP LOCKED`) with request version, eligibility recheck, clinician ID, and lease expiration. Before a clinician reserves it, the authenticated established patient or exact prospective-applicant access-key owner may withdraw a ready queued request through separately authorized commands. Their transaction locks the request, queue entry, and provisional appointment; the entry becomes removed and the appointment is cancelled, while scheduling facts remain retained. Applicant withdrawal remains a minimized synthetic access-key flow, not a patient-portal session. A heartbeat may renew only while the physician session and shift remain valid. On lease expiry, the server records the cause and requeues the request if clinical/location/consent/financial freshness still holds; otherwise it routes to review.

Patient position is calculated from eligible entries ahead of the patient, not exposed identifiers. It is labeled approximate. A wait band should combine configured service capacity and recent completed wait distributions; it must degrade to “wait estimate unavailable” rather than invent precision.

## 5. Clinician shift lifecycle

```text
Offline -> Available -> Busy -> WrapUp -> Available
Available -> Paused -> Available
Available | Paused | WrapUp -> Offline
Busy -> PausedAfterVisit (effective after safe encounter closure)
```

- `Available` requires current authorization evidence and realtime presence.
- `Busy` is tied to at most one active reserved/connecting/in-consultation request in the initial release.
- Going offline never abandons an active patient. It prevents a new offer and requires disposition/reassignment of current work.
- Credential expiry, restriction, practice disablement, or security revocation immediately prevents new reservations and invokes a controlled response for active sessions.

## 6. Consultation lifecycle

```text
NotCreated -> Prepared -> Started -> MediaEnded -> DocumentationPending -> Signed -> Finalized
Prepared -> Void (consultation never began)
Started -> AbortedWithDisposition (technical, safety, patient-left, clinician-left)
Signed -> Amended (append-only correction with reason and signature)
```

`Started` requires physician and patient identity checks, location reconfirmation, consent, and an active reservation. Video connection alone does not start a consultation. `Finalized` requires the documentation checklist in the consultation specification and creation of after-visit/billing work items.

## 7. External transaction lifecycle

Each eligibility, network, pharmacy directory, e-prescription, and claim exchange has two related states:

1. **Transport:** `Prepared -> Queued -> Claimed -> Sent -> TransportAcknowledged | RetryScheduled | Quarantined | Canceled`.
2. **Business:** `Pending -> Accepted | Rejected | PartiallyAccepted | AdditionalInformationRequired | Unknown`.

A transport acknowledgment never implies business acceptance. Attempts are append-only. Retries reuse the semantic idempotency key where the protocol permits it, and payload corrections create a new business version linked to the original.

## 8. Workflow requirements

| ID | Requirement | Acceptance evidence |
|---|---|---|
| TEL-WF-001 | Each aggregate MUST expose a machine-readable current state, monotonic version, permitted actions, blocking reasons, and last authoritative update time. | API contract and concurrency tests. |
| TEL-WF-002 | Transition commands MUST be idempotent by semantic command key and reject reuse with different content. | Idempotency replay/conflict tests. |
| TEL-WF-003 | Clinical, operational, financial, and technical states MUST remain distinct; a client MUST NOT infer one from another. | Schema/contract review and UI mapping tests. |
| TEL-WF-004 | `ready_at` MUST be assigned once when all gates first pass and MUST NOT be rewritten by ordinary holds, refreshes, or reconnects. | Queue ordering tests. |
| TEL-WF-005 | A material clinical-data change MUST invalidate affected triage/readiness evidence and return the request to the earliest required safe state. | Dependency invalidation tests. |
| TEL-WF-006 | Location, triage, eligibility, network, consent, credential, and price evidence MUST have explicit freshness rules; expiration MUST block or re-review rather than silently refresh authority. | Time-travel tests. |
| TEL-WF-007 | The administrator acceptance command MUST be atomic with final gate validation, patient promotion/linkage when required, immediate-telehealth appointment and queue-entry creation, and audit/event creation. | Transaction rollback/failure tests. |
| TEL-WF-008 | Reservation MUST be atomic, practice-scoped, eligibility-aware, version-checked, leased, and recoverable. | High-concurrency and crash-recovery tests. |
| TEL-WF-009 | Cancellation and decline MUST record initiator, approved reason, patient-facing reason category, refund/financial implications, and alternate-care communication. | Lifecycle and audit tests. |
| TEL-WF-010 | An emergency result MUST be terminal for the current telehealth request; continuing requires a new request and fresh screening. | Negative transition tests. |
| TEL-WF-011 | Once a consultation starts, media failure or participant departure MUST produce a physician-owned clinical disposition and documented follow-up. | Failure scenario test. |
| TEL-WF-012 | State history MUST be append-only; corrections are new events and cannot delete or rewrite prior events. | Database constraint and audit tests. |
| TEL-WF-013 | Patient status delivery MUST tolerate duplicates and out-of-order messages by using aggregate version and event ID. | Realtime/poll reconciliation tests. |
| TEL-WF-014 | Every recoverable state MUST identify an accountable queue/role, retry/expiry time, and safe patient-facing message. | Operational state audit. |
| TEL-WF-015 | Global/practice/state kill switches MUST stop new intake or queueing as configured while allowing safe completion, reassignment, or clinical closure of active consultations. | Controlled-drain exercise. |
