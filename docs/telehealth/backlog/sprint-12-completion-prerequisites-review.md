# Sprint 12: synthetic completion-prerequisites review

Status: Approved for bounded implementation by [TH-DEC-0015](../decisions/0015-approved-sprint-12-completion-prerequisites-review.md)  
Scope: Owner-only, read-only, minimized structural evidence review during unfinished synthetic wrap-up; no clinical-completeness decision, mutation, signing, finalization, delivery, lifecycle, downstream action, external integration, production use, or patient care

## 1. Outcome

Add a physician-facing projection immediately below the existing SOAP, pharmacy, and safety-disposition workspaces. It helps the physician locate missing structural evidence and makes the unavailable finalization dependencies explicit before a later atomic completion design is considered.

## 2. Stories

| ID | Story |
|---|---|
| `TH-SP12-001` | Add a current-owner query that rebinds every unfinished-wrap-up aggregate and reports only minimized evidence flags/versions from the latest canonical SOAP draft, safety-disposition draft, and optional pharmacy choice. |
| `TH-SP12-002` | Project SOAP section presence without judging applicability or sufficiency; project the physician-selected disposition code and confirmation state without returning authored free text. |
| `TH-SP12-003` | Keep pharmacy optional and explicitly nonblocking; return false signing/completion/delivery/downstream capabilities and stable product blockers. |
| `TH-SP12-004` | Publish a physician-scoped GET with opaque not-found, private/no-store response handling, typed OpenAPI, and consultation-correlated patient/encounter view audit. |
| `TH-SP12-005` | Add a WrapUp-only accessible panel with honest caveats, compact evidence status, manual reload/error recovery, 320 px reflow, and no browser persistence. |
| `TH-SP12-006` | Prove owner/non-owner/administrator behavior, zero durable/lifecycle/downstream delta under repeated reads, privacy allowlist, audit/cache behavior, and full regression/Graphify/planning evidence. |

## 3. Projection contract

The response contains:

- opaque `consultationId`, `consultationStatus`, request/shift/appointment semantic states, and server `asOf`;
- current documentation version plus `hasAnyContent` and presence flags for subjective/objective/assessment/plan;
- current disposition version/code plus adequate-evaluation, follow-up-presence, warning/next-step-presence, communication, urgent/emergency, and interrupted-contact confirmation flags;
- current optional pharmacy-choice version and `patientChoiceConfirmed`, without pharmacy identity/address;
- `structuralEvidencePresent`, which means only that a nonblank SOAP draft and valid disposition draft exist;
- stable product blocker codes and plain-language limitations; and
- four false capability flags for signing, completion, patient delivery, and downstream creation.

No clinical free text, canonical database key, actor identity, payer detail, patient demographic, pharmacy identity, or hidden action token is returned.

## 4. Acceptance evidence

1. No SOAP draft yields `DOCUMENTATION_DRAFT_MISSING`; blank sections remain visible without being called clinically incomplete.
2. No disposition yields `SAFETY_DISPOSITION_DRAFT_MISSING`.
3. With both present, `structuralEvidencePresent=true`, but signature/finalization and downstream-transaction blockers remain and every capability stays false.
4. Optional pharmacy absence never creates a blocker; presence reports only current version and patient-confirmation state.
5. Owner GET succeeds only in current unsigned wrap-up, another physician receives 404, administrator receives 403, and a locking signature removes eligibility.
6. Concurrent/repeated GETs create no row, version, status, signature, clinical, financial, message, task, notification, integration, or external-call delta.
7. Response caching is prohibited and PHI audit records both required view permissions against the opaque consultation.
8. Component and four-browser tests cover load failure/retry, semantics, keyboard, 320 px reflow, serious automated WCAG findings, minimized payload, and no browser persistence.

## 5. Exit boundary

Sprint 12 ends with a read-only evidence review. A final clinical completeness policy, physician sign/finalize command, signed disposition, co-signature, encounter/request/appointment transition, clinician release, AVS/follow-up delivery, prescription, billing/claim, outbox work, production enablement, and patient care remain unavailable and require separate authorization and evidence.
