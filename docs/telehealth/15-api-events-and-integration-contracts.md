# API, events, and integration contracts

## 1. Contract conventions

- Base path: `/api/telehealth/v1`; existing patient portal identity/session routes remain reusable.
- JSON uses explicit enums and ISO 8601 UTC timestamps. Unknown values are not empty strings.
- Mutating requests require `Idempotency-Key` where replay is plausible and `If-Match`/expected aggregate version for stateful updates.
- Successful commands return the authoritative aggregate projection, version, permitted actions, blocking reasons, and correlation ID, or `202 Accepted` with a durable operation/work-item location.
- Errors use `application/problem+json` with stable `type`, `title`, HTTP `status`, safe `detail`, `code`, `correlationId`, optional field `errors`, `currentVersion`, and `recoveryAction`. No PHI or vendor secret appears in an error.
- Browser clients never supply authoritative `practice_id`, patient link, role, SignalR group, price, triage outcome, network result, clinician eligibility, or destination.
- OpenAPI, examples, and consumer-driven contracts are version controlled and checked for compatibility.

## 2. Endpoint catalog

Exact request schemas are generated from the logical data contracts in the linked specifications. The endpoint/application-command boundaries below are normative.

### 2.1 Public brand and availability

| Method/path | Purpose | Authorization |
|---|---|---|
| `GET /api/telehealth/v1/public/context` | Resolve host to approved practice brand, public services/states/hours/notices/emergency content | Anonymous; host/origin bound; cache contains no PHI |
| `GET /api/telehealth/v1/public/availability` | Coarse service availability (`open`, `closed`, `capacity-limited`, `unavailable`) | Anonymous; rate limited; no queue/clinician counts |

### 2.2 Consumer request

| Method/path | Purpose |
|---|---|
| `POST /requests` | Create practice-bound request/prospective context after account/session and age/location prerequisites |
| `GET /requests/{requestId}` | Get authoritative patient projection, version, gates, permitted actions, status |
| `PUT /requests/{requestId}/location` | Attest current location/callback; re-evaluate invalidation |
| `POST /requests/{requestId}/safety-screen` | Submit universal safety answers and evaluate immediately |
| `POST /requests/{requestId}/intake` | Submit versioned complaint/intake snapshot and triage command |
| `POST /requests/{requestId}/clarifications` | Answer a clinician-requested bounded clarification |
| `PUT /requests/{requestId}/demographics` | Submit/confirm minimized demographics and contacts |
| `POST /requests/{requestId}/identity-proofing` | Begin/complete approved proofing operation without exposing vendor secrets |
| `PUT /requests/{requestId}/clinical-summary-confirmation` | Confirm medication/allergy/history summary versions |
| `POST /requests/{requestId}/consents` | Accept exact consent/notice package version |
| `PUT /requests/{requestId}/coverage` | Select/enter/confirm coverage candidate or self-pay intent |
| `POST /requests/{requestId}/coverage/verify` | Start idempotent eligibility/network operation |
| `GET /requests/{requestId}/estimate` | Retrieve current financial estimate/unknowns/expiry |
| `POST /requests/{requestId}/financial-acknowledgments` | Accept exact estimate/financial route |
| `PUT /requests/{requestId}/technology-readiness` | Store device/fallback/accessibility readiness result |
| `POST /requests/{requestId}/submit` | Request practice review after server completeness validation |
| `POST /requests/{requestId}/cancel` | Cancel/leave with approved patient reason |
| `POST /requests/{requestId}/symptoms-worsened` | Immediately create fresh safety assessment path |
| `GET /requests/{requestId}/status` | Lightweight polling projection using ETag/version |
| `POST /requests/{requestId}/video-grants/patient` | Issue current short-lived patient grant after assignment |
| `GET /requests/{requestId}/after-visit-summaries/latest` | Retrieve authenticated current AVS/version |

### 2.3 Practice operations

