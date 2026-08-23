# COV-010 assessment — API contracts, FHIR, transport, and reconciliation

- Status: in review
- Baseline: `phase-1-experimental` at `d77a8320e6751a2deb2daf14cf1ac5d6b00cb989`
- Observed on: 2026-08-21
- Primary reviewers: `phase2_quality_operations`, `phase2_data`, `phase2_security_privacy`
- Independent verification: separate `phase2_verifier` static pass
- Primary coverage: `COV-010`
- Supporting coverage: `COV-001`, `COV-003`, `COV-004`, `COV-006`, `COV-008`, `COV-009`, `COV-014`, `COV-016`, `COV-017`, `COV-019`
- Evidence level: source, migration, route, live Development OpenAPI, focused transport test, clean Release build, and the later synthetic PostgreSQL/FHIR/inbox replay in the [runtime-readiness packet](cov-014-019-runtime-readiness.md); formal FHIR validator replay and real partner delivery remain outstanding

## Assessment question

Do the API descriptions, FHIR projections, integration inbox/outbox, transport boundary, and reconciliation workflows preserve a truthful, scoped, replay-safe contract from caller through durable state and external handoff?

This is an engineering-readiness assessment. It makes no production, certification, interoperability-conformance, privacy-law, clinical, or partner-delivery claim. The application explicitly describes its integration transport as local and deterministic; that boundary is counterevidence to claims of an existing external interface, not evidence that a future interface is ready.

## Representative traces

1. The API registers OpenAPI and maps it only in Development. Routes use named Minimal API handlers and custom permission filters, but most responses are untyped `IResult` branches without response metadata.
2. The FHIR group exposes read/search-only Patient, Encounter, Observation, and SDOH projections behind the ordinary `patients:demo:view` local staff capability.
3. FHIR searches count and limit results to 100. Patient and Observation searches accept identifiers; Encounter search does not normalize a typed `Patient/{id}` reference. Bundles contain no continuation links.
4. The generic outbox persists idempotency, attempts, leases, retries, quarantine, recovery, and event history. Dispatch is invoked for one event ID at a time; no autonomous integration worker was found.
5. The generic inbox deduplicates on `(source, source_message_id)` and supports versioned reconcile/reject decisions with actor, reason, and event history. It does not apply inbound payloads to clinical tables in the reviewed scope.
6. The local transport accepts only deterministic `local://` destinations. No partner adapter, source signature, SMART/OAuth FHIR contract, schema registry, or external acknowledgement contract was found.

## Reproducible checks

| Check | Result |
| --- | --- |
| Resolve Phase 1 baseline and compare product tree | Baseline resolved; `avenchart/`, `avenchart-ui/`, and `infra/` remained unchanged |
| Release API build | Passed with 0 warnings and 0 errors |
| Focused shared UI transport test | Passed: 1 file, 2 tests |
| Development OpenAPI inspection | OpenAPI 3.1.1; 588 paths, 658 operations, 278 schemas; every operation reported only `200`, with no response content or security metadata |
| Schema-not-ready API response | Returned structured `503 application/problem+json` with correlation ID and `schema_not_ready` |
| FHIR validator, MIME negotiation, error and pagination replay | MIME/error/search runtime executed: generic JSON, empty 404, and optional-filter Patient searches failing with 500 were reproduced; formal validator and pagination remain outstanding |
| Inbox/outbox duplicate and transport-fault tests | Divergent same-ID inbox replay reproduced; focused quarantine/recovery passed; concurrent duplicate and real partner transport remain outstanding |

The live OpenAPI inspection is a Development-only observation. The independent verifier corroborated the source-level metadata gap but could not reproduce the live document in its pass. This difference does not change the finding; it limits the runtime evidence level.

## Material strengths and counterevidence

- The shared API transport supplies cancellation, a bounded timeout, session-invalid signaling, and normalized error text.
- Schema-readiness and rate-limit failures use Problem Details with correlation information; unexpected failures are generic and do not expose stack traces.
- FHIR is GET-only and does not claim unsupported write interactions.
- FHIR SQL is parameterized and bounded; integration SQL uses parameters, JSONB, uniqueness, transactions, compare-and-set updates, leases, bounded retries, quarantine, and reasoned recovery events.
- Inbox reconciliation commits state, actor/reason, version, and event history atomically.
- The migration comments explicitly say the integration tables are a local contract, not a vendor credential store, partner adapter, worker policy, or production delivery policy.
- CORS is limited to local development origins without credentials. No external transport, SMART discovery, or source-authentication contract was found, so those future-boundary questions remain open rather than proven vulnerabilities.
- The application and README disclaim production and certification readiness; that narrows the current impact but does not make an advertised `/api/fhir/R4` representation structurally conformant.

## Validated findings

| Finding | Condition | Severity | Reach | Production blocker |
| --- | --- | --- | --- | --- |
| [`P2-07-F001`](../findings/p2-07-f001-openapi-runtime-contract.md) | Generated OpenAPI does not describe the runtime response or authentication contract | Medium | Systemic | No by itself |
| [`P2-07-F002`](../findings/p2-07-f002-fhir-r4-contract.md) | Advertised FHIR R4 representations are not a validated normative R4 contract | High | Cross-cutting | Yes under `P2-D014` |
| [`P2-07-F003`](../findings/p2-07-f003-integration-idempotency-content.md) | Integration idempotency identities are not content-bound | Medium | Repeated | Unknown pending partner scope |
| [`P2-07-F004`](../findings/p2-07-f004-outbox-at-least-once-delivery.md) | Outbox delivery is at-least-once across the transport boundary | Medium | Repeated | Unknown pending external delivery |
| [`P2-07-F005`](../findings/p2-07-f005-external-laboratory-ingestion.md) | No supported external laboratory-result ingestion contract applies results to the clinical record | High | Cross-cutting | Yes under `P2-D014` |

