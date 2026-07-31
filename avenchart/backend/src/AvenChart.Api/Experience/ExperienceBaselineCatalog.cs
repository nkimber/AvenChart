// SPDX-FileCopyrightText: 2026 Neil Kimber and Legacy EHR Modernization Project contributors
// SPDX-License-Identifier: GPL-3.0-or-later

namespace AvenChart.Api.Experience;

public static class ExperienceBaselineCatalog
{
    public const string Revision = "local-experience-baseline-v1";

    private static readonly IReadOnlyList<ExperienceRole> Roles =
    [
        new(
            "administrator",
            "Practice administrator",
            "Configuration, access evidence, reporting, and operational oversight"),
        new(
            "clinician",
            "Clinician",
            "Schedule, patient chart, encounter, result, referral, authorization, and message work"),
        new(
            "front-desk",
            "Front desk",
            "Scheduling, registration, patient search, and communication intake"),
        new(
            "patient",
            "Patient or authorized portal user",
            "Portal identity, appointments, messages, records, and account review"),
    ];

    private static readonly IReadOnlyList<ExperienceEnvironment> Environments =
    [
        new(
            "desktop-chromium",
            "Chromium",
            "Desktop",
            "Desktop Chrome profile",
            ["route-smoke", "accessibility", "material-workflows", "mutation-workflows"],
            "measured-local",
            "avenchart-ui Playwright desktop-chromium projects"),
        new(
            "mobile-chromium",
            "Chromium",
            "Mobile",
            "Pixel 5 profile",
            ["route-smoke", "accessibility", "material-workflows"],
            "measured-local",
            "avenchart-ui Playwright mobile-chromium projects"),
        new(
            "responsive-widths",
            "Chromium",
            "Responsive desktop emulation",
            "320, 390, 768, 1024, and 1440 px by 900 px",
            ["navigation-reflow", "focus-return"],
            "measured-local",
            "route-smoke clinician navigation width matrix"),
        new(
            "desktop-firefox",
            "Firefox",
            "Desktop",
            "Desktop Firefox profile",
            ["route-smoke", "material-workflows"],
            "measured-local",
            "avenchart-ui Playwright desktop-firefox projects"),
        new(
            "desktop-webkit",
            "WebKit",
            "Desktop",
            "Desktop Safari profile",
            ["route-smoke", "material-workflows"],
            "measured-local",
            "avenchart-ui Playwright desktop-webkit projects"),
    ];

