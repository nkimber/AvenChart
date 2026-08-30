# State regulatory and clinical governance

## 1. Governing principle

The patient's confirmed physical location at the time of care drives state routing. Home address, practice address, clinician address, IP geolocation, and phone area code are supporting signals only. The patient attests location during intake and the physician reconfirms it before consultation start. A material location change invalidates licensure, protocol, payer, consent, and claim-place-of-service checks.

This matrix is a design baseline as of 2026-08-30. Qualified counsel and the practice medical director must validate it before production and after any legal/policy change.

## 2. State matrix

| Topic | Georgia | California | Florida |
|---|---|---|---|
| Clinician authority | Active Georgia license or applicable Georgia telemedicine license/authority; practice must verify scope and restrictions | Valid/current California physician license when patient is in California | Active Florida license/compact authority, or valid out-of-state telehealth registration within its restrictions |
| Standard of care | Same standard; new-patient telemedicine permitted only when technology/peripherals are equal or superior to in-person for the applicable standard; adequate history/records | Same standard of care as in person; telehealth does not reduce examination/prescribing duties | Same prevailing professional standard; sufficient telehealth evaluation may diagnose/treat a new patient |
| Consent | Capture practice/legal-approved consent and disclosures; identify provider/credentials and emergency follow-up | Document verbal or written telehealth consent before delivery | Capture practice/legal-approved telehealth consent and notices as applicable |
| Identity/location | Document patient and provider identity; current location required by platform safety policy | Current patient location and provider identity required by platform policy and standard-of-care operation | Current location required to establish Florida routing and emergency plan |
| Prescribing | Apply Georgia rule; MVP excludes all controlled substances, including prohibited pain/chronic-pain pathways | Appropriate examination and medical indication; MVP excludes controlled substances | Statute includes restrictions/exceptions for Schedule II; MVP excludes all controlled substances and abortion services |
| In-person continuity | Track Georgia diligent-effort/in-person follow-up requirements for ongoing care and make episodic scope clear | Follow practice standard and referral/follow-up policy | Registered out-of-state provider must comply with Florida registration limits; provide in-person alternatives when needed |
| Corporate/practice boundary | Practice controls medical decisions | California corporate-practice restrictions require physician/practice control over diagnosis, referrals, care, clinician competence, payer relationships, coding/billing, and records; AvenChart remains administrative/technology vendor | Practice/provider controls medicine; platform applies configured policy |
| Special website duty | Practice/provider disclosure as approved | Clear practice/provider identity and license information | If using registered out-of-state providers, provide the required website hyperlink/information to Florida's telehealth registry/department material |
| Record floor used for design | At least 10 years from last office visit under cited Georgia medical record rule; counsel validates exceptions | At least 7 years after last date of service under current MBC FAQ, with longer program/category rules where applicable | Configurable, counsel-confirmed retention under Florida Board rules and other applicable law; no unsupported numeric default in this spec |

## 3. State-routing behavior

The server resolves a `JurisdictionDecision` from:

- patient-attested location/address and timestamp;
- selected state plus normalized address validation result;
- clinician reconfirmation;
- practice and service enablement;
- protocol state applicability;
- clinician license/registration/privilege evidence effective at service time;
- prescribing restrictions;
- payer/network/service-location constraints; and
- required disclosures/consent version.

IP/device location may flag inconsistency but cannot silently replace the patient's attestation. If signals conflict, the patient must clarify or enter review. The normalized precise address is PHI; coarse state may be used for routing where possible.

## 4. Clinician authority record

For each physician and jurisdiction, retain license/registration type, number, issuing authority, verified source, verification time, effective/expiration dates, status, restrictions, disciplinary/sanction check evidence, practice credentialing/privilege, malpractice coverage where required, service scopes, and reviewer. Eligibility must be computed from these source records, not a manual `is_licensed` flag.

For Florida registered out-of-state physicians, configuration must distinguish registration from full licensure, enforce the registration's limitations, store Florida-required coverage/agent evidence, and publish required practice website information. For Georgia, distinguish a full license from the limited telemedicine license. California care must be rendered through a legally valid practice/provider structure; AvenChart cannot select or control medical judgment.

