# P2-05-F006 — Saved encounter templates can persist clinical SOAP content across sign-out and clinician identities

- Status: validated
- Domain(s): 05, 08
- Coverage item(s): `COV-002`, `COV-011`
- Severity: high
- Production blocker: yes
- Reach: isolated
- Confidence: high
- Reviewer: `phase2_security_privacy`
- Independent verifier: `phase2_verifier`
- Specialist validation: security/privacy, clinical workflow
- Baseline commit: `d77a8320e6751a2deb2daf14cf1ac5d6b00cb989`
- Observed on: 2026-08-21

## Condition

When a clinician names a template and selects **Save current**, the current subjective, objective, assessment, and plan fields are serialized to the persistent, unpartitioned `encounter-templates` local-storage key. Professional sign-out removes the session key but not these saved clinical fields.

## Evidence

- `avenchart-ui/src/pages/clinician/NewEncounter.tsx:20-41` defines the stored SOAP structure and the `encounter-templates` local-storage key.
- `NewEncounter.tsx:299-311` copies current SOAP fields into the template; the reachable interface action is at `NewEncounter.tsx:778-791`.
- `ClinicianShell.tsx:404-416` and `avenchart-ui/src/auth/session.ts:22-41` clear only the clinician session key at sign-out.
- No identity partition, logout cleanup, sensitivity warning, or automatic expiry was found for the template key.
- The unrelated recent-patient writer has no caller and is explicitly excluded from this finding.
- Full trace and checks are in the [COV-002 assessment](../assessments/cov-002-identity-authorization-phi-audit.md).

## Consequence

On a shared browser profile, patient-specific clinical narrative saved through this feature remains after logout and can be loaded by a later clinician identity or inspected by someone with local browser access.

## Cause and reach

A convenience feature stores the live encounter fields as reusable browser data without an identity or sensitivity boundary. The condition occurs only when a clinician explicitly saves the current values.

## Risk calibration

- Impact: disclosure of patient clinical narrative
- Likelihood or preconditions: patient-specific values are saved and another person uses or inspects the same browser profile and origin
- Detectability: low to administrators; visible when a later user opens the saved templates
- Reversibility: local data can be removed, but a disclosure cannot be recalled
- Severity rationale: high and production-blocking for shared clinical workstation use because sensitive narrative persists beyond the authenticated lifecycle

## Uncertainty and counterevidence

Templates may sometimes contain only generic text. No browser runtime trace was performed and no XSS path is asserted. React encoding and rich-content sanitization reduce separate injection risks but do not change persistence across user identities.

## Validation record

- Independent method: separate route, UI-action, serialization, storage, sign-out, and cleanup search; unreachable recent-patient code was deliberately excluded
- Result: corroborated
- Reviewer agreement or dispute: agreement on the narrowed SOAP-template condition, high severity, and target production-blocker status
- Specialist conclusion or outstanding need: security/privacy and clinical-workflow owners must define whether reusable templates may contain patient-specific text; shared-browser trace remains outstanding

## Disposition

Validated. No general local-storage or XSS conclusion is made, and no implementation recommendation is accepted.
