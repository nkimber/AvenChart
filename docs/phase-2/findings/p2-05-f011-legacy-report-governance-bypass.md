# P2-05-F011 — Direct report exports bypass the governed reporting lifecycle

- Status: validated
- Domain(s): 05, 07, 08, 09, 10
- Coverage item(s): `COV-002`, `COV-007`, `COV-011`, `COV-014`
- Severity: high
- Production blocker: yes
- Reach: cross-cutting across direct report families
- Confidence: high
- Reviewers: `phase2_frontend_accessibility`, `phase2_quality_operations`, `phase2_security_privacy`
- Independent verifier: separate `phase2_verifier` pass
- Specialist validation: security/privacy, HIM/report governance, clinical informatics, and legal/compliance review outstanding
- Baseline commit: `d77a8320e6751a2deb2daf14cf1ac5d6b00cb989`
- Observed on: 2026-08-21

## Condition

Compatibility operational and family CSV routes remain enabled beside the governed-report workflow. They bypass report-specific purpose, recipient, definition revision, dataset watermark, row-policy scope, request identity, retained artifact, checksum, retention, requester/recipient download authorization, and download-event evidence.

## Evidence

- The `/api/reports` group receives only `patients:pat_rep:view`; direct operational and family routes add no governed-run requirement at `Program.cs:8046-8056,8169-8186`.
- `ReportRepository.GetFamilyCsvAsync` executes direct practice-wide queries at `ReportRepository.cs:815-940`. Patient output includes canonical ID, name, date of birth, and contact; other families include patient-linked appointments, encounters, referrals, chart location, inventory, and clinical-form values, with up to 5,000 rows.
- Governed queries require a pinned facility or assigned-patient scope at `ReportRepository.cs:550-710`.
- Governed execution retains purpose, recipient, definition/watermark/scope evidence, idempotency, artifact checksum/lifecycle, requester/recipient authorization, and download events at `ReportExecutionRepository.cs:211-360,860-922,1129-1235`.
- `OperationalReports.tsx:107-114,134-192` renders the governed workflow and an enabled legacy export on the same page while accurately warning that the latter predates governance.

## Consequence

A broadly permitted report user can download practice-wide patient and clinical data without the narrower disclosure controls and evidence chain presented as the governed path. Completed CSV disclosure cannot be recalled.

## Cause and reach

Compatibility routes and UI controls remained active after the governed execution boundary was introduced. Even an owner-approved practice-wide export would still bypass purpose, recipient, artifact, retention, and download governance.

## Risk calibration

The path is directly reachable, bulk, and includes patient and signed/corrected clinical-form data. That supports high severity and future-production blocker status without asserting that production data was exposed.

## Uncertainty and counterevidence

Authentication and the report capability remain required; outputs are bounded; queries are parameterized and date validated; the UI labels the path local legacy; and central endpoint audit may record the route. The governed implementation itself is a material strength.

## Validation record

Frontend, quality/operations, security/privacy, and independent verifier passes corroborated the direct-versus-governed contrast. Resource scope and audit correlation also broaden `P2-05-F002` and `P2-05-F003`, but do not absorb this reporting-lifecycle root.

## Disposition

Validated engineering condition and future-production blocker. No legal disclosure conclusion or implementation recommendation is made.
