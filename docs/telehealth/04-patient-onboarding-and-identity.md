# Patient onboarding and identity

## 1. Identity architecture

The patient-facing identity model separates authentication from clinical identity:

```text
ConsumerAccount
  -> verified contacts and authenticators
  -> PracticeEnrollment (practice-specific notices, consent, status)
  -> ProspectivePatient (temporary new-applicant data, if needed)
  -> PatientIdentityLink (reviewed link)
  -> CanonicalPatient
  -> PracticePatientChart/access context
```

An established patient may already have the last three relationships. A new applicant begins with the first three. A consumer account identifier is never accepted as a patient identifier, and a demographic match alone never grants chart access.

## 2. Minimum data set

Data is collected progressively. Fields not needed for the current gate are deferred.

| Stage | Required information |
|---|---|
| Public routing | Practice/brand key; no PHI required |
| Account/contact | Email or mobile, verified channel, password/passkey/federated subject, terms/privacy version |
| Immediate safety | Legal first/last name, date of birth, current physical address or sufficiently precise location, callback number, whether patient is alone/safe to speak, emergency contact when required |
| Identity resolution | Current/previous name, home address, phone/email, sex or other matching attributes permitted by policy; approved proofing evidence; existing portal/link claim if applicable |
| Care | Preferred name, sex assigned at birth and gender information only where clinically relevant and respectfully presented, preferred language, accessibility/interpreter needs, allergies, medications, key history, pregnancy possibility when relevant, primary-care information optional unless protocol requires it |
| Coverage | Payer/product/member/group, subscriber identity/relationship/date of birth, card images optional, coverage priority, plan contact identifiers |
| Billing/records | Mailing address, guarantor/subscriber data, consent/notice evidence, identity source, provenance, request/encounter linkage |

Government identifier/SSN is not part of the default new-patient flow. If a payer or approved proofing vendor requires a sensitive attribute, it must be justified, masked, encrypted, access-restricted, and never logged.

## 3. Existing-patient linkage

1. Authenticate through the configured patient identity adapter.
2. Resolve a single active consumer-to-patient link scoped to the practice context.
3. Compare verified contact or recovery evidence when risk signals require step-up.
4. Require the patient to confirm, not merely view, demographics, contact, coverage, allergies, and medication summaries.
5. Submit changes as versioned requests or governed direct updates. A pending demographic change must not be represented as authoritative until applied.

The existing AvenChart portal session and external identity mapping are reusable boundaries, but production patient authentication must not rely on the local deterministic adapter or a header that a browser can forge.

## 4. New-patient proofing and duplicate resolution

The security/privacy owner must perform the NIST SP 800-63-4 Digital Identity Risk Management process. The provisional target is AAL2-equivalent authentication for PHI/visit access and IAL2-compatible remote proofing before unattended linkage to an existing chart. Assisted/manual alternatives may be approved for accessibility or proofing failure, with equivalent fraud controls and documented redress.

Duplicate search returns internal candidate scores and reason categories only. Patient-facing clients receive `link offered`, `additional verification required`, or `manual review required`, never another record's demographics. Candidate scoring uses normalized name, birth date, verified contact, address history, and practice-approved identifiers; it must be calibrated on synthetic/de-identified test data and monitored for demographic bias.

Possible outcomes:

- **Secure link:** high-confidence candidate plus independent evidence succeeds.
- **Manual identity review:** ambiguous candidates, conflicting demographics, possible fraud, or accessibility exception.
- **Create new canonical patient:** no credible candidate and proofing/acceptance gates pass.
- **Decline/expire:** identity cannot safely be resolved in time; provide alternate practice contact, not a clinical judgment.

Promotion/linkage occurs in the same transaction as practice acceptance. It stores the prospective ID, selected candidate/new patient ID, evidence references, actor/process, policy version, time, and duplicate disposition. A conflict or failure rolls back the patient creation/link and queue entry together.

## 5. Account and recovery behavior

