# User experience, content, and accessibility

## 1. Experience principles

- Lead with safety, provider identity, current status, and the next action.
- Use plain language; “eligible for a video evaluation” is not “diagnosed” or “guaranteed treatment.”
- Preserve patient control: save, go back where safe, correct, cancel, request help, report worsening symptoms, and choose pharmacy/financial route.
- Never use urgency, queue position, insurance uncertainty, prescription expectations, or practice branding to coerce consent or payment.
- Show authoritative timestamps, source/provenance, expirations, uncertainty, and recovery.
- Design mobile-first while retaining an efficient keyboard/screen-reader staff and physician workflow.

WCAG 2.2 Level AA is the minimum conformance target for patient, staff, and physician experiences, including branded variants and provider-embedded components. Accessibility is tested with automated tools and manual keyboard, screen reader, zoom/reflow, contrast, speech/cognitive, captions/communication, and error-recovery scenarios.

## 2. Patient screen map

| Screen | Required content/actions |
|---|---|
| Practice landing | Practice/provider identity, AvenChart role, states/hours, scope/limitations, price/insurance framing, emergency/911/988 guidance, privacy/accessibility/help, sign-in/new patient |
| Account/proofing | Progress, why data is needed, verified contact, secure proofing, accessible manual/redress path, no candidate record disclosure |
| Location and safety | Current physical location/callback, privacy/safe-to-talk, emergency/disconnection plan, direct emergency actions before other forms |
| Complaint/triage | One question group at a time or accessible review pattern, purpose and uncertainty options, back/correct behavior, visible emergency route |
| Details confirmation | Demographics, contacts, allergies, medications, relevant history, clear source/pending-change labels |
| Consent/notices | Short summary plus full accessible content, distinct required/optional choices, download, version/state/practice identity |
| Insurance/network | Select/add coverage, confirm OCR fields, eligibility versus network distinction, timestamps, manual/unknown state, no guarantee |
| Estimate/financial route | Charge/responsibility range and unknowns, assumptions, self-pay/GFE, policies, affirmative acknowledgment |
| Device check | Camera/mic/speaker/browser/network checks, permissions explanation, device switch, keyboard/screen-reader help, retry/support |
| Review/submit | Complete summary, edit links, gate/expiry visibility, no false promise of acceptance |
| Practice review | Honest owner/status, last update, expected next update, cancel/help/worsening/emergency |
| Queue | Approximate position/wait band, connection freshness, notifications, stay/leave, worsening symptoms, service closure guidance |
| Waiting room | Assigned physician when allowed, participant/device state, privacy, consent/location reconfirmation, reconnect/callback guidance |
| Consultation | Large media, clear camera/mic/leave controls, participant roster, optional accessible chat, connection quality and recovery |
| After visit | Accessible AVS, prescription/pharmacy accurate status, tests/referrals/follow-up, warning signs/emergency, claim/financial status separate |

Progress indicators communicate completed/current/remaining stages without implying a safety or insurance pass. Browser back, refresh, multi-tab, session expiry, and interrupted network must restore safely from server state.

## 3. Staff and physician design

Queues are real semantic tables/lists with meaningful headings, keyboard navigation, filters that do not hide urgent work by default, saved views scoped to user, result count, last-updated/stale state, and no color-only statuses. Bulk actions are forbidden for clinical outcomes, queue authorization, prescriptions, signing, and claim submission unless separately specified and safety reviewed.

The physician workspace prioritizes patient/location banner, triage red flags, allergies/medications, current concern, time/freshness, video join, and documentation. Source labels distinguish patient-entered, chart, staff-verified, payer-returned, and clinician-confirmed data. Alerts are specific, actionable, non-duplicative, keyboard reachable, and preserve rationale; severe safety alerts cannot be hidden by a generic dismiss.

Destructive or consequential actions show the exact patient/request and result, require confirmation when error is plausible, and prevent double submit. Success messages are tied to the server response and never optimistic for external business acceptance.

## 4. Realtime and error content

Use an `aria-live="polite"` region for ordinary queue/status changes and `role="alert"` only for urgent failure/safety information. Do not announce every countdown/poll. Preserve focus after updates; move focus only after user-triggered navigation or a blocking safety route. Provide a visible last-updated time and manual refresh.

Errors contain what happened, what was preserved, what the patient/user should do, whether retry is safe, and a correlation code. Examples:

- “We saved your answers, but the practice could not verify your plan right now. Your request has not entered the clinician queue. You can retry or ask the practice to review it.”
- “Your video disconnected. Your visit is still open. Reconnect now. If your symptoms are getting worse, call 911 for an emergency or use the practice callback shown here.”
- “The prescription was sent, but the pharmacy has not confirmed it. Contact the practice if the pharmacy cannot find it.”

Avoid “Something went wrong” as the only message, raw error codes, false success, or endless spinners.

## 5. Content rules

