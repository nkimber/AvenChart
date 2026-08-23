# Phase 2 recommendation register

These are proposed target states, not implementation authorization. They are ordered around risk reduction and dependency direction. `P2-D016` approves their shared target-policy defaults, but each proposal remains unaccepted as an implementation packet until the program owner and required specialists approve its acceptance evidence, owner, sequencing, and rollback. Each record now includes scope, delivery risk/rollback, size rationale, bounded Phase 3 change packets, and a decision record; the [specialist validation plan](../specialist-validation-plan.md) and [Phase 3 roadmap](../phase-3-roadmap.md) apply to all seven.

| Recommendation | Target | Priority | Size | Difficulty | Status |
| --- | --- | --- | --- | --- | --- |
| [P2-R001](p2-r001-identity-resource-safety.md) | Establish approved identity, resource scope, session, audit, and minimum-necessary boundaries | Blocker | XL | Exceptional | Proposed |
| [P2-R002](p2-r002-clinical-record-integrity.md) | Make patient/encounter/result identity, lifecycle, version, attestation, and correction boundaries coherent | Blocker | XL | Exceptional | Proposed |
| [P2-R003](p2-r003-workflow-history-recovery.md) | Make scheduling, communication, recall, therapy, billing, and follow-up workflows durable and recoverable | Blocker | XL | High | Proposed |
| [P2-R004](p2-r004-data-schema-persistence-recovery.md) | Establish one bootstrappable schema authority and measured persistence/recovery behavior | Foundation | L | High | Proposed |
| [P2-R005](p2-r005-ui-response-recovery-accessibility.md) | Establish selection-owned UI state, safe failure recovery, and manual accessibility evidence | Early risk reduction | L | High | Proposed |
| [P2-R006](p2-r006-contracts-integration-report-governance.md) | Make API, FHIR, integration, report, artifact, and configuration contracts explicit and governed | Foundation | XL | High | Proposed |
| [P2-R007](p2-r007-verification-release-operations.md) | Build a risk-shaped verification, release provenance, and operational-evidence gate | Foundation | L | Medium | Proposed |

No proposal is accepted. Every implementation gate remains open and Phase 3 authorization remains pending until the acceptance checklist in each record is satisfied and the program owner explicitly closes the applicable gates and records the sequence decision.