## 5. Consent and disclosure package

Consent is a versioned package scoped by practice, state, modality, service, patient language, and effective time. It must cover, as legal/clinical owners approve:

- identity and role of the practice/provider and AvenChart;
- nature, expected benefits, material risks, limitations, alternatives, and right to stop telehealth;
- technology/privacy risks, participant privacy, and no-recording policy;
- emergency/disconnection process and current-location need;
- communication channels and interpreter/accessibility arrangements;
- prescription discretion and pharmacy choice;
- coverage/network/estimate limitations and financial responsibility;
- record creation, patient access, and applicable notices; and
- audio-only fallback when permitted.

Acceptance stores rendered content checksum, semantic version, language, modality, state, practice, patient/request, timestamp, session/device metadata appropriate to the audit, and signature/affirmative action evidence. The patient can download the exact accepted package.

## 6. Governance requirements

| ID | Requirement | Acceptance evidence |
|---|---|---|
| TEL-REG-001 | The system MUST use patient-attested and physician-reconfirmed physical location to select jurisdiction at the time of service. | Location-change and join tests. |
| TEL-REG-002 | A consultation MUST NOT start unless an effective clinician authority/privilege record covers the patient's state, practice, service, and time. | Eligibility matrix and boundary-time tests. |
| TEL-REG-003 | State legal rules MUST be versioned configuration with source, owner, review date, effective dates, and approval; code MUST fail closed when no applicable rule exists. | Configuration and unknown-state tests. |
| TEL-REG-004 | Georgia routing MUST enforce approved license/telemedicine authority, technology/exam sufficiency for new patients, required identity/records/emergency disclosures, and configured in-person continuity tracking. | Georgia legal scenario suite. |
| TEL-REG-005 | California routing MUST require an active California license, documented telehealth consent, same-standard-of-care behavior, and practice control over all medical decisions identified by the approved corporate-practice analysis. | California scenario and responsibility review. |
| TEL-REG-006 | Florida routing MUST enforce license/compact/registration status and restrictions, registered-provider website disclosures when applicable, and the practice's approved telehealth/prescribing rules. | Florida scenario and website-content tests. |
| TEL-REG-007 | Initial-release controls MUST block controlled-substance prescribing in every state even if a law might permit a narrower case. | Cross-state negative prescription tests. |
| TEL-REG-008 | The service catalog MUST exclude abortion services and any other explicitly prohibited or unapproved service by jurisdiction. | Catalog publication tests. |
| TEL-REG-009 | Consent MUST be obtained and preserved at the legally/clinically approved point before care, and renewed after applicable version, state, modality, or material service change. | Consent matrix and replay tests. |
| TEL-REG-010 | AvenChart-facing content and contracts MUST identify the practice as provider and preserve medical-professional control; platform staff MUST not control clinical protocols or individual care. | Legal/content/permission review. |
| TEL-REG-011 | Regulatory, board, payer, and clinical-source monitoring MUST occur at least quarterly and before each jurisdiction expansion; material changes trigger impact assessment and controlled configuration release. | Compliance calendar and change records. |
| TEL-REG-012 | Record retention MUST be configurable by record class, practice, state, payer/program, patient category, and legal hold, choosing the longest applicable period. | Retention matrix and test. |
| TEL-REG-013 | State expansion MUST be deny-by-default and require a complete legal/clinical/billing/security/content test pack, not only a new dropdown value. | Unknown/new-state release test. |
| TEL-REG-014 | The system MUST retain the state-policy, license, consent, protocol, and payer-rule versions used for each request/encounter/claim. | Historical reconstruction test. |

## 7. Approval owners

- State healthcare counsel: licensure, consent, website/disclosure, prescribing, records, corporate-practice and consumer terms.
- Practice medical director: protocol, exam sufficiency, provider qualifications, continuity, emergency/referral content, and clinical quality.
- Credentialing officer: primary-source verification and privileges.
- Billing/compliance owner: payer/state coding, network, estimates, and claim rules.
- Privacy/security officer: HIPAA roles, BAAs, identity, risk analysis, data handling, and incident process.

Sources are listed in [references.md](references.md).
