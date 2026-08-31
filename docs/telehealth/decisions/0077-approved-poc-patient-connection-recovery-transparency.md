# Decision 0077: POC patient connection-recovery transparency

Status: approved for the non-production POC only

## Decision

When authoritative polling reports that a synthetic request moved from `Connecting` back to `Queued`, clear stale local waiting-room material and show the patient a neutral recovery notice in both established-patient and prospective-patient flows.

## Boundary

- The browser-only behavior recognizes only the authoritative `Connecting` to `Queued` lifecycle transition; it does not infer, expose, or persist a reason.
- It clears device-preflight evidence, waiting-room grant material, and locally retained command material so an ended session cannot be reused in the interface.
- The notice says only that the synthetic connection room is no longer active and the request has returned to its existing queue position. It explicitly says no consultation, clinical decision, or external action occurred.
- It does not create a notification, alter the request, reorder the queue, identify a physician, expose media or signal content, contact a patient, or perform any clinical, financial, integration, or external action.

## Verification

The slice requires a focused transition test, frontend regression and bundle evidence, planning/runtime validation, staging health, and Graphify review. Durable notification delivery, operational messaging policy, patient-specific reason disclosure, production media recovery, and all release gates remain separately governed work.
