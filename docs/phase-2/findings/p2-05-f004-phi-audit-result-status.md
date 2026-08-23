# P2-05-F004 — PHI audit can record response status before a returned result applies its outcome

- Status: validated
- Domain(s): 05, 10
- Coverage item(s): `COV-001`, `COV-002`
- Severity: medium
- Production blocker: no
- Reach: cross-cutting
- Confidence: high
- Reviewer: `phase2_security_privacy`
- Independent verifier: `phase2_verifier`
- Specialist validation: security/privacy
- Baseline commit: `d77a8320e6751a2deb2daf14cf1ac5d6b00cb989`
- Observed on: 2026-08-21

## Condition

For an allowed request, the professional PHI filter reads `HttpContext.Response.StatusCode` after the endpoint delegate returns but before a returned Minimal API `IResult` executes. Results such as `Results.NotFound()` can therefore be recorded with the pre-execution status rather than the outcome sent to the client.

## Evidence

- `Program.cs:1721-1729` returns `IResult` instances for the patient-chart 200 and 404 branches.
- `Program.cs:8877-8885` awaits the endpoint delegate, reads `Response.StatusCode` for the audit event, and then returns the result.
- ASP.NET Core executes the returned `IResult` afterward through `IResult.ExecuteAsync`.
- A disposable .NET 10 reproduction observed `Response.StatusCode == 200` after `Results.NotFound()` was returned and `404` only after `ExecuteAsync` ran.
- The retained baseline smoke test checks audit event/export presence, not recorded 200/404 fidelity.
- Full trace and checks are in the [COV-002 assessment](../assessments/cov-002-identity-authorization-phi-audit.md).

## Consequence

Audit review can misclassify whether a protected resource was actually returned or not found, weakening incident reconstruction and control validation.

## Cause and reach

The audit filter observes handler return rather than completed response execution. The pattern applies to allowed audited endpoints that return an `IResult` without first setting the response status.

## Risk calibration

- Impact: inaccurate outcome evidence for a subset of PHI access events
- Likelihood or preconditions: an audited handler returns a non-200 `IResult` whose status has not already been applied
- Detectability: visible only by comparing stored audit rows with actual responses
- Reversibility: the implementation is localized; past inaccurate outcomes cannot be reconstructed reliably
- Severity rationale: medium because actor/endpoint/authorization evidence remains and the defect affects outcome fidelity rather than granting access

## Uncertainty and counterevidence

Handlers that set the status before returning can be recorded correctly. A database-backed request-to-row trace was unavailable, but the filter order and framework execution timing were independently reproduced. No claim is made that every recorded status is wrong.

## Validation record

- Independent method: source trace plus an isolated .NET 10 execution proof of pre- and post-`ExecuteAsync` status
- Result: corroborated
- Reviewer agreement or dispute: agreement after separating this medium condition from the high resource-correlation finding
- Specialist conclusion or outstanding need: synthetic 200/404 HTTP requests with audit-row comparison should become regression evidence

## Disposition

Validated. No implementation recommendation is accepted.
