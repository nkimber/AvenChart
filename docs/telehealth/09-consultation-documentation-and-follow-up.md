# Consultation, documentation, and follow-up

## 1. Physician workspace

After a valid reservation, the physician workspace provides a purpose-limited, practice-scoped view with:

- patient identity/banner, verified contact, current location/callback, preferred language, accessibility/interpreter needs, and consent state;
- chief concern and patient's own words;
- immutable intake answers, triage assessment, fired rules, review decisions, timestamps, changes, and safety guidance already given;
- relevant problem list, allergies/intolerances, medications, prior encounters, recent results, pregnancy status when relevant, and care-team/primary-care context;
- identity/demographic/coverage/network evidence state and any operational caveat, without unnecessary raw identity artifacts;
- device/video readiness and waiting-room presence; and
- structured documentation, orders, prescription/pharmacy, disposition, after-visit, follow-up, and sign/finalize actions.

Data provenance and freshness are visible. Patient-entered, inferred, externally returned, staff-verified, and clinician-confirmed values must not look identical.

## 2. Start-of-consultation safety check

Before `InConsultation`, the physician must:

1. identify themself by name and professional role;
2. verify the patient's identity using approved data without unnecessarily speaking sensitive identifiers;
3. reconfirm current physical location and callback number;
4. verify consent, privacy and ability to communicate, and any other participant/interpreter;
5. confirm the emergency/disconnection plan;
6. confirm the chief concern and material changes/worsening since triage; and
7. determine that video quality and available examination are adequate to begin.

Failure routes to clarification, technical recovery, higher-acuity disposition, or cancellation with safe instructions. A media connection alone is not evidence these checks occurred.

## 3. Documentation model

A completed encounter contains, as applicable:

- request, required immediate-telehealth appointment, practice, facility/service/billing location, physician, and start/end/modality identifiers;
- patient identity, confirmed service location, callback, interpreter/participants, consent and technology limitations;
- chief complaint, history of present illness, relevant histories/review, patient-supplied measurements/images and provenance;
- remote examination performed and limitations;
- reviewed medications/allergies and reconciliation status;
- assessment and clinician-selected diagnoses with coding provenance;
- medical decision-making, plan, tests/orders/referrals, prescription decision, patient education;
- disposition, follow-up timeframe/owner, warning signs, escalation instructions, and communication method;
- time and other billing-supporting facts without auto-inflating code level;
- author, timestamps, version, signature/finalization, amendments, and linked protocol/triage evidence.

The application may suggest completeness and provide code search, but it may not manufacture observations, diagnoses, exam elements, decision-making, time, or a billing level. Empty templated assertions cannot count as performed care.

## 4. Dispositions

| Disposition | Required behavior |
|---|---|
| `TreatedTelehealth` | Plan and safety net complete; prescription optional |
| `NoTreatmentNeeded` | Reassurance/education and warning signs documented |
| `TestingOrReferralRequired` | Order/referral destination, urgency, owner, result/follow-up plan |
| `UrgentInPerson` | Timeframe/site guidance and acknowledgment/contact plan |
| `EmergencyTransferRecommended` | 911/ED/988 or appropriate emergency instruction, location/callback, actions taken, handoff status without claiming unconfirmed transfer |
| `TechnicalAbort` | Clinical status at disconnect, contact attempts, whether enough evaluation occurred, safe next step, fee/coding decision |
| `PatientLeft` | Clinical status known, contact/safety attempts, warning/next step |
| `ClinicianUnableToComplete` | Reason category, safe reassignment/referral, continuity owner, fee/coding decision |

Every `Started` consultation must end in one disposition. `TechnicalAbort` is not an excuse to omit clinical safety documentation.

## 5. Orders and follow-up

Orders/referrals are structured records with intended destination, urgency, reason, status, owner, due time, result/closure linkage, and communication. The system must not state that an external organization received or accepted an order unless an acknowledgment confirms it.

The after-visit summary (AVS) is generated from signed structured content and includes practice/physician, visit date/modality, patient location state, concerns addressed, patient-friendly assessment, plan, medications/prescriptions and chosen pharmacy, tests/referrals, follow-up, warning signs, emergency guidance, contact route, and financial/claim status phrased separately from care. Sensitive diagnoses may require practice-approved handling, but must remain available to the patient as required.

Material corrections use a signed amendment linked to the original. An AVS change creates and notifies a new version; it never silently replaces what the patient received.

## 6. Consultation requirements

| ID | Requirement | Acceptance evidence |
|---|---|---|
| TEL-CON-001 | Chart access MUST require a valid treatment relationship/context and expose only practice-authorized, purpose-appropriate data with provenance. | Resource authorization tests. |
| TEL-CON-002 | Consultation start MUST require physician identity, patient identity, current location/callback, consent/privacy, change-in-symptoms, emergency plan, and modality sufficiency confirmation. | Start-gate tests. |
| TEL-CON-003 | The physician MUST be able to redirect or abort safely when history, exam, technology, language support, or patient status makes telehealth inadequate. | Clinical disposition scenarios. |
| TEL-CON-004 | Required documentation fields MUST reflect the actual service and remote-exam limitations; defaults MUST NOT assert unperformed examination or negative findings. | Template safety review/tests. |
| TEL-CON-005 | Patient-entered information MUST retain provenance and require clinician review/confirmation where used in the legal encounter. | Provenance/UI test. |
| TEL-CON-006 | A completed encounter MUST have exactly one final disposition, complete safety net, follow-up owner/timeframe, and signed record. | Completeness validator tests. |
| TEL-CON-007 | Every started but interrupted consultation MUST receive a documented clinical safety disposition before the physician can accept new work, except a supervised downtime procedure. | Failure/recovery test. |
| TEL-CON-008 | Diagnosis and billing codes MUST be selected or confirmed by an authorized clinician/coder and preserve author/source/version; the product MUST not upcode automatically. | Coding authorization/audit tests. |
| TEL-CON-009 | Orders/referrals MUST have lifecycle, ownership, due/escalation, delivery/business status, and closure evidence. | Follow-up lifecycle tests. |
| TEL-CON-010 | The AVS MUST be derived from the signed record, accessible, versioned, downloadable, and delivered through the authenticated portal with notification that minimizes PHI. | AVS content/delivery tests. |
| TEL-CON-011 | Signed records MUST be immutable; corrections MUST be append-only signed amendments with reason and patient re-notification when material. | Record amendment tests. |
| TEL-CON-012 | A prescription is optional and clinically independent of patient expectation, queue acceptance, payment route, or visit completion. | No-prescription journey test. |
| TEL-CON-013 | Clinically relevant session communication incorporated into the chart MUST show participants, timestamps, source, and physician review; no media recording/transcript may be implied. | Communication provenance test. |
| TEL-CON-014 | Documentation auto-save MUST be versioned, conflict-safe, locally PHI-minimized, and recoverable without overwriting another active author. | Concurrent edit/crash tests. |
| TEL-CON-015 | The system MUST support practice-defined co-signature/incomplete-chart queues, deadlines, escalation, and clinician lockout policy without blocking patient safety communication. | Incomplete chart workflow test. |
| TEL-CON-016 | Encounter completion MUST atomically create durable AVS, follow-up, prescription (if any), and claim-preparation work items through an outbox or equivalent transaction boundary. | Transaction/outbox tests. |

## 7. Existing capability fit

AvenChart's encounter, SOAP-note versioning, document lifecycle, signing, medication history, patient portal, audit, and billing-line foundations should be reused through a telehealth application service. Telehealth should not add a second legal chart. The encounter receives a telehealth extension/context and links to the request, triage, consent, video session metadata, and financial evidence.
