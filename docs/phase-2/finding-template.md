# Phase 2 finding template

Copy the record below for each candidate finding. The coordinator assigns the canonical ID after checking for duplicates and common causes.

```markdown
# P2-<DOMAIN>-F### — <precise condition, not a proposed fix>

- Status: observed | reproduced | analyzed | validated | disputed | accepted-risk | superseded | closed
- Domain(s): <01–12>
- Coverage item(s): <COV-###>
- Severity: blocker | high | medium | low | opportunity
- Production blocker: yes | no | unknown
- Reach: isolated | repeated | cross-cutting | systemic
- Confidence: low | medium | high
- Reviewer: <agent or person>
- Independent verifier: <required for blocker/high/systemic>
- Specialist validation: none | clinical | security/privacy | legal/compliance | accessibility | certification/interoperability | database/operations
- Baseline commit: <full commit>
- Observed on: YYYY-MM-DD

## Condition

What behavior, structure, omission, or repeated pattern exists? Separate observation from inference.

## Evidence

- Stable file, symbol, migration, test, trace, request, screenshot, query plan, or authoritative requirement
- Exact reproduction or inspection steps
- Actual result
- Expected or required result and its source

## Consequence

Who or what is affected? Explain plausible clinical, privacy, security, correctness, operational, accessibility, delivery, or cost consequences without overstating what has not been validated.

## Cause and reach

Describe likely cause, affected components, frequency, blast radius, detectability, compensating controls, and whether this is a symptom of a broader condition.

## Risk calibration

- Impact:
- Likelihood or preconditions:
- Detectability:
- Reversibility:
- Severity rationale:

## Uncertainty and counterevidence

Record untested assumptions, contradictory evidence, environmental limits, and what would falsify or materially change the conclusion.

## Validation record

- Independent method:
- Result:
- Reviewer agreement or dispute:
- Specialist conclusion or outstanding need:

## Disposition

Link recommendations, accepted-risk decisions, duplicates, superseding findings, or closure evidence. Do not place an unapproved implementation plan here.
```

## Admissibility checklist

- [ ] The title describes a condition rather than a favored solution.
- [ ] Evidence is stable and reproducible.
- [ ] Fact and inference are visibly separate.
- [ ] Consequence and reach are proportionate to the evidence.
- [ ] Severity, confidence, and effort have not been conflated.
- [ ] Counterevidence and uncertainty are recorded.
- [ ] Required independent and specialist validation is identified.
- [ ] The finding is not a duplicate symptom of an existing root cause.
