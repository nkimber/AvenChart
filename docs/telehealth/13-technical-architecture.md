# Technical architecture

## 1. Target shape

Telehealth is a modular feature set inside the existing AvenChart deployable applications for the first release. It uses ASP.NET Core .NET 10 Minimal API route groups, PostgreSQL/Npgsql, React 19/TypeScript 6/Vite 8, existing patient/encounter/billing/integration capabilities, and a separately deployable background worker when durable external dispatch requires it. Module boundaries are enforced in code and tests so they can be extracted later if scale or organizational ownership warrants it.

```text
React applications
  Patient telehealth slice       Staff operations slice       Physician telehealth slice
             \                         |                         /
                         ASP.NET Core API
  Brand | Identity | Intake | Triage | Eligibility | Queue | Consultation | Rx | Claims
                         Domain/application services
  Existing Patient | Portal | Encounter | Forms | Medication | Billing | Audit | Integration
                         PostgreSQL + durable outbox
                         Background dispatch/recovery worker
  Video | Identity proofing | Eligibility/network | Pharmacy | eRx | Clearinghouse adapters
```

Realtime status is a notification path around the domain, not the domain itself. Every client can reconstruct state from HTTPS APIs and versions after reconnect.

## 2. Feature modules

| Module | Owns | May depend on |
|---|---|---|
| PracticeBranding | Host/origin resolution, approved brand projection, public availability | Practice configuration, content |
| ConsumerIdentity | Consumer account link, prospective context, verified contacts, proofing decisions | Existing portal identity adapter, security services |
| TelehealthIntake | Request aggregate, confirmations, consent references, complaint intake | Identity, practice config, forms |
| TelehealthTriage | Protocol versions, evaluator, assessments, clinical-review decisions | Forms/content; read-only relevant patient snapshot |
| CoverageVerification | Coverage candidates, eligibility and network evidence, estimates/routes | Patient insurance, practice contracts, adapters |
| TelehealthOperations | Readiness gate, admin review, queue, clinician shift, match/reservation | Triage, identity, coverage, credentialing, practice config |
| TelehealthConsultation | Encounter preparation/start/disposition, AVS/follow-up coordination | Existing encounter/clinical/portal modules, video metadata |
| TelehealthVideo | Provider abstraction, sessions/grants/webhooks, quality metadata | Operations/consultation authorization only |
| ElectronicPrescribing | Prescription/pharmacy canonical model and gateway | Medication/allergy/encounter, external integration |
| ProfessionalClaims | Claim readiness, scrub, approval, canonical X12 gateway/reconciliation | Encounter, coverage, billing, external integration |
| TelehealthNotifications | Authorized status projection, SignalR/polling, minimal outbound notices | Domain events; no command authority |
| Governance | Protocol/config/consent/payer rule publication and approvals | Audit, content, credentialing |

Dependencies flow from API/UI to application services to domain/persistence/adapters. Adapters never call UI endpoints, repositories do not emit patient-facing text, and a generic integration callback cannot directly mutate an aggregate without its application service.

## 3. ASP.NET Core implementation conventions

- Map cohesive versioned route groups from dedicated extension classes; keep `Program.cs` composition-only.
- Use typed request/response contracts and centralized validation; endpoints translate HTTP/auth context and call one application command/query.
- Return RFC 9457-style Problem Details with stable type/code, correlation ID, field errors, safe recovery information, and no stack trace/PHI.
- Use ASP.NET Core authorization policies plus a resource authorization service that validates practice, facility, purpose, patient/request, state, role, permission, and current relationship.
- Use scoped database/application services and `IHttpClientFactory` clients with named resilience/timeout policies. Never create per-call `HttpClient` instances.
- Use cancellation tokens for request-bound work, but do not cancel a committed durable outbox operation because the browser disconnected.
- Use SignalR groups derived server-side from authorized practice/request/clinician context; never trust a client-provided group name.
- Publish OpenAPI contracts and treat breaking contract drift as a build failure.
- Keep provider SDK types inside adapter projects; the domain uses canonical records/enums.

## 4. Persistence and transaction boundaries

PostgreSQL is the source of truth. Each command uses a short transaction and optimistic aggregate version. Critical operations use database constraints and row locks/conditional updates:

- prospective promotion/link + enrollment + request reassociation + queue creation;
- readiness authorization and `ready_at` assignment;
- next-eligible selection and reservation lease;
- encounter start and active-physician constraint;
- signed prescription + outbox event;
- encounter finalize + AVS/follow-up/claim-prep events; and
- external response correlation + business-state update + work item.

External calls do not occur while holding a database transaction. A command records intent/outbox atomically; dispatch occurs after commit. Synchronous pre-queue checks may call an adapter before the final persistence transaction, but their results are treated as evidence with idempotency/fingerprint and revalidated before authority is granted.

PostgreSQL constraints are the last line of defense for tenant linkage, state values, unique active reservation/encounter, immutable published/signed versions, foreign keys, and idempotency keys. Application validation supplements, not replaces, these constraints.

## 5. Background work

