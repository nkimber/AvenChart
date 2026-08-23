# P2-05-F010 — Controlled-inventory attestation uses another user's transferable session bearer

- Status: validated
- Domain(s): 03, 05, 07, 08, 09, 10
- Coverage item(s): `COV-007`, `COV-011`, `COV-014`
- Severity: high
- Production blocker: yes
- Reach: repeated across custody movement, count submission, and discrepancy correction
- Confidence: high
- Reviewers: `phase2_frontend_accessibility`, `phase2_security_privacy`
- Independent verifier: separate `phase2_verifier` pass
- Specialist validation: identity/security, controlled-inventory operations, pharmacy, and legal/compliance review outstanding
- Baseline commit: `d77a8320e6751a2deb2daf14cf1ac5d6b00cb989`
- Observed on: 2026-08-21

## Condition

The controlled-inventory workflow asks the initiating user to supply a second user's active staff session UUID. That UUID is the second user's reusable bearer credential. The server derives witness/counter attribution from possession of the credential without a separate action, relevant permission check, facility relationship, consent, challenge, or binding to the exact content.

## Evidence

- The UUID sent as `X-AvenChart-Session` is the staff authentication bearer at `StaffIdentityAdapter.cs:18-37`, `avenchart-ui/src/api.ts:105-113`, and `avenchart-ui/src/auth/session.ts:22-33`.
- `InventoryControlledCountsPanel.tsx:125-142,155-162,194-197` explicitly asks for an “Independent counter session ID” or “Witness session ID” with another authenticated user's session UUID.
- `Program.cs:5826-5850,5875-5878,5885-5888` resolves the supplied body credential and checks only that the session is active and its username differs from the initiating actor.
- DTOs expose the credential fields at `InventoryDtos.cs:90-106,146-153`.
- `InventoryRepository.cs:249-386,424-435` persists token-derived witness/counter identity but no second-party request, permission, confirmation, or content digest.
- The retained smoke test passes a provider bearer inside an administrator's request body at `Test-AvenChartBaseline.ps1:11163-11174,11193-11197`, confirming the supported contract.

## Consequence

The first operator can cause controlled-inventory evidence to name a second user without proof that the second user reviewed or approved the observations. The transferred credential can also be replayed as that user on any route the account can access.

## Cause and reach

Dual control is represented as possession of two bearer tokens in one request instead of two authenticated actions joined to one pending record and content version.

## Risk calibration

The ordinary UI actively requires credential transfer, creating both credential-replay and false-attribution risk in controlled-inventory evidence. This supports high severity and future-production blocker status.

## Uncertainty and counterevidence

The second credential must be random, active, unexpired, and belong to a different username. The primary actor remains authorized; idempotency, stock locks, quantity checks, and immutable events are meaningful controls. They do not prove the second user's intent or entitlement. No statutory or controlled-substance compliance conclusion is made.

## Validation record

Frontend, security/privacy, and independent verifier passes agreed on the condition, severity, and distinct root. A two-account synthetic replay and qualified operating-policy review remain outstanding.

## Disposition

Validated and future-production blocking. It is distinct from identity-provider readiness, session revocation, and missing provenance because the evidence has actor fields but weak proof behind the second actor. No implementation recommendation is made.
