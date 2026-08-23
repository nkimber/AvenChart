# P2-05-F001 — No professional, portal, or service identity provider is approved for production

- Status: validated
- Domain(s): 05
- Coverage item(s): `COV-002`
- Severity: high
- Production blocker: yes
- Reach: systemic
- Confidence: high
- Reviewer: `phase2_security_privacy`
- Independent verifier: `phase2_verifier`
- Specialist validation: security/privacy, identity, database/operations
- Baseline commit: `d77a8320e6751a2deb2daf14cf1ac5d6b00cb989`
- Observed on: 2026-08-21

## Condition

The application registers a local-development staff identity adapter. No professional, portal, or service provider is approved for production, and no approved issuer, audience, signature, MFA, device, facility-claim, tenant, or workload-identity contract exists. The repository's own identity-readiness catalog marks these gaps as production-blocking.

## Evidence

- `avenchart/backend/src/AvenChart.Api/Program.cs:157` registers `LocalDevelopmentStaffIdentityAdapter`.
- `Security/IdentityProviderCatalog.cs:12-65` marks all modeled identity types as not production approved.
- `IdentityProviderCatalog.cs:108-151` marks the provider, token-validation, assurance, claims, portal/service, emergency-identity, and tenant-proof gaps with `BlocksProduction: true`.
- `Security/StaffIdentityAdapter.cs:18-38` resolves a GUID supplied in `X-AvenChart-Session` through the local authentication repository.
- The adapter, password and session mechanisms work for the local synthetic experiment; the catalog is a readiness record, not a runtime deployment guard.
- In the deterministic runtime, the identity-readiness endpoint reported `local-identity-adapter-v1`, zero production-approved providers, zero cryptographically validated providers, zero facility-scoped identities, and seven production-blocking gaps.
- Full trace and checks are in the [COV-002 assessment](../assessments/cov-002-identity-authorization-phi-audit.md).

## Consequence

A future production deployment would lack an approved identity-assurance and lifecycle boundary across professional, patient, and workload identities.

## Cause and reach

Phase 1 deliberately implemented local demo identity while preserving unresolved production choices. The condition affects every protected professional route and the entire portal/service identity boundary.

## Risk calibration

- Impact: unauthorized or insufficiently assured access to clinical and administrative information
- Likelihood or preconditions: deployment beyond the controlled local experiment without replacing and approving the identity boundary
- Detectability: high in source and configuration; misuse may not be immediately detectable
- Reversibility: identity and account migration becomes more difficult after deployment
- Severity rationale: high and production-blocking because an explicit, effective authentication boundary is a required production safeguard and the repository itself records the missing contract as blocking

## Uncertainty and counterevidence

PBKDF2 password verification, centralized professional session resolution, logout, expiry, capability checks, and global rate limiting are meaningful experimental controls. No production tenant or deployment target was supplied. Approved provider and tenant contracts plus independent verification would materially change this finding.

## Validation record

- Independent method: separate source trace across registration, adapter, authentication repository, provider catalog, and deployment configuration
- Result: corroborated statically and reproduced through the synthetic runtime readiness endpoint
- Reviewer agreement or dispute: agreement on high/systemic severity and production-blocker status
- Specialist conclusion or outstanding need: identity, security/privacy, and operations owners must approve the target contracts

## Disposition

Validated. `P2-D014` selects a vendor-neutral standards-based SSO boundary, support for major providers, and a first-party non-production test IdP. `P2-D016` approves OpenID Connect discovery/JWKS, Authorization Code with PKCE, explicit token validation, MFA/assurance claims, revocation, and workload identity as the first-release contract while deferring SAML to a customer-driven adapter. Provider-specific and independent acceptance evidence remains open; no implementation recommendation is accepted.
