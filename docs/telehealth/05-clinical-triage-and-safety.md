# Clinical eligibility, triage, and safety model

## 1. Clinical purpose and limits

Triage determines the safest next workflow, not a diagnosis and not a guarantee of treatment. It must prioritize sensitivity for time-critical conditions, direct patients to an appropriate level of care, identify when telehealth is insufficient, and provide an auditable basis for medical-director governance.

The initial engine is deterministic. It uses approved questions, typed answers, explicit rules, and versioned outcomes. It must not use a generative model, opaque risk score, or autonomous diagnostic recommendation.

## 2. Outcome model

| Outcome | Meaning | Product behavior |
|---|---|---|
| `Emergency` | Reported information may indicate an immediate threat to life, limb, vision, or safety | Stop intake; prominently advise 911/nearest emergency department or 988 where appropriate; show current location/callback and emergency plan; record acknowledgment/action without delaying for insurance |
| `UrgentInPerson` | Same-day or defined-time physical evaluation/testing is needed | Stop telehealth queue; state timeframe and appropriate site (urgent care, office, ED if worsening); offer practice contact/location help where configured |
| `ClinicalReview` | Information is incomplete, uncertain, conflicting, higher-risk, or requires clinician judgment | Place in a qualified reviewer queue; no administrator may clear it; patient sees review status and worsening guidance |
| `TelehealthEligible` | Current complaint fits an approved low-acuity pathway and no disqualifier/red flag was found | Continue intake and operational prerequisites; physician may still redirect after evaluation |
| `Unsupported` | Not necessarily emergent, but outside the practice's telehealth service/protocol | Explain limitation, avoid diagnosing, and provide appropriate alternative care route |

Missing, `not sure`, inconsistent, or stale safety information maps to `ClinicalReview` or a more protective outcome. It never defaults to eligible.

## 3. Ordered assessment

1. **Location and rescue context:** physical location, callback, ability/privacy to communicate, disconnection plan, age, supported state.
2. **Universal emergency screen:** immediate symptoms/signs and patient belief that this may be an emergency.
3. **Chief complaint selection:** patient chooses a symptom pathway and describes concerns in their own words.
4. **Complaint-specific red flags:** onset, severity, duration, progression, associated symptoms, objective data if available, and pathway exclusions.
5. **Risk modifiers:** pregnancy/postpartum when relevant, immune compromise, active cancer treatment, recent surgery/hospitalization, significant comorbidities, age threshold, medication risks, prior diagnosis/treatment, substance use, and inability to provide reliable history.
6. **Remote-exam sufficiency:** required camera view, patient participation, language/accessibility support, home measurements only where validated, and whether missing physical examination/testing makes video inappropriate.
7. **Freshness and consistency check:** evaluate changed answers, elapsed time, location change, and contradictions.
8. **Outcome and communication:** return exactly one outcome plus reason codes, patient guidance, and required next action.

## 4. Universal emergency screen

The medical director must approve exact wording, logic, reading level, translations, and care destinations. The initial clinical-content workshop must cover at least:

- severe trouble breathing, blue/gray color, choking, or inability to speak normally;
- chest pressure/pain or equivalent concerning symptoms, especially with shortness of breath, sweating, fainting, or radiation;
- new facial droop, one-sided weakness/numbness, speech difficulty, severe confusion, seizure, loss of consciousness, or sudden severe neurologic change;
- a sudden “worst” headache, headache with new neurologic deficit, significant head injury, stiff neck/high fever, severe eye pain/vision loss, or pregnancy/postpartum danger signs;
- uncontrolled bleeding, severe allergic reaction, major injury/burn, poisoning/overdose, or rapidly worsening severe pain;
- signs concerning for sepsis or shock, including severe illness with confusion, clammy skin, very fast breathing/heart rate, or marked weakness;
- suicidal intent, imminent self-harm/harm to others, inability to remain safe, or acute behavioral crisis; and
- the patient's statement that the situation is an emergency or that they cannot safely wait.

This list defines design coverage, not approved patient wording or an exhaustive diagnostic list. A protocol cannot be published until a licensed clinical owner validates it against current evidence and local emergency resources.

