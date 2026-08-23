# P2-04-F005 — Billing adjudication and EOB import persist fixed synthetic monetary outcomes

- Status: validated
- Domain(s): 04, 07, 08, 09, 10
- Coverage item(s): `COV-007`, `COV-008`, `COV-009`, `COV-011`, `COV-014`
- Severity: high
- Production blocker: yes
- Reach: repeated across two ordinary billing write workflows
- Confidence: high
- Reviewers: `phase2_data`, `phase2_frontend_accessibility`
- Independent verifier: separate `phase2_verifier` pass
- Specialist validation: billing/revenue-cycle, finance, retention/legal, and database-operations review outstanding
- Baseline commit: `d77a8320e6751a2deb2daf14cf1ac5d6b00cb989`
- Observed on: 2026-08-21

## Condition

Ordinary claim-adjudication and EOB-import APIs construct monetary outcomes from fixed demonstration values rather than caller-supplied or integrated adjudication/remittance facts. Both can be invoked repeatedly with fresh identifiers.

## Evidence

- `BillingEobBatchImportRequest` contains only `PatientId` at `BillingDtos.cs:636-637`; the modern client posts only `{ patientId }` at `avenchart-ui/src/api.ts:7623-7630`.
- The modern UI asks to import a local EOB without payer, encounter, source file, amount, service line, reference, or preview at `BillingWorkspace.tsx:156-185,318-330`; the reference UI exposes the same repeatable action at `avenchart/frontend/src/App.tsx:4076-4090,17299-17304,17973-17981`.
- `BillingRepository.ImportEobBatchAsync` constructs two fixed rows, a fixed date, encounter `1000052`, payer `9005` / `Northstar HMO`, codes, amounts, adjustments, references, and actor at `BillingRepository.cs:2286-2433`.
- `BillingRepository.AdjudicateClaimAsync` receives only a claim ID and posts a fixed date, `$42` payment, `$5.75` adjustment, CPT `99214`, `CO-45`, actor, reference, and claim state at `BillingRepository.cs:1798-1940`.
- Both routes are ordinary `acct:bill:write` endpoints at `Program.cs:6615-6624,6702-6713` and have no financial request idempotency guard.

## Consequence

An authorized billing user can change patient balances, payment history, adjustments, payer references, statement readiness, collections state, and claim status using fabricated rather than received financial facts. Repetition can add further monetary rows.

## Cause and reach

Demonstration fixtures were exposed as production-shaped mutations without an explicit fixture-only boundary or source-transaction identity. EOB import and claim adjudication share this root and are not separate findings.

## Risk calibration

The APIs knowingly create monetary state without source financial evidence, and the UI reports a successful import/adjudication. That supports high severity and future-production blocker status.

## Uncertainty and counterevidence

The routes are permission protected, use transactions, validate patient/encounter relationships, and reject locked encounters at request time. The modern UI says the operation is local-only and not clearinghouse adjudication, but it does not disclose that values are fixed fixtures. Disposable-database repetition and downstream-balance verification remain outstanding.

## Validation record

Frontend, data, and independent verifier passes reproduced the request-to-ledger contract. Hard-coded actor attribution broadens `P2-05-F009`; the sequence allocator effect is evaluated separately.

## Disposition

Validated engineering condition and future-production blocker. No implementation recommendation is made.