- Contact verification links/codes are single-use, short-lived, rate-limited, purpose-bound, and stored as non-recoverable verifiers.
- Authentication errors avoid account enumeration.
- Recovery cannot rely only on knowledge-based questions or patient-entered demographics.
- Changing a high-risk attribute or recovery channel requires step-up and alerts the prior verified channel where safe.
- Sessions have absolute and inactivity limits, device/session visibility, server-side revocation, CSRF protection, and appropriate cookie/token protections.
- A shared device flow provides explicit sign-out, clears local PHI, avoids notification previews, and warns before download.
- Failed proofing provides an accessible redress/manual route and never exposes the matching patient record.

## 6. Identity requirements

| ID | Requirement | Acceptance evidence |
|---|---|---|
| TEL-IDN-001 | Consumer authentication, prospective applicant, canonical patient, practice enrollment, and chart-access link MUST be distinct identifiers and records. | Schema and authorization tests. |
| TEL-IDN-002 | A new applicant MUST remain a `ProspectivePatient` until identity, duplicate, clinical, consent, and operational gates pass. | Lifecycle test proving no premature patient row. |
| TEL-IDN-003 | The system MUST collect only the minimum data required for the current stage and MUST state why sensitive fields are needed. | Data inventory/privacy review and UX test. |
| TEL-IDN-004 | Email/mobile ownership verification MUST NOT by itself prove real-world identity or authorize an existing chart link. | Negative linkage test. |
| TEL-IDN-005 | The organization MUST document a NIST SP 800-63-4 risk assessment and approved IAL/AAL/FAL targets before production identity design is accepted. | Approved identity decision record. |
| TEL-IDN-006 | The provisional implementation MUST support phishing-resistant authentication options and step-up to AAL2-equivalent controls for PHI access, recovery, sensitive changes, and consultation join. | Authentication security tests. |
| TEL-IDN-007 | Existing-patient linkage MUST require an authenticated portal link or approved proofing evidence independent of demographic similarity. | Linkage attack tests. |
| TEL-IDN-008 | Duplicate matching MUST be practice-context-aware, must not disclose candidate PHI, and must route ambiguous results to authorized HIM review. | Privacy and duplicate tests. |
| TEL-IDN-009 | Link/create/promotion, enrollment activation, request reassociation, queue entry, and audit MUST commit atomically. | Transaction failure tests. |
| TEL-IDN-010 | A conflicting identity or duplicate decision MUST fail closed while preserving the request and a safe manual next step. | Conflict recovery test. |
| TEL-IDN-011 | Patient-entered changes MUST preserve old/new values, provenance, effective status, reviewer when needed, and request/encounter association. | History/audit test. |
| TEL-IDN-012 | Account enumeration, credential stuffing, verification-code abuse, session fixation, insecure recovery, and forged-link attacks MUST have automated negative tests and monitoring. | Security test report. |
| TEL-IDN-013 | Identity evidence images and raw vendor evidence MUST be access-restricted, encrypted, retained only as approved, and excluded from ordinary application/telemetry logs. | Data-flow/log review. |
| TEL-IDN-014 | Patient access MUST be revoked or re-evaluated when the patient link, enrollment, account, practice, or canonical record becomes inactive/merged/deceased/restricted. | Lifecycle authorization tests. |
| TEL-IDN-015 | Manual identity review MUST show provenance and comparison attributes without allowing a reviewer to overwrite source evidence; decisions require a reason and are auditable. | Review workflow test. |
| TEL-IDN-016 | Abandoned/expired applicant records MUST follow an approved short retention schedule while preserving any safety communication, consent, audit, or legal hold that must remain. | Retention job and hold tests. |

## 7. Repository implementation note

The current staff-only patient create endpoint and `PatientRepository.CreateAsync` flow are not public onboarding APIs. They can inform validation and duplicate behavior, but the telehealth feature requires a dedicated application service and transaction. The current `portal_enabled=false` creation default remains safe until account linkage is independently complete.

