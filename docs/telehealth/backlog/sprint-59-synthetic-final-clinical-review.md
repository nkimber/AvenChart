# Sprint 59 plan: synthetic final clinical-review affirmation

Status: Approved for implementation under [TH-DEC-0062](../decisions/0062-approved-sprint-59-synthetic-final-clinical-review.md)

## Goal

Record the owning physician's immutable, source-version-bound review of the current synthetic SOAP and safety-disposition drafts, without making the encounter legally signed or claim-ready.

## Delivery boundary

- Exact owner, practice, facility, adult patient, released reservation, ended session, unfinished appointment/encounter, `MediaEnded` consultation, and `WrapUp` state only.
- Require every SOAP section and a current safety-disposition draft structurally present; bind an optional existing signed synthetic prescription order.
- Require four affirmative clinician acknowledgments; preserve clinician responsibility and make no automated clinical determination.
- Append immutable review versions and events with exact idempotent replay and source-version snapshots.
- Surface current-match/stale status in the existing private completion review.
- Keep signature/finalization, completion, patient delivery, outbox, billing, claims, integrations, and every external effect disabled.

## Acceptance matrix

| Area | Required evidence |
|---|---|
| Ownership | Exact current physician and wrap-up lineage only; opaque denial for every other actor. |
| Source binding | All SOAP sections and disposition are structurally present; current source version and optional signed-order identifier are snapshotted. |
| Acknowledgment | Review, physician-responsibility, no-automatic-claim-or-delivery, and synthetic-only acknowledgments are all required. |
| Immutability | Append-only record/event tables, source snapshots, content hash, and database trigger proof. |
| Replay | Exact retry returns the same review; conflicting key or stale source fails closed. |
| Completion projection | Current review is shown as structural evidence only; signature, completion, delivery, and downstream flags remain false. |
| No consequence | No canonical signature, lifecycle, AVS, billing, claim, integration, external, or patient-delivery action. |
| Regression | Backend, UI, OpenAPI, authorization, runtime, migration/recovery, planning, and Graphify evidence. |

## Gate preserved

A later, separately approved slice must authorize encounter signature/finalization and its legal policy, atomic completion/release, AVS/patient delivery, coding and human billing review, claim creation/submission, and every integration or production gate.
