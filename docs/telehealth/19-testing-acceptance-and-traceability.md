# Testing, acceptance, and traceability

## 1. Definition of done

A requirement is done only when:

1. its behavior and failure behavior are implemented behind the intended boundary;
2. automated tests exist at the lowest useful layer and pass in CI;
3. required cross-component, manual, clinical, legal, security, accessibility, performance, or recovery evidence is attached;
4. OpenAPI/event/schema/content/config documentation is current;
5. telemetry, runbook, migration/rollback and support ownership exist where operational;
6. the traceability record links requirement -> design/decision -> change/PR -> tests/evidence -> approval; and
7. no unresolved Blocker/High safety, privacy, security, cross-tenant, data-loss, prescription, claim, or accessibility defect remains.

Passing happy-path UI tests alone is insufficient.

## 2. Test layers

| Layer | Required coverage |
|---|---|
| Pure unit/property | Protocol evaluator, readiness predicates, state transitions, invalidation/freshness, matching eligibility/order, status/content mapping, estimate and claim scrub rules |
| Database integration | Constraints, tenant relationships, transactions/rollback, optimistic conflict, queue races, leases, outbox/inbox, immutable versions, retention/holds, migration/restore |
| API/authorization | OpenAPI, validation/Problem Details, identity/practice/resource context, every role/action/state, IDOR, idempotency, stale versions, pagination/rate/body limits |
| Adapter contract | Canonical-to-standard mapping, transport/business distinction, signing/auth, timeout/retry, duplicates/reordering, malformed responses, quarantine and reconciliation |
| Component/UI | Form semantics, state/provenance, errors/retry, focus/live regions, responsive behavior, double submit, session expiry, offline/slow network |
| End-to-end | Full established/new patient, admin, clinical review, physician/video, prescription, claim and after-visit journeys with synthetic data |
| Clinical validation | Medical-director golden cases per protocol/rule/outcome; red-team under-triage; usability/content review; replay after engine/content changes |
| State/legal/billing | GA/CA/FL location/license/consent/prescribing/corporate-practice/website/record cases; payer/product/POS/modifier/acknowledgment cases |
| Security/privacy | Threat/abuse testing, authenticated penetration, cross-tenant/IDOR, proofing/link/recovery, session/CSRF/replay, upload/webhook/egress, PHI leak and audit completeness |
| Accessibility/usability | WCAG 2.2 AA automated/manual, keyboard, screen readers, zoom/reflow, contrast, device/video, cognitive/health literacy, disability-inclusive studies |
| Performance/resilience | Load/soak, noisy neighbor, process/database/provider loss, delayed/duplicate/out-of-order messages, queue/lease recovery, backup/restore, deployment rollback |

## 3. Required clinical golden-case pack

Each published protocol version supplies machine-readable fixtures for:

- every rule true/false path and outcome;
- every numeric/date/unit boundary and just-inside/outside value;
- every missing/unknown/not-sure value;
- contradictory answers and changed answers;
- universal emergency signs within every complaint pathway;
- relevant high-risk context (pregnancy/postpartum, immune compromise, active cancer treatment, recent surgery/hospitalization, comorbidity/medication risks);
- adequate versus inadequate video/exam/measurement;
- location/state change and elapsed-time expiry;
- established versus new-patient distinctions;
- clinical-review clarification and every permitted reviewer decision; and
- worsening symptoms while waiting and at consultation start.

The expected output includes outcome, reason codes, fired-rule order, required content/action, freshness, and review needs. Replay must be bit-for-bit stable for the same engine/protocol/answers.

## 4. End-to-end acceptance scenarios

