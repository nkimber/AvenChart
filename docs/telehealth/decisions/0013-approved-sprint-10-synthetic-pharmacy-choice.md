# Decision 0013: Sprint 10 synthetic pharmacy-choice authorization

Status: Approved — active for the exact disabled synthetic slice below  
Approved date: 2026-08-27  
Decision owner: AvenChart program owner  
Implementation owner: Codex delivery agent under AvenChart program-owner direction  
Review/expiry: 2026-10-31, or immediately when superseded by a later gate decision

## 1. Authorized outcome

Permit only the physician who owns a current synthetic telehealth consultation in unfinished wrap-up to search a deterministic non-production pharmacy directory, see an explicitly associated synthetic chart preference when one exists, and record a patient-confirmed destination draft:

```text
opaque consultation ID + authenticated owning physician + selected treatment facility
  -> re-verify current consultation/request/shift/session/appointment/encounter/adult-patient binding
  -> search neutral synthetic entries by name, city, state, postal code, or consented postal origin
  -> expose directory provenance, electronic-routing capability, optional identifiers, and honest limitations
  -> require expected choice version, idempotency key, synthetic-data acknowledgment,
     and an affirmative statement that the patient chose or confirmed the destination
  -> append one versioned destination snapshot and immutable event
  -> keep the appointment/encounter open, documentation unsigned, and physician unavailable
```

The destination is an unsigned consultation planning draft. It is not a medication order, prescription, prescriber decision, signature, transmission instruction, pharmacy endorsement, claim, completion event, or evidence that a pharmacy can or will fill anything.

## 2. Authorized implementation surfaces

Changes may add one additive migration after V0287 and use the existing telehealth consultation, authorization, PHI-audit, runtime-safety, workspace, and test paths plus:

```text
avenchart/backend/src/AvenChart.Api/Features/Telehealth/TelehealthPharmacyDirectory.cs
docs/telehealth/decisions/0013-approved-sprint-10-synthetic-pharmacy-choice.md
docs/telehealth/backlog/sprint-10-synthetic-pharmacy-choice.md
docs/telehealth/backlog/sprint-10-evidence.md
```

The smallest backend, frontend, OpenAPI, migration/bootstrap, planning, CI, runbook, and evidence edits required to connect and prove this disabled synthetic slice are authorized.

## 3. Required controls

1. The feature remains disabled by default, synthetic-only, rejected in Production, and unable to call a payer, pharmacy, e-prescribing network, geocoder, map service, or other vendor.
2. Both pharmacy routes use only an opaque consultation ID and require physician role, treatment purpose, selected facility, staff identity, `patients:demo view`, `encounters:auth view`, `encounters:auth write` for mutation, and ownership of the current wrap-up encounter.
3. The server rebinds consultation, request, released reservation, wrap-up shift, ended synthetic session, in-progress appointment, open encounter, active adult patient, practice, and facility. Non-owners, administrators, cross-scope identities, stale consultations, and ineligible patients receive the established opaque boundary.
4. Directory results come only from a deterministic `NON_PRODUCTION` adapter. Stable canonical fields are compatible with future Organization/Location-style mapping: opaque directory key, active status, name, structured address, phone, nullable NCPDP/NPI identifiers, electronic-routing capability, source, and dataset version.
5. Search is neutral and bounded. Name/city/state/postal matching and deterministic distance from an explicitly entered supported postal origin are permitted. Distance requires an affirmative location-search acknowledgment, exposes no coordinates, and is followed by stable name/key ordering. No economic, practice-owned, payer-owned, or fill-likelihood ranking is permitted.
6. An existing synthetic chart preference may be shown only when it is actively associated with the rebound patient and resolves to the same current directory dataset. Absence is reported as no preference returned, never as patient refusal or a negative clinical fact.
7. Recording a destination requires a directory key from the current dataset, `ExpectedVersion >= 0`, a semantic idempotency key, `PatientChoiceConfirmed = true`, and `SyntheticDataConfirmed = true`. The client cannot supply patient, encounter, request, actor, time, provenance, address snapshot, NCPDP/NPI, or routing capability.
8. One transaction locks the owned consultation and current choice, appends a monotonic version plus immutable event, and snapshots the directory source/version and destination fields. Exact replay returns the original result; changed key reuse, stale version, invalid/inactive entry, or competing writer fails without partial change.
9. The draft may be replaced before a future prescription exists, but history remains append-only. This slice creates no medication, prescription, signature, order, claim, bill, task, message, AVS, or external-delivery row.
10. Every response is no-store/private and passes through the existing permitted/denied PHI-audit boundary bound to the opaque consultation resource. Search input, destination, patient identifiers, coordinates, and hidden keys do not enter ordinary logs, telemetry, URLs beyond bounded query values, or browser storage.
11. The physician UI labels results as neutral synthetic choices, distinguishes chart preference from search proximity, explains the search origin and approximate distance, supports keyboard and screen-reader use, recovers focus after errors/conflicts, and remains usable at 320 px.
12. The consultation remains `MediaEnded`, request remains `WrapUp`, shift remains `WrapUp`, appointment/encounter remain open, documentation remains unsigned, and every prescribing/claims/completion capability remains false.
13. Manual/unlisted-pharmacy resolution, patient self-service selection, precise current-location geocoding, medication selection, safety checking, prescribing, signature, transmission, CancelRx/RxChange, claim work, clinician release, completion, real integrations, and production care remain unavailable.
14. Unit, adapter, contract, authorization, PostgreSQL owner/non-owner/idempotency/concurrency/rollback/audit/privacy, migration recovery, accessibility, failure-recovery, and complete regression evidence must pass without weakening Sprints 1–9.

