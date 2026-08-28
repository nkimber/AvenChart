# Sprint 13: synthetic prescription-preparation draft

Status: Approved for bounded implementation by [TH-DEC-0016](../decisions/0016-approved-sprint-13-synthetic-prescription-preparation-draft.md)  
Scope: Owner-only deterministic non-controlled catalog search and one append-only physician-authored prescription-preparation draft during unfinished synthetic wrap-up; no recommendation, safety adjudication, canonical prescription/medication, signature, transmission, patient delivery, lifecycle, downstream action, external integration, production use, or patient care

## 1. Outcome

Add the first e-prescribing prerequisite without pretending that a legal prescription or certified NCPDP transaction exists. The owning physician can explicitly choose one active non-controlled synthetic catalog entry and record a structured preparation draft after confirming current medication/allergy review, adequate evaluation, and the patient's current pharmacy choice.

## 2. Stories

| ID | Story |
|---|---|
| `TH-SP13-001` | Add append-only prescription-preparation versions/events with controlled-substance, legal-effect, version, replay, snapshot, and synthetic-only database constraints. |
| `TH-SP13-002` | Add neutral deterministic search over the existing versioned medication vocabulary, returning catalog facts only and excluding controlled or unknown classifications. |
| `TH-SP13-003` | Add an owner-bound wrap-up repository/service that requires explicit directions, current confirmed pharmacy choice, medication/allergy review, adequate evaluation, expected version, and semantic idempotency. |
| `TH-SP13-004` | Publish typed private/no-store GET and PUT routes with opaque not-found, view/write audit, bounded Problem Details, and no canonical or downstream identifiers. |
| `TH-SP13-005` | Add an accessible WrapUp-only panel with empty search, no clinical defaults, explicit acknowledgments, manual save/retry/reload, 320 px reflow, and no browser persistence. |
| `TH-SP13-006` | Prove owner/non-owner/administrator behavior, controlled/unknown rejection, exact replay/contention/stale/signature locking, append-only history, audit/cache/privacy, zero canonical/downstream/lifecycle delta, and full regression/Graphify/planning evidence. |

## 3. Contract boundary

The GET response may contain:

- opaque consultation ID, semantic consultation state, server time, adapter/catalog mode and version;
- up to 20 matching active non-controlled catalog entries with RxNorm code, display/drug name, form, strength, and route as reference facts;
- current draft version with catalog snapshots and explicit physician-entered dose, frequency, quantity, duration, refills, indication, and directions;
- referenced pharmacy-choice version only, never pharmacy identity/address;
- review acknowledgments and false legal/safety/signing/transmission/delivery/completion flags; and
- stable limitations explaining that no interaction/contraindication check, prescription, or transmission exists.

The PUT request contains expected version, selected RxNorm code, manual structured directions, review acknowledgments, and synthetic confirmation. It contains no patient, encounter, appointment, request, pharmacy, prescriber, payer, or canonical prescription identifier.

## 4. Acceptance evidence

1. Empty search returns no catalog default; a query returns stable alphabetical/code results and excludes the seeded controlled entry.
2. Unknown or controlled RxNorm code, missing structured directions, missing review acknowledgment, missing confirmed pharmacy choice, and absent synthetic confirmation fail without a draft/event.
3. A valid first write creates version one/event one; exact retry returns it; changed-key reuse and stale version fail; 20 concurrent first writes create one version/event.
4. A valid revision creates version two while version one remains byte-stable; a locking encounter signature makes GET/PUT opaque not-found.
5. The response and logs expose no canonical patient/request/appointment/encounter/prescription/pharmacy/actor key and no hidden transmission token.
6. Recording changes only the two new append-only tables plus required PHI audit; prescriptions, medications, prescription audit, signatures, AVS/documents, billing, claims, messages, tasks, notifications, outbox/inbox, and every request/consultation/shift/appointment lifecycle fact remain unchanged.
7. API, component, and four-browser tests cover search, no defaults, validation, ambiguous failure/retry, reflow, semantics, serious automated WCAG findings, and no browser persistence.

## 5. Exit boundary

Sprint 13 ends with an unsigned, unchecked preparation draft. Drug-knowledge/interaction adjudication, prescriber authority verification beyond the current synthetic owner gate, prescription signature, canonical prescription creation, NCPDP mapping, transmission, status/recovery work, AVS, completion, billing/claim work, production enablement, and patient care remain unavailable and require separate authorization and evidence.
