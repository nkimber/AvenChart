# Pilot D — Frontend accessibility and failure recovery

## Packet

- Baseline: `d77a8320e6751a2deb2daf14cf1ac5d6b00cb989`
- Coverage sampled: COV-011, COV-012, COV-013, COV-014
- Workflows: clinician shell and laboratory queue; patient portal shell and appointment-request dialog
- Reviewers: coordinator acting in the quality/accessibility role; independent frontend/accessibility reviewer
- Required human validation: accessibility specialist and users with disabilities before any conformance claim

## Independent pass 1

The first pass used static interaction tracing, focused component tests, and a production build. It did not use a screen reader or claim WCAG conformance.

Commands and results:

```text
npm test -- src/pages/portal/PortalAppointments.test.tsx src/pages/portal/PortalShell.test.tsx src/pages/clinician/ClinicianShell.test.tsx src/pages/clinician/LabQueue.test.tsx
4 files and 12 tests passed

npm run build
Production build and bundle budget passed
```

Material strengths:

- the clinician mobile drawer moves focus inside, contains tab focus, closes on Escape, restores focus, and prevents background scrolling;
- the portal appointment dialog applies equivalent initial focus, Tab containment, Escape handling, focus restoration, and `aria-modal` semantics;
- clinician and portal loading and page-level failure states commonly use live regions, alerts, and explicit retry behavior;
- the accessibility browser suite covers many public, clinician, portal, and dynamic-state routes rather than one static page;
- the focused component tests and production build pass.

Candidate conditions from pass 1:

| Candidate | Initial severity | Confidence | Evidence | Specialist need |
| --- | --- | --- | --- | --- |
| The automated accessibility gate does not demonstrate the adopted WCAG 2.2 AA target because it selects only WCAG 2.0/2.1 tags and discards all but serious/critical axe results. | Medium, systemic | High | `avenchart-ui/e2e/accessibility.spec.ts:18-31`; `docs/phase-2/quality-standard.md` | Accessibility |
| A visible skip-link style exists but neither React shell renders a keyboard bypass link; the portal content container is also a generic `div` rather than a `main` landmark. | Medium, repeated | High for source; manual effect unvalidated | `avenchart-ui/src/index.css:9414-9434`; no `skip-link` use under `avenchart-ui/src`; `PortalShell.tsx:238-304`; `ClinicianShell.tsx:613` | Accessibility and intended users |
| An asynchronous appointment-request submission error is rendered without an alert or live status, even though adjacent option-load and page-load errors use `role="alert"`. | Medium, isolated | High for source; assistive-technology effect unvalidated | `PortalAppointments.tsx:297-330,407,565,604`; submission error has no focused component test | Accessibility |

## Independent pass 2

The independent reviewer selected matched clinician and portal sign-in/session-failure paths, with the reference UI as a comparator. In addition to source tracing, the reviewer used keyboard-only Chromium interaction against the Vite UI with the API deliberately unavailable and checked accessibility-tree output, active focus, live regions, and 320×568 reflow. No horizontal overflow was present on the two modern login pages.

Additional strengths:

- both modern login forms expose native labels, correct control roles/names, and appropriate autocomplete purposes;
- clinician and portal shells fail closed while session validation is unresolved and provide explicit retry/sign-out recovery;
- the clinician drawer behavior has a focused component test;
- the portal shell does not render protected child content after session-validation failure.

Candidate conditions from pass 2:

| Candidate | Initial severity | Confidence | Evidence | Specialist need |
| --- | --- | --- | --- | --- |
| Failed clinician and portal sign-in disables the focused submit button, leaves focus on `body`, and inserts a plain error banner with no alert, live region, focus movement, or field association. The same plain status-banner pattern exists in the reference UI. | Medium, repeated | High; reproduced in Chromium | `ClinicianLogin.tsx:19-41,77-108`; `PortalLogin.tsx:19-40,76-107`; `avenchart/frontend/src/App.tsx:6818-6822,7414-7418` | Accessibility and assistive-technology users |
| The automated gate does not enforce the adopted WCAG 2.2 target, omits the reference UI, uses only Chromium despite configured Firefox/WebKit projects, is not part of the normal verification workflow, and retains no current conformance evidence. | Medium, systemic | High | `accessibility.spec.ts:19-29,111-648`; `Test-AvenChartUiAccessibility.ps1:28-46`; `playwright.config.ts:18-23`; `.github/workflows/verify.yml:42-50`; reference `package.json` | Accessibility |
| The document-level skip link targets `#main-content`, but neither modern login page renders that target. In Chromium, activating the link changed the hash but left focus on the link. | Low, repeated on login | High; reproduced in Chromium | `avenchart-ui/index.html:16`; `ClinicianLogin.tsx:44-112`; `PortalLogin.tsx:43-111` | Accessibility and keyboard users |

## Reconciliation

The reviewers independently agreed that the automated accessibility control does not substantiate the adopted WCAG 2.2 AA target. They also found the same underlying dynamic-status weakness in different workflows: pass 1 found an unannounced appointment-submission error; pass 2 reproduced silent failed sign-in in both modern applications and located the pattern in the reference UI. This supports a repeated condition rather than an isolated appointment defect.

The bypass-block conclusion was materially improved by independent review. Pass 1 found no skip-link use inside React source and initially described the shells as lacking a rendered bypass link. Pass 2 located the global link in `avenchart-ui/index.html`, reproduced its missing login target, and confirmed that the authenticated clinician target is valid while the portal target is a generic non-focusable container. The reconciled condition is therefore narrower: the mechanism exists but is inoperative on both login routes, while authenticated portal behavior remains a manual-validation question.

## Independent verification

The verifier, who did not author either pass, reached these dispositions:

| Cluster | Verifier disposition | Reconciled severity/confidence |
| --- | --- | --- |
| Automated accessibility evidence does not enforce the adopted target | Corroborated and systemic | Medium; high confidence |
| Failed clinician and portal sign-in is not programmatically announced and loses focused-button context | Corroborated and reproduced in Chromium | Medium and repeated; high Chromium confidence |
| Appointment submission error | Partially corroborated and narrowed: an application-level `created=false` result uses only the plain banner, while a thrown/network error also emits a live status toast | Medium and isolated; high source confidence, assistive-technology effect unvalidated |
| Login skip target | Corroborated | Low and repeated on login; high Chromium confidence |
| Authenticated portal skip target | Corroborated as a generic non-focusable container; clinician shell is a valid counterexample | Medium candidate; browser/assistive-technology validation still required |

Material agreement is acceptable with the appointment-error narrowing preserved. Manual browser zoom/reflow, high-contrast, reduced-motion, screen-reader, speech-input, authenticated route-transition, and intended-user exercises remain required evidence; automated axe output alone cannot close this pilot or support conformance.
