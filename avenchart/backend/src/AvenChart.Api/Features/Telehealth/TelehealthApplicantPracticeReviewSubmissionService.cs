// SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
// SPDX-License-Identifier: GPL-3.0-or-later

using Microsoft.Extensions.Options;

namespace AvenChart.Api.Features.Telehealth;

public sealed class TelehealthApplicantPracticeReviewSubmissionService(
    TelehealthApplicantPreRequestReadinessRepository repository,
    IOptions<TelehealthOptions> options)
{
    private readonly TelehealthOptions _options = options.Value;

    public async Task<TelehealthApplicantPracticeReviewResponse> GetAsync(
        HttpContext httpContext,
        Guid applicantId,
        string applicantAccessKey,
        CancellationToken cancellationToken)
    {
        RequireConfiguredHost(httpContext.Request.Host);
        var key = TelehealthProspectiveApplicantPolicy.RequireAccessKey(applicantAccessKey);
        var context = await repository.GetAuthorizedAsync(
            _options.PracticeId,
            _options.FacilityId,
            applicantId,
            TelehealthProspectiveApplicantPolicy.Hash(key),
            cancellationToken);
        RequirePracticeReviewEntry(context);
        var snapshot = TelehealthApplicantPreRequestReadinessRepository.PracticeReviewSnapshot(context);
        return ToResponse(context, snapshot, context.PracticeReviewCaseId, context.PracticeReviewStatus,
            context.PracticeReviewSubmittedAt);
    }

    public async Task<TelehealthApplicantPracticeReviewResponse> SubmitAsync(
        HttpContext httpContext,
        Guid applicantId,
        SubmitTelehealthApplicantPracticeReviewRequest request,
        string applicantAccessKey,
        string idempotencyKey,
        CancellationToken cancellationToken)
    {
        RequireConfiguredHost(httpContext.Request.Host);
        var key = TelehealthProspectiveApplicantPolicy.RequireAccessKey(applicantAccessKey);
        var accessKeyHash = TelehealthProspectiveApplicantPolicy.Hash(key);
        var context = await repository.GetAuthorizedAsync(
            _options.PracticeId,
            _options.FacilityId,
            applicantId,
            accessKeyHash,
            cancellationToken);
        RequirePracticeReviewEntry(context);
        var snapshot = TelehealthApplicantPreRequestReadinessRepository.PracticeReviewSnapshot(context);
        var normalized = TelehealthApplicantPracticeReviewSubmissionPolicy.Normalize(request);
        var semanticKey = TelehealthCommandFingerprint.RequireIdempotencyKey(idempotencyKey);
        var commandFingerprint = TelehealthCommandFingerprint.Create(
            "prospective-practice-review-submission-v1",
            applicantId,
            normalized.ExpectedVersion,
            normalized.PracticeReviewSnapshotFingerprint,
            context.OverallRoute!,
            normalized.PatientReportedInformationAcknowledged,
            normalized.PracticeMayRequestInformationOrDeclineAcknowledged,
            normalized.NoTelehealthRequestOrCareQueueAcknowledged,
            normalized.WorseningSymptomsRequireImmediateActionAcknowledged);
        var submitted = await repository.SubmitPracticeReviewAsync(
            _options.PracticeId,
            _options.FacilityId,
            applicantId,
            accessKeyHash,
            normalized,
            semanticKey,
            commandFingerprint,
            cancellationToken);
        return ToResponse(
            context with
            {
                ApplicantVersion = submitted.ApplicantVersion,
                ApplicantStatus = submitted.ApplicantStatus
            },
            snapshot,
            submitted.CaseId,
            submitted.ReviewStatus,
            submitted.SubmittedAt);
    }

    private static TelehealthApplicantPracticeReviewResponse ToResponse(
        TelehealthApplicantPreRequestReadinessContext context,
        TelehealthApplicantPracticeReviewSubmissionSnapshot snapshot,
        Guid? caseId,
        string? reviewStatus,
        DateTimeOffset? submittedAt)
    {
        var submitted = caseId is not null;
        return new(
            ApplicantId: context.ApplicantId,
            ApplicantVersion: context.ApplicantVersion,
            ApplicantStatus: context.ApplicantStatus,
            PracticeReviewSnapshotFingerprint: snapshot.Fingerprint,
            PolicyKey: TelehealthApplicantPracticeReviewSubmissionPolicy.PolicyKey,
            PolicyVersion: TelehealthApplicantPracticeReviewSubmissionPolicy.PolicyVersion,
            ReviewRoute: context.OverallRoute!,
            SubmissionReady: !submitted,
            Submitted: submitted,
            PracticeReviewCaseId: caseId,
            ReviewStatus: reviewStatus,
            SubmittedAt: submittedAt,
            PatientReportedInformationAcknowledged: submitted,
            PracticeMayRequestInformationOrDeclineAcknowledged: submitted,
            NoTelehealthRequestOrCareQueueAcknowledged: submitted,
            WorseningSymptomsRequireImmediateActionAcknowledged: submitted,
            StaffReviewCreated: submitted,
            ClinicianReviewCreated: false,
            PracticeAccepted: false,
            PatientRecordChanged: false,
            TelehealthRequestCreated: false,
            PatientCareQueueEntered: false,
            ClinicianQueueEntered: false,
            AppointmentCreated: false,
            EncounterCreated: false,
            CareAuthorized: false,
            PrescribingEnabled: false,
            BillingEnabled: false,
            ClaimCreated: false,
            IntegrationEnabled: false,
            ExternalCallPerformed: false,
            Direction: submitted
                ? "The synthetic information was submitted for practice review. This is not practice acceptance, a telehealth request, a doctor search, or a place in the patient or clinician queue."
                : "Review the submission limits. If symptoms are urgent or worsening, seek appropriate immediate care instead of waiting for practice review.",
            Limitations:
            [
                "Synthetic demonstration only. The practice-review work item references earlier bounded receipts; it does not copy source values or clinical details.",
                "Practice review may request more information or decline. No response time, doctor availability, coverage, acceptance, appointment, or care is promised.",
                "No telehealth request, patient or clinician queue entry, encounter, prescribing, billing, claim, integration, or external action is created."
            ]);
    }

    private void RequireConfiguredHost(HostString host)
    {
        if (!_options.BrandedHosts.Contains(host.Host, StringComparer.OrdinalIgnoreCase))
        {
            throw new TelehealthProblem(
                StatusCodes.Status404NotFound,
                "telehealth_practice_not_found",
                "Telehealth practice was not found",
                "This host is not configured for the synthetic telehealth practice.");
        }
    }

    private static void RequirePracticeReviewEntry(
        TelehealthApplicantPreRequestReadinessContext context)
    {
        if (context.ApplicantStatus is not (
                TelehealthApplicantPracticeReviewSubmissionPolicy.EntryStatus
                or TelehealthApplicantPracticeReviewSubmissionPolicy.ResultingStatus)
            || context.ReadinessAcknowledgmentId is null
            || string.IsNullOrWhiteSpace(context.ReadinessSnapshotFingerprint)
            || string.IsNullOrWhiteSpace(context.OverallRoute))
        {
            throw TelehealthProblem.Conflict(
                "telehealth_applicant_practice_review_state_conflict",
                "The applicant is not eligible for this bounded synthetic practice review submission.");
        }
    }
}
