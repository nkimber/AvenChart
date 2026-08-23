# Phase 2 recommendation template

Recommendations are created after findings are validated and synthesized. A technology name or architectural pattern is not a recommendation by itself.

```markdown
# P2-R### — <outcome-oriented target state>

- Status: proposed | accepted | deferred | rejected | combined | superseded
- Linked findings: <P2-...>
- Priority band: blocker | early risk reduction | foundation | resilience/performance | maintainability | developer experience
- Size: S | M | L | XL
- Difficulty: low | medium | high | exceptional
- Confidence: low | medium | high
- Proposed owner: <Phase 3 owner or role>
- Decision owner: AvenChart program owner
- Specialist approval needed: <type or none>

## Problem and evidence

Summarize the validated conditions and root causes. Link the complete finding records rather than duplicating them.

## Target state

Describe the intended behavior and boundaries, what remains unchanged, and which implementation details are intentionally left to Phase 3 design.

## Expected value

State measurable patient, user, safety, correctness, privacy, security, operational, accessibility, performance, maintainability, delivery, or cost benefits.

## Scope and affected contracts

Identify components, APIs, data, migrations, deployment, operations, tests, documentation, and externally visible behavior that could change.

## Options considered

| Option | Benefits | Costs and risks | Reason selected or rejected |
| --- | --- | --- | --- |
| Do nothing / accept risk | | | |
| Focused change | | | |
| Broader alternative | | | |

## Dependencies and sequence

List prerequisites, decisions, findings addressed first, work unlocked, conflicts, and the safest ordering.

## Delivery risk and rollback

Describe regression surface, data and clinical risk, rollout stages, observability, reversibility, rollback, and recovery needs.

## Size and difficulty rationale

Separate breadth from technical uncertainty, coordination, migration, specialist involvement, and validation cost.

## Acceptance criteria

- Observable outcome
- Required automated and manual tests
- Required measurements or operational exercises
- Required specialist validation
- Evidence that distinguishes complete from partial implementation

## Phase 3 change packets

Describe dependency-safe increments. Do not start them during Phase 2.

## Decision record

- Decision: accepted | deferred | rejected | combined
- Decided by:
- Date:
- Rationale and conditions:
```

## Acceptance checklist

- [ ] At least one validated finding or a separately justified opportunity is linked.
- [ ] Target state is outcome-oriented and proportionate.
- [ ] Do-nothing and viable alternatives are evaluated.
- [ ] Current-stack replacement, if proposed, meets the rebuttal test in the quality standard.
- [ ] Dependencies, affected contracts, migration, and rollback are explicit.
- [ ] Priority, size, difficulty, and confidence are separate.
- [ ] Acceptance criteria are measurable.
- [ ] Required specialist and program-owner decisions are identified.
