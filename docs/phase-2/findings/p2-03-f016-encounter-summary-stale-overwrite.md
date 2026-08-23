# P2-03-F016 — Sequentially stale encounter-summary forms overwrite newer data

- Status: validated
- Domain(s): 03, 04, 08, 09
- Coverage item(s): `COV-004`, `COV-011`, `COV-014`
- Severity: high
- Production blocker: yes
- Reach: repeated across encounter-summary edits
- Confidence: high
- Reviewers: `phase2_clinical_safety`, `phase2_data`
- Independent verifier: separate `phase2_verifier` pass
- Specialist validation: clinical workflow review outstanding
- Baseline commit: `d77a8320e6751a2deb2daf14cf1ac5d6b00cb989`
- Observed on: 2026-08-21

## Condition

The encounter-summary request does not carry the row version loaded by the browser. EF detects a write overlapping after the repository read, but cannot reject a stale full form submitted after another user's change has already committed.

## Evidence

- `EncounterUpdateRequest` has no expected version at `EncounterDtos.cs:233-239`.
- The UI loads and resubmits all six fields at `PatientEncounters.tsx:2938-2961`.
- `UpdateSummaryAsync` loads the then-current row, applies the submitted form, and increments `RowVersion` at `EncounterStateRepository.cs:19-67`.
- Audit stores changed field names, not before/after values, at `EncounterStateRepository.cs:208-220`.

## Consequence

Two users can load V1; user A saves V2; user B then submits the V1-derived form. The repository loads V2 and commits B's stale values as V3 without a concurrency conflict.

## Cause and reach

The concurrency token begins at request execution rather than the user's read, because the client contract omits the loaded version.

## Risk calibration

The form includes sensitivity and encounter context, and stale replacement is silent. This supports high severity and future-production blocker status.

## Uncertainty and counterevidence

Truly overlapping writes after the repository read produce an EF concurrency exception, and mutation plus audit is atomic. A controlled two-browser runtime reproduction remains outstanding.

## Validation record

Specialist and verifier passes independently reproduced the sequential schedule from the client and EF boundary.

## Disposition

Validated source-level engineering condition and future-production blocker. No implementation recommendation is made.
