# Sprint 15: synthetic prospective-applicant universal safety triage

Status: Approved for bounded implementation by [TH-DEC-0018](../decisions/0018-approved-sprint-15-prospective-safety-triage.md)  
Scope: Applicant-owned emergency-first deterministic universal safety screen after no-candidate staff review; no identity proofing, patient promotion/linkage, complaint, insurance, consent, request, queue, care, downstream action, external integration, production use, or real PHI

## 1. Outcome

Allow a synthetic prospective applicant in `IdentityReviewApproved` to complete the same versioned priority safety evaluation used by the established-patient shell, while preserving a harder boundary: even the passing result only permits a later prospective-intake step. It never creates or authorizes care.

## 2. Stories

| ID | Story |
|---|---|
| `TH-SP15-001` | Add one append-only prospective safety evaluation and constrained terminal applicant safety states with protocol provenance, exact replay, and hard-false consequential flags. |
| `TH-SP15-002` | Add an access-key-owned service that requires current supported-state location, explicit nullable answers, current no-candidate staff review, and server-only outcome mapping. |
| `TH-SP15-003` | Publish a typed private/no-store idempotent applicant safety-triage route with opaque not-found, bounded Problem Details, and no staff/patient session substitution. |
| `TH-SP15-004` | Extend the prospective entry with an accessible yes/no safety form, immediate emergency direction, fixed outcome language, explicit retry/reload, and no clinical-answer persistence. |
| `TH-SP15-005` | Preserve coarse applicant resume status and fixed directions without exposing access hashes, identity-review reason/actor, duplicate candidates, answer fingerprint, or protocol internals. |
| `TH-SP15-006` | Prove outcome precedence, missing-answer rejection, access/version/review-state isolation, exact replay, contention, append-only evidence, zero patient/downstream delta, accessibility, migration, Graphify, and full regression. |

## 3. State and outcome mapping

| Protocol outcome | Applicant state | Applicant-facing meaning |
|---|---|---|
| `Emergency` | `SafetyEmergencyRedirect` | Call 911 or go to the nearest emergency department now; no request was created. |
| `UrgentInPerson` | `SafetyInPersonRequired` | Seek prompt in-person evaluation; no clinician reviewed these answers. |
| `InPersonRequired` | `SafetyInPersonRequired` | This screen cannot continue toward telehealth; arrange in-person care. |
| `ClinicalReview` | `SafetyClinicalReviewRequired` | The uncertain answer cannot pass automatically; a future separately authorized review is required. |
| `TelehealthEligible` | `SafetyScreenPassed` | The universal screen did not identify one of its stop conditions; all later intake and eligibility gates remain unavailable. |

## 4. Acceptance evidence

1. Only the correct branded host, practice/facility, applicant access key, `IdentityReviewApproved` state/version, `NoCandidate` disposition, and `ApprovedForProspectiveIntake` decision can submit.
2. All four safety answers are nullable at the contract boundary and required by normalization; missing answers fail without an evaluation or state change.
3. Current location is explicitly confirmed and must be GA, CA, or FL; residence is not used as an implicit location answer.
4. Emergency always wins; urgent wins over hands-on/unsure; hands-on wins over unsure; unsure routes to clinical review; only all explicit negative answers pass the screen.
5. Exact retry returns one immutable evaluation; changed content, stale version, second semantic command, and concurrent first writers produce no duplicate evidence.
6. Public applicant responses expose only terminal state and fixed next action, never raw answers, review actor/reason, possible candidate, access key/hash, evidence fingerprint, or canonical identifier.
7. Recording changes only the applicant aggregate plus one safety-evaluation row and one event; patient, portal, insurance, consent, intake, request, queue, appointment, encounter, prescription, claim, message/task/notification, integration, and external-call rows remain unchanged.
8. Component and cross-browser tests cover keyboard/radio semantics, focus recovery, ambiguous retry with one command identity, 320 px reflow, serious automated WCAG findings, immediate emergency links, and no clinical answers in local/session storage.

## 5. Exit boundary

Sprint 15 ends at one prospective safety state. Complaint/purpose collection, condition-specific triage, identity proofing, insurance/network verification, consent, patient promotion/linkage, practice acceptance, request creation/reassociation, and queue entry remain unavailable and require separately authorized workflows and evidence.
