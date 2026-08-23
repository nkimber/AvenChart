# P2-05-F008 — Medication-list view permission authorizes clinical-list mutations

- Status: validated
- Domain(s): 03, 05, 07, 09
- Coverage item(s): `COV-002`, `COV-004`, `COV-014`
- Severity: high
- Production blocker: yes
- Reach: cross-cutting across the clinical-list route group
- Confidence: high
- Reviewers: coordinator route trace, `phase2_clinical_safety`
- Independent verifier: separate `phase2_verifier` pass
- Specialist validation: security/privacy and clinical-role policy review outstanding
- Baseline commit: `d77a8320e6751a2deb2daf14cf1ac5d6b00cb989`
- Observed on: 2026-08-21

## Condition

The clinical-list route group requires only medication-list `view` capability. Create, edit, lifecycle, refill, routing, entered-in-error, and delete routes inherit that boundary and add no write-specific authorization.

## Evidence

- `Program.cs:3064-3065` applies `RequireAccessPermission(clinicalLists, "patients", "med", "view")`.
- Mutation endpoints through `Program.cs:3446` have no additional write filter.
- `AuthorizationPolicyCatalog.cs:42` distinguishes medication-list view and write capabilities.
- `AuthRepository.cs:159-167` permits a view check for users with view/add-only/write-like capability, but repository mutations perform no secondary authorization decision.

## Consequence

A session intended to have read-only medication-list access can invoke clinical-list mutations directly through the API, regardless of whether the UI shows those controls.

## Cause and reach

One group-level read policy is treated as sufficient for heterogeneous read and write operations.

## Risk calibration

The affected routes modify and delete clinically material records. The authorization gap is direct, repeated, and cross-cutting, supporting high severity and blocker status.

## Uncertainty and counterevidence

Authentication and the group-level capability check are centralized, and repository calls remain parameterized. A synthetic view-only runtime matrix is still needed, and a security/clinical owner must confirm the intended role model.

## Validation record

Coordinator, clinical-safety, and independent passes reproduced the route-policy inheritance and absence of a secondary check.

## Disposition

Validated source-level engineering condition and future-production blocker. No implementation recommendation is made.