| Scenario ID | Scenario and pass condition |
|---|---|
| E2E-01 | Established Georgia patient with approved known-migraine pattern confirms current GA home location, passes eligibility/network, queues, sees approximate status, consults with GA-authorized physician, receives no controlled drug, AVS and POS 10 claim draft. |
| E2E-02 | New California applicant safely proves identity, ambiguous duplicate goes to HIM without PHI disclosure, links atomically, accepts documented CA consent, is treated by CA-licensed physician under practice-controlled workflow. |
| E2E-03 | New Florida applicant uses a registered out-of-state physician only when all registration restrictions/evidence and website disclosures are valid; pharmacy results remain neutral and patient-selected. |
| E2E-04 | Chest/neurologic/severe-breathing red flag stops before insurance, gives direct emergency guidance and records delivery; admin cannot queue it. |
| E2E-05 | Unknown/concerning headache answer routes to clinical review; only qualified reviewer can create a new eligible/in-person/emergency outcome with rationale. |
| E2E-06 | Insurance active but exact network unknown shows pending/manual/self-pay options and never says in-network or guaranteed; estimate version is acknowledged. |
| E2E-07 | Two physicians concurrently reserve next patient; exactly one obtains the request, ordering stays correct, expired lease safely requeues once. |
| E2E-08 | Patient changes from GA to FL in queue; licensure, consent, protocol, network, estimate and matching invalidate/re-evaluate before assignment. |
| E2E-09 | Symptoms worsen in queue; emergency rescreen takes precedence over position and terminally redirects the current request. |
| E2E-10 | SignalR drops/out-of-order event arrives; polling/version reconciliation shows correct state with no duplicate transition. |
| E2E-11 | Video disconnects after consultation starts; reconnect succeeds or physician completes a documented technical-abort/safety disposition; no duplicate encounter. |
| E2E-12 | Physician attempts controlled or unknown-classification medication; all layers block signing/dispatch and create a safety audit/alert. |
| E2E-13 | Patient selects a different pharmacy; signed prescription snapshots destination, retry is idempotent, transport acknowledgment is not displayed as dispensed. |
| E2E-14 | Signed encounter produces claim draft; actual patient home/other location selects POS 10/02; human approves; stub 999/277CA/835 statuses remain semantically distinct. |
| E2E-15 | Cross-practice/patient/request/SignalR/token ID tampering is denied, produces no existence leak, and creates safe audit/alert evidence. |
| E2E-16 | Video/eligibility/eRx/claims provider outage activates the defined degraded mode with preserved work, honest patient status, owned recovery and reconciliation. |
| E2E-17 | Browser refresh, multi-tab stale submit, session expiry and double-click cannot duplicate patient, request, reservation, prescription, or claim. |
| E2E-18 | Full patient journey is completed by keyboard/screen reader at 400% zoom and slow network; emergency, errors, queue updates, device setup and AVS remain usable. |
| E2E-19 | Backup restore recovers request/encounter/events/outbox/object references within RPO/RTO and reprocessing creates no duplicate external transaction. |
| E2E-20 | Kill switch stops new intake/joins at configured scope while active consultations receive safe completion/continuity and all clients receive accurate status. |

## 5. Traceability matrix

| Requirement family | Primary specifications | Mandatory test/evidence suites |
|---|---|---|
| `TEL-PROD`, `TEL-ACT` | 01, 02 | E2E journeys, role/permission matrix, product acceptance |
| `TEL-WF` | 03 | State transition, time-travel, transaction, idempotency, concurrency, realtime reconciliation |
| `TEL-IDN` | 04 | Proofing/link/duplicate/recovery/IDOR, atomic promotion, privacy/bias/redress |
| `TEL-TRI` | 05 | Medical-director golden cases, deterministic replay, red-team under-triage, content/accessibility |
| `TEL-REG` | 06 | GA/CA/FL legal scenario packs, license/consent/location/config history, owner approvals |
| `TEL-PRA` | 07 | Domain/origin/branding isolation, configuration publication, readiness, queue/matcher/fairness/kill switch |
| `TEL-INS` | 08 | X12-like adapter contracts, evidence/freshness, exact-network matrix, estimate/self-pay/no-guarantee |
| `TEL-CON` | 09 | Start/completeness/disposition/sign/amend, AVS/follow-up/order, interruption/autosave |
| `TEL-VID` | 10 | Provider contract/security, waiting-room isolation, tokens/webhooks, device/accessibility, outage/reconnect |
| `TEL-RX` | 11 | Medication safety/classification, pharmacy choice, SCRIPT mapping, sign/change/cancel/retry/status |
| `TEL-CLM` | 12 | Claim scrub/POS/rules/approval, X12 fixtures, ack/remittance semantics, correction/reconciliation |
| `TEL-ARC`, `TEL-DAT`, `TEL-API` | 13–15 | Architecture/dependency, database/migration, API/OpenAPI/event/FHIR/SMART, worker/adapter chaos |
| `TEL-SEC` | 16 | Risk/threat/control evidence, authenticated penetration, PHI leak, audit/incident/DR |
| `TEL-UX` | 17 | WCAG 2.2 AA, usability, responsive/browser, exact state content, documents/notifications |
| `TEL-NFR` | 18 | SLO/load/soak/chaos/noisy-neighbor/telemetry/runbook/restore/deployment evidence |
| `TEL-TST`, `TEL-ROL` | 19, 20 | Traceability completeness, independent review, go/no-go gates, pilot monitoring/rollback |

