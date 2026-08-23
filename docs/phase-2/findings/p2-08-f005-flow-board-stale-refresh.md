# P2-08-F005 — Failed Flow Board refresh can retain an actionable board for a different selected date

- **Status:** Validated condition
- **Domains:** 03 Clinical workflow; 08 Frontend and accessibility; 09 Quality and verification
- **Coverage:** `COV-011`, `COV-014`
- **Severity:** Medium
- **Production blocker:** Unknown pending scheduling and clinical-operations reliance
- **Reach:** Repeated on the Flow Board day view
- **Confidence:** High static; browser failure trace outstanding
- **Condition:** Changing the Flow Board date or refreshing after a mutation clears the error state but does not clear or version the existing board. If the new request fails, the prior board remains rendered while Arrive, Room, and Complete actions remain enabled.
- **Evidence:** `avenchart-ui/src/pages/clinician/FlowBoard.tsx:22-27,55-67,75-109` stores one board snapshot, sets only an error in the rejection path, and renders the action buttons from that snapshot.
- **Expected:** A failed or stale refresh should not leave actionable records presented under a different date; the UI should distinguish current, stale, loading, and failed snapshots and provide an explicit recovery path.
- **Consequence:** A user could apply a workflow status transition to a prior day’s appointment while the date control displays the newly selected day.
- **Counterevidence:** The failure banner is visible and cards retain their patient/time details; historical status corrections may be intentional. No live failure scenario was run.
- **Validation needed:** Scheduling-operations policy decision and a synthetic browser trace that forces rejection after a date change and after a successful status mutation.
- **Disposition:** Retain as a bounded UI state/recovery condition, distinct from `P2-03-F007` response inversion.