    private static readonly IReadOnlyList<ExperienceTask> Tasks =
    [
        new(
            "staff-authentication",
            "Sign in and recover from an invalid staff session",
            ["administrator", "clinician", "front-desk"],
            "/login",
            "high",
            "A valid user reaches the role-appropriate clinician shell.",
            "Invalid credentials or authorization remain on a labelled error state without exposing internals.",
            "An expired session returns to sign-in and does not preserve a false authenticated state.",
            "Keyboard-labelled form, announced errors, visible focus, responsive reflow.",
            "Interactive route budget requires owner approval; current synthetic route smoke is the measurement method.",
            "public/accessibility and authenticated route-smoke suites"),
        new(
            "schedule-and-flow",
            "Review schedule and patient-flow state",
            ["clinician", "front-desk"],
            "/clinician/schedule",
            "high",
            "The selected day, appointments, status, provider, and facility are understandable.",
            "Unavailable data exposes retry/error state and never invents an empty schedule.",
            "Reload returns authoritative appointment state without duplicating a mutation.",
            "Keyboard navigation and responsive schedule/flow layouts.",
            "Schedule navigation timing remains proposed until an owner accepts a runtime budget.",
            "schedule, calendar, flow, and scheduling route/browser suites"),
        new(
            "patient-registration",
            "Search for and deliberately register a patient",
            ["front-desk", "administrator"],
            "/clinician/patients/new",
            "safety-critical",
            "Required identity fields validate and duplicate review precedes separate-record creation.",
            "Possible duplicate evidence blocks creation until deliberate acknowledgement.",
            "Validation preserves entered context while the user corrects fields or abandons the task.",
            "Semantic errors, keyboard completion, responsive duplicate-review state.",
            "Registration completion timing remains proposed; duplicate-safety behavior is measured.",
            "duplicate-registration serial mutation and clinician accessibility suites"),
        new(
            "patient-chart-context",
            "Open and navigate a patient chart without losing identity context",
            ["clinician", "front-desk", "administrator"],
            "/clinician/patients/MOD-PAT-0004/summary",
            "safety-critical",
            "Stable patient identity remains visible across every patient-chart section.",
            "Unknown or unauthorized patient context fails visibly and does not show another chart.",
            "Back/forward/deep-link navigation restores the intended patient and section.",
            "Responsive patient navigation, visible focus, semantic headings and landmarks.",
            "Patient-summary load timing remains proposed; cross-route context is measured synthetically.",
            "patient-chart route matrix, accessibility, and mutation workflows"),
        new(
            "encounter-documentation",
            "Create and review encounter documentation",
            ["clinician"],
            "/clinician/encounters/new",
            "safety-critical",
            "Patient, encounter, form, signature, and resulting history are unambiguous.",
            "Validation, authorization, and stale-state errors do not silently save or sign.",
            "Retry or correction uses the loaded server contract and preserves immutable history.",
            "Keyboard form completion, labelled errors, responsive content and focus order.",
            "Form interaction/save timing and manual assistive-technology proof remain owner-gated.",
            "encounter route, parity plans, and accessibility suites"),
        new(
            "laboratory-review",
            "Claim, sign, reopen, and bulk-sign laboratory work",
            ["clinician"],
            "/clinician/labs",
            "safety-critical",
            "Queue counts, patient/result context, claim, sign, and reopen outcomes reconcile.",
            "Unauthorized, already-claimed, or stale work fails without inventing a reviewer.",
            "Authoritative queue refresh permits a deliberate retry.",
            "Keyboard-operable filters/actions, understandable result flags, responsive queue.",
            "Queue load/sign timing remains proposed; mutation correctness is measured.",
            "lab-review serial mutation, material workflow, and accessibility suites"),
        new(
            "staff-messaging",
            "Claim and reply to a patient message",
            ["clinician", "front-desk"],
            "/clinician/messages",
            "high",
            "Thread, patient, assignee, reply, and resulting unread/claimed counts reconcile.",
            "Unauthorized or failed reply remains recoverable and does not claim delivery.",
            "Reload restores authoritative thread state and retry does not duplicate a reply.",
            "Keyboard thread/reply flow, announced errors, responsive list/detail layout.",
            "Message completion timing remains proposed; claim/reply behavior is measured.",
            "message serial mutation, route, and accessibility suites"),
        new(
            "patient-document-lifecycle",
            "File, review, route, OCR, version, archive, and retrieve a patient document",
            ["clinician", "administrator"],
            "/clinician/patients/MOD-PAT-0004/documents",
            "safety-critical",
            "Patient/category/version/review/routing state and protected content remain attributable.",
            "Invalid file, ownership, stale version, or terminal action fails without losing source evidence.",
            "Retry uses versioned writes; prior bytes and immutable history remain retrievable.",
            "Keyboard intake/review, accessible preview fallback, responsive register and queues.",
            "Document preview/list timing remains proposed; lifecycle and bundle budgets are measured.",
            "six-document serial mutation, routing/OCR, accessibility, and bundle-budget suites"),
        new(
            "payer-authorization",
            "Assign and transition a patient payer authorization",
            ["clinician", "administrator"],
            "/clinician/patients/MOD-PAT-0004/authorizations",
            "high",
            "Owner, due date, state, reason, version, and immutable history reconcile.",
            "Illegal, stale, or terminal writes fail with authoritative state.",
            "Conflict refresh permits a deliberate action from the current version.",
            "Keyboard transition/editor flow, contrast, responsive queue/detail/history.",
            "Authorization completion timing remains proposed; versioned lifecycle is measured.",
            "authorization serial mutation, direct API, smoke, and dynamic accessibility suites"),
        new(
            "prescription-renewal",
            "Review and resolve a prescription renewal",
            ["clinician"],
            "/clinician/renewals",
            "safety-critical",
            "Patient, prescription, request state, response, and local routing outcome remain explicit.",
            "Stale edit, allergy context, or invalid route blocks the write with an understandable outcome.",
            "Authoritative refresh preserves current request history and prevents duplicate resolution.",
            "Keyboard filters/editors, announced outcomes, responsive request cards.",
            "Renewal completion timing and production eRx behavior remain outside the local baseline.",
            "refill serial mutation, route, and accessibility suites"),
        new(
            "configuration-activation",
            "Review and activate a governed configuration proposal",
            ["administrator"],
            "/clinician/admin",
            "high",
            "Baseline/current/proposed values, version, status, actor, and activation outcome reconcile.",
            "No-op, unauthorized, stale-version, or changed-baseline activation fails.",
            "Conflict refresh and retained revision history support deliberate recovery/rollback.",
            "Keyboard forms/transitions, semantic loading/errors/history, responsive administration layout.",
            "Activation timing remains proposed; lifecycle evidence is measured.",
            "practice-setting mutation, administration route, smoke, and accessibility suites"),
        new(
            "report-filter-and-export",
            "Filter, run, and retrieve an authorized report/export",
            ["administrator", "clinician"],
            "/clinician/reports",
            "high",
            "Scope, filters, dataset/as-of facts, result, format, and export evidence are explicit.",
            "Invalid/unauthorized filters and unavailable artifacts fail without broadening scope.",
            "A retained run/artifact can be retrieved without silently recomputing current data.",
            "Keyboard filters/results, accessible table/download description, responsive report workspace.",
            "Report preview/export timing remains proposed; bundle and route checks are measured.",
            "report smoke, route, accessibility, and controlled-export checks"),
        new(
            "portal-self-service",
            "Use portal messages, appointments, records, and account review",
            ["patient"],
            "/portal/home",
            "high",
            "Authenticated portal scope remains patient-owned and task outcomes are understandable.",
            "Invalid identity, unauthorized record, or delivery failure does not expose another patient or claim success.",
            "Session recovery returns to portal sign-in; retries use server-owned scope.",
            "Keyboard authentication/tasks, screen-reader semantics, mobile reflow and visible focus.",
            "Portal task timing and production identity/disclosure policy remain owner-gated.",
            "portal route, material workflow, and accessibility suites"),
    ];

