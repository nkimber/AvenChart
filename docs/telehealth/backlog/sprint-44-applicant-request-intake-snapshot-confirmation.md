# Sprint 44: applicant request intake snapshot confirmation

Status: Approved for bounded implementation by [TH-DEC-0047](../decisions/0047-approved-sprint-44-applicant-request-intake-snapshot-confirmation.md)
Scope: One applicant-owned, no-free-text request intake snapshot from exact `Intake` version 4 to `Verification` version 5; no clinical publication, consent, canonical coverage, current payer/network result, operational-review work, contact, queue, appointment, encounter, care, financial, integration, external, or production consequence

## 1. Outcome

Bind the already-collected synthetic applicant evidence to the request, collect only one controlled symptom-duration range plus explicit current-source confirmations, create one immutable intake snapshot, and make every later verification and authorization gate visible and false.

## 2. Stories

| ID | Story |
|---|---|
| `TH-SP44-001` | Add an additive migration for one protected applicant intake receipt referencing one generic request intake snapshot, exact source/provenance guards, one-request uniqueness, append-only enforcement, and zero-downstream constraints. |
| `TH-SP44-002` | Add a deterministic policy for the request-bound snapshot fingerprint, four allowed duration ranges, two server-derived synthetic summaries, eight mandatory confirmations, exact version 4-to-5 semantics, and minimized receipt-state projection. |
| `TH-SP44-003` | Add an access-key-bound repository transaction that revalidates the complete request/source chain, complaint publication gate, current location/callback freshness, replay/contention, and exact `Intake` to `Verification` transition. |
| `TH-SP44-004` | Add private/no-store applicant GET/POST endpoints with safe state-change handling, request correlation, stable Problem Details, minimized output, and idempotency. |
| `TH-SP44-005` | Add an accessible no-free-text confirmation form with one unselected controlled duration, clear correction/stop routes, stable retry, focus recovery, and no browser persistence. |
| `TH-SP44-006` | Prove exact projection, success/replay/contention, changed-key/stale/expired/foreign/source/patient/protocol/publication drift denial, immutable evidence, unchanged upstream records, zero downstream action, migration recovery, and full regression. |

## 3. Entry gate

The applicant remains unexpired and `SyntheticRequestCreated` version 26; the request is `Intake` version 4 with `TelehealthEligible`; one exact request creation, location, passing universal assessment, and passing complaint assessment exists; the category is the server-owned `migraine` or `sleep`; the current GA/CA/FL location and callback route remain fresh and unchanged; the canonical patient remains portal-disabled and unmerged; every required pre-request source receipt remains exact; the complaint fixture remains unpublished; and every intake, coverage, operational-review, contact, queue, appointment, encounter, consent, care, financial, integration, and external consequence remains absent.

## 4. Mutation and result

The client selects one supported symptom duration and confirms the displayed/current source boundary. The server derives the synthetic complaint summary, appends one row to `telehealth_intake_snapshots`, appends one protected applicant intake receipt, advances only the request to `Verification` version 5, and appends one event. The applicant, patient, clinical records, coverage records, and all earlier evidence remain unchanged.

`Verification` means that current identity/contact, legal/clinician consent, canonical coverage, eligibility, exact network, financial, and operational gates still need authoritative resolution. It does not mean clinically approved, covered, in network, accepted, queued, scheduled, assigned, or under care.

## 5. Verification matrix

| Area | Required evidence |
|---|---|
| Policy | Duration allowlist, server-derived summary, exact eight confirmations, snapshot determinism, prohibited-input absence, version transition, and minimized response. |
| Data | Full provenance under locks, database-clock freshness, exact complaint/publication validation, one intake/receipt/event, replay/contention, immutable evidence, and no downstream mutation. |
| HTTP | Access-key ownership, configured host/practice/facility, private/no-store, safe errors, request correlation, idempotency, and no prohibited fields. |
| UI | No default duration, no free text, correction guidance, explicit outstanding gates, stable retry, result recovery, reflow, keyboard/focus, and no persistence. |
| Regression | Backend/frontend/lint/build/E2E, live GA/CA/FL, migration/recovery, runtime/authorization/OpenAPI, queue regression, planning validator, Graphify portability/review, bootstrap, and exact cleanup. |

## 6. Gate preserved

No later verification or operational work is bundled into this slice. Canonical coverage creation, synthetic or real eligibility/network execution, financial acknowledgments, consent, operational review, practice acceptance, appointment/queue creation, and care require separately bounded decisions.
