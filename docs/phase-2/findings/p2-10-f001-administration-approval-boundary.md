# P2-10-F001 — Administration change governance does not enforce independent approval

- Status: validated mechanism; sensitive configuration policy approved
- Domain(s): 01, 05, 09, 10
- Coverage item(s): `COV-001`, `COV-007`, `COV-014`
- Severity: medium
- Production blocker: unknown
- Reach: systemic across named configuration families
- Confidence: high on mechanism; medium on consequence
- Reviewers: `phase2_quality_operations`, `phase2_security_privacy`
- Independent verifier: separate `phase2_verifier` pass; partially corroborated pending policy
- Specialist validation: practice operations, clinical configuration, identity/security, and separation-of-duties acceptance evidence outstanding
- Baseline commit: `d77a8320e6751a2deb2daf14cf1ac5d6b00cb989`
- Observed on: 2026-08-21

## Condition

Configuration change requests retain ordered/versioned evidence but do not enforce creator, approver, and activator separation. The same administration capability also exposes direct mutation and rollback routes, so the change-request lifecycle is not a mandatory technical boundary.

## Evidence

- The administration group uses one `admin:acl:write` capability at `Program.cs:6752-6754`.
- Practice-setting transitions do not load or compare `created_by` at `AdministrationRepository.cs:1392-1610`; coding, layout, option, alert, module, and API-client transitions likewise accept the current actor without creator separation, including `AdministrationRepository.cs:547-577,977-1006`.
- Retained tests deliberately use one administrator to create, submit, approve, and activate practice, coding, layout, option, alert, API-client, and configuration-package changes at `Test-AvenChartBaseline.ps1:10686-10695,10768-10886` and `Test-ConfigurationPackage.ps1:66-78`.
- Direct mutation/rollback routes coexist for coding catalogs, form layouts/fields/options, modules, API clients, alert rules, and practice settings at `Program.cs:7192-7196,7217-7220,7234-7236,7267-7268,7271-7272,7281-7289`.
- The registry and UI explicitly say independent approval is pending and direct update is compatibility-only at `AdministrationRepository.cs:30-76` and `PracticeSettingGovernance.tsx:470-479`.

## Consequence

One administrator can create, approve, and activate a proposed change or bypass that lifecycle entirely. Current evidence therefore proves versioned change history, not independent review.

## Cause and reach

Review state is modeled as an optional administrative workflow rather than a role-separated authorization boundary shared by all active configuration mutations.

## Risk calibration

Configuration can affect coding/billing behavior, forms/options, clinical alerts, module state, and API-client registry data. `P2-D016` requires independent approval for classes that affect clinical alerts, access, integrations, or evidence, but not necessarily every small-practice change. Medium severity and unknown whole-finding blocker status avoid overstating lower-risk families while detailed classification remains open.

## Uncertainty and counterevidence

Administrator authorization, reasons, events, caller versions, row locks, stale-baseline checks, transactions, revision history, rollback, and restrictions on delegated users are meaningful controls. The sensitive-class target is approved; accountable owners must map concrete configuration families and validate the break-glass path.

## Validation record

Quality/operations, security/privacy, and independent verifier passes reproduced the mechanism and agreed that security or clinical impact remains policy-dependent.

## Disposition

Validated configuration-governance mechanism with Medium severity and an approved independent-approval target for sensitive classes; production-blocker status remains unknown until those families and acceptance evidence are mapped. No claim is made that same-actor administration is inherently unauthorized, and no implementation recommendation is made.