| Method/path | Purpose |
|---|---|
| `GET /operations/requests` | Filtered practice work queue; permission/resource scoped |
| `GET /operations/requests/{requestId}` | Operational evidence projection; clinical answers read-only/minimum necessary |
| `POST /operations/requests/{requestId}/request-information` | Send bounded missing-information request |
| `POST /operations/requests/{requestId}/hold` / `release-hold` | Apply/release reasoned operational hold |
| `POST /operations/requests/{requestId}/authorize` | Atomic final-gate validation/promotion/ready queue command |
| `POST /operations/requests/{requestId}/decline` | Reasoned operational decline and patient communication |
| `POST /operations/requests/{requestId}/clinical-review` | Route to clinical reviewer; not a clinical decision |
| `GET/POST /governance/...` | Versioned configuration/protocol/consent/payer-rule draft, validate, preview, approve, publish, retire |

### 2.4 Clinical review and physician

| Method/path | Purpose |
|---|---|
| `GET /clinical-reviews` | Qualified reviewer queue |
| `POST /clinical-reviews/{requestId}/decision` | Add reasoned new assessment/outcome |
| `POST /clinician-shifts` | Enter a validated practice/service shift |
| `POST /clinician-shifts/{shiftId}/pause` / `resume` / `end` | Manage availability without abandoning active care |
| `POST /clinician-queue/reserve-next` | Atomic next-eligible reservation; no browse/pick |
| `POST /reservations/{reservationId}/heartbeat` / `release` | Renew/release lease with token/version/reason |
| `GET /consultations/{requestId}/context` | Purpose-limited physician chart/intake projection |
| `POST /consultations/{requestId}/prepare` | Create/link encounter and video intent |
| `POST /consultations/{requestId}/video-grants/physician` | Issue current short-lived physician grant |
| `POST /consultations/{requestId}/start` | Record all clinical start checks and start encounter |
| `PUT /consultations/{requestId}/documentation` | Versioned draft/autosave |
| `POST /consultations/{requestId}/disposition` | Close media/clinical disposition safely |
| `POST /consultations/{requestId}/finalize` | Validate/sign/finalize; create AVS/follow-up/claim work |
| `POST /consultations/{requestId}/amendments` | Signed append-only correction |
| `GET/POST /consultations/{requestId}/prescriptions...` | Draft, safety-check, sign, cancel/change status |

### 2.5 Financial/integration work

| Method/path | Purpose |
|---|---|
| `GET /billing/claims` / `GET /billing/claims/{claimId}` | Claim work queue and version/history |
| `POST /billing/claims/{claimId}/scrub` | Deterministic validation under rule version |
| `POST /billing/claims/{claimId}/approve` | Human approval |
| `POST /billing/claims/{claimId}/submit` | Queue durable gateway command |
| `POST /billing/claims/{claimId}/correct|replace|void` | Governed linked version workflow |
| `POST /billing/claims/{claimId}/status-inquiry` | Queue 276 inquiry when supported |
| `GET /integrations/work-items` | Authorized quarantine/rejection/reconciliation queue |
| `POST /integrations/work-items/{id}/retry|resolve` | Reasoned recovery; never edit historical payload |

### 2.6 Provider callbacks

Callbacks use adapter-specific paths outside browser auth, mTLS/signature as supported, replay windows, destination allowlists, body-size limits, raw-body signature validation, idempotent event IDs, and asynchronous processing:

- `POST /api/telehealth/v1/callbacks/video/{provider}`
- `POST /api/telehealth/v1/callbacks/eprescribing/{provider}`
- `POST /api/telehealth/v1/callbacks/claims/{tradingPartner}`
- `POST /api/telehealth/v1/callbacks/eligibility/{provider}`

An accepted callback returns promptly after durable inbox persistence. Domain application services process validated normalized events after commit.

## 3. Domain event envelope

```json
{
  "eventId": "uuid",
  "eventType": "telehealth.request.queued.v1",
  "occurredAt": "2026-08-26T14:00:00Z",
  "recordedAt": "2026-08-26T14:00:00Z",
  "aggregateType": "TelehealthRequest",
  "aggregateId": "uuid",
  "aggregateVersion": 12,
  "practiceId": "uuid",
  "correlationId": "uuid",
  "causationId": "uuid",
  "actor": { "type": "workforce", "id": "opaque", "purpose": "operations" },
  "schemaVersion": 1,
  "data": {}
}
```

Payloads contain minimum necessary data and references. Consumers deduplicate by `eventId`, order by aggregate/version, detect gaps, and fetch current state. Events are facts in past tense: `request.queued`, `reservation.expired`, `consultation.started`, `prescription.signed`, `claim.submission-queued`. A requested command is not mislabeled as a completed fact.

