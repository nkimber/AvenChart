# Sprint 67 plan: POC synthetic professional-claim preparation

Status: Implemented and verified under [TH-DEC-0070](../decisions/0070-approved-poc-synthetic-professional-claim-preparation.md)

## Goal

Provide a durable, physician-owned, standards-labelled POC seam that demonstrates the structural handoff required before future professional-claim integration.

## Delivery boundary

- Persist one immutable, source-version-bound `PreparedOnly` synthetic receipt after final review and encounter lock.
- Reuse the existing non-production `837P` gateway boundary without generating EDI or calling a payer or clearinghouse.
- Surface preparation state and the intentionally unresolved coding, billing-provider, fee, payer, coverage, and human-billing gates in the physician workspace.
- Keep all clinical documentation, financial, claim, delivery, integration, and production consequences disabled.

## Gate preserved

Actual claims data, coding, coverage and benefit determination, pricing, billing-provider enrollment, human billing review, X12 generation, transmission, acknowledgments, adjudication, payment, patient billing, appointment/encounter completion, patient delivery, and production remain separate governed work.
