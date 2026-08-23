# P2-03-F005 — Merged source identifiers remain directly readable and actionable

- Status: validated
- Domain(s): 03, 04, 07, 09
- Coverage item(s): `COV-003`, `COV-004`, `COV-010`, `COV-014`
- Severity: high
- Production blocker: yes
- Reach: systemic
- Confidence: high
- Reviewers: `phase2_data`, `phase2_clinical_safety`
- Independent verifier: separate `phase2_quality_operations` and COV-010 `phase2_verifier` passes
- Specialist validation: clinician, HIM/patient identity, interoperability
- Baseline commit: `d77a8320e6751a2deb2daf14cf1ac5d6b00cb989`
- Observed on: 2026-08-21

## Condition

Search hides a merged source, but direct chart retrieval and several patient-bound mutations neither redirect its old identifiers to the target nor reject the source. Encounter creation also accepts the source without inspecting merge state.

## Evidence

- `PatientMergeExecutionRepository.cs:418-437` sets the source merge marker.
- `PatientRepository.cs:2830-2842` correctly excludes the source from search.
- `PatientRepository.cs:105-239,1068-1198,2453-2494` directly reads and mutates a known source without a merged check.
- `EncounterRepository.cs:256-355` resolves any matching patient identifier without checking the merge marker.
- `FhirRepository.cs:15-157` accepts canonical or public identifiers for Patient, Encounter, and Observation reads/searches without a merged-source predicate; `Program.cs:896-960` exposes those routes.
- SDOH, record-request, disclosure, and lifecycle paths provide positive counterexamples that reject merged sources.
- The retained merge smoke validates movement/rollback but does not read or mutate the source while merged.

## Consequence

New information can accumulate on the hidden source after its prior data moved to the target, recreating a split longitudinal record that ordinary search does not reveal.

## Cause and reach

Every repository interprets merge state independently; there is no consistently enforced alias, redirect, or source-write invariant.

## Risk calibration

Known identifiers, bookmarks, integrations, and inconsistent resolvers provide credible entry paths. Post-merge writes are not part of the original manifest and can be difficult to reconcile.

## Validation record

All three passes corroborated the source trace and systemic inconsistent reach. COV-010 independently broadened the condition to the FHIR read/search projection; a full disposable lifecycle matrix remains outstanding.

## Disposition

Validated engineering-readiness condition and future-production blocker. No implementation recommendation is made.
