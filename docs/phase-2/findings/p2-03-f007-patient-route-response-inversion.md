# P2-03-F007 — Obsolete frontend responses can combine one resource with another selection

- Status: validated
- Domain(s): 03, 07, 08, 09
- Coverage item(s): `COV-003`, `COV-005`, `COV-007`, `COV-011`, `COV-014`
- Severity: high
- Production blocker: yes
- Reach: systemic across patient chart, portal communication/records, therapy-group, clinician schedule/day, patient timeline, billing-account, and inventory-lot selections
- Confidence: high static confidence
- Reviewers: coordinator frontend trace, `phase2_clinical_safety`, `phase2_frontend_accessibility`
- Independent verifier: separate `phase2_quality_operations` and `phase2_verifier` passes
- Specialist validation: clinician/clinical informatics for consequence; frontend runtime reproduction outstanding
- Baseline commit: `d77a8320e6751a2deb2daf14cf1ac5d6b00cb989`
- Observed on: 2026-08-21

## Condition

Several selection-scoped frontend loads are neither cancelled nor guarded by a request identity. A slower response for selection A can replace detail state after the user has selected B, combining one resource's content with another resource's label, route, or action target.

## Evidence

- `PatientShell.tsx:43-52` lets every completion write state and has no abort controller or request epoch.
- `PatientShell.tsx:64-66` combines the stored patient object with the current route `patientId`.
- `PatientSummary.tsx:375-401` initializes form state from the patient object, while `PatientSummary.tsx:573-578` saves through the route identifier.
- `PortalMessages.tsx:228-259` lets any thread response replace shared state; its title and reply target come from the current selection at `PortalMessages.tsx:390-410`, while the displayed bodies come from independently resolved thread state.
- `TherapyGroups.tsx:58-67` lets an obsolete member/session response replace detail shown beneath the current group selection.
- `BillingWorkspace.tsx:72-105` can issue overlapping patient-account loads from both selection and URL synchronization without response ownership; a payment response can also replace current account state after selection changes at `BillingWorkspace.tsx:156-185`.
- `InventoryWorkspace.tsx:113,131-160,450-515` can let an obsolete three-request lot-detail load replace content while the header and action target are derived from the newer selected lot.
- `ClinicianSchedule.tsx:50-60,62-83,116-125,167-225` reloads selected-date appointments without cancellation or request identity.
- `ClinicianCalendar.tsx` reloads month appointments without cancellation, allowing an older month response to replace the current calendar state.
- `PatientMessages.tsx:45-66,74-86,91-153` and `PatientTimeline.tsx:100-126,175-187` allow patient-route responses to complete without ownership checks; the timeline combines several independently resolved requests.
- `FlowBoard.tsx:22-32,55-75` allows overlapping date loads to write the same board state.
- `PortalDashboard.tsx:71-82` loads the recent-message preview without cancellation; `PortalMessages.tsx:156-223,340-424` lets inbox/thread state complete without request identity; and `PortalRecords.tsx:292-348` starts several independent record loads without shared cancellation or ownership.
- No controlled deferred-response test was located for these paths.

## Consequence

Displayed or form values from one patient can be submitted to another patient's identifier. A portal reply can target message B while the user is shown message A's thread; therapy details can appear under the wrong group; and billing or inventory detail can be combined with a newer patient or lot action target.

## Cause and reach

Asynchronous response ownership is not tied to the route or selection that initiated it, and the affected views trust separately sourced identity and detail state to remain coherent.

## Risk calibration

The required response ordering is timing-dependent but deterministic and repeats across different resource-selection views. Wrong-patient writes or wrong-thread replies can be difficult to detect and reverse, supporting high severity and future-production blocking status.

## Uncertainty and counterevidence

Normal navigation often unmounts or waits for the current page, reducing frequency. Loading hides some child content while a request is pending, and therapy server relationship checks may reject selected stale actions. Clinician message and referral loads demonstrate correct cancellation patterns. Controlled browser/component reproduction remains required.

## Validation record

The COV-003, COV-005, COV-007, and COV-011 specialist passes reproduced the state transitions statically and agreed that they share one response-ownership cause. Runtime deferred-promise and browser validation remain outstanding.

## Disposition

Validated source-level engineering condition and future-production blocker. No implementation recommendation is made.
