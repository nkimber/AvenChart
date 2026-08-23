# COV-004 assessment — encounter and clinical records

- Status: in review
- Baseline: `phase-1-experimental` at `d77a8320e6751a2deb2daf14cf1ac5d6b00cb989`
- Observed on: 2026-08-21
- Primary reviewers: `phase2_clinical_safety`, `phase2_data`
- Independent verification: separate read-only `phase2_verifier` pass
- Primary coverage: `COV-004`
- Supporting coverage: `COV-002`, `COV-006`, `COV-008`, `COV-009`, `COV-011`, `COV-014`
- Evidence level: source and retained-test trace, clean Release build, complete modern UI unit suite; database-backed interleavings and qualified clinical-policy decisions remain outstanding

## Assessment question

Does the fixed Phase 1 baseline preserve trustworthy, attributable, versioned clinical evidence as encounters are documented, signed, locked, corrected, transmitted, acknowledged, and retained?

This is an engineering-readiness assessment. Clinical, pharmacy, health-information-management, retention, legal, and interoperability conclusions remain subject to appropriately qualified validation. It makes no compliance, certification, or production-use claim.

## Representative traces

### Encounter documentation, signing, and locking

1. Encounter summary edits use an EF concurrency token, but the browser-loaded version is absent from the request. A sequentially stale form is therefore accepted after the repository loads the newer row.
2. SOAP notes have append-only versions, row locks, and structured conflicts when a caller supplies `ExpectedVersion`; the server makes that token optional, so another client can opt out.
3. Legacy encounter signatures bind signer, time, lock state, and amendment text, but not the clinical content or versions being attested.
4. A locking signature and several encounter-bound writes do not serialize on one shared aggregate lock. A write can pass its check and commit after the signature.
5. Governed clinical forms are a strong counterexample: they pin definitions, require expected versions, hash evaluated content, retain events, and use successor instances for amendments.

### Clinical lists, prescriptions, and vitals

1. The clinical-list route group requires medication-list view capability, while all of its write and delete routes inherit that same boundary and add no write-specific authorization.
2. Problems, allergies, immunizations, prescriptions, and procedure orders expose physical deletion paths; prescription deletion removes its audit first without a shared transaction.
3. Prescription create, deactivate, and direct-refill paths commit the clinical mutation and audit separately and can fall back to an `admin` actor rather than the authenticated user.
4. Medication updates have sound optimistic concurrency and atomic lifecycle events, but overwrite the clinical values without retaining before/after content.
5. Vitals accept an all-empty record and implausible numeric values, do not retain the author or a correction relationship, and expose only the newest observation through the reviewed encounter contract.

### Alerts and procedure orders

1. Allergy-review acknowledgement is keyed to encounter and rule name, not the evaluated allergy state or rule revision. A historical acknowledgement can suppress a newly recurring condition.
2. Procedure orders can be rewritten after transmission or reporting without an expected version, actor, reason, immutable revision, or transmitted snapshot.
3. The absence of local prescription allergy and dose checks is retained as a scope question, not a finding: the UI explicitly describes the current catalog as non-authoritative and the intended e-prescribing/CDS boundary is not approved.

## Reproducible checks

| Check | Result |
| --- | --- |
| Resolve the Phase 1 tag and compare `avenchart/`, `avenchart-ui/`, and `infra/` with the baseline | Baseline resolved; product tree remained unchanged during assessment |
| Release API build | Passed with 0 warnings and 0 errors |
| Complete modern UI unit suite | 31 files and 178 tests passed |
| Focused SOAP, encounter-lifecycle, clinical-form, and alert tests | 13 focused API tests and 18 focused clinical/UI tests passed in specialist passes |
| Modern UI production build and bundle budget | Passed; initial bundle 201,524 of 256,000 bytes, 128 chunks |
| PostgreSQL concurrency, fault injection, deletion/recovery, and invalid-value scenarios | Not run: Docker/PostgreSQL was unavailable in the assessment environment |

The green tests establish useful contract and lifecycle behavior, but do not exercise view-only write attempts, content-attestation verification, signing-versus-write interleavings, stale summary or SOAP clients, interrupted prescription audit, destructive recovery, invalid vitals, recurring alert conditions, medication-content reconstruction, or post-transmission order edits.

## Material strengths and counterevidence

- SOAP note history is append-only, uses one transaction with `FOR UPDATE`, retains supersession, attributes the authenticated author, and presents a usable conflict flow when the version token is supplied.
- Governed clinical forms are revision-pinned, schema-validated, expected-version checked, content-hashed, idempotent, evented, and amended through successor instances.
- Medication lifecycle transitions combine optimistic concurrency with actor, reason, and immutable lifecycle events in one EF save.
- Prescription content edits and portal refill approval use stronger row locks, transactions, versions, and actor-aware evidence than the older create/deactivate/direct-refill paths.
- Encounter archive and restore require a reason and expected version and atomically retain audit evidence.
- Procedure creation verifies patient/encounter correspondence; transmission is idempotent and refuses an initial transmit when a report already exists.
- Signature actors and times are server-derived, and signature rows are append-only.
- Alert acknowledgement is authenticated, timed, permitted only while the condition exists, and explicitly reopenable.
- UI confirmation reduces accidental permanent deletion.

