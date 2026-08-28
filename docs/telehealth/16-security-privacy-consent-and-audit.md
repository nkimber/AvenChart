# Security, privacy, consent, and audit

## 1. Security objectives

Protect confidentiality, integrity, and availability of ePHI and clinical operations; prevent cross-practice/patient access; prevent fraudulent identity linkage; prevent unauthorized triage, queue, encounter, prescription, or claim actions; ensure safe continuity during failure; and retain reliable evidence of access and decisions.

HIPAA compliance is an organizational program, not a product badge. Before production, the covered entity/business-associate roles, BAAs, risk analysis, policies, training, incident process, contingency plans, and technical safeguards must be approved. Vendors handling PHI require due diligence and appropriate agreements.

## 2. Trust boundaries and primary abuse cases

| Boundary/abuse case | Required controls |
|---|---|
| Public branded internet to patient APIs | Verified host/practice binding, WAF/rate limits, secure session/CSRF, input/file validation, no account enumeration, bot/abuse controls |
| Consumer account to patient chart | Independent identity proofing/link evidence, resource authorization, step-up/recovery controls, duplicate-safe linkage, audit |
| One practice/patient/request to another | Server-derived tenant/resource context, composite constraints, non-sequential IDs, IDOR tests, authorized SignalR groups/caches |
| Administrator to clinical authority | Separate permissions/services/UI, immutable triage evidence, clinician-only review/sign/prescribe, audit/monitoring |
| Clinician to queue/chart | Active workforce identity, purpose/facility/practice, credential/license/shift/reservation relationship, short access lifetime |
| Browser to video provider | Short-lived scoped grant, opaque IDs, origin policy, no provider secret/media recording, participant roster |
| Platform to external vendors | Per-adapter service identity, mTLS/OAuth/signatures as applicable, egress/destination allowlist, minimum payload, BAA, idempotency, response validation |
| Provider callback to domain | Raw signature/freshness/replay validation, durable inbox, schema/destination checks, no direct blind state mutation |
| Support/telemetry/export | PHI redaction/minimization, correlation codes, restricted break-glass, export authorization/watermark/expiry/audit |
| Insider/configuration abuse | Least privilege, separation of duties, reason/approval, change history, anomaly alerts, periodic access review |

## 3. Authentication and sessions

- Workforce identity uses approved OIDC with MFA/phishing-resistant option, short sessions appropriate to clinical work, centralized revocation, and no production local/test identity provider.
- Patient identity follows the risk decision in the onboarding specification. Sensitive changes, chart linkage, recovery, and visit join support step-up.
- Browser credentials use secure, HttpOnly, SameSite cookies or an approved token pattern with CSRF defense; no durable bearer token in local storage.
- Sessions are issuer/audience/client/device/context bound as feasible, rotate after authentication/privilege change, expire server-side, and can be revoked for account, enrollment, practice, clinician, or incident changes.
- Service credentials live in an approved secret/key manager, are audience/destination scoped, rotated, inventory-tracked, and not shared between adapters/environments.

## 4. Authorization

Every PHI or state-changing request evaluates:

1. authenticated subject/service and assurance/session validity;
2. practice/facility context and active membership/enrollment;
3. role and specific permission/action;
4. purpose of use and workflow relationship;
5. resource ownership (patient/request/encounter/claim) and state;
6. clinician credential/license/shift/reservation when clinical;
7. minimum-necessary field projection; and
8. emergency/break-glass policy if applicable.

List/query endpoints are filtered at the source, not loaded broadly and trimmed in UI. Cache keys include authorization context and protected responses are `private, no-store` unless a reviewed alternative exists.

## 5. PHI protection and privacy

- Encrypt network traffic; encrypt database, backups, object storage and queues using managed keys, with field/application protection for especially sensitive identifiers/evidence where threat analysis requires it.
- Maintain data-flow and vendor/subprocessor inventories. Disclose intended uses and prevent secondary advertising/model training/data sale.
- Practice data minimization and purpose limitation in screen, API, export, event, support, notification, analytics and vendor payloads.
- Do not put PHI/tokens in URLs, referrers, exception text, client analytics, session replay, crash reporting, logs, trace attributes, metric labels, source maps, or notification previews.
- Sanitize/scan uploads; isolate rendering/download; set safe media/content-disposition; reject unsupported or polyglot content.
- Use an approved geocoding/map vendor and BAA/data-use posture before sending address/location; prefer local/coarse computation when possible.
- Complete privacy impact and HIPAA Security Rule risk analyses, including branded domains, video, identity proofing, communications, eligibility, eRx, claims, and analytics.

## 6. Consent and patient control

Consent evidence follows the state-governance and data specifications. Consent withdrawal affects future processing where legally applicable but does not erase records the practice must retain. The product distinguishes treatment consent, telehealth consent, privacy acknowledgment, electronic communications, financial acknowledgment, optional location permission, and any research/marketing choice. No optional choice is bundled into required treatment consent.

