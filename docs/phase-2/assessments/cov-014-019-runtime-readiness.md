# COV-014/COV-019 runtime, recovery, deployment, and integration readiness

- Status: evidence collected; implementation gate open
- Baseline: `phase-1-experimental^{}` = `d77a8320e6751a2deb2daf14cf1ac5d6b00cb989`
- Observed on: 2026-08-21
- Primary reviewer: Phase 2 coordinator under `P2-D008` and the `avanchart-phase-2-assessment` contract
- Prior independent verification: COV-002, COV-006, COV-009, COV-010, COV-011, and external-feedback verifier packets
- Primary coverage: `COV-009`, `COV-010`, `COV-014`, `COV-015`, `COV-016`, `COV-017`, `COV-019`
- Supporting coverage: `COV-002`, `COV-006`, `COV-011`, `COV-012`
- Product mutation boundary: application source, tests, schema, and deployment implementation remained unchanged; only deterministic synthetic runtime data and Phase 2 records changed

## Scope decision

`P2-D014` fixes the target as a production-worthy US ambulatory EHR, supports only the modern `avenchart-ui` clinician and portal interface, requires standards-conformant FHIR, external laboratory-result intake tested with a synthetic laboratory, multi-facility and purpose-of-use authorization, vendor-neutral standards-based SSO, and a first-party test identity provider. The program owner directed every implementation gate to remain open until an explicit closure instruction.

## Environment

- Docker Desktop client/server 29.2.1
- PostgreSQL 17.10 in the isolated AvenChart Compose runtime
- .NET SDK 10.0.400
- Node.js 24.13.1 and npm 11.8.0
- Deterministic synthetic reset applied 201 packaged migrations
- Azure CLI had an enabled read-only context for the existing AvenChart demo resource group

## Reproducible runtime results

| Evidence | Result |
| --- | --- |
| Release API build | Passed, 0 warnings and 0 errors |
| Modern UI unit suite | Passed, 31 files / 178 tests |
| Modern UI lint | Passed |
| Modern UI production build and bundle budget | Passed; initial JavaScript 201,524 / 256,000 bytes; 128 chunks checked |
| C# format verification | Failed with extensive whitespace diagnostics; broadens `P2-02-F002` |
| Broad API/workflow smoke | Failed: 157 / 207 checks passed and 50 failed; failures included stale route/status/assertion contracts and cascades after encounter locking |
| Modern UI accessibility gate | Failed: 6 / 8 browser scenarios passed; two authorization-state scenarios received fixture HTTP 400 before accessibility scanning |
| Modern UI material workflows | Failed: 54 passed, 6 skipped, 4 failed; the medication inventory-link option was absent and timed out in Chromium desktop/mobile, Firefox, and WebKit |
| Modern UI isolated mutation workflows | Failed: the first inbox scenario reached cleanup but deletion failed; 10 later scenarios did not run |
| Migration resilience | Passed interruption/resume at committed checkpoints 1, 64, and 127; checksum drift and unexpected-ledger entries failed closed as expected |
| Empty-database migration bootstrap | Failed as predicted: `V0005` reached `relation "facilities" does not exist`; independently confirms `P2-04-F001` |
| Backup/restore rehearsal | Passed: backup created, post-backup marker added, database restored, marker removed, dataset/migration state recovered |
| Integration quarantine/recovery | Passed: three explicit failures quarantined the synthetic event, reasoned recovery requeued it, and a fourth dispatch completed locally |
| Critical-result acknowledgement | Passed with a temporary synthetic critical result and cleanup |
| Dependency vulnerability inventory | NuGet reported no known vulnerable direct/transitive packages from configured sources; npm audit reported zero known vulnerabilities across 278 dependencies |

The failed verification suites are retained as evidence. They were not repeatedly changed or rerun until green. Several failures are stale harness contracts rather than demonstrated product failures, which is itself material to `P2-09-F002`: the repository cannot presently use these suites as trustworthy release gates without reconciliation.

## Identity and authorization runtime

- The identity-readiness API reports `local-identity-adapter-v1`, zero production-approved or cryptographically validated providers, zero facility-scoped identities, and seven production-blocking gaps.
- A front-desk fixture successfully read a full patient chart without any purpose-of-use value.
- Supplying fabricated `X-Purpose-Of-Use` and facility headers returned the same `200` response and body.
- The resolved staff session contains username/display/role/staff/timestamps but no enforced facility, organization, team, patient, or purpose claim.

This reproduces `P2-05-F001` and `P2-05-F002` at runtime. `P2-D014` resolves the policy uncertainty: multi-facility and purpose-of-use enforcement are required, and the identity boundary must be vendor-neutral OIDC/OAuth SSO rather than hard-coded to one provider.

## FHIR runtime

- Missing authentication returned `401`.
- Authenticated `/api/fhir/R4/metadata`, Patient read, Encounter read/search, and Observation search returned data, but `Content-Type` remained `application/json` even when `Accept: application/fhir+json` was sent.
- A missing Patient returned an empty `404`, not a FHIR `OperationOutcome`.
- CapabilityStatement `format` serialized as one string; the hand-authored statement retains the structural defects already recorded in `P2-07-F002`.
- Patient search returned `500` when no filters, only `name`, or only `identifier` were supplied. It returned `200` only when both were supplied. PostgreSQL reported `42P08 could not determine data type of parameter $1` from `FhirRepository.SearchPatientsAsync`.
- The production-like Compose environment did not expose `/openapi/v1.json`; the Development document remains the incomplete route inventory recorded by `P2-07-F001`.

