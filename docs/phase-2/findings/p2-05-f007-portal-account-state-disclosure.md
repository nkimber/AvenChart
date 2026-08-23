# P2-05-F007 — Portal login reveals selected account lifecycle states before password verification

- Status: validated
- Domain(s): 05
- Coverage item(s): `COV-002`, `COV-012`
- Severity: medium
- Production blocker: no
- Reach: repeated
- Confidence: high
- Reviewer: `phase2_security_privacy`
- Independent verifier: `phase2_verifier`
- Specialist validation: security/privacy
- Baseline commit: `d77a8320e6751a2deb2daf14cf1ac5d6b00cb989`
- Observed on: 2026-08-21

## Condition

For an existing portal username, one-time-reset, pending-password-setup, and disabled-access states return distinct responses before the submitted password is verified. Unknown usernames and active accounts with an incorrect password return the generic invalid-credentials response.

## Evidence

- `Data/PatientPortalRepository.cs:72-139` looks up the username and returns state-specific failures at lines 121-134; password verification starts afterward.
- The distinct responses reveal that a guessed username exists in one of the selected states and disclose that state.
- A global per-IP rate limiter and strong password hashing are present; no authentication bypass results from this condition.
- No dedicated portal failed-login audit or per-account lockout was found.
- Full trace and checks are in the [COV-002 assessment](../assessments/cov-002-identity-authorization-phi-audit.md).

## Consequence

An unauthenticated requester can confirm selected portal usernames and learn their account state, improving subsequent targeting or social-engineering information.

## Cause and reach

The login flow provides workflow-specific guidance before credential verification. It repeats for every account in the affected lifecycle states.

## Risk calibration

- Impact: bounded account discovery and state disclosure
- Likelihood or preconditions: the requester guesses or knows an affected username
- Detectability: globally rate-limited requests may be observable, but dedicated portal login audit was not found
- Reversibility: response behavior can change; information already disclosed cannot be withdrawn
- Severity rationale: medium because the behavior does not bypass authentication and active bad-password and unknown-account responses remain indistinguishable

## Uncertainty and counterevidence

The deployed rate-limit topology and proxy-derived client identity were not runtime-validated. The distinct guidance may improve legitimate recovery experience. That tradeoff requires an explicit security/product decision rather than an assumed preference.

## Validation record

- Independent method: separate branch-order and response comparison plus countercontrol search
- Result: corroborated
- Reviewer agreement or dispute: agreement on medium/repeated severity and non-blocker status
- Specialist conclusion or outstanding need: security/product review and a synthetic response-matrix test remain outstanding

## Disposition

Validated. No implementation recommendation is accepted.
