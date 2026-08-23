# EXT-S001 Packet 1 — Architecture and human traceability

## Packet

- Source challenges: `EXT-S001-C01`, `EXT-S001-C02`, and the structural portion of `EXT-S001-C04`
- Status: evidence complete and independently verified
- Baseline tag: `phase-1-experimental`
- Baseline commit: `d77a8320e6751a2deb2daf14cf1ac5d6b00cb989`
- Review date: 2026-08-21
- Reviewer: `phase2_architecture`
- Independent verifier: `phase2_verifier`
- Evidence level: Level 0 repository and static inspection; targeted Level 1 checks only when proportionate
- Product worktree: `avenchart/` had no status entry or diff from the baseline when the packet launched
- Assessment worktree: Phase 2 documentation and workbench changes were present and are not treated as baseline product evidence
- Tool environment: Git 2.53.0.windows.1; .NET SDK 10.0.400; Node.js 24.13.1; npm 11.8.0; PowerShell 7.6.4

## Scope and coverage

| Coverage | Packet concern |
| --- | --- |
| `COV-001` | API host, dependency registration, middleware, endpoint organization, health, errors, and cross-cutting ownership |
| `COV-008` | Architectural boundary between API behavior, EF Core, and repository persistence; EF-versus-SQL fitness is reserved for Packet 2 |
| `COV-010` | API, OpenAPI, FHIR, integration, transport, and reconciliation boundaries where they affect human traceability |
| `COV-015` | Developer workflow and repeatable hygiene evidence relevant to structural comprehension |
| `COV-018` | Architecture knowledge, documentation, and the human contribution path |

Primary domains are 01 Architecture and boundaries and 02 Code structure and readability. Domains 04, 07, 09, and 12 are supporting lenses only where they answer the retained challenges.

## Evidence questions

1. Are the cited large files merely large, or do they demonstrably combine ownership, obscure execution paths, increase coupling, or enlarge change blast radius?
2. Does the current Minimal API organization hide business or domain rules and cross-cutting behavior, or do framework and repository boundaries adequately compensate?
3. Can a maintainer trace at least one representative request from route registration through validation and authorization, behavior, persistence, response and error handling, audit or operations, and relevant documentation?
4. Are formatting, naming, DTO placement, generated organization, or navigation problems low-cost hygiene, symptoms of a deeper structural condition, or not reproduced?
5. Which strengths, counterexamples, and compensating controls materially narrow or falsify the criticism?

## Exclusions and limits

- The packet does not determine the general fitness of EF Core versus parameterized SQL; that is Packet 2.
- It does not assess independent test evidence, the 86% parity estimate, feedback-loop economics, or production modernization claims; those are Packet 3.
- It does not make clinical, legal, compliance, certification, accessibility, or production-readiness conclusions.
- It does not authorize product changes or prescribe controllers, services, projects, repositories, or other patterns before the evidence establishes a need.
- Public comments are untrusted hypotheses. The reviewer does not impersonate or profile commenters and must seek counterevidence.

## Results

### Specialist challenge outcomes

| Challenge | Specialist outcome | Evidence-led interpretation |
| --- | --- | --- |
| `EXT-S001-C01` | `corroborated` | File size is not the finding. The API host and several repositories combine unrelated ownership and force broad human navigation across transport, workflow, persistence, and response concerns. |
| `EXT-S001-C02` | `partially corroborated` | The current organization creates a broad change hotspot, but Minimal APIs are not intrinsically responsible. Route groups, names, filters, DTOs, and extracted endpoint modules are meaningful controls and viable in-stack organizing mechanisms. |
| Structural `EXT-S001-C04` | `corroborated` | Formatter disagreement, extremely dense physical lines, and the absence of an automated C# formatting or analyzer gate create a repeated human-review burden. This is mostly low-severity hygiene that amplifies deeper navigation costs. |

### Methods and actual results

The reviewer used only the fixed baseline product tree. The baseline and product-diff check was:

