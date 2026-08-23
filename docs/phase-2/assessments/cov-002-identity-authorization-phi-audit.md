# COV-002 assessment — identity, authorization, and PHI audit

- Status: in review
- Baseline: `phase-1-experimental` at `d77a8320e6751a2deb2daf14cf1ac5d6b00cb989`
- Observed on: 2026-08-21
- Primary reviewer: `phase2_security_privacy`
- Independent verifier: `phase2_verifier`
- Primary coverage: `COV-002`
- Supporting coverage: `COV-001`, `COV-011`, `COV-012`, `COV-014`, `COV-015`, `COV-016`
- Evidence level: source and framework reproduction, clean build, UI tests, dependency advisory checks; database-backed runtime scenarios remain outstanding

## Assessment question

Does the fixed Phase 1 baseline establish a production-appropriate identity boundary, resource-level authorization, session lifecycle, browser-data lifecycle, and reconstructable audit trail for professional and patient access to protected health information?

This is an engineering-readiness assessment. HHS material identifies questions requiring privacy or legal interpretation; this packet does not claim HIPAA compliance or noncompliance and does not authorize production use.

## Representative traces

### Professional chart access

1. `POST /api/auth/login` calls `AuthRepository.LoginAsync`.
2. An active local account with a valid PBKDF2 password receives an eight-hour GUID session.
3. A later request supplies `X-AvenChart-Session`.
4. `LocalDevelopmentStaffIdentityAdapter` resolves the session and `AccessPermissionFilter` checks a SEC-01 capability.
5. `/api/patients` and `/api/patients/{canonicalId}` use broad repository reads without a patient, care-team, facility, or purpose predicate.
6. The filter writes a PHI audit event after the endpoint delegate returns but before a returned `IResult` executes.

### Patient portal access

1. Portal login calls `PatientPortalRepository` outside the staff identity and capability pipeline.
2. The resulting session contains the canonical patient identity.
3. Ordinary portal reads derive the patient from that session rather than accepting a browser-selected patient identifier.
4. Ordinary portal home, profile, appointment, clinical-summary, laboratory, and document reads do not pass through the general PHI audit. Specialized message and generated-report actions have dedicated audit events.
5. Disabling portal access changes `patients.portal_enabled`, but resolution of an already-issued portal session does not re-evaluate that flag.

## Reproducible checks

| Check | Result |
| --- | --- |
| Resolve the Phase 1 tag and compare `avenchart/`, `avenchart-ui/`, and `infra/` with the baseline | Baseline resolved; product tree remained unchanged during assessment |
| Release build | Passed with 0 warnings and 0 errors |
| Modern UI suite | 31 files and 178 tests passed |
| Focused identity/session UI slice | 4 files and 68 tests passed |
| NuGet vulnerable-package check | No known vulnerable packages reported by configured sources |
| npm production dependency audit | Zero vulnerabilities reported |
| Minimal API status-timing reproduction | `Results.NotFound()` left `Response.StatusCode` at 200 after handler return and changed it to 404 only when `ExecuteAsync` ran |
| Database-backed HTTP and session-revocation scenarios | Not run: Docker/PostgreSQL was unavailable in the assessment environment |

One attempted UI invocation used Vitest's unsupported `--runInBand` option and failed before tests ran. The correct repository command subsequently passed; the failed attempt is retained because it does not provide test evidence.

## Material strengths and counterevidence

- Password hashing uses PBKDF2-SHA256 with 600,000 iterations, random salts, bounded parsing, fixed-time comparison, and legacy-hash upgrade.
- Staff authentication and capability checks are centralized. Missing, expired, and logout-ended sessions return 401; capability failures return 403.
- Staff invalid-credential responses are generic. A global fixed-window per-IP limiter and fixed local CORS allowlist are present.
- Portal ordinary reads are bound to the patient stored in the portal session; no ordinary portal path reviewed accepted an arbitrary patient identifier from the browser.
- Specialized reporting and configuration-delegation paths demonstrate facility, purpose, and delegated-scope controls. These do not protect ordinary chart access but disprove a claim that scoped authorization is absent everywhere.
- The PHI audit intentionally excludes bodies, query strings, document content, and patient identifiers, reducing secondary PHI exposure. Dedicated portal message/report audits carry richer context.
- Reviewed SQL is parameterized. XML import prohibits DTDs and bounds input. Print and rich-HTML paths use encoding or sanitization.
- Azure external ingress disables insecure transport and uses managed identity and Key Vault references. The approved production topology remains unknown.
- No confirmed injection, XXE, plaintext production secret, cryptographic primitive defect, wildcard credentialed CORS policy, executable XSS path, or known vulnerable production dependency was found.

