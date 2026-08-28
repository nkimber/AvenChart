# Data model, provenance, and retention

## 1. Data modeling principles

- Use UUIDs for new externally referenced aggregates; never expose sequential queue/patient identifiers publicly.
- Every tenant-bearing row has an explicit `practice_id`; child rows must be constrained to the same practice through composite keys or validated transaction boundaries.
- All timestamps use `timestamptz` in UTC; capture patient/practice time zone separately for display and rules.
- Coded values use constrained reference/code tables or check constraints, not arbitrary status strings.
- Mutable aggregates have a monotonic `version` used for optimistic concurrency; histories/events are append-only.
- Published protocols/configuration, accepted consent artifacts, signed clinical records, submitted payloads, acknowledgments, and assessment evidence are immutable.
- Large/raw external payloads and images use encrypted object storage references with integrity checksum and governed access, rather than uncontrolled database/log copies.

## 2. Logical schema

### 2.1 Identity and enrollment

| Entity | Key fields and constraints |
|---|---|
| `consumer_account_link` | `consumer_id`, identity-provider subject/issuer, status, verified contacts; unique issuer+subject |
| `prospective_patient` | `prospective_id`, `practice_id`, minimized demographics, verified contact refs, status, version, expiry; no canonical patient required |
| `identity_proofing_event` | provider/policy/result/evidence refs, risk signals, time, attempt; append-only |
| `patient_match_candidate` | prospective, protected candidate patient, score/reason version; reviewer-only, expiring |
| `patient_identity_decision` | link/create/manual outcome, evidence/policy, actor, reason, prior decision; append-only |
| `practice_enrollment` | practice, canonical/prospective/consumer links, status, effective times, notice/consent scope; unique active practice+patient relationship as policy requires |

### 2.2 Request, intake, triage, and consent

| Entity | Key fields and constraints |
|---|---|
| `telehealth_request` | request UUID, practice, consumer, prospective/patient link, state, service, current location ref, existing immediate-telehealth appointment link after acceptance, workflow status, ready time, version, created/expiry/terminal reason |
| `telehealth_request_event` | prior/new state, action, reason, actor/context, aggregate version, correlation/idempotency, time; unique request+version |
| `telehealth_location_attestation` | normalized address/coordinates only as approved, state, home/other flag, patient attestation, verifier/reconfirmation, time, source, checksum |
| `telehealth_intake_snapshot` | clinical form instance/revision, answer checksum, patient provenance, created/superseded; immutable |
| `telehealth_triage_protocol_version` | scope, content/rule JSON or normalized definitions, checksum, owner/approvals/evidence, effective/review/retire dates, status |
| `telehealth_triage_assessment` | protocol/engine version, answer snapshot, fired-rule evidence, outcome/reasons, actor, time, supersedes; immutable |
| `telehealth_clinical_review` | assessment, eligible reviewer, clarification, decision/rationale, new assessment ref, time |
| `telehealth_consent_evidence` | consent package/version/checksum, practice/state/modality/language, patient action/signature evidence, session/time; immutable |
| `telehealth_confirmation` | typed confirmation key, subject/data version confirmed, actor, time, expiry/supersedes |

### 2.3 Financial prerequisites

| Entity | Key fields and constraints |
|---|---|
| `coverage_candidate_version` | request/patient, payer/product/member/subscriber protected fields, source, patient confirmation, priority, status |
| `eligibility_verification` | query fingerprint, service/date/entities, normalized result, trace/raw ref, source, verified/expiry, version |
| `network_verification` | exact product/billing/rendering/location/service inputs, result/source/evidence, verified/expiry |
| `telehealth_estimate_version` | financial route, inputs/rules, charge/responsibility range, unknowns, content checksum, expiry |
| `financial_acknowledgment` | estimate version/content checksum, route, patient action, time; immutable |

### 2.4 Queue and workforce

| Entity | Key fields and constraints |
|---|---|
| `telehealth_operational_review` | gate snapshot/version, action, approved reason/note, reviewer, time |
| `telehealth_queue_entry` | request, practice/service/state, ready time, clinical priority/reason, status, version; one active entry/request |
| `telehealth_clinician_shift` | clinician, practice/service, state capability set, state, presence/heartbeat, start/end, version |
| `telehealth_clinician_credential` | license/registration/privilege/payer facts, source/evidence, effective/expiry/restrictions, verification |
| `telehealth_reservation` | request/entry/clinician/shift, lease token hash, reserved/heartbeat/expires/released, status, version; unique active request and unique active clinician |

### 2.5 Consultation and downstream

| Entity | Key fields and constraints |
|---|---|
| `telehealth_consultation_context` | request, existing encounter, reservation, confirmed location/consent, modality, start/end/disposition, version; one active/request |
| `telehealth_video_session` | opaque provider/session refs, participant pseudonyms, lifecycle/quality, no media fields |
| `telehealth_communication_item` | session participant/time/type, protected content/object ref, incorporation status, retention/hold; no media transcript |
| `telehealth_follow_up_task` | encounter/order/referral, owner, urgency/due, status/escalation/closure |
| `telehealth_avs_version` | encounter/signed version, structured/rendered checksum, delivery states, supersedes |
| `pharmacy_directory_snapshot` | source/version, identifiers/contact/capabilities, selected origin/distance metadata |
| `electronic_prescription_version` | encounter/prescriber/pharmacy, structured medication, classification/safety/signature, lifecycle, supersedes |
| `professional_claim_version` | encounter/coverage/provider/coding/location/charge/rules, control numbers, lifecycle, supersedes |
| `external_transaction` | adapter/environment/standard/version/destination, semantic key, payload checksum/ref, transport/business state, correlation |
| `external_transaction_attempt` | claim/lease/attempt, request/response refs, status/error category, timing; append-only |
| `telehealth_work_item` | type, aggregate, owner role/user, priority/due/status, reason/evidence, version |

