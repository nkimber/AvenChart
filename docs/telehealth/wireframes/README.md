# Telehealth low-fidelity wireframes

Status: Approved design input; not production UI  
Visual sheet: [telehealth-wireframes.html](telehealth-wireframes.html)  
Decision baseline: [Decision 0001](../decisions/0001-g0-development-baseline.md)

## 1. Purpose

These wireframes settle information hierarchy, navigation, safety-action placement, queue status, role separation and basic responsive behavior before React implementation. They intentionally avoid final brand artwork, vendor-specific video controls, exact clinical/legal language and polished visual design.

The HTML sheet is a static, keyboard-readable document. Buttons and fields illustrate intended controls but do not submit data. Each frame has a stable screen ID for backlog, test and design references.

## 2. Patient frames

| Screen ID | Screen | Critical decisions represented | Primary requirements |
|---|---|---|---|
| `PAT-01` | Practice-branded landing | Practice is provider; AvenChart is technology; states/hours/scope; emergency action before intake | `TEL-PROD-001..003`, `TEL-PRA-001..002`, `TEL-UX-007` |
| `PAT-02` | Current location and safety | Current physical location/callback first; direct emergency option; privacy/disconnection information | `TEL-PROD-004..006`, `TEL-TRI-001..004` |
| `PAT-03` | Complaint and triage | Patient words, uncertainty option, progress without promising eligibility, emergency action persistent | `TEL-TRI-003..013`, `TEL-UX-006..010` |
| `PAT-04` | Insurance and estimate | Eligibility and network separated; timestamp/source limits; self-pay and estimate; no guarantee | `TEL-INS-001..016` |
| `PAT-05` | Practice review and queue | Review before queue; approximate position only after acceptance; last update, cancel and worsening action | `TEL-PRA-005..014`, `TEL-WF-001..014` |
| `PAT-06` | Waiting room and video | Identity/location confirmation, participant roster, device status, reconnect/emergency plan, no recording | `TEL-CON-002..003`, `TEL-VID-001..015` |
| `PAT-07` | After-visit summary | Disposition, plan, prescription status, follow-up/warning signs and claim status remain distinct | `TEL-CON-006..013`, `TEL-RX-014`, `TEL-CLM-016` |

## 3. Administrator frames

| Screen ID | Screen | Critical decisions represented | Primary requirements |
|---|---|---|---|
| `ADM-01` | Operational work queue | Gate status/freshness, blockers/owner, no clinical editing, accessible table/filter | `TEL-ACT-003..006`, `TEL-PRA-005..006`, `TEL-UX-014` |
| `ADM-02` | Request review | Read-only clinical outcome, identity/duplicate/consent/financial/technology evidence, reasoned hold/decline/authorize | `TEL-WF-006..009`, `TEL-PRA-003..006` |

## 4. Physician frames

| Screen ID | Screen | Critical decisions represented | Primary requirements |
|---|---|---|---|
| `PHY-01` | Telehealth shift and next patient | Explicit available state, eligibility result, no browse-and-pick, capacity/paused state | `TEL-PROD-011..012`, `TEL-PRA-007..008` |
| `PHY-02` | Consultation workspace | Patient/location/safety banner, provenance, chart/intake/triage, video and documentation in one task | `TEL-PROD-013..016`, `TEL-CON-001..016` |
| `PHY-03` | Prescription and close visit | Medication/allergy safety, patient-selected pharmacy, optional prescription, disposition/AVS/signing | `TEL-RX-001..016`, `TEL-CON-006..016` |

## 5. Interaction notes

- Emergency and worsening-symptom actions remain visually and programmatically available throughout intake and queue states.
- Queue position does not appear until operational acceptance. Before then the patient sees the responsible owner and next action.
- Realtime text uses a polite live region in implementation; urgent safety/error content uses an alert only when action is required.
- Administrator screens never offer an edit or override control for triage outcome, clinical priority, diagnosis or treatment.
- Physician queue exposes next eligible work, not a patient list for cherry-picking. Patient identity/chart detail appears only after reservation.
- All consequential actions identify the patient/request and require authoritative server confirmation; visual “success” is not optimistic.
- Mobile layouts stack primary content before secondary detail. Tables become labeled row groups or controlled horizontal tables without hiding actions.
- Exact state, consent, clinical, financial and emergency wording must come from approved versioned content packages.

## 6. Accessibility acceptance for implementation

The implemented frames must support keyboard-only completion, meaningful heading/landmark structure, persistent visible focus, correct field/error relationships, 320 CSS pixel reflow, 400% zoom, non-color state labels, screen-reader-safe status updates, target sizes, reduced motion, device permission recovery and accessible generated documents. The HTML wireframe is a design aid, not WCAG conformance evidence for the eventual application.