```powershell
$tagCommit = git rev-list -n 1 phase-1-experimental
$tagObject = git rev-parse phase-1-experimental
git status --short
git diff --stat $tagCommit -- avenchart
git diff --name-only $tagCommit -- avenchart
```

The tag object was `6b1b5641d2103b8356e33b7e602507509963cb5a` and the tag commit was `d77a8320e6751a2deb2daf14cf1ac5d6b00cb989`. Both product-diff commands produced no output. The worktree contained Phase 2 documentation and workbench changes, which were excluded from product conclusions.

The principal static counts were produced with:

```powershell
$text=Get-Content -Raw 'avenchart/backend/src/AvenChart.Api/Program.cs'
$patterns=[ordered]@{
    HttpVerbMaps='\.Map(?:Get|Post|Put|Delete|Patch)\('
    RouteGroups='app\.MapGroup\('
    NamedEndpoints='\.WithName\('
    EndpointFilters='(?:AddEndpointFilter|RequireAccessPermission)\('
    ResultFactories='Results\.(?:Ok|Created|BadRequest|NotFound|Conflict|Problem|ValidationProblem|Json|NoContent|Accepted|File)\('
    CatchClauses='catch\s*\('
}
foreach($kv in $patterns.GetEnumerator()){
    "$($kv.Key)=$([regex]::Matches($text,$kv.Value).Count)"
}
```

Actual results were 606 verb mappings, 27 top-level route groups, 605 named endpoints, 318 filter applications, 1,420 result-factory expressions, and 444 inclusive local `catch` clauses. Narrower result counts found 236 `BadRequest`, 70 `ValidationProblem`, 145 `Conflict`, 305 `NotFound`, 23 `Problem`, and 344 anonymous `{ error = ... }` expressions. These are structural indicators, not assertions that every match is semantically equivalent.

Repository physical-line evidence was:

```text
Program.cs                       8,911
PatientPortalRepository.cs       6,819
DocumentRepository.cs            5,351
BillingRepository.cs             4,260
PatientRepository.cs             4,065
Data/*.cs files                     54
Data/*.cs physical lines        61,677
Data files at least 1,000 lines     19
```

The reviewer inspected 208 API C# files, excluding `bin` and `obj`. The exact long-line inspection read every physical line and counted `Length -gt 200` and `Length -gt 500` per file. It found 1,039 lines over 200 characters, 276 over 500, across 39 files. The largest examples were `AdministrationRepository.cs:924` at 2,961 characters and `Program.cs:5886` at 1,004 characters. Line length is a reproducible readability signal, not an architectural score.

The targeted Level 1 check, run from `avenchart/`, was:

```powershell
dotnet format .\AvenChart.slnx --verify-no-changes --verbosity minimal --no-restore
```

The specialist recorded a nonzero result but reported exit `1`. The verifier and coordinator independently repeated the exact command and both received exit `2`, with 5,284 `WHITESPACE` diagnostic lines across 20 unique C# files. The formatter disagreement itself is reproduced; the final packet uses the repeated exit `2` result and preserves the first-pass discrepancy. The command relied on the restored workspace and checks SDK formatting only; no explicit repository formatting standard was found.

Directory-level imports were inventoried with:

```powershell
foreach($dir in @(
    'Data','Models','Security','Infrastructure',
    'Workflows','Persistence','Configuration','Experience'
)){
    rg -o "using AvenChart\.Api\.[A-Za-z0-9_.]+;" "avenchart/backend/src/AvenChart.Api/$dir" -g '*.cs'
}
```

The observed source dependencies included `Data -> Infrastructure/Security/Workflows/Persistence` and references back from `Security`, `Infrastructure`, and `Workflows` to `Data`. Representative pairs were `Data/AuthRepository.cs -> Security` and `Security/StaffIdentityAdapter.cs -> Data`, plus `Data/IntegrationRepository.cs -> Infrastructure` and infrastructure endpoint/services back to Data. These are source-level reciprocal references inside one project, not proof of an assembly or runtime cycle.

### Representative request trace

