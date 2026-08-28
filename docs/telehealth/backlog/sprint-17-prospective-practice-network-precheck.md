# Sprint 17: synthetic prospective practice-network precheck

Status: Approved for bounded implementation by [TH-DEC-0020](../decisions/0020-approved-sprint-17-prospective-practice-network-precheck.md)  
Scope: Applicant-owned selection from a versioned deterministic plan catalog after visit-purpose classification; practice-level fixture only, with no subscriber/member data, individual eligibility, exact practice-and-physician network confirmation, coverage creation, estimate/payment, identity proofing, patient promotion/linkage, clinical decision, consent, request, queue, care, downstream action, external integration, production use, or real PHI

## 1. Outcome

Let a synthetic prospective applicant discover how a practice-level plan precheck will behave without pretending the individual is covered or the eventual physician is in network. Record one immutable catalog selection and outcome at `PracticeNetworkPrecheckRecorded`; stop before member data, eligibility, exact network, coverage, financial, request, or care gates.

## 2. Stories

| ID | Story |
|---|---|
| `TH-SP17-001` | Add one append-only practice-network precheck and constrained `VisitPurposeRecorded -> PracticeNetworkPrecheckRecorded` event with review, safety, purpose, catalog, state, and effective-window provenance plus hard-false consequential flags. |
| `TH-SP17-002` | Add a versioned server-owned NON_PRODUCTION catalog with exactly three synthetic payer/product choices and deterministic practice-level confirmed-fixture, unknown, and out-of-network-fixture statuses. |
| `TH-SP17-003` | Publish applicant-owned private/no-store options and idempotent record routes with opaque not-found, bounded Problem Details, minimized contracts, and no patient/staff-session substitution. |
| `TH-SP17-004` | Extend the prospective entry with accessible plan radios, status/guarantee distinctions, persistent emergency direction, stable retry/reload, and no plan selection persistence. |
| `TH-SP17-005` | Keep applicant resume coarse and every individual eligibility, physician network, exact network, coverage, financial, request, queue, care, and external consequence false. |
| `TH-SP17-006` | Prove catalog allowlisting/effective scope, state/access/version isolation, exact replay, contention, append-only evidence, source/result minimization, zero insurance/patient/downstream delta, accessibility, migration, Graphify, and full regression. |

## 3. Synthetic catalog and result semantics

| Plan key | Display | Practice-level fixture result | Meaning |
|---|---|---|---|
| `harbor-mutual-hd` | Harbor Mutual — High Deductible | `PracticeNetworkConfirmedFixture` | Versioned synthetic contract fixture says the practice participates for the applicant state/purpose; member eligibility and physician participation are not checked. |
| `blue-valley-standard` | Blue Valley Health — Standard | `NetworkUnknown` | The fixture has no authoritative practice-product participation result; never present as in network. |
| `pine-state-choice` | Pine State Choice — Choice | `PracticeOutOfNetworkFixture` | Versioned synthetic fixture says the practice does not participate; no self-pay election, estimate, or financial acknowledgment is created. |

The catalog is scoped to the configured practice/facility, Georgia/California/Florida, the controlled visit-purpose categories, adapter mode `NON_PRODUCTION`, evidence key `avenchart-synthetic-prospective-practice-network-2026-08`, version 1, and the Decision 0020 effective window. The client sends only expected applicant version, `planKey`, and synthetic confirmation.

## 4. Acceptance evidence

1. Only the configured branded host and correct access-key owner of an unexpired, current `VisitPurposeRecorded` applicant with `NoCandidate`, approved identity review, passing universal safety evidence, and an immutable controlled purpose can list or record.
2. The options route returns only catalog/effective metadata, fixed labels, and limitations. The record request accepts no labels, status, member/subscriber, policy/group, card, physician, price, free-text, or outcome content.
3. The repository transaction resolves the plan from the current server catalog, snapshots current applicant state/purpose/safety location, and rejects unsupported/expired fixtures without writing evidence.
4. Every response keeps `memberEligibilityChecked`, `renderingPhysicianNetworkChecked`, `coverageVerified`, `exactNetworkConfirmed`, and all identity/patient/financial/request/queue/care/downstream/external flags false, including for `PracticeNetworkConfirmedFixture`.
5. Exact retry returns one immutable precheck; changed content, stale version, second semantic command, and concurrent first writers create no duplicate evidence.
6. Recording changes only the applicant aggregate plus one precheck and event; `insurance_records`, patients, portals, intake/coverage evidence, requests, queues, appointments, encounters, prescriptions, claims, messages, tasks/notifications, integration, and external-call evidence remain unchanged.
7. Component and cross-browser tests cover radio semantics, status distinctions, focus recovery, ambiguous retry with one command identity, 320 px reflow, serious automated WCAG findings, persistent emergency links, explicit no-guarantee content, and no plan selection in local/session storage.

## 5. Exit boundary

Sprint 17 ends at practice-level synthetic plan discovery. Subscriber/member capture, individual eligibility/benefits, exact practice-and-rendering-physician network confirmation, estimates/self-pay, financial acknowledgment, identity proofing, patient promotion/linkage, consent, clinical protocols, request creation, and queue entry remain unavailable and separately gated.