## 5. Candidate initial symptom pathways

These are complaint-based candidates for medical-director approval. Each protocol must define inclusion, exclusions, red flags, required answers, optional evidence, exam sufficiency, outcome, and follow-up—not merely list a diagnosis.

| Pathway | Potentially appropriate examples | Common reasons for review/in-person/emergency routing |
|---|---|---|
| Headache / known migraine pattern | Mild/moderate recurrence similar to a previously evaluated pattern, no red flags | First/worst/sudden onset, neurologic signs, trauma, fever/stiff neck, pregnancy/postpartum, concerning blood pressure, vision loss, immunocompromise/cancer treatment, persistent vomiting |
| Sleep difficulty | Short-duration insomnia or sleep-hygiene concern without acute risk | Suicidality, mania/psychosis, dangerous somnolence, suspected severe sleep apnea, substance withdrawal, pregnancy/complex medication issue, controlled-sedative request |
| Upper respiratory / sinus / cough | Mild stable symptoms without breathing distress | Hypoxia if reliable measurement, severe dyspnea, chest pain, dehydration, high-risk comorbidity, prolonged/worsening course, need for physical exam/testing |
| Sore throat | Low-risk symptoms where video/history can support disposition | Airway/swallowing difficulty, drooling, neck swelling, severe dehydration, concerning rash, immune compromise, testing/exam needed under protocol |
| Urinary symptoms | Narrow adult uncomplicated lower-urinary presentation | Pregnancy, male/complex anatomy as configured, fever/flank pain, vomiting, sepsis signs, recurrent/resistant infection, kidney disease, STI concern requiring tests/exam |
| Rash / minor skin concern | Localized, stable, camera-visible rash/lesion | Rapid spread, mucosal involvement, severe pain, fever/systemic symptoms, face/eye involvement, major burn, purpura, anaphylaxis, image quality insufficient |
| Eye redness | Narrow non-traumatic mild symptoms with preserved vision | Pain, photophobia, visual change, trauma/chemical exposure, contact-lens high risk, severe swelling, neurologic signs |
| Mild gastrointestinal symptoms | Short-duration mild nausea/diarrhea/reflux-type concern | Severe/localized pain, blood/black stool, persistent vomiting, dehydration, pregnancy, jaundice, rigid abdomen, high-risk history |
| Minor musculoskeletal concern | Mild atraumatic strain with preserved function | Deformity, major trauma, neurovascular deficit, inability to bear weight, joint infection signs, possible clot, severe swelling/pain |
| Established medication refill | Stable non-controlled medication with adequate chart/history and monitoring | Controlled drug, unsafe lapse, required labs/exam absent, pregnancy risk, contraindication/interactions, medication not previously established |

Active cancer treatment, major immunocompromise, recent hospitalization/surgery, significant pregnancy/postpartum concerns, and other complex contexts default to clinical review or unsupported unless a specifically approved pathway states otherwise. A patient with a serious chronic diagnosis is not labeled an emergency solely because of that diagnosis; the pathway evaluates the current concern and risk context safely.

## 6. Protocol definition and evaluation

Each immutable protocol version contains:

- practice/service/state scope, owner, approvers, status, effective/retirement times, evidence references, and review-by date;
- localized question/content versions and validated reading level;
- typed fields (`boolean`, coded single/multi-select, bounded number/unit, date/time, short text) and permitted `unknown` behavior;
- rule priority, condition tree, reason code, terminal/nonterminal action, outcome, and patient/staff/clinician content key;
- prerequisite and invalidation dependencies;
- clinical-review triggers, required reviewer qualification, response target, and expiration;
- required remote-exam/technology capabilities and allowed fallback;
- follow-up and safety-net content; and
- executable fixtures covering every rule, boundary, unknown value, conflicting combination, and no-match behavior.

Rules are evaluated server-side over a canonical answer snapshot. The result stores protocol/version checksum, answer snapshot checksum, every fired rule in evaluation order, final outcome/reasons, timestamp, engine version, and superseded assessment link. Client logic may improve usability but cannot confer eligibility.

