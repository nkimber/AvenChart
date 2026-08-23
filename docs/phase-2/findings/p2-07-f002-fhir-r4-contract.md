# P2-07-F002 — The advertised FHIR R4 surface is not a validated R4 contract

- Status: validated condition
- Domain(s): 03, 06, 07, 09
- Coverage item(s): `COV-003`, `COV-006`, `COV-010`, `COV-019`
- Severity: high
- Production blocker: yes — FHIR interoperability is required by `P2-D014`
- Reach: cross-cutting across the advertised FHIR surface
- Confidence: high static and synthetic runtime; formal validator replay outstanding
- Reviewer: `phase2_quality_operations`
- Independent verifier: `phase2_verifier`
- Specialist validation: certification/interoperability and clinical/HIM outstanding
- Baseline commit: `d77a8320e6751a2deb2daf14cf1ac5d6b00cb989`
- Observed on: 2026-08-21

## Condition

Routes are exposed under `/api/fhir/R4` and return FHIR-looking objects, but the hand-authored CapabilityStatement, resources, search bundles, MIME behavior, and errors have not been validated as normative R4 representations.

## Evidence

- `Program.cs:896-923` advertises R4 `4.0.1` and `kind = instance`, while `FhirDtos.cs:6-25` omits required instance metadata and models `format` as a scalar and `searchParam` as strings rather than R4 components.
- Search bundles use relative `fullUrl` values (`FhirRepository.cs:44,74,118,154`) and have no continuation links even though searches clamp to 100 and return a larger `total` (`FhirRepository.cs:13,25-46,59-75,88-120`; `FhirDtos.cs:42-54`).
- `FhirEncounterResource` exposes `reason: string` and `ReadEncounter` hard-codes every encounter as `finished` (`FhirDtos.cs:50`; `FhirRepository.cs:219-223`), without selecting local lifecycle state.
- Encounter subject search does not normalize `Patient/{id}` while Observation search does (`FhirRepository.cs:59-75,88-120,281-286`).
- FHIR routes use ordinary `Results.Ok` and empty `Results.NotFound` (`Program.cs:925-960`); no `application/fhir+json` negotiation or `OperationOutcome` construction was located.
- The only FHIR runtime assertion checks selected SDOH fields (`Test-AvenChartBaseline.ps1:2576,2612-2613`); no R4 validator, MIME, error, CapabilityStatement, pagination, or lifecycle test was found.
- Synthetic runtime sent `Accept: application/fhir+json`, but successful metadata, Patient, Encounter, and Observation responses remained `application/json`; a missing Patient returned an empty `404` rather than `OperationOutcome`.
- Patient search returned `500` for no filters, name-only, and identifier-only requests. It returned `200` only when both optional filters were supplied; PostgreSQL reported `42P08` from `FhirRepository.SearchPatientsAsync`.
- `P2-D014` explicitly selects standards-conformant FHIR as required production scope, removing the former scope uncertainty.

## Consequence

A conforming consumer may reject the CapabilityStatement or bundles, fail to generate the advertised search interface, lose records beyond the first page, or interpret encounter meaning differently from the source. No failed partner exchange is claimed because no partner flow was exercised.

## Cause and reach

Selected FHIR concepts were approximated with local records without a chosen implementation guide, normative serializer, validator, or conformance test boundary. The condition reaches every advertised FHIR resource type.

## Risk calibration

The condition is high and a production blocker because a structurally invalid and partially failing interoperability contract can silently fail at a trust boundary, and `P2-D014` makes FHIR a supported production requirement. The precise implementation guide and certification profile remain to be selected.

## Uncertainty and counterevidence

The surface is read-only, bounded, compact, and accurately avoids unsupported FHIR writes. The README disclaims production and certification readiness. A local compatibility profile could narrow the required contract, but that profile has not been documented.

## Validation record

Static source inspection and independent verification corroborated the representation and pagination gaps. Synthetic runtime then reproduced MIME, error, and patient-search failures. A formal validator replay, >100-row dataset, and non-finished encounter remain outstanding.

## Disposition

Validated engineering-readiness condition with conditional production impact. No Phase 3 implementation recommendation is accepted.