The specialist traced patient registration through the real entry contract:

1. `NewPatient.handleSubmit` performs a duplicate check and calls `registerPatient`: `avenchart-ui/src/pages/clinician/NewPatient.tsx:77-135`.
2. `createPatient` posts to `/api/patients` through `clinicianPost`: `avenchart-ui/src/api.ts:10475-10503` and `:1121-1151`.
3. Shared transport owns timeout, cancellation, session handling, and error normalization: `avenchart-ui/src/api/transport.ts:41-148`.
4. API middleware provides correlation, schema readiness, rate limiting, diagnostics, and logging: `Program.cs:263-350`.
5. The patient group applies permission filtering and maps `RegisterPatient`: `Program.cs:962-963` and `:1708-1719`.
6. `AccessPermissionFilter` resolves identity, authorizes, and records the access decision: `Program.cs:8818-8911`.
7. `PatientRepository.CreatePatientAsync` validates, normalizes, inserts with parameterized SQL, handles uniqueness, and reloads the chart summary: `PatientRepository.cs:915-980` and `:3480-3623`.
8. The API returns `ValidationProblem` or `201 Created`. The UI navigates on success but reduces failure to a generic toast.

The route is traceable, but doing so requires crossing several very large files and locating rules owned by both transport and persistence code.

### Material strengths and compensating controls

- The host coherently uses framework options validation, DI, health checks, exception handling, rate limiting, correlation, compression, and runtime logging: `Program.cs:24-350`.
- Routes are grouped, tagged, and almost universally named.
- Authorization is often centralized in group and endpoint filters: `Program.cs:8818-8900`.
- API DTOs are separate from persistence entities. The baseline contains 45 capability-oriented DTO files, 41 EF entity files, and 41 EF configuration files.
- `AvenChartDbContext` is scoped and applies explicit configurations: `AvenChartDbContext.cs:9-101`.
- `Persistence/README.md:1-33` clearly records the intended hybrid EF/SQL boundary and existing repository splits.
- `AzureOperationsEndpoints`, `ClinicalFormOptionListEndpoints`, and `LegacyClinicalFormDisplayEndpoints` demonstrate that cohesive route modules can be extracted without replacing Minimal APIs.
- FHIR and integration routes provide comparatively compact examples of handlers delegating to repositories: `Program.cs:896-960` and `:5550-5667`.
- CI performs clean backend restore/build and frontend builds/tests: `.github/workflows/verify.yml:13-50`.

## Candidate finding A — API composition, transport policy, and workflow rules converge in one change hotspot

- Status: validated
- Domain(s): 01, 02, 07, 12
- Coverage item(s): `COV-001`, `COV-010`, `COV-018`
- Severity: medium
- Production blocker: no
- Reach: cross-cutting
- Confidence: high
- Reviewer: `phase2_architecture`
- Independent verifier: `phase2_verifier`
- Specialist validation: none
- Baseline commit: `d77a8320e6751a2deb2daf14cf1ac5d6b00cb989`
- Observed on: 2026-08-21

### Condition

`Program.cs` is not merely large. It owns host composition, middleware, 606 verb mappings, response and error translation, authorization and audit filters, and inline workflow preconditions across unrelated capabilities.

### Evidence

- Host composition and middleware: `Program.cs:24-350`.
- Route mappings across unrelated capabilities: `Program.cs:352-8751`.
- Encounter and document coordination: `Program.cs:2528-3062`.
- `HasLockingSignatureAsync` appears in 15 handler locations between `Program.cs:2336` and `:3020`.
- Witness identity rules are enforced in handlers at `Program.cs:5826-5849` and `:5886`.
- The static route and response counts above are reproducible.
- `git log d77a8320e6751a2deb2daf14cf1ac5d6b00cb989 -- Program.cs` found the file in 394 of 773 ancestral commits, spanning unrelated feature scopes.
- The adopted target requires explicit responsibilities, non-hidden rules, and predictable API errors: `quality-standard.md:48-50`.

