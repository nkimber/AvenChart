# P2-03-F010 — Encounter locking is a non-atomic check across several clinical writers

- Status: validated condition
- Domain(s): 03, 04, 09
- Coverage item(s): `COV-004`, `COV-007`, `COV-008`, `COV-009`, `COV-014`
- Severity: high
- Production blocker: yes
- Reach: systemic across several encounter-bound writers
- Confidence: medium-high static confidence
- Reviewers: `phase2_clinical_safety`, `phase2_data`
- Independent verifier: separate `phase2_verifier` pass; condition partially corroborated pending live interleaving
- Specialist validation: clinician and database-concurrency review outstanding
- Baseline commit: `d77a8320e6751a2deb2daf14cf1ac5d6b00cb989`
- Observed on: 2026-08-21

## Condition

Vitals, legacy layout forms, procedure orders, billing adjudication/payment imports, and other encounter-bound writers check for a locking signature without holding a common encounter lock through commit. Signing does not participate in a conflicting aggregate lock.

## Evidence

- `EncounterRepository.SignAsync` opens no transaction and takes no encounter aggregate lock at `EncounterRepository.cs:598-683`.
- Vitals check lock state separately from the later save at `EncounterStateRepository.cs:79-125,182-192`.
- Layout forms use a transaction but no lock that conflicts with signing at `EncounterLayoutFormRepository.cs:30-72`.
- Procedure status and content updates check on one connection and mutate on another at `ProcedureRepository.cs:960-985,1076-1114`.
- Billing adjudication, ordinary payment, and EOB import check encounter lock state before beginning the financial transaction at `BillingRepository.cs:1828-1842,2014-2021,2336-2343`.

## Consequence

A writer can observe “unlocked,” a signer can then commit a locking signature, and the writer can commit afterward. The stored chronology can contradict the asserted clinical-finality boundary.

## Cause and reach

Lock state and mutation are not serialized through one shared aggregate row, transaction, or version. The pattern spans several persistence implementations rather than one repository technology.

## Risk calibration

The interleaving is timing-dependent but affects clinical-finality semantics across common writers. This supports high severity and blocker status against the adopted target.

## Uncertainty and counterevidence

SOAP correctly locks the encounter and latest note in one transaction; governed forms have a strong independent lifecycle. A real two-session PostgreSQL reproduction is required before treating every inferred race outcome as measured behavior.

## Validation record

Specialist and independent passes reproduced the check/write separation and narrowed the affected paths. Live database interleaving was unavailable.

## Disposition

Validated source-level condition with medium-high confidence and future-production blocker status. COV-007 broadened its reach to encounter-bound billing mutations; runtime confirmation remains required. No implementation recommendation is made.