The existing AvenChart clinical-form engine may render and validate versioned questions. Its current `show`, `hide`, `require`, `warning`, and `calculate` behaviors are not a clinical triage decision engine. Telehealth requires a dedicated `TelehealthTriageProtocol` evaluator and immutable `TriageAssessment` aggregate.

## 7. Clinical review and deterioration

- A clinical reviewer sees source answers, fired rules, timestamps, patient location, relevant chart context, and any communication—not an editable facsimile of the patient's response.
- The reviewer may ask a bounded clarification, create a new assessment, or choose a supported outcome with reason and narrative. The original assessment remains immutable.
- Clinical review has a configured response target and expiry. If the practice cannot review safely in time, the patient receives an alternative care route.
- While waiting, the patient can report new/worsening symptoms at any time. Periodic prompts repeat critical guidance and can require a fresh abbreviated safety screen.
- At consultation start, the physician repeats identity/location and key red flags. A new concern creates a new assessment/disposition.

## 8. Clinical requirements

| ID | Requirement | Acceptance evidence |
|---|---|---|
| TEL-TRI-001 | Triage MUST execute before insurance/payment/queue work and return exactly one governed outcome. | Ordered integration tests. |
| TEL-TRI-002 | The engine MUST be deterministic, server-authoritative, versioned, explainable, and free of generative/opaque clinical decision logic in the initial release. | Architecture review and golden-fixture replay. |
| TEL-TRI-003 | Universal emergency rules MUST be evaluated for every request regardless of selected complaint. | Per-path emergency fixture tests. |
| TEL-TRI-004 | `Unknown`, missing, inconsistent, out-of-range, or stale safety answers MUST route to review or a higher-acuity outcome, never eligibility. | Property/boundary tests. |
| TEL-TRI-005 | A protocol MUST NOT be published without medical-director approval, clinical evidence references, executable fixtures, effective dates, review date, and rollback/retirement plan. | Protocol publication gate test. |
| TEL-TRI-006 | A published protocol version MUST be immutable; changes create a new version and never alter a historical assessment. | Database and replay tests. |
| TEL-TRI-007 | Every assessment MUST retain the exact answer and rule evidence needed to reproduce its result. | Reproducibility test. |
| TEL-TRI-008 | Administrators MUST NOT change clinical answers, outcomes, priority, or reviewer decisions. | Authorization and UI tests. |
| TEL-TRI-009 | A clinical override/review decision MUST require an eligible clinician, permitted outcome, reason code, narrative, and new assessment record. | Reviewer workflow test. |
| TEL-TRI-010 | Emergency content MUST use direct action language, current location-aware resources, 911/ED guidance and 988 where appropriate, without requiring additional form completion. | Clinical/content/accessibility review. |
| TEL-TRI-011 | The product MUST record emergency guidance shown, delivery channel, patient acknowledgment or inability to acknowledge, and any staff escalation; it MUST NOT claim emergency dispatch unless confirmed. | Audit and negative-claim test. |
| TEL-TRI-012 | Eligibility MUST expire on configured time, location change, material answer/history change, or clinical deterioration. | Time/location invalidation tests. |
| TEL-TRI-013 | Remote-exam sufficiency MUST be explicit per pathway; lack of required video/measurement/testing MUST route safely. | Device/capability pathway tests. |
| TEL-TRI-014 | The physician MUST retain authority to redirect an eligible request when the actual evaluation is unsuitable or below the applicable standard of care. | Encounter disposition test. |
| TEL-TRI-015 | Clinical performance MUST be monitored for emergency under-triage, avoidable over-triage, review rate, pathway exits, adverse events, and disparities, with case review rather than automated rule tuning. | Clinical quality dashboard and review minutes. |
| TEL-TRI-016 | Translations and content changes MUST be clinically reviewed as protocol content; fallback to an unapproved language MUST not create eligibility. | Localization governance tests. |

## 9. Safety-case evidence before pilot

For every enabled pathway, the medical director must approve a safety case containing intended population, exclusions, foreseeable harms, red-flag coverage, remote-exam limitations, evidence basis, validation fixtures, reviewer workflow, monitoring thresholds, patient instructions, and rollback trigger. Synthetic cases must include ambiguous language, accessibility needs, interrupted intake, changing location, and deteriorating symptoms.

