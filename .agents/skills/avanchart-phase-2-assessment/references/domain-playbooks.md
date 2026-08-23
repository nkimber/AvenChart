# Domain playbooks

Use the sections assigned to the current packet. These questions supplement, but do not replace, the quality standard and coverage matrix. They are investigation prompts, not presumed defects.

## Architecture, boundaries, and readability — domains 01 and 02

- Trace representative requests from entry point to durable effect and back to the user.
- Identify actual ownership of business rules, cross-cutting behavior, configuration, transactions, and failures.
- Inspect dependency direction, cohesion, coupling, cyclic knowledge, global state, service lifetimes, and change blast radius.
- Distinguish large-but-cohesive code from mixed-responsibility code; measure duplication by behavior, not similar syntax alone.
- Check whether naming and organization let a new maintainer find the real execution path without relying on generated intent or historical context.
- Evaluate the ASP.NET Core host, DI registration, middleware order, endpoint organization, configuration validation, error handling, DTO boundaries, health surfaces, and background-service ownership against the current application model.

Return a current-state map, representative traces, strengths, boundary conditions, and candidate findings. Do not recommend microservices, CQRS, repositories, projects, or abstractions without a demonstrated problem and comparative consequence.

## Domain correctness and clinical safety — domain 03

- Identify clinical and business invariants, lifecycle states, prohibited transitions, calculations, units, terminology, timestamps, and correction rules.
- Trace exact terminology and flag values from capture or inbound integration through normalization, persistence, queues, display, correction, and follow-up; exercise the real entry path rather than seeding the downstream spelling directly.
- Test wrong-patient, duplicate, stale, partial, concurrent, interrupted, delayed, reordered, and retried scenarios where applicable.
- For acknowledgement, signature, and review states, determine which content version was attested, what changes reopen or invalidate it, and how responsible ownership and closed-loop follow-up are established.
- Trace patient identification, clinical decision support, orders, results, acknowledgement, communication, referrals, portal messages, and downtime behavior using the applicable SAFER Guide areas.
- Verify what the UI communicates, what the API accepts, what the database preserves, what the audit records, and how recovery behaves.
- Separate functional parity with OpenEMR from independent evidence that the resulting behavior is safe and correct.

Agents may identify potential safety consequences but must route clinical adequacy and consequence judgments to a clinician or clinical informaticist.

## Data and persistence — domain 04

- Inventory EF Core, parameterized SQL, schema, migrations, sequences, database functions, background work, backup, restore, and seed ownership.
- Review transaction boundaries, isolation assumptions, optimistic or pessimistic concurrency, idempotency, retries, uniqueness, foreign keys, check constraints, timestamps, soft deletion, retention, and audit linkage.
- Challenge whether cascade deletion, correction, archive, restore, and backup recovery preserve clinically and operationally material evidence independently of the mutable source record.
- Compare API/domain DTOs, EF entities, SQL mappings, and schema for duplicated lifecycle logic or drift.
- Inspect generated SQL and query plans where correctness or performance depends on translation, includes, projection, ordering, pagination, or locking.
- Examine cancellation, async I/O, command timeouts, round trips, payload size, indexing, bulk work, and representative data volumes.
- Replay migration and recovery paths using disposable synthetic data when authorized.

Do not score the layer by percentage EF adoption. State why EF Core or SQL is the appropriate or inappropriate expression of each material case.

## Security, privacy, and access control — domain 05

- Map users, services, external systems, trust boundaries, sensitive assets, authentication flows, session/token lifecycle, and privilege transitions.
- Review authorization at every boundary, practice or tenant isolation, direct-object access, patient portal separation, privileged operations, break-glass behavior, and least privilege.
- Trace ePHI collection, use, disclosure, logging, caching, transport, storage, export, backup, and deletion; check minimum-necessary exposure.
- Inspect validation, injection defenses, browser trust, CORS, CSRF as applicable, secrets, data protection keys, cryptography, dependency risk, rate limiting, and abuse paths.
- Verify that security and audit logs are useful without disclosing sensitive data and that alerts and incident evidence can be produced.
- Inspect when audit status is captured relative to framework result execution, which resource can be correlated, which decisions are omitted, and whether portal, service, and professional paths use equivalent evidence boundaries.

Use current HHS/NIST requirements and OWASP ASVS as verification references. Do not declare HIPAA compliance or legal applicability.

## Performance and scalability — domain 06

