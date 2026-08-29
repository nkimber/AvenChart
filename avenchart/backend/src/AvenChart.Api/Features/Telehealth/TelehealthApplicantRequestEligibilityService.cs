// SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;

namespace AvenChart.Api.Features.Telehealth;

public sealed class TelehealthApplicantRequestEligibilityService(
    TelehealthApplicantRequestEligibilityRepository repository,
    TelehealthProspectiveMemberInsuranceDetailsProtector protector,
    ITelehealthProspectiveEligibilityGateway gateway,
    IOptions<TelehealthOptions> options)
{
    private readonly TelehealthOptions _options = options.Value;

    public async Task<TelehealthApplicantRequestEligibilityResponse> GetAsync(
        HttpContext httpContext,
        Guid applicantId,
        string applicantAccessKey,
        CancellationToken cancellationToken)
    {
        RequireConfiguredHost(httpContext.Request.Host);
        var key = TelehealthProspectiveApplicantPolicy.RequireAccessKey(applicantAccessKey);
        return ToResponse(await repository.GetAsync(
            _options.PracticeId,
            _options.FacilityId,
            applicantId,
            TelehealthProspectiveApplicantPolicy.Hash(key),
            cancellationToken));
    }

    public async Task<TelehealthApplicantRequestEligibilityResponse> RunAsync(
        HttpContext httpContext,
        Guid applicantId,
        RunTelehealthApplicantRequestEligibilityVerification request,
        string applicantAccessKey,
        string idempotencyKey,
        CancellationToken cancellationToken)
    {
        RequireConfiguredHost(httpContext.Request.Host);
        var key = TelehealthProspectiveApplicantPolicy.RequireAccessKey(applicantAccessKey);
        var normalized = TelehealthApplicantRequestEligibilityPolicy.Normalize(request);
        var semanticKey = TelehealthCommandFingerprint.RequireIdempotencyKey(idempotencyKey);
        var commandFingerprint = TelehealthCommandFingerprint.Create(
            "synthetic-applicant-request-eligibility-verification-v1",
            applicantId,
            normalized.ExpectedRequestVersion,
            normalized.EligibilitySnapshotFingerprint,
            normalized.SyntheticDataConfirmed,
            normalized.NoGuaranteeAcknowledged);
        return ToResponse(await repository.RunAsync(
            _options.PracticeId,
            _options.FacilityId,
            applicantId,
            TelehealthProspectiveApplicantPolicy.Hash(key),
            normalized,
            semanticKey,
            commandFingerprint,
            ResolveEligibilityAsync,
            cancellationToken));
    }

    private async ValueTask<TelehealthProspectiveEligibilityAdapterResult> ResolveEligibilityAsync(
        TelehealthApplicantRequestEligibilityCandidate candidate,
        CancellationToken cancellationToken)
    {
        var payload = protector.Unprotect(candidate.ProtectedPayload);
        RequireProtectedSnapshot(candidate, payload);
        var checkedAt = candidate.DatabaseNow.ToUniversalTime();
        var result = await gateway.CheckAsync(
            new(
                candidate.PlanKey,
                payload.MemberId,
                payload.GroupNumber,
                payload.SubscriberRelationship,
                payload.SubscriberFirstName,
                payload.SubscriberLastName,
                payload.SubscriberDateOfBirth,
                DateOnly.FromDateTime(checkedAt.UtcDateTime),
                SyntheticTelehealthProspectiveEligibilityGateway.ServiceCategory,
                checkedAt),
            cancellationToken);
        RequireAdapterContract(result, checkedAt);
        return result;
    }

    private static void RequireProtectedSnapshot(
        TelehealthApplicantRequestEligibilityCandidate candidate,
        TelehealthProtectedMemberInsurancePayload payload)
    {
        var memberLast4 = payload.MemberId.Length >= 4 ? payload.MemberId[^4..] : string.Empty;
        var groupLast4 = payload.GroupNumber is { Length: >= 4 } ? payload.GroupNumber[^4..] : null;
        var valid = payload.MemberId.Length is >= 6 and <= 32
            && payload.MemberId.StartsWith("SYN-", StringComparison.Ordinal)
            && FixedTimeEquals(memberLast4, candidate.MemberIdLast4)
            && candidate.GroupNumberPresent == (payload.GroupNumber is not null)
            && ((groupLast4 is null && candidate.GroupNumberLast4 is null)
                || (groupLast4 is not null
                    && candidate.GroupNumberLast4 is not null
                    && FixedTimeEquals(groupLast4, candidate.GroupNumberLast4)))
            && FixedTimeEquals(payload.SubscriberRelationship, candidate.SubscriberRelationship)
            && FixedTimeEquals(payload.CoveragePriority, candidate.CoveragePriority)
            && payload.SubscriberRelationship is "Self" or "Spouse" or "Parent" or "Other"
            && !string.IsNullOrWhiteSpace(payload.SubscriberFirstName)
            && !string.IsNullOrWhiteSpace(payload.SubscriberLastName);
        if (!valid)
        {
            throw TelehealthProblem.Conflict(
                "telehealth_applicant_member_details_protection_invalid",
                "The protected synthetic member-details receipt cannot be validated. Start again with a new synthetic applicant.");
        }
    }

    private static void RequireAdapterContract(
        TelehealthProspectiveEligibilityAdapterResult result,
        DateTimeOffset checkedAt)
    {
        var valid = result.AdapterMode == SyntheticTelehealthProspectiveEligibilityGateway.AdapterMode
            && result.CompatibilityTarget == SyntheticTelehealthProspectiveEligibilityGateway.CompatibilityTarget
            && result.DatasetKey == SyntheticTelehealthProspectiveEligibilityGateway.DatasetKey
            && result.DatasetVersion == SyntheticTelehealthProspectiveEligibilityGateway.DatasetVersion
            && result.DatasetEffectiveFrom == SyntheticTelehealthProspectiveEligibilityGateway.DatasetEffectiveFrom
            && result.DatasetEffectiveThrough == SyntheticTelehealthProspectiveEligibilityGateway.DatasetEffectiveThrough
            && result.InquiryTraceToken != Guid.Empty
            && result.ResponseTraceToken != Guid.Empty
            && result.InquiryTraceToken != result.ResponseTraceToken
            && result.CheckedAt == checkedAt
            && result.ExpiresAt == checkedAt.AddMinutes(15)
            && IsValidOutcome(result);
        if (!valid)
        {
            throw TelehealthProblem.Conflict(
                "telehealth_applicant_request_eligibility_adapter_contract_invalid",
                "The bounded synthetic request eligibility adapter returned an invalid result.");
        }
    }

    private static bool IsValidOutcome(TelehealthProspectiveEligibilityAdapterResult result) =>
        result.BusinessOutcome switch
        {
            "EligibleBenefitsReported" => result is
            {
                TransportOutcome: "SimulatedAccepted", MemberMatchStatus: "Matched",
                EligibilityStatus: "Active", BenefitInformationStatus: "Reported",
                MemberMatched: true, MemberEligibilityChecked: true, MemberBenefitsChecked: true
            },
            "CoverageInactive" => result is
            {
                TransportOutcome: "SimulatedAccepted", MemberMatchStatus: "Matched",
                EligibilityStatus: "Inactive", BenefitInformationStatus: "NotReported",
                MemberMatched: true, MemberEligibilityChecked: true, MemberBenefitsChecked: false
            },
            "SubscriberNotFound" => result is
            {
                TransportOutcome: "SimulatedAccepted", MemberMatchStatus: "NotMatched",
                EligibilityStatus: "Unknown", BenefitInformationStatus: "NotReported",
                MemberMatched: false, MemberEligibilityChecked: true, MemberBenefitsChecked: false
            },
            "UnableToDetermine" => result is
            {
                TransportOutcome: "SimulatedUnavailable", MemberMatchStatus: "Unknown",
                EligibilityStatus: "Unknown", BenefitInformationStatus: "Unknown",
                MemberMatched: false, MemberEligibilityChecked: false, MemberBenefitsChecked: false
            },
            _ => false
        };

    private static bool FixedTimeEquals(string left, string right)
    {
        var leftBytes = Encoding.UTF8.GetBytes(left);
        var rightBytes = Encoding.UTF8.GetBytes(right);
        return leftBytes.Length == rightBytes.Length
            && CryptographicOperations.FixedTimeEquals(leftBytes, rightBytes);
    }

    private static TelehealthApplicantRequestEligibilityResponse ToResponse(
        TelehealthApplicantRequestEligibilityRecord result)
    {
        var adapter = result.AdapterResult;
        var complete = result.VerificationId is not null;
        return new(
            ApplicantId: result.ApplicantId,
            ApplicantVersion: result.ApplicantVersion,
            ApplicantStatus: result.ApplicantStatus,
            RequestId: result.RequestId,
            RequestVersion: result.RequestVersion,
            RequestStatus: result.RequestStatus,
            PolicyKey: TelehealthApplicantRequestEligibilityPolicy.PolicyKey,
            PolicyVersion: TelehealthApplicantRequestEligibilityPolicy.PolicyVersion,
            EligibilitySnapshotFingerprint: result.EligibilitySnapshotFingerprint,
            ContextExpiresAt: result.ContextExpiresAt,
            PayerDisplayName: result.PayerDisplayName,
            ProductDisplayName: result.ProductDisplayName,
            MaskedMemberId: $"••••{result.MemberIdLast4}",
            MaskedGroupNumber: result.GroupNumberLast4 is null ? null : $"••••{result.GroupNumberLast4}",
            SubscriberRelationship: result.SubscriberRelationship,
            CoveragePriority: result.CoveragePriority,
            CurrentLocationStateCode: result.CurrentLocationStateCode,
            PurposeCategory: result.PurposeCategory,
            VerificationReady: !complete,
            VerificationCompleted: complete,
            VerificationId: result.VerificationId,
            DateOfService: result.DateOfService,
            ServiceCategory: complete ? SyntheticTelehealthProspectiveEligibilityGateway.ServiceCategory : null,
            AdapterMode: adapter?.AdapterMode,
            CompatibilityTarget: adapter?.CompatibilityTarget,
            DatasetKey: adapter?.DatasetKey,
            DatasetVersion: adapter?.DatasetVersion,
            TransportOutcome: adapter?.TransportOutcome,
            MemberMatchStatus: adapter?.MemberMatchStatus,
            EligibilityStatus: adapter?.EligibilityStatus,
            BenefitInformationStatus: adapter?.BenefitInformationStatus,
            BusinessOutcome: adapter?.BusinessOutcome,
            MemberMatched: adapter?.MemberMatched ?? false,
            MemberEligibilityChecked: adapter?.MemberEligibilityChecked ?? false,
            MemberBenefitsChecked: adapter?.MemberBenefitsChecked ?? false,
            CheckedAt: adapter?.CheckedAt,
            ExpiresAt: adapter?.ExpiresAt,
            ProtectedPayloadReferenced: true,
            ProtectedPayloadCopied: false,
            ProtectedPayloadDecryptedInServerMemory: complete,
            PriorEligibilityResultReused: false,
            CurrentEligibilityEvidenceCreated: complete,
            RawTransactionCreated: false,
            CanonicalCoverageCreated: false,
            CoverageSelected: false,
            NetworkVerificationCreated: false,
            RenderingPhysicianNetworkChecked: false,
            CoverageVerified: false,
            ExactNetworkConfirmed: false,
            FinancialRouteCreated: false,
            OperationalReviewCreated: false,
            PracticeAccepted: false,
            PatientContacted: false,
            PatientCareQueueEntered: false,
            ClinicianQueueEntered: false,
            DoctorSearchStarted: false,
            QueuePositionAssigned: false,
            AppointmentCreated: false,
            EncounterCreated: false,
            ConsentCreated: false,
            CareAuthorized: false,
            IntegrationEnabled: false,
            ExternalCallPerformed: false,
            Direction: DirectionFor(adapter?.BusinessOutcome),
            Limitations:
            [
                "NON_PRODUCTION synthetic demonstration only. No payer, clearinghouse, provider directory, pharmacy, or other external destination was contacted.",
                "The protected member payload is decrypted only in server memory, validated against the masked source receipt, and is never returned, copied into this result, or logged by this workflow.",
                "Fresh member eligibility and reported benefit information are separate from practice and rendering-physician network participation and never guarantee coverage, payment, or patient responsibility.",
                "No canonical coverage, coverage selection, network verification, financial route, operational review, contact, queue, appointment, encounter, consent, care, integration, or external action is created."
            ]);
    }

    private static string DirectionFor(string? businessOutcome) => businessOutcome switch
    {
        null => "Run the fresh synthetic eligibility check. Exact network participation and every downstream gate will remain pending.",
        "EligibleBenefitsReported" => "Fresh synthetic eligibility is active and benefit information is reported. Exact practice and rendering-physician network checks remain required.",
        "CoverageInactive" => "The fresh synthetic result is inactive. Do not infer coverage or advance this request.",
        "SubscriberNotFound" => "The fresh synthetic result did not match the subscriber. Correction or review is required before any later gate.",
        _ => "The fresh synthetic adapter could not determine eligibility. Do not infer coverage, benefits, network status, or payment responsibility."
    };

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
}
