# Actors, permissions, and journeys

## 1. Actor model

| Actor | Purpose | Allowed actions | Explicitly prohibited |
|---|---|---|---|
| Anonymous visitor | Learn whether a practice offers immediate telehealth | View branded public information; start a prospective flow | View PHI, queue details, clinicians, or practice internals |
| Consumer/applicant | Create and verify an account and supply new-patient intake | Manage own draft, identity evidence, contacts, consents, coverage, triage answers | Access any patient chart before safe linkage; change protocol outcome |
| Established patient | Request care using a linked portal identity | Confirm/update own details, select coverage/pharmacy, complete triage, wait, join, view visit output | Access another patient's data or staff-only content |
| Authorized representative | Deferred in initial release | None | Act for a patient |
| Practice intake administrator | Resolve operational prerequisites | Review identity/demographic/coverage/consent/technology completion; hold, accept, decline, contact patient | Diagnose, edit clinical answers, downgrade safety outcomes, select treatment, sign chart |
| Clinical reviewer | Resolve a `ClinicalReview` outcome | Review answers/chart, request clarification, route to in-person/emergency/unsupported, or declare telehealth eligible with rationale | Falsify original answers/rules or perform administrative acceptance implicitly |
| Telehealth physician | Conduct care | Enter shift, reserve next eligible patient, review chart, consult, document, diagnose, order, prescribe within scope, follow up, sign | Treat while ineligible, prescribe controlled substances in MVP, rewrite prior evidence |
| Medical director | Govern clinical service | Author/approve/retire protocols, service catalog, escalation policy, clinical quality review | Change an in-flight assessment's version or erase evidence |
| Credentialing/compliance staff | Govern clinician authority | Maintain licenses, registrations, privileges, sanctions/restrictions, effective dates and evidence | Mark a clinician eligible without required evidence |
| Billing staff | Prepare professional claims | Review coverage/network evidence, code/validate, approve submit, reconcile acknowledgments/remittance | Represent estimate as guarantee; send medical claim to pharmacy |
| Privacy/security/HIM staff | Govern PHI, identity, records and incidents | Audit, disclosure/record workflow, retention holds, incident handling, duplicate resolution | Browse PHI without authorized purpose/context |
| Support operator | Help with non-clinical technical issues | View minimum operational metadata, guide reconnection, escalate | View clinical content by default; provide medical advice |
| External adapter | Exchange a bounded transaction | Act only under service identity, destination allowlist, scoped payload, and contract | Initiate unrelated actions or receive unnecessary PHI |

## 2. Actor requirements

| ID | Requirement | Acceptance evidence |
|---|---|---|
| TEL-ACT-001 | Every workforce action MUST use an authenticated workforce identity, active practice/facility context, permission, purpose of use, and resource-level authorization. | Authorization matrix and negative tests. |
| TEL-ACT-002 | Patient-facing authorization MUST bind the consumer session to exactly the request, enrollment, and canonical patient links it is allowed to access. | IDOR/cross-account tests. |
| TEL-ACT-003 | Administrative and clinical permissions MUST be separate. No administrative permission may imply clinical review, prescribing, encounter signing, or triage override. | Permission composition tests. |
| TEL-ACT-004 | A physician's queue visibility MUST be the minimum needed for the active practice/service; full chart access begins only after a valid reservation or an otherwise documented treatment relationship. | PHI-access timing and audit tests. |
| TEL-ACT-005 | Clinical reviewer eligibility MUST meet the same state/practice clinical authority required to make the review decision. | Reviewer eligibility test. |
| TEL-ACT-006 | Billing, support, compliance, and medical-director roles MUST receive task-specific views, not one shared superuser view. | Role-based UI/API tests. |
| TEL-ACT-007 | Break-glass access, if enabled, MUST require a reason, expire promptly, notify the privacy workflow, and never confer queue or prescribing eligibility. | Break-glass test and audit evidence. |
| TEL-ACT-008 | Service identities MUST be non-human, non-interactive, least-privilege, rotated, audience-bound, and attributable to one adapter/destination. | Credential and authorization review. |
| TEL-ACT-009 | Delegation and impersonation MUST be disabled for initial patient care unless a separately approved representative workflow is implemented. | Configuration and endpoint tests. |
| TEL-ACT-010 | Each action shown in a queue or chart MUST display authoritative server state and actor identity; stale client state MUST not grant authority. | Optimistic-concurrency and stale-response tests. |

## 3. Established-patient journey

