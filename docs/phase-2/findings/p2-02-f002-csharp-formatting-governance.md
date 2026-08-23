# P2-02-F002 — C# formatting and analyzer hygiene lack consistent repository governance

- Status: validated
- Domain(s): 02, 09, 12
- Coverage item(s): `COV-001`, `COV-008`, `COV-015`, `COV-018`
- Severity: low
- Production blocker: no
- Reach: repeated
- Confidence: high
- Reviewer: `phase2_architecture`
- Independent verifier: `phase2_verifier`
- Specialist validation: none
- Baseline commit: `d77a8320e6751a2deb2daf14cf1ac5d6b00cb989`
- Observed on: 2026-08-21

## Condition

The fixed baseline contains widespread SDK formatter disagreement and dense generated-style physical lines, while the backend project, contributor guidance, and CI do not establish an automated C# formatting or analyzer gate.

## Evidence

- From `avenchart/`, `dotnet format .\AvenChart.slnx --verify-no-changes --verbosity minimal --no-restore` returned exit `2` with 5,284 `WHITESPACE` diagnostic lines across 20 unique C# files. The verifier and coordinator independently reproduced this result.
- Across 208 API C# files, excluding `bin` and `obj`, 1,039 physical lines exceed 200 characters, 276 exceed 500, and 39 files contain at least one line over 200 characters.
- `AdministrationRepository.cs:924` is 2,961 characters; `Program.cs:5886` is 1,004 characters.
- No repository `.editorconfig`, `Directory.Build.props`, analyzer package, warnings-as-errors setting, CI format gate, or contributor formatting command was located.
- `AvenChart.Api.csproj` enables nullable references but does not add formatting or analyzer enforcement.
- `.github/workflows/verify.yml:21-24` restores and builds without a formatting check.
- Full commands, actual results, and limits are preserved in [EXT-S001 Packet 1](../external-feedback/ext-s001-packet-1-architecture-human-traceability.md).

## Consequence

Dense physical lines impede scanning, breakpoint placement, focused diffs, and review of compound control flow. The condition is principally a human-review and developer-experience burden and amplifies the navigation cost of the larger structural findings.

## Cause and reach

The long-line pattern appears in 39 files and is concentrated in `Program.cs`, administration, and inventory code. The absence of a repository-enforced gate allows SDK formatting disagreement to persist.

## Risk calibration

- Impact: review speed and comprehension
- Likelihood or preconditions: encountered when reviewing an affected file
- Detectability: very high
- Reversibility: high
- Severity rationale: repeated but primarily hygienic; no behavioral failure was demonstrated

## Uncertainty and counterevidence

Line length is an imperfect proxy, and many backend files are conventionally formatted. The SDK formatter verifies its defaults rather than a separately adopted project style. The specialist's first pass reported exit `1`; the verifier and coordinator independently obtained exit `2`, which is the retained result. All passes agreed that the check failed.

## Validation record

- Independent method: independent SDK formatter run, diagnostic aggregation, line-length inventory, and repository-policy search
- Result: `corroborated` at low severity and repeated reach
- Reviewer agreement or dispute: agreement after preserving and resolving the first pass's exit-code discrepancy
- Specialist conclusion or outstanding need: none

## Disposition

Validated from the structural and human-review portion of `EXT-S001-C04`. No implementation recommendation is accepted.