## Validated findings

| Finding | Condition | Severity | Reach | Production blocker |
| --- | --- | --- | --- | --- |
| [`P2-05-F001`](../findings/p2-05-f001-no-production-identity-provider.md) | No professional, portal, or service identity provider is approved for production | High | Systemic | Yes |
| [`P2-05-F002`](../findings/p2-05-f002-chart-access-not-resource-scoped.md) | Ordinary professional chart access is capability-scoped but not patient-, team-, facility-, or purpose-scoped | High | Systemic | Yes |
| [`P2-05-F003`](../findings/p2-05-f003-phi-audit-resource-correlation.md) | PHI access evidence cannot correlate ordinary reads to the protected patient resource | High | Systemic | Yes |
| [`P2-05-F004`](../findings/p2-05-f004-phi-audit-result-status.md) | PHI audit can record response status before a returned `IResult` applies its outcome | Medium | Cross-cutting | No |
| [`P2-05-F005`](../findings/p2-05-f005-session-disablement-revocation.md) | Account or portal-access disablement does not invalidate existing sessions | High | Repeated | Yes |
| [`P2-05-F006`](../findings/p2-05-f006-soap-template-browser-persistence.md) | Saved encounter templates can persist clinical SOAP content across sign-out and clinician identities | High | Isolated | Yes |
| [`P2-05-F007`](../findings/p2-05-f007-portal-account-state-disclosure.md) | Portal login reveals selected account lifecycle states before password verification | Medium | Repeated | No |

The engineering conditions and severity calibration were independently reproduced. The five high findings are production blockers against AvenChart's adopted future-production quality target, not claims that the Phase 1 synthetic experiment caused a breach or violated a statute.

## Narrowed or rejected claims

- The recent-patient cache is not a finding: `recordRecentPatient` has no caller in the current UI.
- JavaScript-readable bearer sessions remain a threat-model candidate, not a finding; no executable XSS or hostile same-origin path was established.
- Missing general `Cache-Control: no-store`, Content Security Policy, `Clear-Site-Data`, and forwarded-header configuration are evidence gaps pending deployed response and topology traces. No actual cache disclosure, XSS, or proxy exploit is claimed.
- Audit retention, purge, and tamper-resistance policy was not located. This remains an operations/privacy decision and evidence gap rather than a statutory conclusion.
- Azure ingress is counterevidence to an external plaintext-transport finding. The local HTTP runtime does not establish the future production topology.

## Authoritative target references

- [HHS minimum-necessary guidance](https://www.hhs.gov/hipaa/for-professionals/privacy/guidance/minimum-necessary-requirement/index.html)
- [HHS Security Rule guidance](https://www.hhs.gov/hipaa/for-professionals/security/index.html)
- [HHS audit protocol](https://www.hhs.gov/hipaa/for-professionals/compliance-enforcement/audit/protocol/index.html)
- [NIST SP 800-63B session guidance](https://pages.nist.gov/800-63-4/sp800-63b.html)
- [OWASP ASVS 5.0](https://owasp.org/www-project-application-security-verification-standard/)
- [OWASP Authentication Cheat Sheet](https://cheatsheetseries.owasp.org/cheatsheets/Authentication_Cheat_Sheet.html)
- [ASP.NET Core Minimal API responses](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/minimal-apis/responses?view=aspnetcore-10.0)

## Required specialist decisions and remaining evidence

- Clinical operations and privacy owners must decide the required patient, care-team, facility, purpose, and exceptional-access model.
- Identity/security and operations owners must approve the production identity provider, assurance, claims, tenant, session, revocation, and workload-identity contracts.
- Privacy/legal and operations owners must approve audit subject correlation, outcome fidelity, retention, access, minimization, and forensics requirements.
- A disposable synthetic runtime must exercise staff and portal disable-and-reuse, cross-facility/patient access, known/unknown patient audit rows, ordinary portal audit coverage, the portal login response matrix, shared-workstation template persistence, and deployed response headers.

`COV-002` remains **In review** because these owner decisions and runtime negative scenarios are still outstanding. The findings themselves are validated engineering conditions and may now support later recommendation analysis; they do not authorize product changes.
