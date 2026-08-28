# Nonfunctional requirements, observability, and operations

## 1. Proposed service objectives

These are initial engineering targets requiring product/operations approval at `G0`; vendor-dependent measures are reported separately from platform measures.

| SLI | Initial target | Measurement notes |
|---|---|---|
| Patient intake/queue and clinician operations availability | 99.9% monthly | Valid requests that can complete the core operation; approved maintenance excluded and reported |
| Safety-screen evaluation | 99.95% monthly; p95 <= 1 second | Server result after valid submission; no external dependency |
| Core API latency | Reads p95 <= 500 ms; writes p95 <= 1 second | Excludes external vendor wait; per route and practice size |
| Queue update freshness | p95 <= 5 seconds; polling fallback <= 15 seconds | From committed state to connected client visibility |
| Reservation correctness | Zero double-active reservations | Safety invariant, not percentage budget |
| Video grant creation | p95 <= 2 seconds excluding provider outage | After valid authorization |
| Waiting-room to media connection | p95 <= 10 seconds after both parties ready | Provider/browser/network segmented |
| Durable job lag | p95 <= 30 seconds for normal priority; safety/visit-critical <= 5 seconds where asynchronous | Queue age by work type |
| External transaction loss | Zero committed intents lost | At-least-once dispatch with reconciliation |
| Recovery point objective | <= 5 minutes for telehealth operational/clinical state | Must be validated with storage/backup design |
| Recovery time objective | <= 60 minutes for core request/encounter state | Continuity mode may precede full vendor restoration |

SLOs do not permit violating a safety invariant. Error-budget policy can slow change or invoke capacity work; it cannot authorize bypassing triage, licensure, consent, identity, signing, or external response semantics.

## 2. Planning capacity envelope

Before pilot, load tests must demonstrate at least the approved pilot envelope. The provisional baseline is:

- 100 enabled practices, 2,000 simultaneously queued requests platform-wide, and 100 queued per practice;
- 500 concurrent application-level consultations/active reservations platform-wide (media is provider-hosted);
- 100 status events/second sustained and 500/second burst;
- 50 external dispatch operations/second sustained with downstream throttling;
- 10 years of retained encounter/audit history under representative indexes and row widths; and
- no more than 60% steady-state saturation of database connection, CPU, worker, and SignalR capacity at forecast peak.

These figures are planning assumptions, not customer promises. Capacity modeling must replace them with signed pilot forecasts and vendor limits. Load tests use synthetic PHI-free data and include one noisy practice without starving others.

## 3. Resilience

- Apply per-operation timeout budgets; do not stack unbounded SDK retries, HTTP retries, worker retries, and user retries.
- Retry only transient/idempotent operations with exponential backoff and jitter. Permanent validation/business rejections create work items.
- Use circuit breakers/bulkheads per provider/destination and practice-aware rate limits.
- Persist before notifying; clients reconcile after lost/duplicate/out-of-order messages.
- Leases recover queue reservations/jobs after process death; clocks use authoritative UTC and monitored synchronization.
- Degraded modes are explicit: intake closed, manual coverage review, polling instead of realtime, video join paused, prescription/claim queued but not sent.
- Dependency recovery includes reconciliation from last durable cursor/control number; it does not blindly replay everything.

## 4. Observability

Every request/command/job/external transaction carries a correlation ID; causal links connect patient request, encounter, prescription and claim without using patient name/member/diagnosis as telemetry. Structured logs capture safe event code, environment, service/version, practice as an approved pseudonymous identifier, aggregate type/opaque ID, state/version, outcome category, latency, dependency, retry/attempt, and error class.

Key dashboards/alerts:

- safety-screen errors/latency and no-outcome invariant;
- request counts/age by non-PHI state, state jurisdiction, service, and practice;
- review/verification backlog and SLA age;
- queue wait distribution, abandonment, expiry, deterioration, capacity and fairness;
- active shifts/reservations, lease expiry/reassignment/double-reservation invariant;
- video grant/join/reconnect/failure by provider/browser/network, no media content;
- incomplete encounters/AVS/follow-up tasks;
- prescription/claim transport and business outcomes, missing acknowledgments and quarantines;
- authorization denials, cross-tenant attempts, proofing/recovery abuse, audit gaps;
- database/connection/index/job/outbox/SignalR health and saturation; and
- SLO/error-budget and deployment/version comparison.

Metrics with small cohorts or sensitive service/state combinations require privacy thresholds and role-restricted dashboards.

## 5. Operational runbooks

At minimum:

1. patient reports emergency/deterioration while queued;
2. no eligible physician/capacity or service closing;
3. expired/stuck reservation and duplicate-match conflict;
4. database/cache/SignalR/API deployment failure;
5. video provider outage or widespread device issue;
6. eligibility/network vendor outage or incorrect status;
7. prescription rejection/missing acknowledgment/wrong pharmacy/cancel failure;
8. claim rejection/missing response/remittance mismatch;
9. suspected PHI exposure/cross-tenant access/compromised identity/vendor webhook;
10. audit/event gap, outbox backlog or poison message;
11. clinician credential/license/configuration invalidation during active work;
12. backup restore, regional outage, and continuity reconciliation; and
13. clinical protocol/configuration rollback/adverse-event response.

Each runbook names trigger, severity, detection, command owner, clinical continuity owner, communication, containment, safe state, recovery/reconciliation, evidence, and post-incident review.

## 6. Nonfunctional requirements

| ID | Requirement | Acceptance evidence |
|---|---|---|
| TEL-NFR-001 | Product/operations MUST approve measurable SLOs, calculation queries, exclusions, owners, alerts, and error-budget response before pilot. | SLO workbook/dashboard validation. |
| TEL-NFR-002 | Safety and correctness invariants MUST be alertable zero-tolerance measures, not hidden inside aggregate availability percentages. | Invariant alert exercises. |
| TEL-NFR-003 | Core APIs, queue, jobs, and database MUST meet approved latency/capacity targets at forecast peak plus documented headroom. | Repeatable load report. |
| TEL-NFR-004 | One practice, payer, vendor, or job class MUST not exhaust shared connections/workers/rate limits and starve other practices or safety work. | Noisy-neighbor/bulkhead test. |
| TEL-NFR-005 | All retryable work MUST have bounded timeout, retry classification, backoff/jitter, idempotency, attempt visibility, expiry/quarantine, and owner. | Chaos/fault-injection suite. |
| TEL-NFR-006 | Realtime and external dependencies MUST have documented degraded modes that preserve authoritative state and patient safety. | Dependency outage exercises. |
| TEL-NFR-007 | Telemetry MUST be structured/correlated/actionable and PHI-minimized; logs/metrics/traces MUST pass automated leak checks. | Observability QA/security scan. |
| TEL-NFR-008 | Alerts MUST map to a staffed response, severity, runbook, escalation, and test; unactionable noisy alerts must be corrected. | On-call drill and alert review. |
| TEL-NFR-009 | Health endpoints MUST distinguish liveness, readiness, and dependency/degraded capability without exposing secrets or tenant data. | Health contract tests. |
| TEL-NFR-010 | Backup/restore and regional/major outage recovery MUST meet approved RPO/RTO with aggregate/outbox/object integrity and reconciliation evidence. | Timed DR exercise. |
| TEL-NFR-011 | Deployments MUST support backward-compatible rolling operation, migration readiness, feature-scoped enablement, automated rollback triggers, and safe active-session drain. | Deployment rehearsal. |
| TEL-NFR-012 | Production changes to protocols, state rules, payer rules, adapters, credentials, and brand content MUST be observable by version and reversible to an approved safe configuration. | Config rollback exercise. |
| TEL-NFR-013 | Clocks, certificates, domains, licenses, consent/protocol reviews, secrets, vendor contracts and trading-partner enrollments MUST have advance-expiry monitoring. | Expiry simulation and dashboard. |
| TEL-NFR-014 | Patient-facing maintenance/outage status MUST be honest, accessible, practice-specific, and include safe alternatives; no endless loading state is allowed. | Outage UX tests. |
| TEL-NFR-015 | Support tooling MUST locate work by safe correlation code, show authoritative owner/next action, and avoid default PHI access. | Support exercise. |
| TEL-NFR-016 | Performance and availability results MUST be segmented by meaningful browser/network/practice/state/vendor cohorts without creating privacy risk. | Dashboard/privacy review. |
| TEL-NFR-017 | Client bundles MUST have enforced size/performance budgets, route-level loading, resilient autosave, and tested low-bandwidth behavior. | Web performance test. |
| TEL-NFR-018 | Database queries and migrations MUST be measured against production-shaped volumes; new slow/locking query regressions block release. | Query plan/migration evidence. |
| TEL-NFR-019 | An operational readiness review MUST prove runbooks, on-call access, dashboards, alerts, vendor contacts, downtime forms/process, reconciliation, and patient communications. | Signed ORR package. |
| TEL-NFR-020 | Post-incident review MUST include clinical safety, privacy, equity/accessibility, financial, and technical effects and track corrective action to closure. | Incident review template/sample. |