The implementation traceability register is a machine-readable artifact generated/maintained with columns: requirement ID, normative text checksum, owner, implementation issue/ADR, code/config/content location, automated test IDs, manual evidence URI/checksum, state, approver, release, and exception/expiry. CI fails on duplicate/missing IDs, removed normative text without supersession, or `MUST` requirements marked release-ready without evidence.

## 6. Test data and environments

- Use synthetic patient/person/provider/payer/pharmacy/claim data only outside production. Include diverse names, addresses, languages, disabilities, sex/gender/pregnancy contexts where clinically relevant, coverage relationships, and ambiguous duplicates.
- Synthetic identifiers are reserved/non-routable and provider stubs reject production-like destinations. Email/SMS/fax/eRx/X12/video endpoints are allowlisted sinks.
- Time and randomness are injectable for expiry/order/lease/idempotency tests.
- Adapter fixtures are licensed/permitted and de-identified; raw production payloads never become test fixtures.
- Clinical golden cases are reviewed in a protected governed repository but contain no real patient data.

## 7. Test requirements

| ID | Requirement | Acceptance evidence |
|---|---|---|
| TEL-TST-001 | Every normative requirement MUST have an owner and linked automated/manual/approval evidence before its release gate can pass. | Traceability completeness report. |
| TEL-TST-002 | CI MUST detect duplicate/missing requirement IDs, incompatible OpenAPI/event/schema changes, migration errors, and stale protocol fixtures. | CI gate demonstration. |
| TEL-TST-003 | Every protocol version MUST pass approved golden cases and independent clinical red-team review before publication. | Signed clinical validation report. |
| TEL-TST-004 | Authorization tests MUST cover every role/action/resource/state plus cross-practice, cross-patient, list/filter, SignalR, export and callback paths. | Permission coverage report. |
| TEL-TST-005 | Critical transaction/invariant tests MUST run with real PostgreSQL and concurrent clients, not mocks alone. | Integration race report. |
| TEL-TST-006 | Adapter certification MUST include mapping, semantics, authentication, idempotency, duplicate/reorder/delay, malformed content, outage and reconciliation. | Per-adapter certification report. |
| TEL-TST-007 | State legal/payer test packs MUST be versioned with the rule sources and replayed after related configuration/code/content changes. | GA/CA/FL and payer reports. |
| TEL-TST-008 | Accessibility MUST combine automated and manual assistive-technology/usability testing across every critical journey and brand/theme. | WCAG conformance report. |
| TEL-TST-009 | Security testing MUST include authenticated multi-role attack paths, identity/linkage/recovery abuse, PHI leakage, webhook/egress/upload, audit and vendor boundaries. | Security assessment and closure. |
| TEL-TST-010 | Load/soak/chaos/restore/deployment tests MUST use production-shaped topology/volumes and verify clinical/transaction invariants, not latency alone. | Performance/resilience report. |
| TEL-TST-011 | Tests MUST prove stubs/test identity/data/destinations cannot operate in production mode. | Runtime safety report. |
| TEL-TST-012 | Patient-facing state/content snapshot tests MUST prove accurate uncertainty and transport/business semantics. | Content contract report. |
| TEL-TST-013 | Test failures and defects MUST be triaged for clinical safety, privacy/security, accessibility, financial and data-integrity impact. | Defect workflow audit. |
| TEL-TST-014 | A production-like dress rehearsal MUST complete every E2E-01 through E2E-20 scenario with signed evidence before pilot. | Dress rehearsal package. |
| TEL-TST-015 | Independent reviewers MUST verify Blocker/High/systemic/clinical-safety findings and closure evidence before go-live. | Independent verification report. |
| TEL-TST-016 | Regression selection MUST follow dependency/impact analysis; changes to shared identity, patient, encounter, audit, outbox, forms, medications or billing run both telehealth and existing regression suites. | CI selection and report. |

## 8. Release acceptance exit criteria

- 100% of in-scope `MUST` requirements have passing evidence and approvals; no expired waivers.
- 100% clinical rules have medical-director fixtures/review; no unexplained result drift.
- Zero open Blocker/High safety, cross-tenant, identity-linkage, PHI exposure, data-loss, signing, controlled-drug, duplicate-prescription/claim, or WCAG critical-journey defects.
- SLO/load/restore/continuity and external adapter/trading-partner certification gates pass.
- State legal, practice, privacy/security, billing, accessibility, operations and product owners sign the exact release/configuration versions.

