# P2-05-F005 — Account or portal-access disablement does not invalidate existing sessions

- Status: validated
- Domain(s): 05
- Coverage item(s): `COV-002`
- Severity: high
- Production blocker: yes
- Reach: repeated
- Confidence: high
- Reviewer: `phase2_security_privacy`
- Independent verifier: `phase2_verifier`
- Specialist validation: security/privacy, identity
- Baseline commit: `d77a8320e6751a2deb2daf14cf1ac5d6b00cb989`
- Observed on: 2026-08-21

## Condition

Professional account activity is checked at login but not during session resolution. Portal access disablement changes `patients.portal_enabled`, while resolution of an already-issued portal session checks session end/expiry and patient existence but not the access flag. Both identity boundaries issue fixed eight-hour sessions.

## Evidence

- `Data/AuthRepository.cs:30-36` enforces staff account activity during login.
- `AuthRepository.cs:310-415` creates and resolves staff sessions without rechecking the account's active state.
- `Data/PatientRepository.cs:1593-1613` disables portal access by changing the patient flag.
- `Data/PatientPortalRepository.cs:162-205` resolves sessions without rechecking `portal_enabled`; session creation at `PatientPortalRepository.cs:6457-6487` uses an eight-hour lifetime.
- The retained portal lifecycle smoke test toggles the access flag but does not reuse a session issued before disablement.
- Normal logout and absolute expiry are implemented and tested.
- Full trace and checks are in the [COV-002 assessment](../assessments/cov-002-identity-authorization-phi-audit.md).

## Consequence

A previously issued professional or portal session can continue reading protected information after the underlying account or portal access has been disabled, until logout or fixed expiry.

## Cause and reach

Session validity is determined from the session row without re-evaluating the authoritative account/access state. The same lifecycle omission appears independently on the professional and portal boundaries.

## Risk calibration

- Impact: continued access to protected information after intended revocation
- Likelihood or preconditions: a session was issued before disablement and remains available to its holder
- Detectability: access may appear in partial audit data, but the revocation mismatch is not surfaced
- Reversibility: the session eventually expires or can be logged out; information already viewed cannot be recalled
- Severity rationale: high and production-blocking because account disablement must have a prompt and demonstrable effect on active authorization

## Uncertainty and counterevidence

No database-backed disable-and-reuse trace was available. Normal logout and absolute expiry work. No database trigger or external revocation control was found; discovery of one would materially alter the finding.

## Validation record

- Independent method: separate lifecycle trace for login, issuance, resolution, account/access change, logout, expiry, and existing tests on both identity boundaries
- Result: corroborated
- Reviewer agreement or dispute: agreement on high/repeated severity and target production-blocker status
- Specialist conclusion or outstanding need: identity/security owners must define revocation latency and administrator controls; synthetic reuse tests remain outstanding

## Disposition

Validated. The condition is distinct from the missing production identity-provider contract because it is present in the current session implementation.