1. Patient opens the practice's branded telehealth URL and sees provider identity, service states/hours, scope, privacy links, price/coverage framing, accessibility help, and emergency guidance.
2. Patient signs in through the practice portal identity route. Account recovery occurs outside the request transaction and uses approved identity controls.
3. The system confirms the practice enrollment and retrieves the patient's practice-authorized chart context.
4. Patient enters current physical address/location and callback number; the system checks age, state support, service availability, and emergency acknowledgments.
5. Patient completes universal and complaint-specific triage. An unsafe result exits to an explicit care route; no insurance or queue work follows.
6. Patient confirms demographics, communication preferences, allergies/medications/history summary, telehealth consent, notices, technology readiness, and insurance or self-pay choice.
7. Eligibility and exact network checks execute or enter manual review. The patient sees evidence time, limitations, expected charge/cost route, and required acknowledgments.
8. Practice staff review operational exceptions and accept the clinically eligible request.
9. Patient sees queue status, approximate wait/position, how to leave safely, and how to report worsening symptoms. Location and safety are periodically reconfirmed after material wait.
10. An eligible physician reserves the request. Both parties enter the waiting room, pass device checks, reconfirm location/identity, and join video.
11. Physician conducts and documents the consultation, including disposition and any non-controlled prescription.
12. Patient receives the after-visit summary and follow-up route; billing staff receive a claim-ready work item.

## 4. New-patient journey

The new-patient journey follows the same safety and care stages, with these additions:

1. A `ConsumerAccount` and practice-scoped `ProspectivePatient` are created without creating a canonical chart.
2. Contact channels are verified; minimum demographics and identity evidence are collected using data minimization.
3. Duplicate candidates are calculated against practice-visible records. Exact/high-confidence matches require secure linkage or HIM review; the UI never exposes candidate data.
4. Practice enrollment notices and state-specific telehealth consent are accepted and versioned.
5. Coverage and network checks use applicant data but do not prove identity or create a treatment relationship.
6. Once identity, duplicate resolution, operational acceptance, and clinical eligibility gates pass, the prospective record is atomically linked to an existing canonical patient or promoted to a new one. The telehealth request retains the source applicant identifier and promotion evidence.
7. If the request exits before promotion, only the minimum applicant/safety/audit record is retained under the configured retention rule; abandoned drafts are not silently converted to patients.

## 5. Practice-administrator journey

1. Staff sees practice-scoped columns for safety outcome, identity, duplicate status, demographic confirmation, consent, coverage, network, price acknowledgment, technology, and current freshness.
2. Safety-blocked requests are visible only as non-actionable outcomes with escalation evidence. Staff cannot place them in the clinician queue.
3. Staff resolves allowed operational exceptions, requests missing information, applies a reasoned hold, or declines using approved reason codes and patient-facing content.
4. When every gate passes, staff accepts. The server re-evaluates all gates in one transaction and assigns `ready_at`.
5. Staff may monitor, contact, cancel, or reassign for operational reasons. Any out-of-order move requires an authorized reason and is audited; it cannot supersede clinical safety.

## 6. Physician journey

1. Physician selects practice/service and enters a telehealth shift. The platform validates effective license/registration, privileges, restrictions, payer relationships, and state/service configuration.
2. The next eligible request is offered/reserved atomically. The physician may decline only with an approved reason; repeated declines are monitored.
3. The physician sees the complete intake evidence and relevant chart context, not only a patient-entered summary.
4. At join, the physician verifies patient identity, physical location, callback number, privacy, and consent; identifies themself and confirms the emergency/disconnection plan.
5. The physician performs an adequate examination for the presentation, documents limitations, and changes modality/disposition if video is insufficient.
6. The physician records assessment, diagnoses as appropriate, plan, orders, prescription decision, warning signs, and follow-up, then signs/finalizes.
7. The shift moves to wrap-up and then available, paused, or offline. A new request is not auto-joined without explicit physician action.

## 7. Journey requirements

| ID | Requirement | Acceptance evidence |
|---|---|---|
| TEL-ACT-011 | Every journey MUST support save/resume until clinical freshness expires; resumption MUST re-run time-sensitive gates. | Resume/expiry tests. |
| TEL-ACT-012 | A patient MUST be able to cancel before consultation, leave the queue, or decline video without coercion; the UI MUST explain consequences and alternate care routes. | Patient control tests. |
| TEL-ACT-013 | A request MUST not become invisible when a vendor or client fails; it enters a recoverable state with owner, next action, and patient-facing status. | Failure-injection tests. |
| TEL-ACT-014 | A patient reporting worsening symptoms while waiting MUST be able to re-enter safety screening immediately; the prior queue position cannot inhibit escalation. | Queue deterioration scenario. |
| TEL-ACT-015 | The system MUST support an interpreter/accessibility-needs flag and operational arrangement without exposing unnecessary clinical details. Multi-party video remains gated until approved. | Accessibility-support workflow test. |
| TEL-ACT-016 | Patient-facing status MUST distinguish draft, review, accepted/queued, assigned/connecting, consultation, completed, redirected, declined, canceled, and technical failure. | Content/state mapping test. |
| TEL-ACT-017 | No role MAY convert a vendor `unknown`, timeout, or transport acknowledgment into a positive clinical, coverage, network, prescription, or claim outcome. | Adapter semantics tests. |
| TEL-ACT-018 | A support operator MUST be able to issue a technical correlation code that lets clinical/operations staff find the request without exposing PHI in ordinary support logs. | Support workflow and log review. |

