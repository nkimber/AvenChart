# P2-03-F009 — Clinical attestations do not identify the content or version attested

- Status: validated
- Domain(s): 03, 04, 09
- Coverage item(s): `COV-004`, `COV-006`, `COV-008`, `COV-014`
- Severity: high
- Production blocker: yes
- Reach: cross-cutting across encounter signatures, laboratory report review, and critical-result acknowledgement
- Confidence: high
- Reviewers: `phase2_clinical_safety`, `phase2_data`, `phase2_frontend_accessibility`
- Independent verifier: separate `phase2_verifier` pass
- Specialist validation: clinician, clinical informatics, HIM, and signature-policy review outstanding
- Baseline commit: `d77a8320e6751a2deb2daf14cf1ac5d6b00cb989`
- Observed on: 2026-08-21

## Condition

Encounter signatures, laboratory report review, and critical-result acknowledgement retain their own workflow metadata and versions but do not bind the action to the clinical content or result version being attested.

## Evidence

- `EncounterSignRequest` contains only `IsLock` and `Amendment` in `EncounterDtos.cs:290-292`.
- `EncounterRepository.SignAsync` does not collect SOAP, summary, vital, form, order, document, or coding versions at `EncounterRepository.cs:598-683`.
- Its digest hashes encounter/table identifiers, signer, time, lock flag, and amendment only at `EncounterRepository.cs:672-674`.
- `NewEncounter.tsx:260-279` implements the visible “Sign & close” action with `isLock: false`.
- Laboratory report sign requests carry only review version and reason; review events identify no result content at `ProcedureDtos.cs:419-438` and `ProcedureRepository.cs:2114-2245`.
- Critical acknowledgement identifies a result and acknowledgement version but no result-content version at `ProcedureDtos.cs:306-328` and `V0214__critical_lab_result_acknowledgements.sql:1-24`.
- Result creation and correction do not change report review or acknowledgement state at `ProcedureRepository.cs:1580-1677`.

## Consequence

Stored evidence cannot later establish which clinical record or result state the actor reviewed. Later content can change without invalidating the signature, report review, or critical acknowledgement.

## Cause and reach

The state-proof models attest metadata about the action rather than an identifiable aggregate snapshot. The condition affects multiple clinical attestation paths.

## Risk calibration

Reliable attestation provenance is a core future-production requirement. The condition is repeated and difficult to reconstruct after later changes, supporting high severity and blocker status.

## Uncertainty and counterevidence

Actors and times are server-derived, signature/review/acknowledgement events are retained, report-review transitions are version checked, and a locking encounter signature rejects many sequential mutations. Result corrections preserve prior values, report review can be manually reopened, and governed clinical forms provide a stronger content-bound counterexample. Qualified owners must define the intended clinical and evidentiary meaning of each action and the required amendment behavior.

## Validation record

The COV-004 specialists and verifier reproduced the encounter digest boundary. COV-006's three specialists and Pilot C verifier independently reproduced the laboratory manifestation. Runtime content-change and amendment scenarios remain outstanding.

## Disposition

Validated source-level engineering condition and future-production blocker. No implementation recommendation is made.
