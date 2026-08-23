# AvenChart Phase 2 quality standard

## Purpose and status

This standard defines what “good” means for the Phase 2 assessment. It is an engineering assessment framework, not legal advice, clinical validation, an accessibility conformance report, an ONC certification determination, or authorization for production use.

The standard evaluates the fixed Phase 1 baseline as a candidate foundation for a future production-capable US ambulatory EHR. Conclusions must distinguish present implementation evidence from future organizational, operational, legal, and certification work.

## Decision hierarchy

When criteria conflict, use this order unless the program owner records an exception:

1. Patient and user safety
2. Clinical and data correctness
3. Privacy, security, and access control
4. Availability, recoverability, and auditability
5. Regulatory and contractual obligations
6. Accessibility and usability
7. Maintainability and human comprehension
8. Measured performance and scalability
9. Delivery efficiency and cost
10. Stylistic consistency

Higher-ranked qualities are not unlimited trump cards. Reviewers must still explain the actual tradeoff, evidence, affected scope, and proportionality.

## Three kinds of expectations

### Required production safeguards

These are assessed as potential production blockers when absent or materially ineffective:

- correct patient identity, record association, lifecycle behavior, calculations, and clinical state transitions;
- confidentiality, integrity, and availability of electronic protected health information;
- explicit authentication, authorization, least privilege, sensitive action auditing, and minimum-necessary data exposure;
- safe failure behavior, backup, restore, continuity, and recovery for clinically relevant workflows;
- controlled configuration, secrets, dependencies, deployment, rollback, and operational access;
- reliable traceability of material data mutations and privileged actions;
- validation appropriate to the risk of the workflow and the consequences of an error.

Applicable requirements must be confirmed against the current authoritative rule and by qualified specialists. Phase 2 may identify readiness gaps; it cannot certify compliance.

### Project quality targets

These are AvenChart’s adopted engineering defaults:

- WCAG 2.2 Level AA is the accessibility target for both web interfaces, supported by automated and manual evaluation.
- The simplest architecture that satisfies the accepted requirements is preferred.
- Responsibilities and dependency direction are explicit enough that a human maintainer can trace a user action through UI, API, domain behavior, persistence, and operations.
- Business and clinical rules are not hidden in transport, persistence, rendering, or incidental infrastructure code.
- API contracts are explicit, validated, consistently authorized, version-conscious, and use predictable error semantics.
- Tests are organized by risk and prove important behavior at unit, integration, database, contract, browser, and recovery boundaries as appropriate.
- Performance decisions are based on representative measurement, query behavior, payloads, round trips, contention, and operating assumptions.
- Logs, metrics, traces, health signals, and audit records support diagnosis without disclosing sensitive information.
- Documentation enables a clean-checkout build, safe synthetic operation, architectural understanding, and repeatable verification.

### Future certification and market readiness

Phase 2 inventories readiness for ONC Health IT Certification, interoperability obligations, FHIR-based exchange, and other potential market requirements. These are reported as:

- already supported with evidence;
- partially supported;
- unsupported;
- unknown pending product-scope or specialist decision;
- not applicable with an approved rationale.

Certification criteria are not silently converted into product requirements. The program owner must choose the intended certification scope before Phase 3 plans certification-specific implementation.

## Solution principles

### Evidence before preference

A recommendation must solve an observed problem or produce a measurable opportunity. “Modern,” “clean,” “enterprise,” and named patterns are not benefits without a system-specific consequence.

### Simple, cohesive boundaries

Prefer cohesive feature or business-capability boundaries and explicit cross-cutting ownership. Do not add services, repositories, abstractions, projects, message buses, microservices, CQRS, or other layers solely to match an architectural fashion.

### Current stack is a rebuttable constraint

ASP.NET Core, PostgreSQL, EF Core, React, TypeScript, and Azure remain the presumptive platform. A replacement proposal must show:

1. an accepted requirement the current choice cannot meet proportionately;
2. evidence that configuration, restructuring, or focused replacement is insufficient;
3. migration, operational, security, data, training, and rollback consequences;
4. a materially better alternative under the same constraints.

### EF Core and SQL are complementary tools

Prefer EF Core for ordinary entity retrieval and mutation, relationships, change tracking, optimistic concurrency, transactions, and queries that remain clear and observable. Prefer parameterized SQL when database-specific behavior, bulk work, complex reporting, carefully controlled locking, or measured query requirements make it materially clearer or safer.

Do not count SQL statements or EF usage as a quality score. Assess:

- correctness and injection resistance;
- transaction and concurrency boundaries;
- query plans, indexes, payload projection, round trips, and cancellation;
- duplication of mapping and lifecycle logic;
- testability and observability;
- schema ownership and migration compatibility;
- whether the chosen mechanism is the simplest reliable expression of the requirement.