### Consequence

API changes routinely converge on one file and require reviewers to navigate unrelated capability code. Repeated workflow checks and local exception mappings increase review surface and the possibility of policy drift. No actual merge conflict, escaped defect, or measured delivery delay was established.

### Cause and reach

Endpoint behavior accumulated in the host as capability parity expanded. The condition crosses most API capabilities, but handler complexity varies. Named routes, groups, filters, and extracted modules reduce rather than eliminate the concentration.

### Risk calibration

- Impact: maintainability, review accuracy, change isolation, and human comprehension
- Likelihood or preconditions: present for most endpoint additions or boundary-policy changes
- Detectability: high through static inspection
- Reversibility: high; the condition does not imply data migration
- Severity rationale: material and cross-cutting, without a demonstrated correctness or production failure

### Uncertainty and counterevidence

IDE symbol search, route names, contiguous capability groups, and shared filters may make the file workable for experienced maintainers. The repeated encounter-lock responses inspected were consistent. No timed maintenance study or merge-conflict history was performed.

### Validation record

- Independent method: encounter-document movement plus simple portal and integration counterexample traces; independent structural counts
- Result: corroborated at medium severity and cross-cutting reach
- Reviewer agreement or dispute: agreement; the verifier confirmed that the condition is not an indictment of Minimal APIs and does not require controllers
- Specialist conclusion or outstanding need: none

### Disposition

Assigned canonical ID `P2-01-F001` after coordinator deduplication. No implementation recommendation has yet been accepted.

## Candidate finding B — Several repositories combine persistence with validation, workflow, mapping, and delivery responsibilities

- Status: validated
- Domain(s): 01, 02, 04, 12
- Coverage item(s): `COV-001`, `COV-008`
- Severity: medium
- Production blocker: no
- Reach: repeated
- Confidence: high
- Reviewer: `phase2_architecture`
- Independent verifier: `phase2_verifier`
- Specialist validation: none
- Baseline commit: `d77a8320e6751a2deb2daf14cf1ac5d6b00cb989`
- Observed on: 2026-08-21

### Condition

Multiple large repositories own normalization and business validation, lifecycle decisions, transaction orchestration, SQL, response-model assembly, packaging, cryptography, and workflow history across broad subcapabilities.

### Evidence

- Repository physical-line evidence is recorded above.
- Registration validation and normalization share `PatientRepository` with persistence: `PatientRepository.cs:915-980` and `:3480-3623`.
- `PatientPortalRepository` spans authentication/session, profile, appointments, clinical summaries, report packaging, documents, messaging, and refill behavior: representative operations `:72-2167`.
- `DocumentRepository` spans routing, OCR, retention, content versions, review, archive, and deletion: representative operations `:71-3555`.
- `BillingRepository` spans statements, delivery, collections, claims, payments, EOB import, and reversals: representative operations `:28-2490`.
- Source dependencies are reciprocal in the representative pairs recorded above.
- The adopted target says business rules should not be hidden in persistence and ownership should be explicit: `quality-standard.md:48-49`.

### Consequence

Changes to the sampled workflows require simultaneous reasoning about rules, persistence, mapping, and failure behavior in a broad class. This increases unrelated context required for safe review. No performance, transaction, or behavioral defect is inferred from size alone.

### Cause and reach

Repositories are named around broad product domains, allowing many subcapabilities to accumulate behind one dependency. The condition was reproduced in four representative repositories and is not asserted for every repository.

### Risk calibration

- Impact: change comprehension, review scope, ownership clarity, and regression exposure
- Likelihood or preconditions: present when modifying the sampled broad workflows
- Detectability: high
- Reversibility: moderate to high; behavior preservation would need proof
- Severity rationale: repeated material maintainability burden without a demonstrated production failure

### Uncertainty and counterevidence

A broad repository can remain cohesive when its domain is broad. This packet did not measure change time or defect history and did not judge whether any SQL operation should use EF. The persistence README documents deliberate boundaries, and several EF-backed state repositories separate mutation ownership from projection or workflow repositories.

