# Decision 0004: bootstrap-schema reconciliation

Status: Approved — active for deterministic bootstrap regeneration and review  
Proposed date: 2026-08-26  
Approved date: 2026-08-26  
Decision owner: AvenChart program owner  
Implementation owner: Codex delivery agent under program-owner direction  
Review owner: Independent data reviewer

## Decision requested

Amend Decision 0003 solely to permit deterministic regeneration and review of:

```text
avenchart/database/bootstrap/base-schema.sql
```

The repository-wide migration-recovery suite stops before its scenarios because:

```text
node scripts/generate-postgres-seed.mjs --verify-bootstrap
```

reports that this committed generated file is stale. Decision 0003 requires the full empty, populated, interruption and recovery rehearsal but does not list the generated bootstrap file, so implementation stopped without changing it.

A read-only generator interception compared the complete committed and generated contents without writing the target. All 1,490 logical lines are identical after newline normalization. The committed file contains 1,489 CRLF sequences; the generator emits LF only. The normalized/generated SHA-256 is `6a1a6ca3de61608654921edb843d48a4b07dcc8899d3e6ca4056cf8b838745a2`; the current raw CRLF file hash is `f55ad7cf27d741987333301097dd1adc5837942576445bfb33ebd59529b2b3ac`. Therefore the observed drift is formatting-only, but changing the generated authority still requires the explicit path amendment.

## Authorized procedure

1. Run `node scripts/generate-postgres-seed.mjs --bootstrap-schema` once.
2. Review the complete generated diff; do not hand-edit the generated schema.
3. Confirm the resulting diff is a CRLF-to-LF normalization with no logical-line change; otherwise stop and require a broader data-owner decision.
4. Run `node scripts/generate-postgres-seed.mjs --verify-bootstrap`.
5. Run the full `Test-TelehealthMigrationResilience.ps1` suite against an isolated database.
6. Record independent data review and update the Sprint evidence packet.

No generator, runtime, migration, seed-data or application-code change is authorized by this decision. It does not authorize editing `V0282`, deleting durable data, deploying telehealth or enabling patient care.

## Risk and rollback

The generated bootstrap is a broad empty-database authority and may expose unrelated drift. Review is therefore whole-file and data-owner governed. Before commit, the original file remains recoverable from version control. After release, correction uses a separately reviewed forward change; migration history is never rewritten.

## Approval instruction

To activate this amendment, the program owner must explicitly state:

> Approve Decision 0004 exactly as written. Authorize only deterministic regeneration and review of `avenchart/database/bootstrap/base-schema.sql`, with a stop for unexplained non-telehealth drift.

## Approval record

On 2026-08-26, after being told that the generated bootstrap file was the remaining blocker, the AvenChart program owner explicitly stated: “I give you permission to modify the generated bootstrap file. I am about to go to bed and I will not be able to intervene or give human permissions for about 10 hours. I want you to be able to operate during this time and I give you authorization and permission to make whatever changes you need to be able to run this goal as a long running job, uninterrupted.” Together with the program owner's prior approval of all current decisions, this activates Decision 0004 exactly as written. It does not authorize production enablement, live patient care, destructive data changes, unexplained schema drift, or any weakening of the stated stop conditions.

## References

- [Decision 0003](0003-proposed-sprint-01-synthetic-foundation.md)
- [Sprint 1 evidence](../backlog/sprint-01-evidence.md)
- [Sprint 1 plan](../backlog/sprint-01-foundation.md)
