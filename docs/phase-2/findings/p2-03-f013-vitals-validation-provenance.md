# P2-03-F013 — Vitals accept empty or implausible observations without correction provenance

- Status: validated condition
- Domain(s): 03, 04, 09
- Coverage item(s): `COV-004`, `COV-011`, `COV-014`
- Severity: high
- Production blocker: yes
- Reach: repeated across encounter vital capture
- Confidence: high engineering confidence; clinical thresholds require validation
- Reviewers: `phase2_clinical_safety`, `phase2_data`
- Independent verifier: separate `phase2_verifier` pass
- Specialist validation: nursing, clinician, and clinical-informatics review outstanding
- Baseline commit: `d77a8320e6751a2deb2daf14cf1ac5d6b00cb989`
- Observed on: 2026-08-21

## Condition

Every vital measurement is optional; neither browser nor server enforces a nonempty observation or physiological ranges. The record does not retain author, explicit units, version, or correction linkage, and the reviewed clinical projection exposes only the latest row.

## Evidence

- All measurements are nullable in `EncounterDtos.cs:245-255`.
- UI inputs have numeric steps but no required, minimum, or maximum constraints at `PatientEncounters.tsx:3704-3771`.
- Persistence validates only the timestamp and copies values directly at `EncounterStateRepository.cs:79-125`.
- The API does not resolve an authenticated author at `Program.cs:2449-2468`.
- Encounter detail selects only the latest vital row at `EncounterRepository.cs:148-153`.

## Consequence

Empty, negative, impossible, or unit-mistaken observations can become the current displayed clinical value. A later entry can hide the row without formally identifying a correction or its author.

## Cause and reach

Vitals are modeled as ungoverned nullable scalars rather than attributable observations with an explicit validation and correction lifecycle.

## Risk calibration

The server accepts clinically implausible inputs on every vital path. Exact bounds remain clinician-owned, but absence of any safety boundary supports high severity and blocker status.

## Uncertainty and counterevidence

Rows are append-only through the located API, so prior database data is not physically overwritten. The UI labels current US units and uses numeric controls. Qualified owners must define allowable ranges, unit semantics, and correction policy.

## Validation record

The UI-to-column trace was independently reproduced. Live invalid-value and trend tests remain outstanding.

## Disposition

Validated engineering condition and future-production blocker, with clinical policy calibration outstanding. No implementation recommendation is made.