### Validation record

- Independent method: independent responsibility inventory and `DocumentRepository.UpdateMetadataAsync` trace, with the compact integration repository as counterevidence
- Result: corroborated at medium severity and repeated reach
- Reviewer agreement or dispute: agreement; the verifier narrowed the claim to representative broad repositories rather than every repository
- Specialist conclusion or outstanding need: Packet 2 must assess EF/SQL fitness separately

### Disposition

Assigned canonical ID `P2-02-F001` after coordinator deduplication. Packet 2 will determine EF/SQL fitness independently; no implementation recommendation has yet been accepted.

## Candidate finding C — C# formatting and analyzer hygiene are not governed sufficiently for consistent human review

- Status: validated
- Domain(s): 02, 09, 12
- Coverage item(s): `COV-001`, `COV-008`, `COV-015`, `COV-018`
- Severity: low
- Production blocker: no
- Reach: repeated
- Confidence: high
- Reviewer: `phase2_architecture`
- Independent verifier: `phase2_verifier`
- Specialist validation: none
- Baseline commit: `d77a8320e6751a2deb2daf14cf1ac5d6b00cb989`
- Observed on: 2026-08-21

### Condition

The baseline contains widespread SDK formatter disagreement and dense generated-style physical lines, while the backend project, contributor instructions, and CI do not establish an automated C# formatting or analyzer gate.

### Evidence

- The exact formatter and long-line checks above are reproducible.
- No `.editorconfig`, `Directory.Build.props`, C# analyzer package, warnings-as-errors setting, CI format step, or contributor format command was located.
- `AvenChart.Api.csproj` enables nullable references but no formatting or analyzer enforcement.
- `.github/workflows/verify.yml:21-24` restores and builds without a format check.
- `CONTRIBUTING.md` asks for builds but not formatter or analyzer verification.

### Consequence

Dense lines impede scanning, breakpoint placement, focused diffs, and review of compound control flow. This is principally low-cost hygiene, but it amplifies the navigation cost of the larger structural conditions.

### Cause and reach

The pattern is repeated across 39 files and is concentrated in `Program.cs`, administration, and inventory code. The absence of an automated gate permits it to persist.

### Risk calibration

- Impact: review speed and comprehension
- Likelihood or preconditions: encountered when reviewing an affected file
- Detectability: very high
- Reversibility: high
- Severity rationale: widespread but primarily stylistic; no behavioral failure was demonstrated

### Uncertainty and counterevidence

Line length is an imperfect proxy and many backend files are conventionally formatted. The formatter checks SDK defaults, not an adopted repository style.

### Validation record

- Independent method: independent SDK formatter run, exact diagnostic aggregation, line-length inventory, and repository-policy search
- Result: corroborated at low severity and repeated reach; coordinator repeated the formatter result
- Reviewer agreement or dispute: agreement after correcting the first pass's exit-code report from `1` to repeated exit `2`
- Specialist conclusion or outstanding need: none

### Disposition

Assigned canonical ID `P2-02-F002` after coordinator deduplication. No implementation recommendation has yet been accepted.

## Independent verification

The verifier selected three paths that differed from the specialist's patient-registration trace:

1. a simple patient-portal message-read mutation;
2. encounter-document movement as a cross-repository workflow; and
3. integration inbox receipt, with the outbox lifecycle as counterevidence.

The encounter-document route at `Program.cs:2702-2761` independently reproduced the API hotspot. The handler loads two encounter projections, checks membership, enforces same-patient and locking-signature rules, resolves identity, invokes `DocumentRepository`, reloads both projections, and translates failures. This is more than request binding, but the shared policy filters and extracted endpoint modules materially limit severity.

`DocumentRepository.UpdateMetadataAsync` at `DocumentRepository.cs:2469-2682` independently reproduced the repository condition: validation, transaction ownership, patient and encounter checks, mapping, mutation, audit-event construction, and response assembly are owned together. By contrast, the 450-line `IntegrationRepository` is a meaningful counterexample with cohesive inbox/outbox responsibility, validation, idempotency, leases, transactions, and a transport abstraction. The finding is therefore repeated, not universal.

