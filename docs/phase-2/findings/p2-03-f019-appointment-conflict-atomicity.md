# P2-03-F019 — Enforced appointment-conflict checks are not atomic with creation

- Status: validated condition
- Domain(s): 03, 04, 07, 09
- Coverage item(s): `COV-005`, `COV-008`, `COV-009`, `COV-014`
- Severity: high
- Production blocker: yes
- Reach: repeated across conflict-enforced appointment creation
- Confidence: high static confidence
- Reviewers: `phase2_clinical_safety`, `phase2_data`, `phase2_frontend_accessibility`
- Independent verifier: separate `phase2_verifier` pass
- Specialist validation: scheduling operations, clinician, and database-concurrency review outstanding
- Baseline commit: `d77a8320e6751a2deb2daf14cf1ac5d6b00cb989`
- Observed on: 2026-08-21

## Condition

When a caller requests conflict enforcement, provider, patient, facility, and room availability are checked before creation on a separate connection and transaction. The subsequent insert has no database constraint or shared lock that preserves the checked result.

## Evidence

- The API awaits availability validation and then separately invokes creation at `Program.cs:2141-2172`.
- Availability opens its own connection at `AppointmentRepository.cs:926-1062`; creation opens another connection and performs a plain insert at `AppointmentRepository.cs:400-505`.
- The appointment schema has ordinary indexes but no exclusion or equivalent conflict constraint at `generate-postgres-seed.mjs:1057-1080,3115`.
- The modern appointment dialog explicitly requests conflict enforcement at `NewAppointmentDialog.tsx:162-191`.

## Consequence

Two requests can both observe an available slot and then create overlapping appointments even though both requested enforcement. The returned success therefore does not prove that the scheduling invariant held at commit.

## Cause and reach

The conflict decision is implemented as a preflight rather than a transactionally enforced creation invariant. Intentional overlap remains available when the request flag is false; this finding concerns calls that explicitly set it true.

## Risk calibration

The race is timing-dependent but affects a common scheduling commitment and contradicts an explicit caller policy. This supports high severity and future-production blocker status.

## Uncertainty and counterevidence

The modern UI performs a client check and sends the enforcement flag. Retained smoke tests also establish that overlap can be intentional when the flag is false. A two-session PostgreSQL interleaving is still required to measure the inferred outcome.

## Validation record

The data specialist and independent verifier reproduced the split connection boundary, absence of a commit-time invariant, and UI reach. Docker/PostgreSQL was unavailable for live interleaving.

## Disposition

Validated source-level condition and future-production blocker. No implementation recommendation is made.
