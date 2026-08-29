// SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
// SPDX-License-Identifier: GPL-3.0-or-later

using AvenChart.Api.Features.Telehealth;

namespace AvenChart.Api.Tests.Telehealth;

public sealed class TelehealthApplicantRequestUniversalSafetyPolicyTests
{
    private static readonly string Snapshot = new('a', 64);

    [Fact]
    public void Normalize_accepts_explicit_answers_and_canonicalizes_context()
    {
        var normalized = TelehealthApplicantRequestUniversalSafetyPolicy.Normalize(Valid(
            stateCode: " ga ",
            snapshot: Snapshot.ToUpperInvariant()));

        Assert.Equal(2, normalized.ExpectedRequestVersion);
        Assert.Equal(Snapshot, normalized.ContextSnapshotFingerprint);
        Assert.Equal("GA", normalized.CurrentLocationStateCode);
        Assert.True(normalized.CurrentLocationConfirmed);
        Assert.True(normalized.CallbackNumberConfirmed);
        Assert.True(normalized.SyntheticDataConfirmed);
        Assert.False(normalized.HasEmergencyWarning);
        Assert.False(normalized.SevereOrWorsening);
        Assert.False(normalized.RequiresHandsOnExam);
        Assert.False(normalized.Unsure);
    }

    [Theory]
    [InlineData(null, false, false, false)]
    [InlineData(false, null, false, false)]
    [InlineData(false, false, null, false)]
    [InlineData(false, false, false, null)]
    public void Normalize_rejects_every_missing_answer(
        bool? emergency,
        bool? severe,
        bool? handsOn,
        bool? unsure)
    {
        var problem = Assert.Throws<TelehealthProblem>(() =>
            TelehealthApplicantRequestUniversalSafetyPolicy.Normalize(Valid(
                emergency: emergency,
                severe: severe,
                handsOn: handsOn,
                unsure: unsure)));

        Assert.Equal("telehealth_applicant_request_safety_answer_required", problem.Code);
    }

    [Theory]
    [InlineData(false, true)]
    [InlineData(true, false)]
    public void Normalize_requires_location_and_callback_reconfirmation(bool location, bool callback)
    {
        var problem = Assert.Throws<TelehealthProblem>(() =>
            TelehealthApplicantRequestUniversalSafetyPolicy.Normalize(Valid(
                locationConfirmed: location,
                callbackConfirmed: callback)));

        Assert.Equal("telehealth_applicant_request_safety_context_confirmation_required", problem.Code);
    }

    [Fact]
    public void Normalize_requires_synthetic_data_confirmation()
    {
        var problem = Assert.Throws<TelehealthProblem>(() =>
            TelehealthApplicantRequestUniversalSafetyPolicy.Normalize(Valid(synthetic: false)));

        Assert.Equal("telehealth_applicant_request_safety_synthetic_confirmation_required", problem.Code);
    }

    [Theory]
    [InlineData(TelehealthTriageOutcome.Emergency, "EmergencyRedirected", "EmergencyCareNow", false, false, false, true)]
    [InlineData(TelehealthTriageOutcome.UrgentInPerson, "InPersonRecommended", "PromptInPersonCare", false, false, false, true)]
    [InlineData(TelehealthTriageOutcome.InPersonRequired, "InPersonRecommended", "InPersonCareRequired", false, false, false, true)]
    [InlineData(TelehealthTriageOutcome.ClinicalReview, "ClinicalReview", "ClinicalReviewRequired", false, false, true, false)]
    [InlineData(TelehealthTriageOutcome.TelehealthEligible, "SafetyScreening", "UniversalSafetyPassed", true, true, false, false)]
    public void Outcome_mapping_preserves_universal_screen_boundary(
        TelehealthTriageOutcome outcome,
        string status,
        string disposition,
        bool passed,
        bool complaintTriageRequired,
        bool clinicalReviewRequired,
        bool terminal)
    {
        Assert.Equal(status, TelehealthApplicantRequestUniversalSafetyPolicy.ResultingRequestStatus(outcome));
        Assert.Equal(disposition, TelehealthApplicantRequestUniversalSafetyPolicy.PublicDisposition(outcome));
        Assert.Equal(passed, TelehealthApplicantRequestUniversalSafetyPolicy.UniversalSafetyPassed(outcome));
        Assert.Equal(complaintTriageRequired, TelehealthApplicantRequestUniversalSafetyPolicy.ComplaintSpecificTriageRequired(outcome));
        Assert.Equal(clinicalReviewRequired, TelehealthApplicantRequestUniversalSafetyPolicy.ClinicalReviewRequired(outcome));
        Assert.Equal(terminal, TelehealthApplicantRequestUniversalSafetyPolicy.TerminalForTelehealth(outcome));
    }

    [Fact]
    public void Snapshot_is_deterministic_source_bound_masked_and_freshness_capped()
    {
        var requestId = Guid.Parse("51000000-0000-4000-8000-000000000051");
        var creationId = Guid.Parse("52000000-0000-4000-8000-000000000052");
        var confirmationId = Guid.Parse("53000000-0000-4000-8000-000000000053");
        var locationId = Guid.Parse("54000000-0000-4000-8000-000000000054");
        var safetyId = Guid.Parse("55000000-0000-4000-8000-000000000055");
        var confirmedAt = new DateTimeOffset(2026, 8, 28, 12, 0, 0, TimeSpan.Zero);
        var applicantExpiresAt = confirmedAt.AddMinutes(20);

        var first = TelehealthApplicantRequestUniversalSafetyPolicy.Snapshot(
            requestId, creationId, confirmationId, locationId, safetyId, 2, "GA", "0123",
            confirmedAt, applicantExpiresAt);
        var replay = TelehealthApplicantRequestUniversalSafetyPolicy.Snapshot(
            requestId, creationId, confirmationId, locationId, safetyId, 2, "GA", "0123",
            confirmedAt, applicantExpiresAt);
        var changed = TelehealthApplicantRequestUniversalSafetyPolicy.Snapshot(
            requestId, creationId, confirmationId, locationId, safetyId, 2, "CA", "0123",
            confirmedAt, applicantExpiresAt);

        Assert.Equal(first, replay);
        Assert.NotEqual(first.Fingerprint, changed.Fingerprint);
        Assert.Equal("***-***-0123", first.MaskedCallbackPhone);
        Assert.Equal(applicantExpiresAt, first.ContextExpiresAt);
        Assert.DoesNotContain("0123", first.Fingerprint, StringComparison.Ordinal);
    }

    private static EvaluateTelehealthApplicantRequestUniversalSafety Valid(
        int version = 2,
        string? snapshot = null,
        string stateCode = "GA",
        bool locationConfirmed = true,
        bool callbackConfirmed = true,
        bool synthetic = true,
        bool? emergency = false,
        bool? severe = false,
        bool? handsOn = false,
        bool? unsure = false) => new(
            version,
            snapshot ?? Snapshot,
            stateCode,
            locationConfirmed,
            callbackConfirmed,
            synthetic,
            emergency,
            severe,
            handsOn,
            unsure);
}