    private static readonly IReadOnlyList<ExperienceCriterion> Criteria =
    [
        new(
            "accessibility-target",
            "accessibility",
            "Target accessibility standard",
            "proposed",
            "WCAG 2.2 AA for adopted critical workflows",
            "Current automated gate covers WCAG 2.1 A/AA serious and critical findings.",
            "avenchart-ui accessibility result and documented manual gaps",
            "Accessibility owner"),
        new(
            "automated-accessibility",
            "accessibility",
            "Automated route and dynamic-state accessibility",
            "met-local",
            "No serious or critical automated findings in supported Chromium profiles",
            "Every public, clinician, patient-chart, portal, and selected dynamic state is scanned.",
            "avenchart-ui/test-results/accessibility-result.json",
            "Accessibility owner"),
        new(
            "manual-assistive-technology",
            "accessibility",
            "Manual screen-reader and zoom validation",
            "owner-gated",
            "Approved assistive-technology/browser combinations and representative users",
            "No owner-approved NVDA, JAWS, VoiceOver, TalkBack, or 200–400% zoom protocol is recorded.",
            "UX-05 field-validation dependency",
            "Accessibility owner"),
        new(
            "supported-route-matrix",
            "responsive",
            "Supported browser route rendering",
            "met-local",
            "All registered routes render without page errors in the declared matrix",
            "Chromium desktop/mobile plus Firefox/WebKit desktop route projects.",
            "avenchart-ui/test-results/route-smoke-result.json",
            "UX owner"),
        new(
            "responsive-navigation",
            "responsive",
            "Responsive navigation and focus return",
            "met-local",
            "No navigation trap at 320, 390, 768, 1024, or 1440 px",
            "Desktop navigation or mobile drawer is visible, Escape closes, and focus returns.",
            "route-smoke supported-width check",
            "UX owner"),
        new(
            "initial-bundle-budget",
            "performance",
            "Initial JavaScript budget",
            "met-local",
            "At most 256000 uncompressed bytes",
            "Production build fails when the initial chunk exceeds the budget.",
            "avenchart-ui/dist/bundle-budget.json",
            "Frontend owner"),
        new(
            "route-chunk-budget",
            "performance",
            "Lazy route JavaScript budget",
            "met-local",
            "At most 307200 uncompressed bytes per lazy route chunk",
            "Production build fails when any emitted non-initial JavaScript chunk exceeds the budget.",
            "avenchart-ui/dist/bundle-budget.json",
            "Frontend owner"),
        new(
            "runtime-performance-budget",
            "performance",
            "Critical-task runtime budgets",
            "owner-gated",
            "Initial route, chart, schedule, save, search, preview, and large-list budgets",
            "Synthetic methods exist, but product percentiles and thresholds are not approved.",
            "UX-01 owner decision and future performance traces",
            "UX and operations owners"),
        new(
            "patient-context-safety",
            "safety",
            "Patient and task context remains visible",
            "measured-local",
            "Critical patient tasks retain patient identity and server-owned scope",
            "Patient shell and mutation tests verify stable IDs/ownership on representative paths.",
            "patient-chart route and serial mutation suites",
            "Clinical safety owner"),
        new(
            "duplicate-write-recovery",
            "safety",
            "Retry and conflict do not duplicate writes",
            "measured-local",
            "Every adopted critical mutation has idempotency or optimistic conflict evidence",
            "Representative registration, document, configuration, authorization, refill, message, and lab paths are covered; full inventory remains open.",
            "serial mutation and backend smoke suites",
            "Clinical and product owners"),
        new(
            "error-recovery-contract",
            "resilience",
            "Loading, error, retry, empty, conflict, and session states",
            "measured-local",
            "Every adopted critical task exposes an authoritative recovery path",
            "Route/accessibility and material mutation fixtures cover representative states; exhaustive task coverage remains open.",
            "Modern UI functional review and browser suites",
            "Product owner"),
        new(
            "analytics-policy",
            "privacy",
            "PHI-free task analytics policy",
            "owner-gated",
            "Approved purpose, retention, access, consent, and deployment for allow-listed events",
            "Vocabulary is defined but collection is disabled; no production analytics policy is recorded.",
            "local-experience-baseline-v1 analytics definitions",
            "Privacy and product owners"),
    ];