- Define the expected user count, concurrency, data size, background activity, latency budget, and deployment topology before judging capacity.
- Measure representative latency distributions, database and network round trips, slow queries, payloads, allocation, blocking, contention, queues, and resource limits.
- Inspect pagination, streaming, batching, caching, timeouts, rate limits, retries, leases, and backpressure.
- Check whether measurements use realistic synthetic volume and include warm/cold and failure conditions where material.
- Prefer removing unnecessary work before adding caches or distributed infrastructure.

Report only the scenario measured and the confidence with which it predicts the target environment.

## APIs, integrations, and interoperability — domain 07

- Inventory routes, methods, DTOs, validation, authorization, errors, versioning, OpenAPI, files, callbacks, and external contracts.
- Check idempotency, ordering, retries, reconciliation, leases, duplicate delivery, partial failure, timeouts, and external outage behavior.
- Trace integration inbox/outbox ownership and confirm that audit, privacy, and recovery rules cross the boundary.
- Compare FHIR behavior with the selected FHIR version and US implementation guide; distinguish local convenience endpoints from standards conformance.
- Inventory potential ONC certification criteria and test methods without treating all criteria as approved scope.

Do not infer interoperability or certification from the presence of FHIR-named code.

## Frontend and accessibility — domain 08

- Trace clinician and portal workflows through loading, empty, success, validation, conflict, unauthorized, error, retry, and recovery states.
- Induce asynchronous failures and verify active focus, status announcements, usable bypass targets, and recovery in the accessibility tree; an axe scan of the initial success state does not cover these behaviors.
- Review information architecture, navigation, state ownership, stale data, optimistic updates, focus management, destructive confirmations, and clinical ambiguity.
- Evaluate WCAG 2.2 Level AA using automated checks plus keyboard, screen-reader semantics, zoom/reflow, contrast, target size, authentication, timing, and error-recovery inspection.
- Compare behavior across the modern, portal, and reference interfaces where they represent the same capability.
- Verify that sensitive data is not unnecessarily retained in URLs, browser storage, logs, screenshots, or error messages.

Automated accessibility success is not a conformance claim. Record manual and specialist validation needs.

## Testing and quality controls — domain 09

- Classify tests by unit, integration, database, contract, browser, migration, recovery, accessibility, performance, and operational purpose.
- Map tests to high-risk workflows and failure modes rather than relying on total count or line coverage.
- Inspect determinism, isolation, data realism, assertions, negative cases, concurrency, mutation sensitivity, flakiness, runtime, and diagnostic quality.
- Verify the real ASP.NET Core pipeline, authorization, PostgreSQL behavior, and browser integration where framework wiring matters.
- Identify false confidence from mocks, happy paths, static scripts, implementation-coupled assertions, or unexercised test files.
- Compare test fixtures with values produced by the real UI and integration contracts so direct database setup does not normalize away a cross-layer mismatch.

## Observability and operations — domain 10

- Map logs, metrics, traces, correlation, audit events, health checks, readiness, liveness, alerts, dashboards, and support workflows.
- Exercise configuration failure, dependency outage, queue stalls, database errors, migration failure, backup/restore, rollback, and downtime behavior when authorized.
- Review secret stores, data-protection persistence, proxy headers, TLS assumptions, deployment identity, least privilege, environment drift, and operational access.
- Confirm that runbooks identify detection, containment, recovery, data reconciliation, and evidence preservation.
- Keep demo/development evidence distinct from production topology claims.

## Dependencies and supply chain — domain 11

- Inventory direct and transitive dependencies, support horizons, lockfiles, container bases, build tools, infrastructure modules, and external downloads.
- Review vulnerabilities, provenance, licenses, update cadence, reproducibility, artifact integrity, secret exposure, and CI permissions.
- Verify clean restore/build behavior and identify unpinned or environment-dependent inputs.
- Separate an available update from an actionable risk; consider compatibility and regression cost.

## Documentation and developer experience — domain 12

- Follow clean-checkout setup, build, test, reset, troubleshooting, architecture discovery, and a representative change path as written.
- Check whether documentation matches current commands, ports, configuration, safety limits, data assumptions, and deployment behavior.
- Inspect decision records, ownership, contribution controls, local feedback speed, generated artifacts, and knowledge concentrated in code or history.
- Record the time and blockers encountered without turning personal preference into a finding.

## Verifier playbook

- Receive the candidate finding and evidence, not a desired outcome.
- Reproduce through a second path or construct a plausible counterexample.
- Check hidden preconditions, compensating controls, affected scope, detectability, reversibility, and severity.
- Compare the cited requirement with the exact implementation context and current version.
- Return `corroborated`, `partially corroborated`, `not reproduced`, `disputed`, or `needs more evidence`, with rationale.
- Never silently edit the original finding; the coordinator records resolution.