FHIR R4 is now required scope, so `P2-07-F002` is High and an unconditional production blocker. HL7 R4 requires an instance CapabilityStatement to identify its implementation, models search parameters as named/typed components, specifies `application/fhir+json`, and recommends `OperationOutcome` on failures: [CapabilityStatement](https://hl7.org/fhir/R4/capabilitystatement.html), [FHIR HTTP](https://hl7.org/fhir/R4/http.html).

## Synthetic external laboratory boundary

- Internal staff procedure APIs created an order, a report, and an atomic result with `201`, demonstrating a working local persistence path.
- The report accepted `NO-SUCH-SPECIMEN-P2`, reproducing the lineage condition in `P2-03-F028`.
- A generic `lab.result.v1` integration inbox message returned `201` but created zero `lab_results` rows.
- Replaying the same `(source, source_message_id)` with a different result value returned `200` and `duplicate = true`; the clinical result count remained 2,400.
- No external laboratory source authentication, selected standard/profile, patient/order/specimen resolver, terminology normalization, clinical apply transaction, correction equivalence, notification, or reconciliation-to-clinical-record path was found.

This establishes new `P2-07-F005` and supplies runtime confirmation for `P2-07-F003` and `P2-03-F028`.

## Existing Azure demo evidence

Read-only inspection found a running, healthy demo revision with HTTPS-only external ingress, one minimum/two maximum replicas, private-network PostgreSQL 17, disabled database public access, seven-day backups, managed identity, RBAC/soft-delete/purge-protected Key Vault, successful migration jobs, and healthy proxied API readiness.

Important limits:

- the deployed image identifies commit `4a3c2b2` from 2026-08-10 and reports 192 migrations, while the fixed Phase 1 baseline is `d77a832...` with 201 migrations;
- PostgreSQL high availability and geo-redundant backup are disabled;
- the UI response did not include HSTS, CSP, `X-Content-Type-Options`, or Referrer-Policy headers in the observed request;
- ACR, Key Vault, and Log Analytics public network access remain enabled; no metric alerts were configured;
- no Azure restore, failover, scale, alert, incident, or current-baseline deployment exercise was performed.

The demo proves that the deployment design can run. It does not prove production readiness or deployment of the assessed baseline.

## Finding disposition

- Add `P2-07-F005`: no supported external laboratory-result ingestion contract applies results to the clinical record — High, production blocker Yes.
- Convert `P2-07-F002` from conditional to High / production blocker Yes because FHIR R4 is explicitly required by `P2-D014`; add live search/MIME/error evidence.
- Broaden `P2-07-F003` with live divergent-payload replay.
- Broaden `P2-03-F028` with a live report/result accepted against a nonexistent specimen reference.
- Broaden `P2-04-F001` with failed empty-database bootstrap and successful seed-first migration/restore counterevidence.
- Broaden `P2-05-F001` and `P2-05-F002` with live identity-readiness and no-purpose/fabricated-scope patient access.
- Broaden `P2-09-F002` with the actual smoke, accessibility, material-workflow, and mutation-gate failures.
- Add bounded deployment and dependency evidence to `COV-016` and `COV-017` without creating a new root.

## Coverage impact

- `COV-009`, `COV-010`, `COV-014`, `COV-015`, `COV-016`, `COV-017`, and `COV-019` now have representative runtime evidence. Their unresolved conditions become finding/recommendation inputs rather than unknown environmental availability.
- `COV-013` remains Excluded under `P2-D014`.
- Manual assistive-technology testing, risk/policy adjudication, representative concurrency/query-plan performance, and a current-baseline production topology remain open.
- At completion of the runtime packet, the register contained 64 findings: 39 High, 23 Medium, and 2 Low, with 37 High findings then unconditional production blockers. The later `P2-D016` lifecycle and critical-follow-up decisions make all 39 High findings blockers against the adopted target.

## Human decision update

`P2-D016` approves all twelve recommended target-policy defaults. Independent accessibility evaluation and qualified legal/HIM, clinical, interoperability, security/privacy, and operations acceptance evidence remain required. Recommendation acceptance and explicit gate closure remain separate decisions.

## Readiness disposition

Runtime availability is no longer the main blocker to finishing Phase 2 analysis. The material blockers are validated product conditions, failed/stale verification gates, specialist decisions, recommendation acceptance, and explicit implementation authorization. Every gate remains open by program-owner instruction.

## Final local state

After evidence capture, the guarded reset restored the local database to `avenchart-shared-synthetic-v1`, reapplied all 201 packaged migrations, and reproduced the expected dataset counts. API liveness/readiness and PostgreSQL/schema checks returned `healthy`; the modern UI returned HTTP `200`. The excluded reference UI was stopped. The API, PostgreSQL, and modern UI remain available for the next authorized Phase 2 validation session. Temporary synthetic records created by the experiments were removed by the reset.