    private static readonly IReadOnlyList<ExperienceAnalyticsEvent> AnalyticsEvents =
    [
        new(
            "route_loaded",
            "Measure non-PHI route readiness and coarse duration.",
            ["routeId", "roleId", "deviceClass", "durationBucket", "outcome"],
            false,
            "defined-not-collected"),
        new(
            "task_started",
            "Measure entry into a catalogued critical task.",
            ["taskId", "roleId", "entryPoint"],
            false,
            "defined-not-collected"),
        new(
            "task_completed",
            "Measure coarse task completion without clinical content.",
            ["taskId", "roleId", "outcome", "durationBucket"],
            false,
            "defined-not-collected"),
        new(
            "task_recovered",
            "Measure use of a catalogued retry, conflict, or session recovery path.",
            ["taskId", "roleId", "recoveryType", "outcome"],
            false,
            "defined-not-collected"),
        new(
            "ui_error_shown",
            "Measure coarse error class and whether recovery was available.",
            ["taskId", "routeId", "errorClass", "recoverable"],
            false,
            "defined-not-collected"),
        new(
            "accessibility_preference_applied",
            "Measure use of a non-identifying presentation preference after policy approval.",
            ["preference", "surface"],
            false,
            "defined-not-collected"),
    ];

    private static readonly IReadOnlyList<string> ForbiddenAnalyticsProperties =
    [
        "patientId",
        "patientName",
        "dateOfBirth",
        "email",
        "phone",
        "address",
        "diagnosis",
        "medication",
        "messageBody",
        "documentName",
        "freeText",
        "sessionId",
        "username",
        "ipAddress",
    ];

