// SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;

namespace AvenChart.Api.Features.Telehealth;

public sealed class TelehealthProspectiveEligibilityService(
    TelehealthProspectiveEligibilityRepository repository,
    TelehealthProspectiveMemberInsuranceDetailsProtector protector,
    ITelehealthProspectiveEligibilityGateway gateway,
    IOptions<TelehealthOptions> options)
{
    private readonly TelehealthOptions _options = options.Value;

    public async Task<TelehealthProspectiveEligibilityResponse> RecordAsync(
        HttpContext httpContext,
        Guid applicantId,
        RecordTelehealthProspectiveEligibilityRequest request,
        string applicantAccessKey,
        string idempotencyKey,
        CancellationToken cancellationToken)
    {
        RequireConfiguredHost(httpContext.Request.Host);
        var key = TelehealthProspectiveApplicantPolicy.RequireAccessKey(applicantAccessKey);
        if (request.ExpectedVersion <= 0)
        {
            throw TelehealthProblem.BadRequest(
                "telehealth_applicant_version_invalid",
                "Expected applicant version must be positive.");
        }
        if (!request.SyntheticDataConfirmed)
        {
            throw TelehealthProblem.BadRequest(
                "telehealth_applicant_eligibility_synthetic_acknowledgment_required",
                "Confirm that this is a NON_PRODUCTION synthetic eligibility check.");
        }

        var semanticKey = TelehealthCommandFingerprint.RequireIdempotencyKey(idempotencyKey);
        var fingerprint = TelehealthCommandFingerprint.Create(
            "prospective-synthetic-eligibility-v1",
            applicantId,
            request.ExpectedVersion,
            request.SyntheticDataConfirmed);
        var result = await repository.RecordAsync(
            _options.PracticeId,
            _options.FacilityId,
            applicantId,
            TelehealthProspectiveApplicantPolicy.Hash(key),
            request.ExpectedVersion,
            semanticKey,
            fingerprint,
            ResolveEligibilityAsync,
            cancellationToken);
        return ToResponse(result);
    }

    private async ValueTask<TelehealthProspectiveEligibilityAdapterResult> ResolveEligibilityAsync(
        TelehealthProspectiveEligibilityCandidate candidate,
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
        TelehealthProspectiveEligibilityCandidate candidate,
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
                "telehealth_applicant_eligibility_adapter_contract_invalid",
                "The bounded synthetic eligibility adapter returned an invalid result.");
        }
    }

    private static bool IsValidOutcome(TelehealthProspectiveEligibilityAdapterResult result) =>
        result.BusinessOutcome switch
        {
            "EligibleBenefitsReported" => result is
            {
                TransportOutcome: "SimulatedAccepted",
                MemberMatchStatus: "Matched",
                EligibilityStatus: "Active",
                BenefitInformationStatus: "Reported",
                MemberMatched: true,
                MemberEligibilityChecked: true,
                MemberBenefitsChecked: true
            },
            "CoverageInactive" => result is
            {
                TransportOutcome: "SimulatedAccepted",
                MemberMatchStatus: "Matched",
                EligibilityStatus: "Inactive",
                BenefitInformationStatus: "NotReported",
                MemberMatched: true,
                MemberEligibilityChecked: true,
                MemberBenefitsChecked: false
            },
            "SubscriberNotFound" => result is
            {
                TransportOutcome: "SimulatedAccepted",
                MemberMatchStatus: "NotMatched",
                EligibilityStatus: "Unknown",
                BenefitInformationStatus: "NotReported",
                MemberMatched: false,
                MemberEligibilityChecked: true,
                MemberBenefitsChecked: false
            },
            "UnableToDetermine" => result is
            {
                TransportOutcome: "SimulatedUnavailable",
                MemberMatchStatus: "Unknown",
                EligibilityStatus: "Unknown",
                BenefitInformationStatus: "Unknown",
                MemberMatched: false,
                MemberEligibilityChecked: false,
                MemberBenefitsChecked: false
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

    private static TelehealthProspectiveEligibilityResponse ToResponse(
        TelehealthProspectiveEligibilityRecord result)
    {
        var adapter = result.AdapterResult;
        return new(
            EligibilityResultId: result.EligibilityResultId,
            ApplicantId: result.ApplicantId,
            ApplicantVersion: result.ApplicantVersion,
            ApplicantStatus: result.ApplicantStatus,
            CurrentLocationStateCode: result.CurrentLocationStateCode,
            PurposeCategory: result.PurposeCategory,
            PlanKey: result.PlanKey,
            PayerDisplayName: result.PayerDisplayName,
            ProductDisplayName: result.ProductDisplayName,
            PracticeNetworkStatus: result.PracticeNetworkStatus,
            MemberIdMask: TelehealthProspectiveMemberInsuranceDetailsPolicy.Mask(result.MemberIdLast4),
            GroupNumberMask: result.GroupNumberLast4 is null
                ? null
                : TelehealthProspectiveMemberInsuranceDetailsPolicy.Mask(result.GroupNumberLast4),
            SubscriberRelationship: result.SubscriberRelationship,
            CoveragePriority: result.CoveragePriority,
            DateOfService: result.DateOfService,
            ServiceCategory: result.ServiceCategory,
            AdapterMode: adapter.AdapterMode,
            CompatibilityTarget: adapter.CompatibilityTarget,
            DatasetKey: adapter.DatasetKey,
            DatasetVersion: adapter.DatasetVersion,
            DatasetEffectiveFrom: adapter.DatasetEffectiveFrom,
            DatasetEffectiveThrough: adapter.DatasetEffectiveThrough,
            InquiryTraceToken: adapter.InquiryTraceToken,
            ResponseTraceToken: adapter.ResponseTraceToken,
            TransportOutcome: adapter.TransportOutcome,
            MemberMatchStatus: adapter.MemberMatchStatus,
            EligibilityStatus: adapter.EligibilityStatus,
            BenefitInformationStatus: adapter.BenefitInformationStatus,
            BusinessOutcome: adapter.BusinessOutcome,
            MemberMatched: adapter.MemberMatched,
            MemberEligibilityChecked: adapter.MemberEligibilityChecked,
            MemberBenefitsChecked: adapter.MemberBenefitsChecked,
            CheckedAt: adapter.CheckedAt,
            ExpiresAt: adapter.ExpiresAt,
            RecordedAt: result.RecordedAt,
            RawTransactionCreated: false,
            RenderingPhysicianNetworkChecked: false,
            CoverageVerified: false,
            ExactNetworkConfirmed: false,
            IdentityProofed: false,
            CanonicalPatientCreated: false,
            ChartLinked: false,
            PortalAccountCreated: false,
            ProspectiveIntakeCompleted: false,
            ConsentCreated: false,
            PracticeAccepted: false,
            CoverageRecordCreated: false,
            EstimateCreated: false,
            FinancialAcknowledgmentCreated: false,
            RequestCreated: false,
            QueueEnabled: false,
            AppointmentCreated: false,
            EncounterCreated: false,
            CareEnabled: false,
            PrescribingEnabled: false,
            BillingEnabled: false,
            ClaimCreated: false,
            CommunicationEnabled: false,
            IntegrationEnabled: false,
            ExternalCallPerformed: false,
            Direction: DirectionFor(adapter.BusinessOutcome),
            Limitations:
            [
                "NON_PRODUCTION fixture only. No payer, clearinghouse, pharmacy, or external service was contacted, and no raw ASC X12 270 or 271 transaction was created or stored.",
                "Eligibility and reported benefit information are separate from exact practice-and-rendering-physician network participation and are never a guarantee of payment or coverage.",
                "No benefit amount, deductible, copay, coinsurance, estimate, canonical coverage, patient, chart, portal, consent, request, queue, appointment, encounter, prescribing, billing, claim, communication, integration, external action, or care capability was created."
            ]);
    }

    private static string DirectionFor(string businessOutcome) => businessOutcome switch
    {
        "EligibleBenefitsReported" =>
            "The synthetic fixture reports active eligibility and benefit information for this date and service. Exact network participation, coverage verification, financial details, and every later intake gate remain required.",
        "CoverageInactive" =>
            "The synthetic fixture reports inactive eligibility. Do not treat the member as covered or advance to a care request.",
        "SubscriberNotFound" =>
            "The synthetic fixture did not match the subscriber. Review the synthetic details in a separately authorized correction workflow; no care request was created.",
        _ =>
            "The synthetic adapter could not determine eligibility. Do not infer coverage, benefits, network status, or payment responsibility."
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