| Avoid | Use |
|---|---|
| “You qualify for treatment” | “Your answers can be evaluated by video; the physician may recommend other care.” |
| “Insurance approved” | “Coverage appears active as of [time]. Network status: [state]. This does not guarantee payment.” |
| “Finding your doctor” before acceptance | “Your practice is reviewing your request.” |
| Exact wait promise | “About 2–4 requests are ahead of you; this may change for clinical or operational reasons.” |
| “Prescription sent successfully” on HTTP 200 | “Queued for electronic delivery” / “Accepted by the pharmacy network” according to evidence |
| “Claim paid” on 999/277CA | “Claim received/accepted for processing”; payment only after remittance |
| Diagnosis labels in triage | Symptom pathway and level-of-care outcome |

Clinical/legal/financial strings are keys to approved, versioned content packages, not developer-authored literals scattered through UI code.

## 6. Accessibility requirements

| ID | Requirement | Acceptance evidence |
|---|---|---|
| TEL-UX-001 | All telehealth experiences and brand variants MUST meet WCAG 2.2 AA with documented exceptions/remediation blocked before production. | Automated and manual conformance report. |
| TEL-UX-002 | Every function MUST work by keyboard with logical focus order, visible focus, no traps, skip/navigation aids, and safe focus restoration after updates/errors. | Keyboard test matrix. |
| TEL-UX-003 | Controls, fields, tables, dialogs, status, errors, progress, media, and notifications MUST have correct accessible names/roles/states/relationships. | Accessibility tree/screen-reader tests. |
| TEL-UX-004 | Content MUST reflow at 320 CSS pixels and 400% zoom without loss, overlap, two-dimensional scrolling except essential tables/media, or hidden actions. | Reflow/zoom visual tests. |
| TEL-UX-005 | Text/non-text contrast, target size, spacing, motion, orientation, timeout, flashing, and color use MUST satisfy WCAG 2.2 AA and preserve safety meaning without color alone. | Design-token and manual tests. |
| TEL-UX-006 | Forms MUST expose instructions, required/optional status, formats, constraints, field/group errors, summary links, preserved values, and correction without re-entry. | Form/error tests. |
| TEL-UX-007 | Emergency and worsening-symptom actions MUST remain prominent, accessible, and available throughout intake/queue/waiting; they MUST not be buried in terms or disabled by a failure. | Safety-action journey tests. |
| TEL-UX-008 | Realtime updates MUST use measured live-region behavior, preserve focus, support polling/manual refresh, and avoid repetitive announcements. | Screen-reader/realtime tests. |
| TEL-UX-009 | Video controls MUST support accessible device setup, captions/provider capabilities where approved, participant awareness, chat, device switching, reconnect, and an accessible non-video support route. | Video accessibility certification. |
| TEL-UX-010 | Patient content MUST target plain language, define unavoidable terms, state uncertainty, and pass clinical/legal/content review; translations require equivalent review. | Content/readability/localization approval. |
| TEL-UX-011 | Status wording MUST correspond exactly to authoritative domain/business state and MUST not imply diagnosis, acceptance, coverage, delivery, payment, or dispensing without evidence. | State-content contract tests. |
| TEL-UX-012 | Timeouts MUST warn users, allow extension where safe, preserve completed data, explain freshness rechecks, and not create accidental cancellation. | Session/time-travel tests. |
| TEL-UX-013 | Authentication/proofing MUST provide accessible alternatives and redress without lowering security through inaccessible recovery. | Disability-inclusive identity tests. |
| TEL-UX-014 | Queue and clinician views MUST expose freshness, blockers, provenance, permitted actions and concurrency conflicts without relying on hover or color. | Staff UX tests. |
| TEL-UX-015 | Consequential actions MUST prevent duplicate activation, identify the subject/action, and provide undo/correction where clinically/legally possible. | Double-click/stale/confirmation tests. |
| TEL-UX-016 | Notifications MUST honor consent/preference, use minimum necessary preview content, link to authenticated current state, and never use sensitive diagnosis/medication text in lock-screen content. | Notification privacy tests. |
| TEL-UX-017 | Downloaded consent/AVS/estimate documents MUST be tagged/structured, readable, keyboard accessible, and usable at zoom/print. | Document accessibility tests. |
| TEL-UX-018 | Analytics/session-replay tools MUST not capture PHI; accessibility/UX metrics use approved non-PHI events. | Client telemetry inspection. |
| TEL-UX-019 | Supported language, interpreter, hearing/vision/mobility/cognitive needs and preferred communication accommodations MUST be captured and carried to staff/physician workflows. | Accommodation journey tests. |
| TEL-UX-020 | Usability testing MUST include patients with disabilities, low digital/health literacy, mobile-only access, slow networks, and English-language limitations before broad release. | Research report and remediation closure. |

## 7. Responsive and compatibility baseline

Patient workflows support current major mobile/desktop browsers approved by the video provider and practice device policy. Staff/physician workflows support the approved managed-browser baseline. Unsupported clients receive a useful explanation and alternative contact before sensitive data entry. Compatibility is based on tested capability, not user-agent assumptions alone.