A durable worker claims jobs with a lease, bounded batch, `SKIP LOCKED` or equivalent, attempt count, next attempt, and heartbeat. It dispatches external transactions, notifications, expirations, stale-queue checks, lease recovery, follow-up escalations, and reconciliation. Handlers are idempotent and safe under at-least-once delivery. Poison jobs are quarantined with an owned work item and protected diagnostic reference.

Periodic work uses a production-capable scheduler/worker topology, not an in-process timer whose state disappears on API restart. API and worker readiness reflect database and mandatory dependencies separately from liveness.

## 6. Adapter ports

| Port | Baseline implementations |
|---|---|
| `IIdentityProofingProvider` | Deterministic synthetic stub; future approved provider |
| `IEligibilityGateway` | X12-like deterministic stub; future clearinghouse/payer service |
| `INetworkParticipationSource` | Versioned practice contract registry + stub; future payer/directory adapters |
| `ITelehealthVideoProvider` | Local simulator; future managed WebRTC provider |
| `IPharmacyDirectory` | Synthetic directory; future NCPDP/licensed directory service |
| `IDrugKnowledgeService` | Approved versioned test catalog; future licensed clinical data source |
| `IEPrescriptionGateway` | NCPDP SCRIPT canonical stub; future certified network/vendor |
| `IProfessionalClaimGateway` | X12 canonical stub; future certified clearinghouse |
| `INotificationGateway` | Local sink; future BAA-reviewed SMS/email/push services |

Every adapter advertises capabilities, environment (`stub`, `certification`, `production`), standard/version, destinations, and health. Production startup validates that mandatory ports are production-certified and refuses unsafe fallback.

## 7. Delivery increments

1. **Foundation:** domain schema, practice-brand resolver, request state, authorization resources, audit/outbox, API contracts, feature flags.
2. **Safe intake:** established/new patient identity shell, form rendering, emergency/triage engine, consent, clinical review, synthetic cases.
3. **Operations:** eligibility/network/estimate stubs, admin queue, readiness transaction, clinician shift/matcher/reservation, patient realtime/polling.
4. **Consultation:** video simulator/provider adapter, waiting room, physician workspace, encounter/AVS/follow-up.
5. **Downstream:** pharmacy directory, non-controlled eRx stub, claim-prep/X12 stub, work queues/reconciliation.
6. **Production hardening/pilot:** certified vendors, state/payer configurations, security/accessibility/performance/DR evidence, monitored practice pilot.

No increment may present a stub response as live or bypass the clinical safety case.

## 8. Architecture requirements

| ID | Requirement | Acceptance evidence |
|---|---|---|
| TEL-ARC-001 | Telehealth MUST be implemented as cohesive feature modules with explicit dependency direction and application services; `Program.cs`, a single repository, or a single React component MUST NOT become the feature owner. | Architecture tests/review and size/dependency report. |
| TEL-ARC-002 | The existing patient/encounter/medication/billing/audit records MUST remain systems of record; telehealth adds linked context rather than duplicate legal charts. | Data/architecture review. |
| TEL-ARC-003 | State transitions MUST execute through domain commands with validation, authorization, version checks, transaction, audit, and outbox. | Command integration tests. |
| TEL-ARC-004 | External provider types and assumptions MUST be isolated behind canonical ports and capability/version discovery. | Dependency and contract tests. |
| TEL-ARC-005 | No external network call may be made inside a database transaction; committed outbound intent MUST use a durable outbox. | Failure/transaction tests. |
| TEL-ARC-006 | Critical uniqueness/concurrency/tenant/immutability rules MUST be enforced in PostgreSQL in addition to application code. | Migration constraint and race tests. |
| TEL-ARC-007 | Realtime notifications MUST be advisory and replay/reconcile against authoritative versioned state. | Disconnect/reorder tests. |
| TEL-ARC-008 | API and workers MUST be horizontally safe: no business state, lease authority, or idempotency memory may depend only on one process. | Multi-instance tests. |
| TEL-ARC-009 | Stub/certification/production adapters and data MUST be environment-separated and production MUST fail closed on unsafe adapter configuration. | Startup safety tests. |
| TEL-ARC-010 | The architecture MUST reserve a marketplace-before-enrollment interface without creating cross-practice access or embedding marketplace logic in the request aggregate. | Boundary review/isolation test. |
| TEL-ARC-011 | New APIs MUST use centralized Problem Details, validation, authorization, audit, OpenAPI, and correlation conventions. | API conformance test. |
| TEL-ARC-012 | Long-running/retryable work MUST run through durable leased jobs with at-least-once-safe handlers and quarantine/manual recovery. | Worker chaos tests. |
| TEL-ARC-013 | Feature flags/kill switches MUST be server-authoritative, scoped, audited, safe for in-flight work, and not used to hide incomplete mandatory controls. | Flag/rollback exercise. |
| TEL-ARC-014 | Major framework/provider upgrades MUST pass contract, clinical safety, browser, accessibility, and rollback validation before rollout. | Upgrade gate evidence. |