## 4. Standards posture

The internal directory contract preserves the pharmacy business identifier separately from the API resource key and models the physical destination separately from later electronic endpoints. That shape can map to HL7 FHIR Organization/Location/Endpoint concepts without making this route a FHIR API. Any later e-prescription gateway remains a separate canonical NCPDP SCRIPT adapter. CMS currently permits SCRIPT 2017071 during the transition and requires SCRIPT 2023011 for Part D e-prescribing beginning January 1, 2028; this sprint transmits neither version.

## 5. Explicit exclusions

This decision does not authorize:

- clinical medication selection, allergy/medication reconciliation, drug knowledge, formulary, benefit, interaction, contraindication, controlled-substance, or prescribing logic;
- a prescription draft or signed prescription, pharmacy network connection, fax, eRx, SCRIPT transaction, dispense/pickup/payment assertion, or professional/pharmacy claim;
- a claim that a listed pharmacy is real, open, preferred by a payer/practice, electronically reachable, in network, able to fill, close to the patient's actual location, or endorsed;
- automatic use of home/current location, browser geolocation, external geocoding, precise coordinates, or sharing a search origin with a pharmacy; or
- real consent, identity proofing, minors/proxies/guardians, real people, real PHI, production enablement, patient care, or closure of any independent review gate.

## 6. Stop conditions and rollback

Stop if a non-owner or cross-scope identity can search or read a preference; a stale/competing command partially changes state; search steering is introduced; coordinates or PHI reach logs, URLs, storage, or cacheable responses; a destination creates or changes a prescription/claim/encounter lifecycle; any outbound call occurs; the physician becomes available; or an earlier safeguard regresses. Rollback disables/removes the pharmacy-choice routes and UI while retaining the additive schema and immutable synthetic audit evidence.

## 7. Approval record

The program owner directed complete implementation of the approved telehealth goal, approved all decisions, and authorized uninterrupted work and the changes needed to continue the long-running job. This record applies that authority only to the bounded disabled synthetic directory/search and destination-draft slice above. It does not broaden authority to real care, prescribing, transmission, disposition, signing, completion, claims, or external vendors.

## References

- [Decision 0012](0012-approved-sprint-09-consultation-wrap-up-handoff.md)
- [Prescribing and pharmacy specification](../11-prescribing-and-pharmacy.md)
- [Technical architecture](../13-technical-architecture.md)
- [Sprint 10 plan](../backlog/sprint-10-synthetic-pharmacy-choice.md)
- [CMS e-prescribing standards](https://www.cms.gov/medicare/regulations-guidance/electronic-prescribing)
- [HL7 FHIR Organization](https://hl7.org/fhir/organization.html)
- [HL7 FHIR Location](https://hl7.org/fhir/R4/location.html)
