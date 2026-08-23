# Pilot A — Authentication, authorization, and PHI boundary

## Packet

- Baseline: `d77a8320e6751a2deb2daf14cf1ac5d6b00cb989`
- Coverage sampled: COV-001, COV-002, COV-011, COV-012
- Trace: professional sign-in through one patient chart request, with patient-portal scope as counterevidence
- Reviewers: security/privacy specialist; architecture/API-boundary specialist
- Required human validation: security/privacy and clinical operations; legal/compliance for regulatory conclusions

## Independent pass 1

The security/privacy reviewer traced browser session handling, sign-in, password verification, server session resolution, centralized capability authorization, patient lookup, access-decision audit, and failure behavior. The product tree matched the fixed baseline. No active API/database runtime was available, so this was a Level 0 source trace with existing workflow proofs inspected as counterevidence.

Material strengths:

- staff patient routes share a centralized server-side authorization boundary with 401/403 handling and access-decision audit;
- staff identity resolution is behind an adapter seam;
- local password hashing uses PBKDF2-SHA256, 600,000 iterations, random salts, and fixed-time comparison;
- protected clinician content is withheld while session validation is unresolved or unavailable;
- patient-portal reads derive patient identity from the server-side portal session rather than a browser-selected patient;
- central access records omit request bodies and direct patient identifiers, reducing secondary PHI exposure;
- existing synthetic contracts cover missing, malformed, expired, and revoked sessions plus capability denial and login/logout behavior.

Candidate conditions from pass 1:

| Candidate | Initial severity | Confidence | Key evidence | Specialist need |
| --- | --- | --- | --- | --- |
| The active staff identity mechanism is explicitly local-development only; production assurance, MFA, recovery, issuer/claim validation, device policy, and facility-scope decisions remain unresolved. | High; production blocker for the adopted target | High | `IdentityProviderCatalog.cs:12-38,108-176`; `AuthRepository.cs:310-415`; `auth/session.ts:22-40` | Security/privacy |
| Staff authorization checks capability but not practice, facility, patient/team relationship, or purpose. A staff user with the patient-view capability can request any known patient identifier in the shared table. | High; production blocker status proposed | High | `AuthRepository.cs:127-157`; `AuthorizationPolicyCatalog.cs:139-187`; `Program.cs:962-964,1721-1729`; `PatientRepository.cs:105-118,230-242` | Security/privacy and clinical operations |
| The shared PHI access record cannot identify the patient/resource accessed, and ordinary portal reads do not use an equivalent general PHI-access audit. | Medium, systemic | High for implementation; medium for compliance consequence | `V0004__phi_access_audit.sql`; `PhiAuditRepository.cs:12-42`; `Program.cs:437-895,8854-8897`; `PatientPortalRepository.cs:251-330` | Security/privacy and legal/compliance |

## Independent pass 2

The architecture/API-boundary reviewer independently followed the same professional-login and patient-detail path, built the .NET solution in Release mode with no warnings or errors, and ran 68 focused transport, shell, identity-provider, and API tests successfully. Database-backed behavior remained a static trace because no synthetic runtime was active.

The reviewer independently confirmed the development-only identity boundary and the unscoped, broad patient-detail permission. It also found two additional boundary conditions:

- HTTPS redirection and HSTS are conditional on `RuntimeSafety.RequireHttps`, the checked-in default is false, the modern UI defaults to an HTTP API, and Compose exposes that HTTP port. This is a target-readiness condition until an approved TLS-terminating deployment topology is supplied, not evidence that the loopback development topology is presently unsafe.
- The professional PHI audit reads `HttpContext.Response.StatusCode` after a handler returns an `IResult` but before that result is executed. In addition to omitting patient/resource identity, an allowed handler returning 404 or another result can therefore be recorded with the pre-execution status. Unauthenticated 401 exits before an audit write. Runtime reproduction is still required.

## Reconciliation

Both reviewers traced the same execution boundary and independently agreed on its two most material conditions: professional identity is explicitly not production-approved, and patient-detail authorization is application-capability-only rather than facility, relationship, or purpose scoped. Both also agreed that the representative response is a broad PHI aggregate and that the access-decision record cannot identify which patient was accessed. Their severity judgments were equal for identity and authorization and differed by one level for audit completeness, which is within the pilot’s acceptable range and will be resolved after runtime evidence and specialist review.

The transport-default condition and pre-execution audit-status behavior were found by pass 2 only. They remain source-supported candidate conditions rather than accepted conclusions until the verifier checks configuration boundaries and the status behavior is reproduced.

## Independent verification

The verifier, who did not author either pass, reached these dispositions:

| Cluster | Verifier disposition | Reconciled severity/confidence |
| --- | --- | --- |
| Development-only professional identity | Corroborated | High; production blocker in the repository’s own target-readiness model; high confidence |
| Capability-only patient access and broad aggregate | Engineering behavior corroborated; minimum-necessary adequacy reserved for specialists | High and systemic; production-blocker decision pending; high confidence |
| Conditional/default-off application HTTPS | Partially corroborated and narrowed: checked-in Azure ingress has `allowInsecure: false`, so an externally exposed plaintext Azure path was not reproduced | Medium topology-assurance gap unless a deployment permits plaintext; high source confidence |
| Audit resource identity and portal coverage | Corroborated | High production-readiness concern; high confidence for implementation, specialist consequence pending |
| Pre-`IResult` response-status fidelity | Plausible from filter ordering but not reproduced | Medium, `needs-more-evidence` |

The Azure ingress countercontrol is recorded in `infra/azure/operations/application.bicep:45-60,138-145`. Runtime evidence must still confirm the deployed topology, alternate paths, forwarded-protocol behavior, HSTS ownership, and certificate/domain configuration. Audit-status fidelity needs safe synthetic 201/204/400/404 experiments.

Material reviewer agreement is acceptable after these distinctions. The current synthetic single-organization context, strong password/session mechanics, centralized ACL, patient-bound portal queries, specialized portal message/report audit, and deliberate audit-data minimization remain important counterevidence. These are target-readiness conditions, not proof of an active production breach or an authentication bypass in the local demo.
