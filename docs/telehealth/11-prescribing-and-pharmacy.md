# Prescribing and pharmacy

## 1. Scope

The initial release supports physician-created, non-controlled outpatient prescriptions when clinically appropriate. It does not support controlled substances, refill delegation, medication administration, pharmacy adjudication, or an AvenChart-operated pharmacy. No patient flow suggests that paying for or completing a visit entitles the patient to medication.

## 2. Pharmacy choice

The patient can:

- select an active preferred pharmacy already in the chart;
- search a directory by name, city, postal code, or proximity to a patient-selected origin (home, current location, or another location);
- view neutral facts such as name, address, phone, hours when sourced, distance estimate, and supported electronic-routing identifiers;
- choose a pharmacy not found and route to staff/manual resolution; or
- change choice before prescription transmission, subject to physician review after transmission.

Results must not be ranked by AvenChart/practice economic benefit or presented as required. This is especially important for Florida's pharmacy-choice protections. Location search requires patient permission or an entered address; precise location is not shared with pharmacies merely for directory search.

The selected record snapshots directory source/version, NCPDP/NABP identifier when available, NPI where applicable, address, phone/fax, electronic-routing capability, and patient selection time. The physician confirms the destination before signing.

## 3. Medication safety workflow

Before signing, the physician must review:

- active medication list and reconciliation status;
- allergies/intolerances and reaction/severity where known;
- relevant diagnoses, pregnancy/renal/hepatic or other protocol-specific context;
- duplicate therapy, interaction and contraindication alerts from approved sources;
- medication, strength, dose, route, frequency, quantity, units, duration/day supply, refills, substitutions/DAW, indication/instructions as required;
- selected pharmacy and patient counseling/follow-up; and
- whether required tests/examination/monitoring are complete.

Alerts must be attributable/versioned. The physician may override eligible alerts only with an approved reason and must never override the controlled-substance block. The product must not infer “no known allergies” from missing data.

## 4. Prescription lifecycle

```text
Draft -> SafetyChecked -> Signed -> Queued -> Sent
Sent -> TransportAcknowledged -> Accepted | Rejected | ChangeRequested | CancelPending
ChangeRequested -> RevisedAndSigned | Declined
Signed/Sent/Accepted -> CancelPending -> Canceled | CancelRejected
Rejected -> CorrectedAndResigned | ManualFollowUp
```

`Signed` is a clinical/legal action and requires current physician authentication, authority, encounter, medication content checksum, pharmacy, and signature time. `Sent` is transport only. Pharmacy acceptance, dispensing, pickup, and payer payment are not inferred unless a corresponding supported business message/evidence exists.

## 5. Standards-oriented adapter

`IEPrescriptionGateway` uses a canonical model mapped to the NCPDP SCRIPT transaction version selected by destination/payer policy. New adapter work targets SCRIPT 2023011. CMS permits either SCRIPT 2017071 or 2023011 during the transition through December 31, 2027 and requires exclusive SCRIPT 2023011 use for Part D e-prescribing beginning January 1, 2028; 2017071 is therefore transition compatibility, not AvenChart's forward target. Supported transaction families must be explicitly certified, such as NewRx, CancelRx, RxChange, RxRenewal, status/error, medication history, and real-time prescription benefit where separately contracted.

The development stub generates deterministic accept, reject, duplicate, delayed, change-request, cancellation, and outage scenarios. It is not a fax or live prescription service and must be marked `NON_PRODUCTION`.

## 6. Requirements

| ID | Requirement | Acceptance evidence |
|---|---|---|
| TEL-RX-001 | Only the treating physician or another explicitly authorized prescriber with current state/practice authority MAY create and sign a prescription. | Resource/role/license tests. |
| TEL-RX-002 | Initial-release controls MUST reject every controlled substance using maintained drug classification plus defense-in-depth service and adapter validation; unknown classification fails closed. | Drug fixture/negative tests. |
| TEL-RX-003 | A prescription MUST require an active encounter, medication/allergy review, adequate evaluation, clinical indication, complete structured directions, chosen pharmacy, and prescriber signature. | Completeness/safety tests. |
| TEL-RX-004 | Missing allergy or medication information MUST be visibly unknown and require resolution/acknowledgment; it MUST NOT be normalized to none. | Missing-data tests. |
| TEL-RX-005 | Interaction/contraindication alerts MUST preserve knowledge-source/version and clinical action; permitted override requires a reason and audit. | Alert lifecycle tests. |
| TEL-RX-006 | The patient MUST retain pharmacy choice, including neutral preferred/nearby/search/manual options; ordering and content MUST not steer for economic benefit. | Florida and general choice tests. |
| TEL-RX-007 | Proximity search MUST disclose origin/permission, minimize location sharing, and never restrict results to financially preferred pharmacies. | Privacy/ranking tests. |
| TEL-RX-008 | Pharmacy identity MUST use a canonical directory record and snapshot electronic-routing identifier/address/source before signature. | Directory versioning tests. |
| TEL-RX-009 | Prescription clinical state, transport state, pharmacy business state, dispense state, and pharmacy claim state MUST remain distinct. | State model/semantic tests. |
| TEL-RX-010 | The adapter MUST use a versioned canonical model with certified NCPDP SCRIPT mappings; raw free-text transmission is not the normal integration. | Schema/mapping contract tests. |
| TEL-RX-011 | Signed content MUST be immutable; correction creates a new signed version and required cancel/change workflow linked to the original. | Correction/cancel tests. |
| TEL-RX-012 | Retries MUST be idempotent and MUST NOT create duplicate prescriptions; conflicting reuse is quarantined for review. | Duplicate/retry tests. |
| TEL-RX-013 | Rejection/change/cancel status MUST create an owned physician/staff task with urgency, patient notification policy, and safe closure. | Recovery workflow tests. |
| TEL-RX-014 | The AVS MUST show prescribed medication, directions, chosen pharmacy, transmission/business status phrased accurately, and what to do if not received. | AVS/status content tests. |
| TEL-RX-015 | E-prescribing vendors, pharmacy directory vendors, and drug knowledge vendors handling PHI MUST pass BAA/security/privacy/availability/data-use review. | Vendor approval evidence. |
| TEL-RX-016 | Production startup MUST reject the stub gateway and any uncertified standard/version/destination combination. | Environment/config safety tests. |

## 7. Clarification about insurance information

The e-prescription may include the patient, prescriber, medication, pharmacy, and permitted coverage identifiers needed for prescription routing/benefit transactions. It must not include or transmit the professional consultation's CMS-1500/837P claim. The dispensing pharmacy independently submits its pharmacy claim through pharmacy benefit channels.
