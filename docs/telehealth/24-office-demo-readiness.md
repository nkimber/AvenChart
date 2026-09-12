# Office telehealth demonstration — 14 September 2026

## Acceptance work in progress

The primary demonstration is an established synthetic patient and a physician
on the same laptop, in separate tabs, with separate cameras. A second pass
covers the new-patient entry and its practice-review handoff. This record will
be updated with actual results and a concise presenter script before handoff.

| Journey / claim | Required evidence |
| --- | --- |
| Start without knowing account names or hidden routes | Open the telehealth entry, sign in for each role, arrive at the correct workspace |
| Patient can request a visit with a clear next action | Location, safety review, existing details and coverage, queue entry through normal UI |
| Physician can pick up the request | Start availability, reserve, patient status updates without a manual refresh |
| Selected camera is actually used | Choose the second camera, inspect the capture source; keep selections independent across participants |
| A connected call means both people joined | Waiting alone, second participant joining, leaving, rejoining, mute and camera controls |
| Visit survives routine use | Queue polling, tab switching, page reload and saved-draft recovery |
| Visit can reach its defined completion | Documentation, wrap-up, disposition, required review, signing/closure, patient receipt |
| First-time errors are recoverable | Denied media permission, missing device, interrupted request, expired reservation and retry |
| Layout is usable in the office | Desktop and smaller laptop viewport, keyboard focus, primary action visible, no horizontal clipping |
| Demo is repeatable | Complete or release owned work, end physician availability, start the next demonstration |
| New-patient path is honest and navigable | Start fresh, review required steps, verify handoffs and describe limits in the presenter script |

Validation uses an isolated Docker Compose project named
`avenchart-telehealth-demo-qa` with a fresh synthetic database. Browser tests,
unit checks, physical-device checks and deployed acceptance are recorded
separately; passing one does not imply the others passed.

## Findings being addressed

- Telehealth entry loses its destination at sign-in and initially selects a
  general portal fixture rather than the documented telehealth patient.
- Internet calling mixes browser device IDs with ACS device IDs and silently
  falls back to the first camera when an explicit selection is not found.
- Physician active-work recovery returns a reservation only while its lease is
  active; consultation and wrap-up recovery need verification.

## Results

- Internet calling implementation checkpoint: nine focused regression tests
  pass for second-camera capture, unplugged-camera refusal, remote-participant
  presence, same-laptop audio silencing, microphone/camera toggles, cancellation
  during join, disconnected-call cleanup/rejoin, unsupported speaker selection,
  and denied-permission recovery. These tests use a mocked ACS SDK; actual
  physical-camera and deployed-call acceptance remain pending.
- Overall workflow implementation and browser acceptance remain in progress.
  This document is not yet a claim that the Monday demonstration is ready.
- Physician recovery checkpoint: the API now returns owned Busy/WrapUp work
  with its consultation identifier. Three component regressions pass for
  consultation recovery without polling over unsaved notes, locked wrap-up
  recovery, and preservation during a failed refresh. TypeScript and targeted
  ESLint pass; the API Release container builds and becomes healthy. Full
  database-backed lifecycle acceptance is still pending.
