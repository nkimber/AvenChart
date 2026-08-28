# Decision 0002: Proposed scoped verification authorization

Status: Approved — active for the exact scoped verification change  
Proposed date: 2026-08-26  
Approval date: 2026-08-26  
Decision owner: AvenChart program owner  
Implementation owner: Codex delivery agent under AvenChart program-owner direction  
Risk owner: AvenChart program owner  
Review/expiry: 2026-09-30, or immediately when superseded by a Phase 2 gate decision

## 1. Decision requested

Authorize the smallest non-runtime Phase 3 verification packet needed before telehealth feature implementation:

1. accept `P2-R007-A — Verification manifest and baseline` only for the scoped files below;
2. authorize creation of a deterministic telehealth planning-artifact validator;
3. authorize adding one mandatory invocation of that validator to the existing pull-request verification workflow; and
4. keep every other Phase 2 implementation gate, recommendation, finding and production blocker open.

This is not an authorization to implement telehealth application behavior. It does not authorize patient, clinician, administrator, database, migration, API, frontend, integration, deployment, runtime, or production configuration changes.

## 2. Exact authorized file scope

The authorization, if approved, is limited to:

```text
scripts/Test-TelehealthPlanningArtifacts.ps1
.github/workflows/verify.yml
docs/telehealth/**
```

The `verify.yml` change is limited to invoking the validator. It may not add secrets, external destinations, deployment authority, production data, live integrations, application builds beyond the existing workflow, or a mechanism that enables telehealth.

Any other path or behavior remains unauthorized by this decision.

## 3. Findings and recommendation affected

| Item | Relationship to this decision |
|---|---|
| `P2-09-F001` | The validator supplies a versioned denominator and evidence ledger for the telehealth requirement set instead of an unsupported completeness assertion. |
| `P2-09-F002` | The validator adds a risk-shaped, always-on planning gate before feature code, while explicitly acknowledging that structural checks are not independent clinical/security/accessibility verification. |
| `P2-R007-A` | This decision accepts only the verification-manifest/baseline packet for the exact authorized scope. `P2-R007-B` through `P2-R007-E` remain proposed. |
| Residual `COV-014`, `COV-015`, `COV-017`, `COV-018` evidence gaps | Unchanged. The validator does not prove runtime, deployment, release, accessibility, security, or operational readiness. |

No finding is closed by this decision. No residual risk associated with the other 37 High findings is accepted.

## 4. Target use

The permitted target is repository planning verification for synthetic, pre-implementation telehealth artifacts. The result may be described only as “planning artifacts structurally consistent.” It must not be described as clinically safe, legally compliant, secure, accessible, interoperable, implementation-ready, production-ready, or approved for patient care.

## 5. Residual risk accepted for this scope

The program owner accepts that:

- a validator authored in the same delivery stream can contain correlated blind spots;
- syntactic requirement coverage does not prove correct requirements, adequate acceptance evidence, or faithful implementation;
- Markdown/link/JSON validation cannot substitute for specialist review or runtime testing; and
- a green workflow could be misrepresented unless its bounded claim is visible in logs and documentation.

These risks are accepted only to permit the planning validator and its CI invocation. They do not waive later independent review or testing.

## 6. Compensating controls

The active implementation must preserve all of these controls:

1. The validator reads repository files only and makes no network calls.
2. It processes no PHI, secrets, vendor credentials, live identifiers, or production endpoints.
3. It validates exactly 329 normative requirement definitions, exact-once primary backlog coverage, unique identifiers, permitted story values, dependency references, relative links, safeguard identifiers, and static wireframe integrity.
4. Its output identifies itself as structural planning evidence, not a readiness or conformance result.
5. Existing build, test, migration, runtime, accessibility and supply-chain gates remain unchanged and mandatory.
6. A failing validator blocks merge; bypass requires a separately recorded program-owner decision.
7. The script receives focused review against the requirements and machine-readable backlog before activation.
8. The authorized paths are reviewed after the change to prove no broader application or runtime impact.

## 7. Stop conditions and rollback

Stop and revoke this authorization if the change:

- reaches an application, test project, database, migration, deployment or runtime path;
- accesses a network, secret, live service, production configuration or real patient data;
- weakens an existing workflow gate;
- claims clinical, legal, security, accessibility or production readiness; or
- cannot deterministically reproduce the documented 329-requirement coverage result.

Rollback is removal of the validator invocation and script while retaining the failed evidence and this decision record. Rollback does not authorize merging planning artifacts that no longer pass an equivalent reviewed check.

## 8. Evidence required to close this scoped packet

- validator source and focused review;
- local passing and intentionally failing fixtures or controlled mutation checks;
- CI run showing the validator is mandatory;
- proof that only the authorized paths changed;
- updated [planning validation report](../backlog/validation-report.md); and
- program-owner review at or before the expiry date.

Closure of this scoped packet does not close the Phase 2 exit gate.

## 9. Approval record

The AvenChart program owner approved every current decision on 2026-08-26 with the instruction:

> I approve all of your current decisions.

Because this document was the only current proposed decision and already bounded approval to its exact scope, the instruction activates Sections 1–8 without broadening them. The stated residual risk is accepted through 2026-09-30. No other Phase 2 gate, recommendation, packet or finding is closed. Any broader or materially different work requires a revised decision record before implementation.

## 10. Activation evidence

Activation on 2026-08-26 produced the following local evidence:

| Evidence | Result |
|---|---|
| Validator positive run | 44 checks passed; zero failures |
| Missing-coverage mutation | Rejected with exit code 1 |
| Broken wireframe-label mutation | Rejected with exit code 1 |
| Expired-decision mutation | Rejected with exit code 1 |
| Workflow contract | Unconditional `telehealth-planning` job invokes the validator |
| Network/live data | None used |
| Active safeguard manifest | Only `TH-SG-001` active |
| Application/database/test/deployment/runtime paths | Unchanged by the Decision 0002 implementation |

The [planning validation report](../backlog/validation-report.md) records the detailed bounded claim. Hosted GitHub Actions evidence remains pending until the change is pushed through normal repository review. The scoped packet therefore remains active, not closed, and expires on the recorded date unless reviewed or superseded.

## References

- [Phase 2 exit gate](../../phase-2/phase-2-exit-gate.md)
- [Phase 3 roadmap](../../phase-2/phase-3-roadmap.md)
- [P2-R007](../../phase-2/recommendations/p2-r007-verification-release-operations.md)
- [P2-09-F001](../../phase-2/findings/p2-09-f001-functional-coverage-provenance.md)
- [P2-09-F002](../../phase-2/findings/p2-09-f002-default-verification-gate.md)
- [Decision 0001](0001-g0-development-baseline.md)
- [Engineering safeguards](../backlog/engineering-safeguards.md)
