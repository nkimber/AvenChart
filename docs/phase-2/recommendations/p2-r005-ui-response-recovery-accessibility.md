# P2-R005 — Establish selection-owned UI state, safe failure recovery, and accessibility evidence

- **Status:** Proposed — target policy approved by `P2-D016`; implementation is not authorized
- **Linked findings:** `P2-03-F007`, `P2-08-F003` through `P2-08-F006`, `P2-09-F002`
- **Priority band:** Early risk reduction
- **Size:** L
- **Difficulty:** High
- **Confidence:** High static; browser/AT impact pending
- **Proposed owner:** Frontend platform and accessibility lead
- **Decision owner:** AvenChart program owner
- **Specialist approval needed:** Accessibility, clinical workflow, frontend runtime

## Problem and evidence

Adopt a shared async-state contract that binds responses to route/selection, distinguishes stale/loading/failed/current data, prevents failed views from retaining unsafe actions, and consistently announces errors with retry/focus recovery across clinician and portal surfaces.

The linked UI findings establish response inversion and stale actionable state across patient, schedule, Flow Board, portal, therapy, billing, and inventory paths, plus error views without consistent assistive-technology recovery. Strong dashboard/shell patterns prove that the defect is route-specific rather than a reason to replace React or the supported modern UI.

## Target state

Every supported modern UI route owns the request/result that feeds its actions; obsolete and failed data cannot be acted upon, mutations give safe conflict/retry outcomes, and recovery is perceivable and operable by keyboard and assistive technology.

## Expected value

Reduce wrong-resource actions, stale workflow transitions, silent failure, and inaccessible recovery while retaining the existing strong shell/dashboard/appointment patterns.

## Options considered

Prefer a small shared state/transport pattern over a UI rewrite. First add deferred-response and forced-failure tests, then migrate highest-risk selection/action pages, then remaining modern clinician/portal surfaces. Roll back per route behind feature flags or adapters while preserving API contracts.

## Dependencies and sequence

Start `R005-A` alongside the `R007-A` test manifest. Apply identity/session and API error conventions from `R001` and concurrency rules from `R002`/`R003` before migrating their high-risk pages. Complete high-risk selection/action routes before lower-risk screens, then obtain independent accessibility evidence in `R005-D`. The reference UI is excluded despite the historical wording above; only the modern clinician and portal UI are in scope.

## Acceptance criteria

Controlled browser tests prove obsolete responses cannot overwrite current selections; failed refreshes disable or clearly invalidate stale actions; every representative error is keyboard- and screen-reader-perceivable with retry; WCAG 2.2 AA manual evidence, visual regression, zoom/reflow, and clinical workflow sign-off are recorded.

## Scope and affected contracts

- The supported modern clinician and portal UI only: shared request transport, route/selection state, page-level loaders, mutation feedback, retry, focus, and accessibility components.
- High-risk patient, schedule, Flow Board, portal-message, therapy, billing, inventory, and governed-report pages, plus their API error/concurrency contracts.
- Playwright/browser tests, deferred-response fixtures, visual regression, keyboard/screen-reader/zoom evidence, and UI error telemetry.
- The reference UI is explicitly excluded; no parity or remediation work is authorized for it.

## Delivery risk and rollback

UI-state changes can discard unsaved work, hide a valid result, or introduce route-specific regressions. Migrate per route behind small adapters or flags; retain form drafts only with clear ownership and PHI-safe lifecycle; record correlation IDs for failures; and return an affected route to the proven prior loader if a release regression appears. Do not leave stale actions enabled as a fallback.

## Size and difficulty rationale

This is Large because the same response-ownership and recovery failures recur across many pages and APIs. Difficulty is High because correctness must be proven in a real browser, under latency/failure interleavings, and with assistive technology and clinical workflow review—not merely by unit tests.

## Phase 3 change packets

1. **R005-A — UI fault and race test foundation:** deferred responses, controlled network failures, route changes, mutation failure fixtures, and freshness rules for browser suites.
2. **R005-B — Selection-owned high-risk UI:** patient shell, schedule/Flow Board, portal messaging, therapy, inventory, and billing response ownership with stale-action invalidation.
3. **R005-C — Mutation and recovery contract:** optimistic-concurrency UX, retry/cancel/draft behavior, actionable failure states, and API problem-detail alignment.
4. **R005-D — Accessibility validation and closure:** semantic error announcements, focus recovery, keyboard/zoom/reflow/contrast evidence, and independent WCAG 2.2 AA specialist review.

## Decision record

- **Decision:** Pending acceptance as a Phase 3 recommendation.
- **Decided by:** AvenChart program owner.
- **Date:** Not set.
- **Rationale and conditions:** `P2-D016` approves the modern-UI target policy. Acceptance requires a frontend/accessibility delivery owner, an independent accessibility reviewer, per-route rollback approach, clinical workflow coverage, and the acceptance evidence above.