Raw SQL requires a stated reason and focused verification. EF Core requires inspection of generated behavior where performance or correctness depends on it.

### Framework-aligned ASP.NET Core

Assess whether the API uses the modern host and middleware pipeline coherently, keeps handlers thin, applies authorization at boundaries, validates configuration early, treats `DbContext` as request-scoped, uses explicit DTO contracts, produces predictable problem responses, and provides meaningful health and operational signals. Framework alignment is a means to reduce custom risk, not a reason for wholesale rewrites.

### Secure and private by design

Treat browser state and external input as untrusted. Review trust boundaries, identity proof, session behavior, authorization policy, tenant or practice isolation, sensitive data flow, auditability, secrets, cryptography, dependency integrity, and misuse cases. Parameterization is necessary but not sufficient for data-layer security.

### Safety is a system property

Clinical safety review covers workflows, user interface, configuration, data, integration, downtime, communication, patient matching, decision support, test-result follow-up, and operational recovery. Passing unit tests or matching an upstream screen does not establish safe behavior.

### Measure performance in context

Performance findings state the workload, data volume, environment, percentile or distribution, resource behavior, and expected operating limit. Prefer removing unnecessary work and round trips before introducing caches or special infrastructure. A synthetic benchmark is evidence only for the scenario it measures.

### Risk-shaped testing

Test depth follows consequence and uncertainty. Safety-critical and irreversible behavior needs boundary, negative, concurrency, failure, recovery, and audit evidence. Test count and line coverage are supporting indicators, not substitutes for behavioral proof.

## Domain rating model

Each domain receives one evidence-anchored maturity rating:

| Rating | Meaning |
| --- | --- |
| 0 — Unacceptable | A known condition makes production or clinical reliance unacceptable, or essential controls are absent. |
| 1 — Ad hoc | Important behavior exists but is inconsistent, weakly controlled, difficult to reproduce, or dependent on individual knowledge. |
| 2 — Partial | A reasonable approach exists in meaningful areas, but material gaps, inconsistency, or unvalidated assumptions remain. |
| 3 — Controlled | The approach is consistent, documented, tested proportionately, operationally supportable, and fit for the stated target with no unresolved blocker. |
| 4 — Measured | Controlled behavior is additionally measured, monitored, exercised, and improved using representative evidence. |

Every rating also records confidence (`low`, `medium`, or `high`), coverage, strengths, findings, and unresolved unknowns. No aggregate numeric score is used for go/no-go decisions.

## Authoritative reference families

Reviewers must verify that they are using the current applicable version and cite the specific requirement used.

- [HHS HIPAA Security Rule](https://www.hhs.gov/hipaa/for-professionals/security/index.html) and [HHS HIPAA Privacy Rule](https://www.hhs.gov/hipaa/for-professionals/privacy/index.html)
- [NIST SP 800-66 Rev. 2](https://csrc.nist.gov/pubs/sp/800/66/r2/final) for HIPAA Security Rule implementation context
- [NIST Secure Software Development Framework](https://csrc.nist.gov/pubs/sp/800/218/final)
- [OWASP Application Security Verification Standard](https://owasp.org/www-project-application-security-verification-standard/)
- [2025 SAFER Guides](https://healthit.gov/clinical-quality-and-safety/safer-guides/)
- [ONC Health IT Certification Program](https://healthit.gov/certification-health-it) and the current regulations and test methods for any selected scope
- [HL7 FHIR specification](https://hl7.org/fhir/) and selected US implementation guides
- [WCAG 2.2](https://www.w3.org/TR/WCAG22/), with Level AA as the AvenChart target
- [ASP.NET Core documentation](https://learn.microsoft.com/aspnet/core/), [EF Core documentation](https://learn.microsoft.com/ef/core/), and [Npgsql documentation](https://www.npgsql.org/doc/)
- [PostgreSQL documentation](https://www.postgresql.org/docs/current/)

These references inform the assessment; they do not make every referenced recommendation mandatory or establish compliance by citation alone.

## Recommendation acceptance test

A recommendation is eligible for acceptance only when it:

- links to validated findings or a separately justified opportunity;
- defines a target state without prematurely prescribing unnecessary implementation detail;
- explains patient, user, risk, quality, delivery, or cost value;
- evaluates viable alternatives and the do-nothing option;
- identifies prerequisites, affected contracts and data, migration order, reversibility, and rollback;
- separates size, difficulty, priority, and confidence;
- contains measurable acceptance criteria and evidence expected in Phase 3;
- names required clinical, security, privacy, legal, accessibility, certification, or operational validation.
