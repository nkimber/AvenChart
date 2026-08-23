# P2-R007 — Build a risk-shaped verification, release provenance, and operational-evidence gate

- **Status:** Proposed — target policy approved by `P2-D016`; implementation is not authorized
- **Linked findings:** `P2-09-F001`, `P2-09-F002`, `P2-04-F001`, and residual COV-014/COV-015/COV-016/COV-017/COV-018 evidence gaps
- **Priority band:** Foundation
- **Size:** L
- **Difficulty:** Medium
- **Confidence:** High on evidence gap and approved release floor; implementation cost and operational evidence pending
- **Proposed owner:** Quality, release, and operations lead
- **Decision owner:** AvenChart program owner
- **Specialist approval needed:** Database/operations, release engineering, accessibility, security, clinical owners

## Problem and evidence

Replace the build-plus-modern-unit default gate with documented risk tiers covering API/database/browser/accessibility/workflow/recovery suites, and add reproducible parity provenance, SBOM/license/vulnerability evidence, signed release artifacts, deployment checks, and operational runbooks.

The linked verification and runtime-readiness evidence shows clean build/unit/lint coverage alongside missing or failing broad browser, API, database, recovery, accessibility, empty-bootstrap, deployment, provenance, and current-environment proof. The historical parity percentage is not traceable to a denominator/evidence matrix. This is an evidence-governance gap, not a claim that every suite should run on every pull request.

## Target state

Risk-proportionate, owned, fresh synthetic evidence controls acceptance and release decisions; artifacts, dependencies, deployment state, recovery capability, residual risk, and parity claims are traceable to the version being assessed.

## Expected value

Reduce false confidence, make the 86% historical claim auditable or retire it, and provide a repeatable release decision with known residual risk and recovery evidence.

## Options considered

Compare pull-request-only, scheduled/full-suite, and release-gated tiers. Start by mapping existing suites and costs; do not require every expensive suite on every pull request. Preserve a fallback gate while new tiers are piloted.

## Dependencies and sequence

`R007-A` starts before other implementation as the shared test/evidence manifest. Each recommendation owns its critical scenarios and contributes them to appropriate pull-request, scheduled, or release tiers. `R007-B/D` follows when the relevant database, browser, migration, and deployment surfaces exist; `R007-C/E` consolidates provenance and final readiness only after the delivery waves have current evidence.

## Acceptance criteria

Every required risk has an owned suite and freshness rule; critical browser/database/recovery/accessibility scenarios run in a synthetic environment; parity denominator/evidence ledger is versioned or the claim is withdrawn; SBOM/license/vulnerability/provenance artifacts are retained; backup/restore and deployment rollback are rehearsed; program owner and specialists approve the gate.

## Scope and affected contracts

- CI tier definitions, test manifest/freshness policy, synthetic database/browser environments, API/database/Playwright/accessibility/recovery suites, failure retention, and release evidence index.
- Build outputs, dependency/SBOM/license/vulnerability/provenance records, versioning, deployment verification, rollback, backup/restore, incident/recovery, observability, and operating runbooks.
- The production-worthiness release gate that evaluates all Phase 3 work. This does not authorize a production deployment or close any Phase 2 gate.

## Delivery risk and rollback

Over-broad or flaky gates can halt safe delivery or train teams to bypass evidence. Start with an owned manifest and measured pilot tier, make quarantine/expiry rules explicit, preserve the existing fallback build gate only while its limitations are visible, and publish failure diagnostics. Rollback means returning a release process to a previously proven tier while retaining all failed evidence and tracking the remediation deadline.

## Size and difficulty rationale

This is Large because it makes multiple technical streams auditable and repeatable. Difficulty is Medium: the mechanisms are conventional, but meaningful evidence requires stable synthetic infrastructure, realistic fault scenarios, role ownership, and disciplined operation over time.

## Phase 3 change packets

1. **R007-A — Verification manifest and baseline:** inventory suites, risks, owners, environments, freshness, known exclusions, and parity-evidence disposition.
2. **R007-B — Database, browser, and recovery evidence:** wire critical concurrency, migration, browser-failure, accessibility, backup/restore, and fault-injection suites into risk tiers.
3. **R007-C — Supply-chain and artifact provenance:** SBOM, license/vulnerability review, signed/versioned outputs, evidence retention, and release traceability.
4. **R007-D — Deployment and operating proof:** deployment validation, secure configuration/secret handling, monitoring/alerts, rollback, incident, and recovery runbooks.
5. **R007-E — Production-worthiness decision process:** release checklist, residual-risk register, specialist approvals, gate evaluation, and evidence handoff.

## Decision record

- **Decision:** Pending acceptance as a Phase 3 recommendation.
- **Decided by:** AvenChart program owner.
- **Date:** Not set.
- **Rationale and conditions:** `P2-D016` approves the release-evidence target. Acceptance requires named quality/release/operations owners, a costed suite plan, fallback/expiry policy, evidence retention owner, and the acceptance evidence above.