Existing canonical patient, insurance, appointment, encounter, medication/prescription, billing, audit, FHIR, and integration-outbox entities remain linked systems of record. Exact physical table names may follow repository conventions, but semantic separation and constraints above are normative.

## 3. Data classifications

| Class | Examples | Handling baseline |
|---|---|---|
| Restricted clinical/identity | triage answers, diagnoses, location, identity/card images, raw X12/NCPDP, chat/attachments | Strong resource authorization, encryption, no ordinary logs/analytics, restricted support, audited access |
| PHI operational | queue/request status, approximate wait, contact preference, adapter result category | Minimum necessary, authenticated views, opaque IDs, no public cache |
| Security secret | client credentials, signing/encryption keys, webhook secret, session/token material | Approved secret/key manager, non-export where possible, rotation, never database/log/UI |
| Approved public | practice brand, hours, supported states, public contact/emergency content | Versioned publication; no derived availability/queue details that reveal PHI |
| Audit/compliance | access/transition/consent/signature/dispatch evidence | Append-only/tamper-evident controls, restricted access, integrity validation, governed retention |

## 4. Retention and deletion

Retention is rule-driven by record class, practice, patient location/state, program/payer, age/category, last service, legal hold, complaint/investigation, contract, and the longest applicable period. The production matrix requires counsel/HIM approval. Initial design anchors include Georgia's cited 10-year medical-record rule and California's cited at-least-7-year rule, while Florida remains a counsel-confirmed configured value rather than an invented global number.

Record classes include applicant draft, identity evidence, verification artifacts, consent, triage/safety communication, legal encounter/AVS, prescription, claim/financial, integration payload, video metadata, operational chat/attachment, security/audit, and backup copies. Each rule defines start event, minimum/maximum, hold behavior, patient-access implications, purge/anonymize action, and evidence of disposition.

Deletion is an authorized job that first evaluates holds and cross-record dependencies, then deletes/crypto-shreds eligible objects and records a non-PHI disposition certificate. Legal clinical/audit history is not “deleted” through ordinary UI. Backups expire through controlled media lifecycle; deletion promises disclose backup latency accurately.

## 5. Data requirements

| ID | Requirement | Acceptance evidence |
|---|---|---|
| TEL-DAT-001 | Every telehealth record MUST have explicit practice/context ownership and database-enforced relational integrity to prevent cross-practice linkage. | Migration constraints and isolation tests. |
| TEL-DAT-002 | New aggregates MUST use opaque identifiers; public APIs/notifications MUST not expose sequential patient, queue, encounter, or claim keys where avoidable. | Contract enumeration tests. |
| TEL-DAT-003 | Mutable aggregates MUST use monotonic optimistic versions; critical transitions MUST also use database locks/constraints. | Concurrency tests. |
| TEL-DAT-004 | Published, accepted, assessed, signed, submitted, and acknowledged artifacts MUST be immutable and corrected only by linked superseding records. | Constraint/history tests. |
| TEL-DAT-005 | Every consequential derived decision MUST retain input/version/checksum/source evidence sufficient for historical reconstruction. | Replay/reconstruction test. |
| TEL-DAT-006 | Clinical, operational, financial, transport, and business statuses MUST use separate typed fields/entities. | Schema review. |
| TEL-DAT-007 | PHI-bearing raw payloads/images/attachments MUST use protected storage references, checksum, content type/size, malware status, retention class, and access audit. | Object lifecycle/security tests. |
| TEL-DAT-008 | Secrets, media, access tokens, raw identifiers, and PHI MUST NOT appear in ordinary logs, metrics labels, traces, URLs, or client storage. | Automated log/telemetry scan. |
| TEL-DAT-009 | Retention MUST select the longest applicable approved rule, honor legal holds atomically, and produce deletion/disposition evidence. | Time-travel/hold/purge tests. |
| TEL-DAT-010 | Database migrations MUST be forward-compatible, transactional where safe, resumable for backfills, observable, and proven against production-shaped synthetic volumes. | Migration rehearsal report. |
| TEL-DAT-011 | Backup/restore MUST preserve aggregate/event/outbox consistency and encrypted object references; restored integration work MUST remain idempotent. | Restore/replay exercise. |
| TEL-DAT-012 | Production PHI MUST NOT be copied to development/test; synthetic fixtures must cover clinical and demographic diversity without re-identification. | Environment/data governance audit. |
| TEL-DAT-013 | Queue/order indexes MUST support scoped ready selection, expiry, work-item aging, and outbox claims without table-wide locks at target load. | Query plan/load evidence. |
| TEL-DAT-014 | Data dictionaries MUST define owner, purpose, source, classification, null/unknown semantics, retention, and FHIR/external mapping for every new field. | Approved schema catalog. |

## 6. Migration approach

Use additive migrations: create tables/types/indexes; deploy dual-compatible code; backfill only derived links with checkpoints; validate counts/invariants; enable reads; then enable writes/flags. Do not rewrite existing patient/encounter identifiers. If a relationship cannot be established deterministically, create a work item instead of guessing. Rollback disables feature writes and reverts compatible code; it does not destructively drop newly created clinical/audit records.
