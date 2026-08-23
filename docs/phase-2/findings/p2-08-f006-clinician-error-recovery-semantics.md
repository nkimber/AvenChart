# P2-08-F006 — Clinician async failures are not consistently announced or recoverable

- **Status:** Validated condition
- **Domains:** 08 Frontend and accessibility; 09 Quality and verification
- **Coverage:** `COV-011`, `COV-014`
- **Severity:** Medium
- **Production blocker:** Unknown pending assistive-technology and workflow validation
- **Reach:** Repeated across representative clinician and portal pages
- **Confidence:** High static for markup; user impact requires browser and assistive-technology evidence
- **Condition:** Several asynchronous failures insert a visual `.error-banner` without a live-region role, focus transfer, programmatic association, or an explicit retry action.
- **Evidence:** `PatientSearch.tsx:63-73`, `ClinicianSchedule.tsx:160-169`, `PatientMessages.tsx:79-87`, `PatientTimeline.tsx:175-183`, and `FlowBoard.tsx:67-75` render page-specific error banners. The Dashboard, shell, Lab Queue, Suspense boundary, and Toast provide stronger status/retry patterns.
- **Additional portal evidence:** `PortalDashboard.tsx:213-221`, `PortalMessages.tsx:479-497,787-797`, `PortalRecords.tsx:612-711,898-910,997-1000`, and `PortalAccount.tsx:116` contain error states with inconsistent live-region, focus, and retry semantics; `PortalShell` and `PortalAppointments` provide stronger alert/retry patterns.
- **Expected:** Failure and recovery status should be perceivable and announced consistently to keyboard and assistive-technology users, with a clear retry or alternate recovery action.
- **Consequence:** A screen-reader user may not learn that loading failed, and several views require an indirect action such as resubmitting or navigating away to recover.
- **Counterevidence:** The text remains visible in the DOM; users can often resubmit or change filters; stronger accessible patterns exist elsewhere. No NVDA, JAWS, VoiceOver, or forced-failure browser run was performed.
- **Validation needed:** Accessibility specialist review and synthetic forced-failure runs across representative browsers and assistive technologies.
- **Disposition:** Retain as a conditional accessibility/recovery condition; do not infer formal WCAG nonconformance from source inspection alone.