These controls materially narrow the findings. The assessment does not attribute the conditions to EF Core or SQL as technologies: both mechanisms have strong and weak examples. The deciding factors are transaction scope, expected versions, content binding, authorization, and retained evidence.

## Validated findings

| Finding | Condition | Severity | Reach | Production blocker |
| --- | --- | --- | --- | --- |
| [`P2-03-F009`](../findings/p2-03-f009-encounter-signature-content.md) | Encounter signatures do not identify the clinical content or version attested | High | Repeated | Yes |
| [`P2-03-F010`](../findings/p2-03-f010-encounter-lock-boundary.md) | Encounter locking is a non-atomic check across several clinical writers | High | Systemic | Yes |
| [`P2-03-F011`](../findings/p2-03-f011-clinical-record-hard-delete.md) | Ordinary APIs physically delete clinical records and supporting evidence | High | Cross-cutting | Yes |
| [`P2-03-F012`](../findings/p2-03-f012-prescription-audit-atomicity.md) | Several prescription mutations are not atomic with correctly attributed audit | High | Repeated | Yes |
| [`P2-03-F013`](../findings/p2-03-f013-vitals-validation-provenance.md) | Vitals accept empty or implausible observations without correction provenance | High | Repeated | Yes |
| [`P2-03-F014`](../findings/p2-03-f014-alert-acknowledgement-state.md) | Allergy-review acknowledgement is not bound to the condition or rule revision | Medium | Repeated | Unknown pending clinical policy |
| [`P2-03-F015`](../findings/p2-03-f015-medication-edit-provenance.md) | Medication edits cannot reconstruct the prior clinical content | High | Repeated | Yes |
| [`P2-03-F016`](../findings/p2-03-f016-encounter-summary-stale-overwrite.md) | Sequentially stale encounter-summary forms overwrite newer data | High | Repeated | Yes |
| [`P2-03-F017`](../findings/p2-03-f017-soap-version-optional.md) | SOAP concurrency protection is optional at the server boundary | High | Repeated | Yes |
| [`P2-03-F018`](../findings/p2-03-f018-procedure-order-history.md) | Procedure orders remain rewriteable after transmission or reporting | High | Repeated | Yes |
| [`P2-05-F008`](../findings/p2-05-f008-clinical-list-write-authorization.md) | Medication-list view permission authorizes clinical-list mutations | High | Cross-cutting | Yes |

The engineering conditions were independently reproduced from the fixed source and retained tests. High severity reflects the adopted future-production target; it does not assert that real patient harm, unauthorized mutation, or records loss occurred in the synthetic experiment.

## Narrowed or retained as unknown

- Encounter locking is not uniformly absent. SOAP has a sound shared-lock pattern, and clinical forms have their own strong lifecycle. The finding is the inconsistent aggregate boundary across other writers.
- SOAP does not silently lose earlier versions. Its risk is that a caller can omit the version and make a stale snapshot current.
- Medication edit concurrency and event atomicity are strong. The finding is specifically the absence of prior/new clinical content in the retained evidence.
- Older vital rows remain in the database. The reviewed contract exposes only the newest row and supplies no correction or provenance semantics.
- Allergy review may intentionally mean “reviewed once for this encounter.” Until a clinical owner defines recurrence semantics, blocker status remains unknown and severity is held at medium.
- Local allergy, interaction, and dose checking for new prescriptions may be out of current product scope. This remains an explicit readiness question pending the intended prescribing, eRx, pharmacy, and CDS architecture.
- The retained smoke harness calls an unsupported encounter-delete route. This is supporting evidence for existing `P2-09-F002`, not a separate condition.

## Required specialist decisions and remaining evidence

- A clinician and clinical informaticist must define encounter-signature meaning, lock scope, stale-document behavior, vital validation/correction, alert recurrence, and acceptable clinical-history reconstruction.
- Pharmacy and medication-safety specialists must define prescription evidence and medication-correction requirements and decide the eventual allergy/dose/CDS boundary.
- HIM, records-management, privacy, and legal owners must define amendment, retention, deletion, legal-hold, and signature-evidence policy.
- Laboratory/procedure and interoperability owners must define immutable transmitted-order identity and change communication.
- A security/privacy owner must confirm the capability model for clinical-list reads and mutations.
- A disposable synthetic PostgreSQL runtime must exercise signing-versus-write races, stale summary and SOAP submissions, audit fault injection, invalid vitals, alert recurrence, destructive deletion/recovery, and post-transmission order changes.
- A browser/API authorization matrix must prove that a synthetic view-only principal cannot invoke any clinical-list mutation.

`COV-004` remains **In review** because these human decisions and runtime negative scenarios are outstanding. The validated engineering conditions may support later recommendation analysis; they do not authorize product changes.
