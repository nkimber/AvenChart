# COV-003 assessment — patient identity, lifecycle, demographics, and SDOH

- Status: in review
- Baseline: `phase-1-experimental` at `d77a8320e6751a2deb2daf14cf1ac5d6b00cb989`
- Observed on: 2026-08-21
- Primary reviewers: `phase2_clinical_safety`, `phase2_data`
- Independent verification: separate read-only `phase2_quality_operations` pass
- Primary coverage: `COV-003`
- Supporting coverage: `COV-004`, `COV-005`, `COV-008`, `COV-010`, `COV-011`, `COV-012`, `COV-014`
- Evidence level: source and retained-test trace, clean Release build, complete modern UI unit suite; database-backed interleavings remain outstanding

## Assessment question

Does the fixed Phase 1 baseline preserve one reliable patient identity through registration, demographic correction, chart navigation, merge, lifecycle changes, record administration, and SDOH follow-up?

This is an engineering-readiness assessment. Clinical, health-information-management, privacy, legal, and interoperability conclusions remain subject to appropriately qualified validation. It makes no compliance, certification, or production-use claim.

## Representative traces

### Registration and duplicate review

1. The modern registration page fingerprints name, DOB, phone, and email and fails closed when duplicate lookup fails.
2. When candidates exist, it requires an explicit browser-side separate-patient confirmation.
3. The registration request carries only patient fields; it contains no reviewed fingerprint, candidates, override rationale, version, or idempotency evidence.
4. `POST /api/patients` validates ordinary fields and inserts directly. Identifier uniqueness is enforced, but duplicate review is not.
5. The retained Phase 1 smoke script deliberately creates an exact, 100-score duplicate through the API and only detects it afterward.

### Demographic correction

1. The Summary page presents contact and demographics as one edit and one Save action.
2. Save commits contact first and demographics second through separate HTTP requests and transactions.
3. Both full-area contracts omit an expected patient version. Row locks serialize each database request, but cannot detect a form prepared from stale patient data.
4. Before/after audit is written atomically with each individual mutation, providing useful forensic evidence but not conflict prevention or a shared commit boundary.

### Merge and post-merge use

1. Preview calculates current match evidence and combined counts.
2. The audit plan stores patient identifiers, score, reasons, rationale, actor, and time, but no reviewed patient snapshot, version, or expiry.
3. Execution locks the plan and patients, rechecks current structural blockers, moves supported rows transactionally, records a manifest, and supports rollback.
4. Execution does not recompute or compare the identity evidence that was approved.
5. Search hides the merged source, but direct chart, patient-administration, encounter, and other resolvers can still accept its old identifier without redirecting or rejecting it.

### Chart lifecycle and SDOH

1. The persistent patient header shows name, DOB, age, sex, chart number, and provider.
2. Retired and deceased warnings are limited to Summary; encounter creation accepts those patients without an explicit exception contract, while appointment creation correctly rejects them.
3. Patient-shell requests are not cancelled or tied to a request epoch. A late response for patient A can be combined with patient B's current route identifier.
4. SDOH assessments use EF optimistic concurrency, but generated goal target dates are recalculated as the current UTC date plus 90 days on every read.

## Reproducible checks

| Check | Result |
| --- | --- |
| Resolve the Phase 1 tag and compare `avenchart/`, `avenchart-ui/`, and `infra/` with the baseline | Baseline resolved; product tree remained unchanged during assessment |
| Release build | Passed with 0 warnings and 0 errors |
| Complete modern UI suite | 31 files and 178 tests passed |
| Focused API/disclosure slice | 2 files and 62 tests passed during specialist review |
| Direct exact-duplicate behavior | Retained baseline script creates the duplicate before detecting it |
| Database-backed interleavings and lifecycle matrix | Not run: Docker/PostgreSQL was unavailable in the assessment environment |

The green tests do not cover duplicate enforcement at the server boundary, interrupted composite save, stale demographic forms, stale merge review, post-merge source writes, patient-route response inversion, retired/deceased encounter creation, or stable SDOH target dates.

## Material strengths and counterevidence

- The patient header uses several high-value disambiguators and remains visible across chart tabs.
- The modern registration UI has a thoughtful, fail-closed duplicate workflow; duplicate scoring, review queues, dispositions, constrained merge, and rollback are present.
- Patient identifiers use sequences and database uniqueness. No inappropriate demographic uniqueness constraint is proposed.
- Individual demographic/contact writes lock the patient and atomically retain before/after audit.
- Merge execution is transactional, locks relevant rows, detects unsupported populated dependencies, is single-use, manifests moved records, and supports rollback.
- Main search hides merged sources. Newer disclosure, record-request, SDOH, and lifecycle paths explicitly reject merged sources.
- Appointment creation and rescheduling reject retired and deceased patients.
- Lifecycle and deceased corrections require reasons and retain actor/time history.
- Record requests enforce one open request and use optimistic concurrency. Disclosure authority and request transitions have strong version, lock, policy, and event controls.
- SDOH uses EF Core optimistic concurrency and rejects merged patients.