## Existing findings broadened

- [`P2-03-F005`](../findings/p2-03-f005-merged-source-actionable.md) now includes FHIR Patient, Encounter, and Observation reads that accept known identifiers without a merged-source predicate.
- [`P2-05-F001`](../findings/p2-05-f001-no-production-identity-provider.md) now includes the absence of an approved external FHIR/SMART/workload identity contract.
- [`P2-05-F002`](../findings/p2-05-f002-chart-access-not-resource-scoped.md) now includes practice-wide FHIR searches and reads behind only `patients:demo:view`.
- [`P2-05-F003`](../findings/p2-05-f003-phi-audit-resource-correlation.md) now includes FHIR reads/searches whose access event lacks patient/resource correlation.
- [`P2-05-F009`](../findings/p2-05-f009-workflow-mutation-provenance.md) now includes queue/dispatch operations whose repository contracts do not persist the authenticated actor; inbox decisions and requeue are counterexamples.
- [`P2-09-F002`](../findings/p2-09-f002-default-verification-gate.md) now includes missing OpenAPI, FHIR validator, duplicate-content, queue-progress, and transport-fault scenarios.

## Unknowns and calibration

- Whether OpenAPI is a supported external client contract or only a development route inventory remains an API-owner decision.
- FHIR R4 is required by `P2-D014`; the exact US Core/SMART/other implementation guide and certification scope remain to be selected.
- FHIR Encounter status is statically collapsed to `finished`; a non-finished database counterexample and clinical/HIM adjudication are outstanding.
- No worker claims due outbox rows. Manual dispatch may be intentional for a local demo, but queue age, retry ownership, dead-letter ownership, and recovery SLOs are not defined.
- A crash after external success and before local completion can cause redelivery; no external partner exists to establish whether this is harmful or correctly deduplicated.
- Same-identity/different-payload behavior was replayed against PostgreSQL and treated as a successful duplicate without applying a clinical result.
- `COV-019` has synthetic evidence complete: the required external laboratory-result path is absent and recorded as `P2-07-F005`.

## Required specialist decisions and remaining evidence

- API architecture owner: decide whether OpenAPI is a supported contract, then define response, error, authentication, and versioning metadata.
- Certification/interoperability specialist: select FHIR profile/IG, validate CapabilityStatement, resource JSON, Bundle links, MIME negotiation, OperationOutcome, search continuation, and lifecycle mapping.
- Clinical/HIM/privacy reviewers: adjudicate merged/inactive/deceased/archived patient and encounter representation, SDOH sensitivity, purpose, and minimum-necessary scope.
- Integration operations/SRE: define autonomous dispatch, queue-age/retry/dead-letter ownership, transport acknowledgements, and partner idempotency.
- Database specialist: run changed-payload duplicate, concurrent receipt, lease overlap, and >100-row FHIR tests in disposable PostgreSQL.
- Independent verifier: retain separate reproduction for high/conditional FHIR and systemic OpenAPI conditions before any Phase 3 acceptance.

## Coverage and scorecard impact

- `COV-010` now has representative static and runtime evidence complete; specialist profile selection and implementation remain open.
- Five contract and integration findings are in the canonical register. `P2-07-F002` and `P2-07-F005` are unconditional production blockers under `P2-D014`.
- Domains 07, 09, and 10 gain material contract/recovery evidence but do not improve their provisional posture; existing high findings continue to cap relevant domains.
- `COV-003`, `COV-005`, `COV-006`, `COV-008`, `COV-009`, `COV-014`, `COV-016`, `COV-017`, and `COV-019` receive supporting evidence; their own rows remain open where stated in the matrix.
- No blanket move from parameterized SQL to EF is supported. The integration repository is a positive example of deliberate SQL, transactional state transitions, and bounded recovery.

## Next evidence, not fixes

1. Regenerate Development OpenAPI and compare representative 200/201/204/400/401/403/404/409/429/500/503 responses and bodies.
2. Run CapabilityStatement, Patient, Encounter, Observation, Bundle, MIME, error, search, and pagination responses through an R4 validator.
3. Seed more than 100 matches; exercise bare and typed subject references and merged, inactive, deceased, archived, preliminary, amended, and corrected records.
4. Submit exact and divergent duplicate inbox/outbox identities, including concurrent submissions, and inspect durable receipts/history.
5. Queue a message without dispatch, observe queue age, and repeat for retry-scheduled and expired-lease states.
6. Inject cancellation, timeout, transport exception, success-before-local-commit, restart, and multi-instance claim scenarios with a synthetic adapter.
7. Obtain explicit product, interoperability, security/privacy, clinical/HIM, and operations decisions before accepting any Phase 3 recommendation.

`COV-010` has evidence complete but remains unresolved for implementation. The product tree is read-only and unchanged from the fixed Phase 1 baseline.
