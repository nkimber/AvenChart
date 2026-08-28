# Sprint 10: synthetic pharmacy choice

Status: Approved for implementation under [Decision 0013](../decisions/0013-approved-sprint-10-synthetic-pharmacy-choice.md)  
Scope: Disabled synthetic physician-owned wrap-up directory/search and patient-confirmed destination draft only

## Sprint outcome

The owning physician can search a neutral, deterministic synthetic pharmacy directory during unfinished wrap-up, see an associated synthetic chart preference when present, and explicitly record a patient-confirmed destination draft. The result is versioned, provenance-preserving, conflict-safe, auditable, and visibly not a prescription or transmission.

## Backlog

| Story | Deliverable | Acceptance evidence |
|---|---|---|
| `TH-SP10-001` | Add `IPharmacyDirectory` and a deterministic `NON_PRODUCTION` GA/CA/FL implementation with bounded neutral search and consented postal-origin distance | adapter unit tests cover stable ordering, filters, origin acknowledgment, no coordinates, inactive/unknown entries, source/version, and zero outbound dependencies |
| `TH-SP10-002` | Add additive preference/choice/version/event/command persistence after V0287 | empty/populated/replay/interruption recovery, append-only history/events, monotonic version, current uniqueness, source snapshot, and generated-bootstrap parity |
| `TH-SP10-003` | Add owner-scoped no-store GET directory/current-choice and PUT patient-confirmed choice routes | physician/wrap-up/adult/facility ownership, exact replay, stale/conflicting write, bounded Problem Details, PHI audit, and zero downstream mutation evidence |
| `TH-SP10-004` | Add an accessible physician pharmacy-choice panel to the existing wrap-up workspace | neutral language, preference/search distinction, origin disclosure, explicit confirmation, keyboard/focus/reflow/loading/error/conflict/retry tests, and no browser persistence |
| `TH-SP10-005` | Extend typed client/OpenAPI, runtime safety/readiness, authorization, concurrency, migration, CI, planning, and runbook coverage | focused and full suites pass with stub rejection in Production and no payer/pharmacy/eRx/geocoder network path |
| `TH-SP10-006` | Publish the complete evidence packet and graph/change-impact review | evidence maps every story/control, records negative assertions, checks exact cleanup/default-data preservation, and retains all independent review gates |

## API boundary

```text
GET /api/telehealth/v1/clinician/consultations/{opaqueConsultationId}/pharmacy-choices
  ?query=<bounded>&state=GA|CA|FL&postalCode=<bounded>&originPostalCode=<bounded>
  &locationSearchAcknowledged=true|false&limit=<bounded>

PUT /api/telehealth/v1/clinician/consultations/{opaqueConsultationId}/pharmacy-choice
X-Idempotency-Key: <semantic key>
{
  "expectedVersion": 0,
  "directoryEntryId": "<opaque synthetic directory key>",
  "patientChoiceConfirmed": true,
  "syntheticDataConfirmed": true
}
```

Both routes are physician-only, consultation-owner-only, facility-scoped, no-store/private, and PHI-audited. Search query values are bounded and must not contain patient names or clinical data. The response contains no patient, encounter, appointment, request, staff, or precise-location identifier.

## Persistence boundary

The slice may add:

- a synthetic patient-to-directory preference association used only when explicitly present;
- append-only consultation pharmacy-choice versions with server-generated actor/time and complete destination provenance snapshot;
- immutable choice lifecycle events; and
- hash/fingerprint-bound idempotency command evidence.

No existing `pharmacies`, `prescriptions`, `medications`, encounter signature, billing, claim, or lifecycle row is created or changed.

## Exit boundary

Sprint 10 ends with a non-prescription destination draft attached to an unfinished synthetic consultation. Patient self-service choice, manual/unlisted resolution, medication safety, drug selection, prescription creation/signing, NCPDP SCRIPT mapping/transmission, pharmacy business acknowledgment, AVS, disposition, completion, clinician release, billing, claims, precise geolocation, live data, and production care remain unavailable.

## Required review packet

Before a later prescription slice, retain open independent clinical/legal review of pharmacy-choice/prescribing rules; security/privacy review of location, query, preference, audit, and browser boundaries; data review of provenance/version/event/idempotency invariants; accessibility/manual workflow review; vendor/licensing review of a real directory and eRx network; and program-owner approval of a separately bounded prescription decision.
