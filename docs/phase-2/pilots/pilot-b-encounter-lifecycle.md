# Pilot B — Encounter and clinical documentation lifecycle

## Packet

- Baseline: `d77a8320e6751a2deb2daf14cf1ac5d6b00cb989`
- Coverage sampled: COV-003, COV-004, COV-008, COV-009, COV-011, COV-014
- Trace: encounter creation, coding, SOAP versions, signatures, forms, documents, concurrency, audit, UI recovery, and downstream encounter detail
- Reviewers: data/persistence specialist; clinical-safety specialist
- Required human validation: clinician or clinical informaticist; legal/compliance for signature/audit meaning; database/operations for concurrency and measurement

## Independent pass 1

The data reviewer used static vertical tracing, a clean Release build, and focused SOAP, encounter-lifecycle, and governed-form tests. The build completed with no warnings or errors and eight focused tests passed. No live database mutation was performed.

Material strengths:

- SOAP notes use a transaction, row locks, expected-version comparison, append-only versions, and a unique encounter/version constraint;
- the UI preserves a stale SOAP draft and exposes the newer server version;
- documents use version snapshots, expected-version conflicts, and separate metadata, review, and archive histories;
- governed clinical forms pin instances to definition revisions and use expected versions, event snapshots, and content-bound signatures;
- EF Core is used for cohesive encounter-state mutations and SQL for more complex projections and versioned workflows; the hybrid boundary itself was not treated as a defect.

Candidate conditions from pass 1:

| Candidate | Initial severity | Confidence | Key evidence | Specialist need |
| --- | --- | --- | --- | --- |
| Creating a locking signature is not serialized with encounter writers that first check for a signature and then mutate, leaving a possible sign-versus-write race. | High | High for source analysis | `EncounterRepository.cs:598-674`; `EncounterStateRepository.cs:31-69,89-123,182-194`; representative writers under `Program.cs:2535-3043` | Clinical and database/operations |
| Encounter signature hashes are derived from metadata, not the SOAP, form, document, coding snapshot, or their version identifiers. | High | High for implementation | `EncounterRepository.cs:598-674`; `EncounterDtos.cs:73-93`; governed-form counterexample in `V0091__governed_clinical_form_engine.sql:118` | Clinical and legal/compliance |
| Actor and durable history evidence is uneven across encounter mutations; creation, vitals, and billing/coding mutations do not all retain actor, prior value, and deletion evidence. | High, cross-cutting | High | `Program.cs:2405-2470,6460-6533`; `EncounterStateRepository.cs`; `BillingRepository.cs`; encounter audit projection in `EncounterRepository.cs:256-408` | Legal/compliance and operations |
| Legacy encounter form records load through the currently active layout instead of a capture-time definition revision. | Medium | High | `V0018__encounter_layout_form_records.sql`; `EncounterLayoutFormRepository.cs:25-132`; revisioned-form counterexample in `V0091__governed_clinical_form_engine.sql` | Clinical |
| Encounter detail is assembled through multiple read-committed queries and loads patient-wide document bytes before filtering to the encounter. | Medium | High for path; medium for measured impact | `EncounterRepository.cs:89-225`; `DocumentRepository.cs:71-90,3674-3784`; `PatientEncounters.tsx:364-375` | Database/operations |

## Independent pass 2

The clinical-safety reviewer independently traced the same package, ran six focused SOAP and encounter-lifecycle tests successfully, and found additional material safeguards around patient association, SOAP conflict recovery, document association/version history, archive/restore, and the whole-lifecycle browser proof.

Independent conclusions:

- the reviewer matched pass 1 exactly on the absence of signed-content binding;
- the reviewer also found inconsistent concurrency boundaries, focusing on summary updates with no client expected version, configured layouts with no expected definition/submission revision, and the new-encounter SOAP path where `ExpectedVersion` is optional;
- the reviewer matched the uneven actor/history condition but rated it medium instead of high because several subsystems retain strong local histories and the UI accurately scopes its summary audit;
- the reviewer identified two additional validation conditions: the trusted server boundary accepts physiologically impossible vital values, and syntactically valid diagnosis codes are projected downstream without governed terminology membership/version validation.

## Reconciliation

The two passes independently agreed on the signature-content condition, the cross-cutting inconsistency of concurrency protection, and fragmented mutation history. The audit severity differs by one level and is retained pending reconciliation evidence. The reviewers’ concurrency findings are complementary rather than contradictory: one identified the uncoordinated locking-signature race, while the other identified stale-client gaps in summary, legacy-form, and wizard SOAP contracts.

Legacy definition drift and the mixed-snapshot/patient-wide document read were found only by the data reviewer. Physiologic validation and governed terminology membership were found only by the clinical reviewer. These are useful examples of the specialist roles adding non-duplicate coverage; they require focused experiments or domain validation rather than forced consensus.

## Independent verification

The verifier, who did not author either pass, reached these dispositions:

| Cluster | Verifier disposition | Reconciled severity/confidence |
| --- | --- | --- |
| Encounter signatures do not identify the signed clinical content | Corroborated | High and systemic; high confidence |
| Locking-signature creation is not atomic with several writers | Corroborated and narrowed: SOAP has materially stronger encounter/current-note row locking, while summary, vitals, and layout forms retain check/use gaps | High and cross-cutting; high confidence for source, medium for exact PostgreSQL interleaving |
| Summary, legacy-form, and wizard SOAP concurrency contracts differ | Corroborated, but mechanisms must remain distinct | Medium pending demonstrated clinical stale-write consequence; high confidence for contracts |
| Actor/history evidence is uneven | Corroborated; separate SOAP/form/signature histories are compensating controls | High-to-medium pending reliable runtime record correlation; high confidence |
| Physiologically impossible vitals reach persistence | Corroborated from contract, repository, UI, and schema | High candidate pending synthetic display trace and clinical limits; high confidence |
| Legacy form drift and mixed encounter snapshots | Plausible hypotheses | Focused experiments required before severity |
| Patient-wide document bytes load before encounter filtering | Source-confirmed | Impact and severity require representative measurement |

Material independent agreement is acceptable with these narrowings. Required next evidence includes sign-versus-write interleavings, two-client stale edits, a signed-content manifest comparison, mutation-to-history reconciliation, impossible-vital submissions, terminology checks, a layout-revision replay, and representative document measurements. Technical behavior will not be promoted to a clinical, legal-signature, coding, or production-readiness conclusion without the named specialists.