    private static readonly IReadOnlyList<ExperienceGap> Gaps =
    [
        new(
            "ux-owner-approval",
            "governance",
            "proposed",
            "Approve roles, critical-task inventory, supported matrix, and measurable acceptance thresholds.",
            "UX, clinical, accessibility, operations, and patient-experience owners",
            true),
        new(
            "wcag-22-and-manual-at",
            "accessibility",
            "owner-gated",
            "Select assistive-technology/browser combinations and complete WCAG 2.2 AA plus zoom/reflow/manual task validation.",
            "Accessibility owner",
            true),
        new(
            "runtime-budgets",
            "performance",
            "owner-gated",
            "Approve route/task percentile budgets, fixture sizes, measurement environment, and exception lifecycle.",
            "UX and operations owners",
            true),
        new(
            "analytics-policy-approval",
            "privacy",
            "collection-disabled",
            "Approve purpose, data minimization, retention, access, consent, and deployment before collecting any event.",
            "Privacy and product owners",
            true),
        new(
            "critical-task-coverage",
            "safety",
            "remediating",
            "Complete task-by-task interruption, authorization, context, duplicate-submit, keyboard, and recovery proof for every adopted workflow.",
            "Product and clinical owners",
            true),
        new(
            "representative-user-validation",
            "validation",
            "not-started",
            "Run approved non-PHI usability/accessibility protocols with representative staff, clinicians, administrators, and patients/proxies.",
            "UX research and accessibility owners",
            true),
    ];

    public static ExperienceBaselineResponse Build()
    {
        var met = Criteria.Count(item => item.LifecycleState == "met-local");
        var measured = Criteria.Count(item => item.LifecycleState == "measured-local");
        var ownerGated = Criteria.Count(item => item.LifecycleState == "owner-gated");
        var proposed = Criteria.Count(item => item.LifecycleState == "proposed");
        return new ExperienceBaselineResponse(
            Revision,
            "proposed",
            "UX + clinical product owner",
            "WCAG 2.2 AA proposed; current automated evidence is WCAG 2.1 A/AA",
            "Modern UI staff and portal applications using synthetic data",
            new ExperienceBaselineCounts(
                Roles.Count,
                Environments.Count,
                Tasks.Count,
                Criteria.Count,
                met,
                measured,
                ownerGated,
                proposed,
                AnalyticsEvents.Count,
                AnalyticsEvents.Count(item => item.CollectionEnabled),
                Gaps.Count),
            Roles,
            Environments,
            Tasks,
            Criteria,
            AnalyticsEvents,
            ForbiddenAnalyticsProperties,
            Gaps);
    }
}

public sealed record ExperienceBaselineResponse(
    string Revision,
    string LifecycleState,
    string OwnerRole,
    string AccessibilityStandard,
    string Scope,
    ExperienceBaselineCounts Counts,
    IReadOnlyList<ExperienceRole> Roles,
    IReadOnlyList<ExperienceEnvironment> Environments,
    IReadOnlyList<ExperienceTask> Tasks,
    IReadOnlyList<ExperienceCriterion> Criteria,
    IReadOnlyList<ExperienceAnalyticsEvent> AnalyticsEvents,
    IReadOnlyList<string> ForbiddenAnalyticsProperties,
    IReadOnlyList<ExperienceGap> Gaps);

public sealed record ExperienceBaselineCounts(
    int Roles,
    int Environments,
    int Tasks,
    int Criteria,
    int MetLocal,
    int MeasuredLocal,
    int OwnerGated,
    int Proposed,
    int AnalyticsEvents,
    int AnalyticsEventsCollected,
    int Gaps);

public sealed record ExperienceRole(
    string Id,
    string Label,
    string Scope);

public sealed record ExperienceEnvironment(
    string Id,
    string Browser,
    string DeviceClass,
    string Viewport,
    IReadOnlyList<string> TestLevels,
    string Status,
    string Evidence);

public sealed record ExperienceTask(
    string Id,
    string Label,
    IReadOnlyList<string> RoleIds,
    string Route,
    string Risk,
    string SuccessCriterion,
    string ErrorCriterion,
    string RecoveryCriterion,
    string AccessibilityCriterion,
    string PerformanceCriterion,
    string Evidence);

public sealed record ExperienceCriterion(
    string Id,
    string Category,
    string Label,
    string LifecycleState,
    string Target,
    string Measurement,
    string Evidence,
    string OwnerRole);

public sealed record ExperienceAnalyticsEvent(
    string EventId,
    string Purpose,
    IReadOnlyList<string> AllowedProperties,
    bool CollectionEnabled,
    string LifecycleState);

public sealed record ExperienceGap(
    string Id,
    string Area,
    string State,
    string RequiredDecision,
    string OwnerRole,
    bool BlocksProduction);
