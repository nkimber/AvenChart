# Sprint 16: synthetic prospective visit-purpose classification

Status: Approved for bounded implementation by [TH-DEC-0019](../decisions/0019-approved-sprint-16-prospective-visit-purpose.md)  
Scope: Applicant-owned controlled migraine/sleep visit-purpose selection after a passing universal safety screen; no free text, complaint-specific triage, clinical eligibility, identity proofing, patient promotion/linkage, insurance, consent, request, queue, care, downstream action, external integration, production use, or real PHI

## 1. Outcome

Allow a synthetic prospective applicant in `SafetyScreenPassed` to identify whether the demonstration visit concerns migraine or sleep. Record the classification immutably and stop at `VisitPurposeRecorded`; do not infer that either category is clinically eligible or treatable by telehealth.

## 2. Stories

| ID | Story |
|---|---|
| `TH-SP16-001` | Add one append-only prospective visit-purpose record and constrained `SafetyScreenPassed -> VisitPurposeRecorded` event with safety-evaluation provenance and hard-false consequential flags. |
| `TH-SP16-002` | Add an access-key-owned service that accepts only `migraine` or `sleep`, rebinds the current passing safety screen and applicant version, and never calls a clinical evaluator. |
| `TH-SP16-003` | Publish a typed private/no-store idempotent applicant purpose route with opaque not-found, bounded Problem Details, and no staff/patient session substitution. |
| `TH-SP16-004` | Extend the prospective entry with an accessible two-choice purpose form, explicit non-eligibility language, immediate emergency direction, stable retry/reload, and no purpose persistence. |
| `TH-SP16-005` | Preserve coarse applicant resume state and fixed next action without exposing safety answers/provenance, identity-review evidence, access credentials, or canonical identifiers. |
| `TH-SP16-006` | Prove category allowlisting, no-free-text contract, state/access/version isolation, exact replay, contention, append-only evidence, zero patient/downstream delta, accessibility, migration, Graphify, and full regression. |

## 3. Category mapping

| Category | Applicant-facing label | Meaning in this slice |
|---|---|---|
| `migraine` | Headache or known migraine pattern | Demonstration navigation classification only; no diagnosis, red-flag protocol, or eligibility decision. |
| `sleep` | Sleep difficulty | Demonstration navigation classification only; no insomnia diagnosis, mental-health/somnolence protocol, or eligibility decision. |

No other value, synonym, custom text, or client-provided label is accepted. The server owns display labels and limitations.

## 4. Acceptance evidence

1. Only the correct branded host, practice/facility, applicant access key, unexpired `SafetyScreenPassed` state/version, `NoCandidate` disposition, approved identity-review decision, and `TelehealthEligible` universal safety evaluation can submit.
2. The request schema contains only expected version, controlled category, and synthetic confirmation; unknown JSON properties do not create stored data and arbitrary categories fail without a state/evidence delta.
3. The service maps the exact normalized category to a fixed display label and never invokes `ITelehealthTriageEvaluator` or returns a clinical outcome.
4. Exact retry returns one immutable purpose; changed content, stale version, second semantic command, and concurrent first writers create no duplicate evidence.
5. Public applicant responses expose only coarse state/fixed next action and never safety answers, purpose fingerprints, review actor/reason, possible candidate, access key/hash, or canonical identifier.
6. Recording changes only the applicant aggregate plus one purpose row and event; patient, portal, insurance, consent, intake completion, clinical assessment, request, queue, appointment, encounter, prescription, claim, message/task/notification, integration, and external-call rows remain unchanged.
7. Component and cross-browser tests cover keyboard/radio semantics, focus recovery, ambiguous retry with one command identity, 320 px reflow, serious automated WCAG findings, persistent emergency links, explicit non-eligibility content, and no purpose in local/session storage.

## 5. Exit boundary

Sprint 16 ends at a controlled visit-purpose classification. Detailed complaint/symptom collection and complaint-specific clinical triage require licensed clinical-owner approval. Identity proofing, insurance/network verification, consent, patient promotion/linkage, practice acceptance, request creation/reassociation, and queue entry remain unavailable and separately gated.
