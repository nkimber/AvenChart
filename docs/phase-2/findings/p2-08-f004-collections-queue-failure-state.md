# P2-08-F004 — A failed collections-queue load is indistinguishable from an unavailable queue

- Status: validated condition
- Domain(s): 07, 08, 09, 10
- Coverage item(s): `COV-007`, `COV-014`
- Severity: medium
- Production blocker: unknown pending billing-operations reliance
- Reach: isolated queue with multi-account effect
- Confidence: high static confidence
- Reviewer: `phase2_frontend_accessibility`
- Independent verifier: not required for an isolated medium condition; browser reproduction outstanding
- Specialist validation: billing/revenue-cycle operations and accessibility review outstanding
- Baseline commit: `d77a8320e6751a2deb2daf14cf1ac5d6b00cb989`
- Observed on: 2026-08-21

## Condition

The billing page silently swallows a collections-queue request failure. Because the section renders only when response state is non-null, failure removes the queue, its empty-state explanation, and any retry control.

## Evidence

- `BillingWorkspace.tsx:44-47` initializes collections as `null`; `BillingWorkspace.tsx:58-66` catches and discards load failure.
- `BillingWorkspace.tsx:562-656` renders the entire section, including “No accounts need collections follow-up,” only when collections state exists.
- The patient-account path at `BillingWorkspace.tsx:249-268` demonstrates a stronger visible alert and Retry pattern.
- No component or browser test forcing a collections failure was located.

## Consequence

A billing user can receive no indication that an actionable work queue failed to load and may infer that no follow-up is available or required.

## Cause and reach

The queue has no explicit loading/error state and discards rejection evidence independently of the rest of the billing page.

## Risk calibration

The failure can hide multiple accounts and is not detectable in the page, but reload can recover and authoritative production reliance is unapproved. Medium severity with blocker status unknown is appropriate.

## Uncertainty and counterevidence

The queue reloads after several successful mutations, and external monitoring could expose failures. No evidence establishes that this is the sole authoritative collections worklist.

## Validation record

The frontend specialist reproduced the rendering path statically. Forced-network browser evidence and an operating-owner decision remain outstanding.

## Disposition

Validated source-level condition; future-production blocker status remains unknown. No implementation recommendation is made.
