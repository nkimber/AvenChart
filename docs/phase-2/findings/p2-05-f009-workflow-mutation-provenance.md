# P2-05-F009 — Workflow and laboratory mutations lack consistent resource-scoped provenance

- Status: validated
- Domain(s): 03, 04, 05, 07, 09, 10
- Coverage item(s): `COV-005`, `COV-006`, `COV-007`, `COV-008`, `COV-009`, `COV-010`, `COV-012`, `COV-014`
- Severity: high
- Production blocker: yes
- Reach: cross-cutting across appointment, recall, therapy, legacy message, laboratory, and billing mutations
- Confidence: high
- Reviewers: `phase2_clinical_safety`, `phase2_data`
- Independent verifier: separate `phase2_verifier` pass
- Specialist validation: clinical operations, security/privacy, HIM, and audit-policy review outstanding
- Baseline commit: `d77a8320e6751a2deb2daf14cf1ac5d6b00cb989`
- Observed on: 2026-08-21

## Condition

Ordinary appointment, recall, therapy, legacy message, laboratory, and billing mutations do not consistently receive and retain the authenticated actor, affected resource, prior/new state or reason, and executed outcome needed to reconstruct who changed a workflow and what changed.

## Evidence

- Appointment create, full update, status change, and delete repository contracts retain no actor-specific mutation history at `AppointmentRepository.cs:400-505,1088-1226`.
- Recall create, activity, and delete retain no authenticated actor or terminal transition evidence at `RecallRepository.cs:13-133`.
- Therapy membership, attendance, session status, and encounter-generation workflows retain clinical state but no authenticated mutation actor at `TherapyGroupRepository.cs:72-496`.
- Legacy message status/content paths hard-code `updated_by = 1`, and reply embeds `admin` text rather than server-derived identity at `MessageRepository.cs:249-317,736-766`.
- Laboratory report/result creation and correction retain no authenticated actor or reason, while specimen creation writes `local-user` at `ProcedureRepository.cs:1126-1209,1386-1454,1580-1677`.
- Billing mutation endpoints generally do not resolve or pass the authenticated actor. Adjudication, ordinary payment, and EOB import persist fixed `119` / `gold-billing-01` attribution at `BillingRepository.cs:1863-1899,2042-2081,2370-2404`, while line, claim, void, and delete paths retain no caller versioned financial event history.
- Central PHI audit cannot supply the missing resource-level mutation account because `P2-05-F003` establishes that it does not correlate ordinary access to patient/resource identity; `P2-05-F004` establishes that result status may be captured too early.
- Integration queue and dispatch routes are permission-filtered, but `IntegrationRepository.QueueAsync` and dispatch state transitions do not receive or persist the authenticated actor (`Program.cs:5573-5601`; `IntegrationRepository.cs:24-64,102-118`). Inbox reconcile/reject and quarantined requeue are positive actor/reason/history counterexamples.

## Consequence

Operational and clinically relevant changes cannot be reliably attributed or reconstructed across these workflows. Investigation, correction, accountability, and recovery evidence can disagree with current state or be absent.

## Cause and reach

Mutation provenance is implemented within selected newer workflows rather than as an invariant shared by resource-changing operations.

## Risk calibration

The condition spans several common patient and laboratory workflows and affects trustworthy evidence rather than presentation alone. It supports high severity and future-production blocker status.

## Uncertainty and counterevidence

Referral transitions, portal appointment events, message assignment/correction/escalation/archive, and reminder dispatch records demonstrate stronger actor, reason, version, and event patterns. Exact retention and audit-content policy require qualified review. This finding is distinct from `P2-05-F003`, which concerns correlation of PHI access rather than durable write history.

## Validation record

Specialist and independent passes reproduced the cross-cutting absence and the stronger counterexamples. No real patient data or production audit was inspected.

## Disposition

Validated engineering condition and future-production blocker. COV-007 broadened the root to ordinary billing mutations and fixed financial actor attribution; COV-010 broadened it to successful integration enqueue/dispatch provenance. No implementation recommendation is made.