Patients can see/download current notices and accepted versions, manage permitted communication choices, review active sessions/devices where supported, report an incorrect identity link, and use record/privacy request paths. Worsening/emergency actions are never gated on marketing or electronic-communication consent.

## 7. Audit

Audited events include authentication/recovery/linkage, PHI view/search/export/download, consent, clinical answers/assessment/rules, configuration publication, admin/reviewer decisions, readiness/queue/lease, chart start/edit/sign/amend, video grants/participant metadata, pharmacy/prescription, claim/financial, vendor payload/acknowledgment, work-item recovery, break-glass, deletion/hold, and access/config reviews.

Audit entries contain event ID, time, actor/service, role/purpose/practice/facility, action/outcome/reason, resource type/opaque ID, patient/request context as protected fields, source/session/device/IP category as approved, correlation/causation, prior/new aggregate version, and evidence checksum/reference. Do not duplicate clinical narrative or raw payload into the audit log.

Audit storage is access-controlled, append-only/tamper-evident, time-synchronized, queryable for investigations, retained by approved policy, backed up, and monitored for gaps. Access to audit is itself audited.

## 8. Security requirements

| ID | Requirement | Acceptance evidence |
|---|---|---|
| TEL-SEC-001 | A documented HIPAA Security Rule risk analysis, privacy impact assessment, data-flow inventory, vendor/BAA inventory, and mitigation plan MUST be approved before pilot PHI. | Signed compliance evidence. |
| TEL-SEC-002 | Production workforce and patient identity MUST use approved providers/configuration; local deterministic/test authentication MUST fail production startup. | Runtime safety test. |
| TEL-SEC-003 | Every protected API/query/event group MUST enforce server-side practice/resource/purpose authorization and minimum-necessary projection. | Comprehensive auth/IDOR suite. |
| TEL-SEC-004 | Administrators MUST NOT receive clinical override/sign/prescribe authority through broad roles; privileged actions require separation of duties where specified. | Permission graph and negative tests. |
| TEL-SEC-005 | PHI/secrets/tokens MUST be encrypted/protected and excluded from URLs, caches, logs, metrics, traces, client analytics/session replay, and notification previews. | Automated data-leak scans. |
| TEL-SEC-006 | Sessions/tokens MUST have secure storage, rotation, revocation, CSRF/replay protections, bounded lifetime, and step-up for high-risk actions. | Session attack tests. |
| TEL-SEC-007 | Uploads and external payloads MUST have strict type/schema/size limits, malware/untrusted-content handling, safe rendering, and protected storage. | Malicious file/payload tests. |
| TEL-SEC-008 | External traffic MUST use destination allowlists, current TLS, scoped service identity, timeout, response validation, and signed/replay-protected callbacks as supported. | Egress/callback penetration tests. |
| TEL-SEC-009 | Video recording/transcription/AI use MUST be prohibited contractually and disabled/tested technically. | Vendor/config/negative evidence. |
| TEL-SEC-010 | Security-relevant and PHI access events MUST be complete, attributable, integrity-protected, monitored, retained, and reviewable without storing excess clinical content. | Audit completeness/integrity test. |
| TEL-SEC-011 | High-risk events MUST alert an owned response process: repeated linkage/proofing failure, cross-tenant denial, privilege/credential anomaly, mass access/export, callback forgery, controlled-drug attempt, audit gap, and unsafe config. | Alert-to-incident exercises. |
| TEL-SEC-012 | Backups, restores, downtime, cyber incident, breach assessment/notification, evidence preservation, and vendor outage MUST have tested playbooks and accountable roles. | Tabletop/restore reports. |
| TEL-SEC-013 | Dependency/container/IaC/secret/SAST/DAST and penetration testing MUST gate release according to severity SLAs, including authenticated multi-role testing. | CI/security report and remediation register. |
| TEL-SEC-014 | Patient notices/consents MUST be unbundled, versioned, downloadable, revocable where applicable, and never used to waive non-waivable protections. | Legal/privacy/UX tests. |
| TEL-SEC-015 | Support access MUST default to non-PHI operational metadata; any PHI access requires authorized purpose/context and is audited. | Support-role test. |
| TEL-SEC-016 | Production data MUST remain out of lower environments; synthetic data, environment isolation, egress restrictions, and safe test destinations are mandatory. | Environment isolation audit. |

## 9. Incident and continuity workflow

Suspected security/privacy/clinical integrity incidents trigger containment without abandoning active patients: revoke affected tokens/adapters, disable new intake or joins at the narrowest safe scope, preserve audit/evidence, identify active requests/consultations, assign clinical continuity actions, notify practice/privacy/security leadership, assess breach/adverse event duties, communicate accurately, recover from trusted state, reconcile duplicate/missed external transactions, and perform corrective-action review.

