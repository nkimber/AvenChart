// SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
// SPDX-License-Identifier: GPL-3.0-or-later

namespace AvenChart.Api.Features.Telehealth;

public sealed record TelehealthProspectiveEligibilityInquiry(
    string PlanKey,
    string MemberId,
    string? GroupNumber,
    string SubscriberRelationship,
    string SubscriberFirstName,
    string SubscriberLastName,
    DateOnly SubscriberDateOfBirth,
    DateOnly DateOfService,
    string ServiceCategory,
    DateTimeOffset CheckedAt);

public sealed record TelehealthProspectiveEligibilityAdapterResult(
    string AdapterMode,
    string CompatibilityTarget,
    string DatasetKey,
    int DatasetVersion,
    DateTimeOffset DatasetEffectiveFrom,
    DateTimeOffset DatasetEffectiveThrough,
    Guid InquiryTraceToken,
    Guid ResponseTraceToken,
    string TransportOutcome,
    string MemberMatchStatus,
    string EligibilityStatus,
    string BenefitInformationStatus,
    string BusinessOutcome,
    bool MemberMatched,
    bool MemberEligibilityChecked,
    bool MemberBenefitsChecked,
    DateTimeOffset CheckedAt,
    DateTimeOffset ExpiresAt);

public interface ITelehealthProspectiveEligibilityGateway
{
    ValueTask<TelehealthProspectiveEligibilityAdapterResult> CheckAsync(
        TelehealthProspectiveEligibilityInquiry inquiry,
        CancellationToken cancellationToken);
}

public sealed class SyntheticTelehealthProspectiveEligibilityGateway
    : ITelehealthProspectiveEligibilityGateway
{
    public const string AdapterMode = "NON_PRODUCTION";
    public const string CompatibilityTarget = "ASC_X12N_270_271_005010X279A1";
    public const string DatasetKey = "avenchart-synthetic-prospective-eligibility-2026-08";
    public const int DatasetVersion = 1;
    public const string ServiceCategory = "ProfessionalTelehealthConsultation";

    public static readonly DateTimeOffset DatasetEffectiveFrom =
        new(2026, 8, 27, 0, 0, 0, TimeSpan.Zero);
    public static readonly DateTimeOffset DatasetEffectiveThrough =
        new(2026, 10, 31, 23, 59, 59, TimeSpan.Zero);

    public ValueTask<TelehealthProspectiveEligibilityAdapterResult> CheckAsync(
        TelehealthProspectiveEligibilityInquiry inquiry,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        RequireSupportedInquiry(inquiry);

        var outcome = (inquiry.PlanKey, inquiry.MemberId) switch
        {
            ("harbor-mutual-hd", "SYN-HM-1001") => new SyntheticOutcome(
                "SimulatedAccepted", "Matched", "Active", "Reported",
                "EligibleBenefitsReported", true, true, true),
            ("blue-valley-standard", "SYN-BV-2002") => new SyntheticOutcome(
                "SimulatedAccepted", "Matched", "Inactive", "NotReported",
                "CoverageInactive", true, true, false),
            ("pine-state-choice", "SYN-PS-3003") => new SyntheticOutcome(
                "SimulatedAccepted", "NotMatched", "Unknown", "NotReported",
                "SubscriberNotFound", false, true, false),
            _ => new SyntheticOutcome(
                "SimulatedUnavailable", "Unknown", "Unknown", "Unknown",
                "UnableToDetermine", false, false, false)
        };

        return ValueTask.FromResult(new TelehealthProspectiveEligibilityAdapterResult(
            AdapterMode,
            CompatibilityTarget,
            DatasetKey,
            DatasetVersion,
            DatasetEffectiveFrom,
            DatasetEffectiveThrough,
            Guid.NewGuid(),
            Guid.NewGuid(),
            outcome.TransportOutcome,
            outcome.MemberMatchStatus,
            outcome.EligibilityStatus,
            outcome.BenefitInformationStatus,
            outcome.BusinessOutcome,
            outcome.MemberMatched,
            outcome.MemberEligibilityChecked,
            outcome.MemberBenefitsChecked,
            inquiry.CheckedAt,
            inquiry.CheckedAt.AddMinutes(15)));
    }

    private static void RequireSupportedInquiry(TelehealthProspectiveEligibilityInquiry inquiry)
    {
        var effectiveFrom = DateOnly.FromDateTime(DatasetEffectiveFrom.UtcDateTime);
        var effectiveThrough = DateOnly.FromDateTime(DatasetEffectiveThrough.UtcDateTime);
        if (inquiry.PlanKey is not ("harbor-mutual-hd" or "blue-valley-standard" or "pine-state-choice")
            || !string.Equals(inquiry.ServiceCategory, ServiceCategory, StringComparison.Ordinal)
            || inquiry.DateOfService < effectiveFrom
            || inquiry.DateOfService > effectiveThrough
            || inquiry.CheckedAt.Offset != TimeSpan.Zero)
        {
            throw TelehealthProblem.Conflict(
                "telehealth_applicant_eligibility_dataset_unavailable",
                "The bounded synthetic eligibility dataset is not available for this inquiry.");
        }
    }

    private sealed record SyntheticOutcome(
        string TransportOutcome,
        string MemberMatchStatus,
        string EligibilityStatus,
        string BenefitInformationStatus,
        string BusinessOutcome,
        bool MemberMatched,
        bool MemberEligibilityChecked,
        bool MemberBenefitsChecked);
}