These controls materially narrow the findings. They do not make the affected decisions invariant across every entry point.

## Validated findings

| Finding | Condition | Severity | Reach | Production blocker |
| --- | --- | --- | --- | --- |
| [`P2-03-F001`](../findings/p2-03-f001-duplicate-review-boundary.md) | Registration can bypass duplicate review and its override evidence | High | Repeated | Yes |
| [`P2-03-F002`](../findings/p2-03-f002-demographics-partial-save.md) | One demographics Save can commit only its contact portion | Medium | Repeated | No |
| [`P2-03-F003`](../findings/p2-03-f003-stale-demographic-overwrite.md) | Stale full-record demographic edits silently overwrite intervening corrections | High | Repeated | Yes |
| [`P2-03-F004`](../findings/p2-03-f004-stale-merge-approval.md) | Merge execution is not bound to the identity state that was reviewed | High | Repeated | Yes |
| [`P2-03-F005`](../findings/p2-03-f005-merged-source-actionable.md) | Merged source identifiers remain directly readable and actionable | High | Systemic | Yes |
| [`P2-03-F006`](../findings/p2-03-f006-lifecycle-encounter-boundary.md) | Encounter creation does not enforce or persistently display patient lifecycle state | High | Cross-cutting | Unknown pending clinical policy |
| [`P2-03-F007`](../findings/p2-03-f007-patient-route-response-inversion.md) | Patient-route response inversion can combine one patient with another patient's route | High | Systemic | Yes |
| [`P2-03-F008`](../findings/p2-03-f008-sdoh-goal-date-drift.md) | Generated SDOH target dates move forward on every read | Medium | Repeated | No |

The engineering conditions were independently reproduced from the fixed source and retained tests. Clinical consequences remain subject to qualified validation. High severity means the condition is material against the adopted future-production target; it does not establish that real patient harm occurred in the synthetic experiment.

## Narrowed or retained as unknown

- Merge is not generally unsafe: execution-time locks, blockers, atomic movement, manifesting, and rollback are substantial controls. `P2-03-F004` is specifically about stale reviewed identity evidence.
- Merged-source handling is inconsistent, not absent everywhere. Search and several newer repositories correctly reject the source.
- Retired or deceased patients may require controlled historical or post-mortem documentation. The finding is the absence of a deliberate exception contract and persistent warning, not a claim that all such documentation must be prohibited.
- SDOH editing replaces prior values without an immutable correction history. This remains a high clinical candidate until the intended correction-versus-new-assessment policy is defined and independently verified.
- Record-request completion records status, actor, and time but not fulfillment/delivery evidence. Whether AvenChart is intended to own that full workflow remains a privacy/legal and product-scope question.
- Registration and demographic correction accept a future DOB, and duplicate dispositions lack actor/history/concurrency. These are retained for later calibration rather than promoted in this packet.
- No general consent-directive, privacy-restriction, or disclosure-accounting surface was located. Applicability is not inferred without approved scope and qualified legal/privacy analysis.

## Authoritative target references

- [2025 SAFER Guides](https://healthit.gov/clinical-quality-and-safety/safer-guides/)
- [2025 SAFER Patient Identification Guide](https://healthit.gov/wp-content/uploads/2025/01/Safer-Guide-6.-Patient-Identification-Final.pdf)
- [HL7 FHIR R4 Patient definitions](https://hl7.org/fhir/R4/patient-definitions.html)
- [HHS individual right of access guidance](https://www.hhs.gov/hipaa/for-professionals/privacy/guidance/access/index.html)

## Required specialist decisions and remaining evidence

- A clinician and clinical informaticist must define retired/deceased encounter behavior, historical-documentation exceptions, persistent warnings, and SDOH follow-up semantics.
- A health-information-management or patient-identity specialist must define duplicate override evidence, merge approval freshness, and merged-source redirect/reject behavior.
- Privacy/legal and records-management owners must define the authoritative record-request, consent/restriction, disclosure, and demographic-correction requirements.
- An interoperability specialist must validate the representation of merged/replaced and inactive patients in FHIR and other outbound contracts.
- A disposable synthetic PostgreSQL runtime must exercise concurrent duplicate registration, interrupted demographics save, two-user stale forms, merge-review identity changes, post-merge source access/writes, retired/deceased encounter creation, and rollback after post-merge change.
- A controlled browser or component test must reproduce the patient-shell response inversion and verify every patient-bound child uses one coherent identity.

`COV-003` remains **In review** because these human decisions and runtime negative scenarios are outstanding. The validated engineering conditions may support later recommendation analysis; they do not authorize product changes.
