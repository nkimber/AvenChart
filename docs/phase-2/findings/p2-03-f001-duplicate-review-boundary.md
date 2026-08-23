# P2-03-F001 — Registration can bypass duplicate review and its override evidence

- Status: validated
- Domain(s): 03, 04, 07, 09
- Coverage item(s): `COV-003`, `COV-011`, `COV-014`
- Severity: high
- Production blocker: yes
- Reach: repeated
- Confidence: high
- Reviewers: `phase2_clinical_safety`, `phase2_data`
- Independent verifier: separate `phase2_quality_operations` pass
- Specialist validation: clinician, clinical informatics, HIM/patient identity
- Baseline commit: `d77a8320e6751a2deb2daf14cf1ac5d6b00cb989`
- Observed on: 2026-08-21

## Condition

The modern UI performs duplicate review and requires a separate-patient confirmation, but neither decision is carried to the registration boundary. The API validates ordinary fields and inserts directly, so alternate clients, replay, and concurrent registration can create same-person charts with distinct identifiers without durable override evidence.

## Evidence

- `NewPatient.tsx:38-54,90-134,318-440` implements the fail-closed browser check and confirmation.
- `Program.cs:1708-1719` passes the registration request directly to the repository.
- `PatientRepository.cs:915-979` validates and inserts without duplicate review; its uniqueness handling covers the public chart identifier.
- `Test-AvenChartBaseline.ps1:2304-2362` directly creates an intentionally 100-score duplicate and treats subsequent detection as success.
- Identifier uniqueness, post-create review, merge, and rollback are countercontrols, not proof of review before creation.

## Consequence

Clinical and administrative history can be split across two charts before later detection, increasing incomplete-chart and wrong-patient-selection risk.

## Cause and reach

The patient-identity decision is browser state rather than an invariant of the registration transaction. The condition applies to direct, retried, integrated, or concurrently submitted registration requests.

## Risk calibration

- Impact: potentially incomplete or incorrectly associated patient history
- Likelihood: direct API creation is an expected retained test behavior; concurrency and replay remain runtime scenarios
- Detectability: later duplicate review may detect candidates, but only after creation
- Reversibility: constrained merge exists but can be operationally significant or blocked
- Severity rationale: correct patient identity is an adopted future-production safeguard

## Uncertainty and counterevidence

Demographic uniqueness would be inappropriate, and the supported UI is thoughtfully fail-closed. External registration procedures may provide human controls, but none are represented in the durable application contract.

## Validation record

The clinical, data, and independent passes reproduced the boundary gap and retained-test behavior. Parallel registration remains to be exercised with disposable PostgreSQL. Qualified HIM/clinical review must approve the eventual matching and override policy.

## Disposition

Validated engineering-readiness condition. No implementation recommendation is made.
