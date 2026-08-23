# P2-03-F017 — SOAP concurrency protection is optional at the server boundary

- Status: validated
- Domain(s): 03, 04, 07, 09
- Coverage item(s): `COV-004`, `COV-008`, `COV-014`
- Severity: high
- Production blocker: yes
- Reach: repeated through alternate, legacy, or direct clients
- Confidence: high
- Reviewers: `phase2_clinical_safety`, `phase2_data`
- Independent verifier: separate `phase2_verifier` pass
- Specialist validation: clinical documentation and informatics review outstanding
- Baseline commit: `d77a8320e6751a2deb2daf14cf1ac5d6b00cb989`
- Observed on: 2026-08-21

## Condition

SOAP `ExpectedVersion` is nullable, and the repository enforces a stale-write conflict only when the caller supplies it.

## Evidence

- The nullable contract appears at `EncounterDtos.cs:257-263`.
- Conflict checking is conditional at `EncounterRepository.cs:518-524`.
- The primary existing-note API supplies a version, but the generic API contract omits it at `avenchart-ui/src/api.ts:9758-9777`.
- `NewEncounter.tsx:239-250` uses the versionless contract.

## Consequence

A stale or racing client can omit the token and append its full SOAP snapshot as the newest version. Earlier history survives, but routine readers can receive stale content as current.

## Cause and reach

A clinical concurrency invariant depends on cooperative client behavior rather than the server contract.

## Risk calibration

The main existing-note editor is protected, which reduces likelihood, but direct and alternate clients can opt out on every request. This supports high severity and blocker status.

## Uncertainty and counterevidence

SOAP writes are transactional and row-locked, retain append-only versions, reject duplicates, and return a structured conflict when the version is supplied. The finding does not allege loss of prior versions.

## Validation record

All passes reproduced the optional contract and alternate-client path. A live omission-after-intervening-write scenario remains outstanding.

## Disposition

Validated engineering condition and future-production blocker. No implementation recommendation is made.
