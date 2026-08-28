# Decision 0006: Sprint 3 patient queue transparency authorization

Status: Approved — active for the exact disabled synthetic slice below  
Approved date: 2026-08-27  
Decision owner: AvenChart program owner  
Implementation owner: Codex delivery agent under AvenChart program-owner direction  
Review/expiry: 2026-10-31, or immediately when superseded by a later gate decision

## 1. Authorized outcome

Extend the approved synthetic request path with a patient-owned, authoritative queue-status projection and resilient HTTP polling:

```text
OperationalReview -> Reviewing your request
Queued            -> You're in line; show an approximate number of requests ahead when current evidence is available
Reserved          -> A physician is getting ready
Redirected        -> This request cannot enter the telehealth queue; retain urgent/emergency guidance
```

The server remains the source of truth. Position is a point-in-time, practice/facility-scoped count using the existing deterministic ready-time ordering. It is labeled approximate, is not a wait-time promise, exposes no other patient or clinician, and becomes unavailable rather than invented when queue evidence is inconsistent.

## 2. Authorized implementation surfaces

Changes may use the existing Decisions 0003 and 0005 telehealth paths plus:

```text
docs/telehealth/decisions/0006-approved-sprint-03-patient-queue-transparency.md
docs/telehealth/backlog/sprint-03-patient-queue-transparency.md
docs/telehealth/backlog/sprint-03-evidence.md
```

The smallest telehealth contract, endpoint, service, repository, frontend, test, runtime-proof, OpenAPI, authorization, planning-validation, and CI composition edits needed to connect and verify the slice are authorized. No database migration is authorized or required for this read projection.

## 3. Required controls

1. Feature defaults off and Production startup still rejects enablement.
2. Only the authenticated request owner under the configured branded host may read a status projection.
3. The response contains only the request identifier/status/version, calm approved content, approximate count when available, snapshot/update times, polling guidance, and non-PHI safety actions.
4. Queue position counts only current `Ready` entries for the same practice and facility ordered ahead by `ready_at` and request identifier.
5. Position is always labeled approximate; no exact wait time, physician workload, other-patient data, or physician identity is exposed.
6. Missing or inconsistent queue evidence returns honest position-unavailable content and never fabricates a count.
7. Polling reconciles authoritative HTTP state, pauses while the page is hidden, resumes on visibility, and applies bounded exponential backoff with jitter after failures.
8. The UI preserves a manual refresh/retry route, last-updated time, connection state, worsening-symptoms direction, and emergency guidance.
9. SignalR is not introduced in this slice. The contract explicitly reports realtime delivery unavailable and remains usable through polling alone.
10. Unit, API, authorization, runtime, OpenAPI, desktop/mobile accessibility, failure-recovery, and planning evidence must be updated without weakening existing gates.

## 4. Explicit exclusions

This decision does not authorize:

- exact wait-time promises, capacity/hours configuration, queue reprioritization, cancellation, notifications, SignalR, SMS, email, or push;
- new-patient/prospective-patient creation, identity proofing, duplicate resolution, chart promotion, or marketplace entry;
- clinician chart access, video/media, consultation, encounter documentation, prescribing, pharmacy, claims, payment, or live integration;
- production enablement, deployment, real people, real symptoms, real PHI, patient care, or closure of any rollout/Phase 2 gate; or
- self-certification of clinical, security/privacy, data, accessibility, legal, operational, or program-owner review.

## 5. Stop conditions and rollback

Stop if one patient can read another request, position crosses practice/facility boundaries, the response reveals another patient or clinician, the client treats polling state as authority for a domain transition, a real destination is contacted, Production accepts the feature, or prior evidence regresses. Rollback removes the status route/UI polling while preserving the existing request and queue state.

## 6. Approval record

The program owner directed Codex to “implement all of this,” approved all decisions, and authorized uninterrupted operation and whatever changes are needed to run the active goal while unavailable. This record interprets that authority only for the bounded, reversible, synthetic read projection above. It does not broaden authority to production, real patient care, external integrations, or independent-review sign-off.

## References

- [Decision 0005](0005-approved-sprint-02-established-patient-readiness.md)
- [Practice queue specification](../07-practice-configuration-and-queue-operations.md)
- [Video and realtime specification](../10-video-realtime-and-communications.md)
- [Sprint 3 plan](../backlog/sprint-03-patient-queue-transparency.md)

