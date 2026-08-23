# P2-R001 — Establish approved identity, resource scope, session, audit, and minimum-necessary boundaries

- **Status:** Proposed — target policy approved by `P2-D016`; implementation is not authorized
- **Linked findings:** `P2-05-F001` through `P2-05-F011`, `P2-03-F005`, `P2-03-F007`
- **Priority band:** Blocker
- **Size:** XL
- **Difficulty:** Exceptional
- **Confidence:** High on engineering need and approved target policy; implementation/provider evidence pending
- **Proposed owner:** Identity/security and privacy program owner
- **Decision owner:** AvenChart program owner
- **Specialist approval needed:** Security, privacy, HIM, legal/retention, clinical operations, infrastructure

## Problem and evidence

Replace local-development bearer identity and broad capability-only PHI access with a vendor-neutral OIDC/OAuth SSO boundary compatible with major providers such as Auth0 and Okta, plus a first-party deterministic test IdP. Enforce resource/facility/purpose policy, session revocation, patient/source resolution, resource-correlated audit, controlled attestation, and approved report/artifact retention. Keep local identity strictly as a disposable test adapter.

The linked `P2-05-*` findings establish local-only identity readiness, absent ordinary patient/facility/purpose enforcement, weak session and browser lifecycle behavior, incomplete PHI-resource audit evidence, report/export governance gaps, and transferable controlled-inventory witness credentials. These are production blockers under the adopted target, not a claim that the current experimental baseline is deployed.

## Target state

An authenticated, vendor-neutral principal is mapped to immutable authorization claims and evaluated against the protected resource, facility, team, purpose, and exceptional-access policy on every supported operation. The modern UI safely manages the resulting session lifecycle, and audits record the executed resource-level outcome without retaining prohibited request content.

## Expected value

Prevent cross-scope access and false actor attribution; make access and mutation evidence reconstructable; establish an auditable basis for future deployment and interoperability decisions.

## Options considered

| Option | Benefits | Costs and risks | Disposition |
| --- | --- | --- | --- |
| Do nothing | No migration | Existing high blockers remain | Rejected for production target |
| Focused provider/scope/audit boundary | Addresses observed causes while retaining ASP.NET/PostgreSQL | Requires policy and data-contract migration | Preferred |
| Replace the application stack | Possible clean boundary | Discards controls, data, and evidence; high migration risk | Rejected without new evidence |

## Acceptance criteria

At least two standards-compatible vendor configurations plus the test IdP are proven. Synthetic cross-facility, patient, purpose, disabled-session, stolen-token, direct-export, and audit-correlation scenarios fail closed with attributable events. Qualified security/privacy/HIM sign-off and a tested rollback preserve audit continuity.

## Dependencies and sequence

Begin with `R007-A` evidence ownership and `R004-A` migration authority. Define OIDC claims and the first-party test IdP before altering protected routes; then establish session/browser lifecycle, resource authorization, and audit/attestation. `P2-R002`, `P2-R003`, and `P2-R006` consume the resulting facility/purpose/resource contract rather than inventing route-local variants.

## Scope and affected contracts

- API authentication middleware, authorization filters, patient/resource resolvers, session models, audit repository, headers/cookies, and problem responses.
- Identity, session, facility, care-team, purpose, exceptional-access, audit, controlled-inventory, and report/export schemas and migrations.
- Modern clinician/portal authentication, sign-out, session-expiry, access-denied, purpose-selection, and break-glass UX. The reference UI remains excluded.
- FHIR/SMART and laboratory-client authorization in coordination with `P2-R006`, plus synthetic test identities, secret/configuration handling, and operations runbooks.

## Delivery risk and rollback

The main risks are locking out valid users, incorrectly denying treatment access, splitting audit history, and issuing incompatible provider claims. Use an isolated test tenant and first-party test IdP, explicit claim-contract tests, feature-gated provider selection, staged compatibility readers, correlation IDs across old/new audit, and a schema compatibility window. Rollback must preserve audit continuity and safely invalidate migrated sessions; it must never restore production use of the legacy bearer adapter.

## Size and difficulty rationale

This is XL because it crosses every protected route, both supported UIs, data contracts, FHIR/laboratory clients, and deployment secrets. It is Exceptional because security, privacy, clinical access, migration, and operations must agree. That breadth does not justify replacing the current stack or creating provider-specific forks.

## Phase 3 change packets

1. **R001-A — Identity and claim contract:** OIDC discovery/JWKS, normalized principal, test IdP, configuration/secret contract, and negative contract tests.
2. **R001-B — Session and browser lifecycle:** server revocation, logout/disable behavior, browser-data cleanup, expiry/recovery UX, migration, and rollback rehearsal.
3. **R001-C — Resource authorization:** facility/purpose/team predicates, exceptional access, patient/report/FHIR/lab resolver enforcement, and cross-scope test matrix.
4. **R001-D — Evidence and attestation:** resource-correlated audit, report-download evidence, content-bound dual attestation, and audit recovery.

## Decision record

- **Decision:** Pending acceptance as a Phase 3 recommendation.
- **Decided by:** AvenChart program owner.
- **Date:** Not set.
- **Rationale and conditions:** `P2-D016` approves the target policy. Acceptance requires named delivery owner(s), specialist validation plan, packet sequencing, rollback ownership, and the acceptance evidence above.