The verifier's formatter result was:

```text
exit code: 2
WHITESPACE diagnostic lines: 5,284
unique affected C# files: 20
```

The coordinator repeated the same command from `avenchart/` and independently obtained the same three results. The separate physical-line inventory also reproduced 208 API C# files, 1,039 lines over 200 characters, 276 over 500, and 39 files containing at least one line over 200 characters.

Final challenge dispositions:

| Challenge | Final disposition | Canonical findings |
| --- | --- | --- |
| `EXT-S001-C01` | `corroborated` | `P2-01-F001`, `P2-02-F001` |
| `EXT-S001-C02` | `partially corroborated` | `P2-01-F001` |
| Structural `EXT-S001-C04` | `corroborated` | `P2-02-F002`, with the broader structural effect cross-linked to the first two findings |

No finding is a production blocker. `P2-01-F001` and `P2-02-F001` are medium severity; `P2-02-F002` is low severity.

### Separate evidence leads

The verifier identified three conditions that were outside this packet's conclusions and remain `needs more evidence`:

- Patient-portal message mutation and its audit insert appear to be separate autocommit operations at `PatientPortalRepository.cs:2021-2054`. A data and security/privacy review with synthetic fault injection is required before canonicalization.
- Encounter locking-signature checks occur in the endpoint before `DocumentRepository` starts its transaction at `Program.cs:2735-2748`. A synthetic concurrency experiment plus data and clinical review is required before any safety or correctness conclusion.
- Invalid or missing patient-portal session headers return HTTP 200 failure DTOs at `Program.cs:831-842`, while staff authorization returns 401 through the central filter. API and security reviewers must determine whether this is an intentional contract or an inconsistent failure boundary.

These leads do not increase the severity of the validated maintainability findings and are not silently converted into findings.

## Specialist validation required

The three validated structural and hygiene findings do not require external specialist validation. The separate audit-atomicity lead requires data and security/privacy review; the locking interleaving requires data and clinical review; and the portal-session response contract requires API and security/privacy review. Each remains `needs more evidence`.

## Coverage and scorecard impact

- `COV-001` and `COV-010` have independently validated evidence for the Packet 1 architecture question, but neither row is complete across all assigned domains.
- `COV-008` has validated structural-boundary evidence only; EF/SQL fitness remains unassessed.
- `COV-015` and `COV-018` have limited hygiene and documentation evidence; broader developer-workflow and documentation assessment remains incomplete.
- Domains 01 and 02 have evidence consistent with a provisional `2 — Partial` ceiling for these slices, but this packet is insufficient to assign final domain ratings.
- Domains 04, 07, 09, and 12 receive supporting evidence only.

## Unknowns and counterevidence

- No measured onboarding time, review time, merge-conflict rate, or defect correlation is available.
- No runtime or OpenAPI comparison established whether local error mappings produce materially incompatible public schemas.
- The semantic behavior of all 606 endpoints and 54 repositories was not reviewed.
- EF/SQL correctness and fitness remain reserved for Packet 2.
- Access-audit response timing and resource correlation require security/privacy and operations review.
- Baseline documentation is strong for startup, operations, and persistence policy but lacks an API ownership map or representative change-path guide.

## Recommended next evidence

1. Run synthetic fault injection for patient-portal message mutation and audit atomicity.
2. Exercise a synthetic concurrency interleaving around encounter signing and document movement.
3. Classify a broader endpoint sample to measure how much of the 606-route surface contains orchestration or business rules.
4. Run a timed maintainer-navigation exercise across additional capabilities using only baseline documentation.
5. Compare representative generated OpenAPI responses with runtime `400`, `401`, `403`, `404`, `409`, `429`, `500`, and `503` behavior.
6. Continue Packet 2 independently; do not infer EF Core or SQL fitness from repository size or this packet.