## 4. Interoperability projection

The internal workflow is not forced into FHIR where FHIR does not model queue control. A versioned projection uses HL7 FHIR R4 (4.0.1) and the approved US Core release for exchange:

| Domain | Candidate FHIR representation |
|---|---|
| Patient/practice/clinician/location | US Core Patient, Organization, Practitioner/PractitionerRole, Location |
| Coverage | Coverage; eligibility exchange may use CoverageEligibilityRequest/Response where a FHIR partner supports it, without replacing required X12 semantics |
| Intake/triage | Questionnaire/QuestionnaireResponse plus Provenance; clinical result may use Observation/RiskAssessment only with an approved profile |
| Consent | Consent plus retained rendered legal artifact/provenance |
| Request/queue | Appointment/Task projections with AvenChart extensions only after profiling; internal aggregate remains authoritative |
| Consultation | Encounter, clinical notes/DocumentReference, Condition, Observation, ServiceRequest, DiagnosticReport as applicable |
| Prescription | MedicationRequest projection; NCPDP SCRIPT remains the pharmacy transaction standard |
| Claim | Claim/ExplanationOfBenefit projection for API exchange where supported; X12 remains the HIPAA transaction path |
| Audit | AuditEvent/Provenance projections, with internal audit evidence retained |

SMART App Launch 2.2 is the target authorization profile for future third-party FHIR applications. It delegates an already-authorized scope; it does not create the underlying patient/practice authorization. CapabilityStatement and profile conformance must be explicit and validation-tested.

## 5. API and event requirements

| ID | Requirement | Acceptance evidence |
|---|---|---|
| TEL-API-001 | Every endpoint MUST declare authentication, resource authorization, practice/context derivation, validation, idempotency, concurrency, audit, response, and failure contracts in OpenAPI. | OpenAPI conformance test. |
| TEL-API-002 | Mutating APIs MUST accept a semantic idempotency key where replay is possible and reject key reuse with different normalized content. | Replay/conflict tests. |
| TEL-API-003 | State-changing APIs MUST enforce expected aggregate version and return a recoverable conflict with authoritative version/actions. | Stale-client tests. |
| TEL-API-004 | Problem Details MUST use stable non-PHI codes/types and correlation IDs; internal exceptions/vendor payloads MUST not leak. | Error-snapshot/security tests. |
| TEL-API-005 | Practice, patient, request, role, price, outcome, eligibility, and destination authority MUST derive server-side, not from trusted client fields. | Parameter-tampering tests. |
| TEL-API-006 | Callbacks MUST validate raw-body authenticity, freshness, replay, event ID, body/schema, provider/destination, and persist to a durable inbox before processing. | Forgery/replay/duplicate tests. |
| TEL-API-007 | Events MUST be immutable, uniquely identified, schema-versioned, causally correlated, minimum necessary, and ordered per aggregate version. | Event contract tests. |
| TEL-API-008 | Consumers MUST tolerate duplicate/out-of-order events, detect version gaps, and reconcile from authoritative APIs. | Event chaos tests. |
| TEL-API-009 | Transport and business outcomes MUST use separate fields/events and accurate HTTP semantics; `202`/`200` cannot imply external acceptance. | Semantic contract tests. |
| TEL-API-010 | Breaking changes require a new API/event version and migration period; additive optional fields remain backward compatible. | Compatibility diff gate. |
| TEL-API-011 | FHIR exports MUST validate against declared FHIR R4/US Core profiles and include CapabilityStatement, provenance, paging/error behavior, and authorization tests. | Official validator/Inferno-style contract report as applicable. |
| TEL-API-012 | SMART scopes MUST be least-privilege and layered on resource authorization; possession of a SMART token MUST NOT bypass practice/patient constraints. | SMART authorization tests. |
| TEL-API-013 | Rate limits, body/file limits, timeouts, pagination/cursors, cache policy, and safe retry guidance MUST be specified per endpoint class. | Gateway/API policy tests. |
| TEL-API-014 | API examples and stubs MUST use synthetic data and visibly state environment/capability; production must reject test credentials/routes. | Artifact and startup tests. |

