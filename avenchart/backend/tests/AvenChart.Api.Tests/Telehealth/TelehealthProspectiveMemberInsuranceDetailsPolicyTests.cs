// SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
// SPDX-License-Identifier: GPL-3.0-or-later

using AvenChart.Api.Features.Telehealth;
using Microsoft.AspNetCore.DataProtection;

namespace AvenChart.Api.Tests.Telehealth;

public sealed class TelehealthProspectiveMemberInsuranceDetailsPolicyTests
{
    private static readonly DateOnly Today = new(2026, 8, 27);

    [Fact]
    public void Normalize_accepts_and_normalizes_self_subscriber_details()
    {
        var result = Normalize(SelfRequest(memberId: " syn-hm-1001 ", groupNumber: " syn-group-01 "));

        Assert.Equal(6, result.ExpectedVersion);
        Assert.Equal("SYN-HM-1001", result.MemberId);
        Assert.Equal("SYN-GROUP-01", result.GroupNumber);
        Assert.Equal("Self", result.SubscriberRelationship);
        Assert.Null(result.SubscriberFirstName);
        Assert.Null(result.SubscriberLastName);
        Assert.Null(result.SubscriberDateOfBirth);
    }

    [Fact]
    public void Normalize_allows_an_absent_group_number()
    {
        var result = Normalize(SelfRequest(groupNumber: "  "));

        Assert.Null(result.GroupNumber);
    }

    [Theory]
    [InlineData("Spouse")]
    [InlineData("Parent")]
    [InlineData("Other")]
    public void Normalize_requires_complete_non_self_subscriber_identity(string relationship)
    {
        var result = Normalize(NonSelfRequest(relationship));

        Assert.Equal(relationship, result.SubscriberRelationship);
        Assert.Equal("Morgan", result.SubscriberFirstName);
        Assert.Equal("Example", result.SubscriberLastName);
        Assert.Equal(new DateOnly(1980, 2, 3), result.SubscriberDateOfBirth);
    }

    [Theory]
    [InlineData(0, "SYN-HM-1001", "telehealth_applicant_version_invalid")]
    [InlineData(6, "HM-1001", "telehealth_applicant_synthetic_insurance_identifier_invalid")]
    [InlineData(6, "SYN-A", "telehealth_applicant_synthetic_insurance_identifier_invalid")]
    [InlineData(6, "SYN-ABC_123", "telehealth_applicant_synthetic_insurance_identifier_invalid")]
    [InlineData(6, "SYN-ABCDEFGHIJKLMNOPQRSTUVWXYZ12345", "telehealth_applicant_synthetic_insurance_identifier_invalid")]
    public void Normalize_rejects_invalid_version_or_member_identifier(
        int version,
        string memberId,
        string expectedCode)
    {
        var problem = Assert.Throws<TelehealthProblem>(() => Normalize(SelfRequest(
            expectedVersion: version,
            memberId: memberId)));

        Assert.Equal(expectedCode, problem.Code);
    }

    [Theory]
    [InlineData("GROUP-01")]
    [InlineData("SYN-A")]
    [InlineData("SYN-GROUP 01")]
    public void Normalize_rejects_invalid_present_group_identifier(string groupNumber)
    {
        var problem = Assert.Throws<TelehealthProblem>(() => Normalize(SelfRequest(groupNumber: groupNumber)));

        Assert.Equal("telehealth_applicant_synthetic_insurance_identifier_invalid", problem.Code);
    }

    [Fact]
    public void Normalize_requires_synthetic_acknowledgment()
    {
        var problem = Assert.Throws<TelehealthProblem>(() => Normalize(SelfRequest(syntheticConfirmed: false)));

        Assert.Equal("telehealth_applicant_member_details_synthetic_acknowledgment_required", problem.Code);
    }

    [Fact]
    public void Normalize_requires_detail_confirmation()
    {
        var problem = Assert.Throws<TelehealthProblem>(() => Normalize(SelfRequest(detailsConfirmed: false)));

        Assert.Equal("telehealth_applicant_member_details_confirmation_required", problem.Code);
    }

    [Theory]
    [InlineData("")]
    [InlineData("self")]
    [InlineData("Child")]
    public void Normalize_rejects_unknown_relationship(string relationship)
    {
        var problem = Assert.Throws<TelehealthProblem>(() => Normalize(SelfRequest(relationship: relationship)));

        Assert.Equal("telehealth_applicant_subscriber_relationship_invalid", problem.Code);
    }

    [Fact]
    public void Normalize_rejects_reentered_identity_for_self()
    {
        var request = SelfRequest() with { SubscriberFirstName = "Taylor" };

        var problem = Assert.Throws<TelehealthProblem>(() => Normalize(request));

        Assert.Equal("telehealth_applicant_self_subscriber_details_forbidden", problem.Code);
    }

    [Theory]
    [InlineData(null, "Example", "1980-02-03", "telehealth_applicant_subscriber_name_invalid")]
    [InlineData("Morgan", null, "1980-02-03", "telehealth_applicant_subscriber_name_invalid")]
    [InlineData("Morgan", "Example", null, "telehealth_applicant_subscriber_birth_date_invalid")]
    [InlineData("Morgan", "Example", "2010-02-03", "telehealth_applicant_subscriber_birth_date_invalid")]
    [InlineData("Morgan", "Example", "1900-02-03", "telehealth_applicant_subscriber_birth_date_invalid")]
    public void Normalize_rejects_incomplete_or_invalid_non_self_identity(
        string? firstName,
        string? lastName,
        string? birthDate,
        string expectedCode)
    {
        var request = NonSelfRequest("Spouse") with
        {
            SubscriberFirstName = firstName,
            SubscriberLastName = lastName,
            SubscriberDateOfBirth = birthDate
        };

        var problem = Assert.Throws<TelehealthProblem>(() => Normalize(request));

        Assert.Equal(expectedCode, problem.Code);
    }

    [Fact]
    public void ResolveSubscriber_rebinds_self_identity_from_the_applicant()
    {
        var normalized = Normalize(SelfRequest());

        var payload = TelehealthProspectiveMemberInsuranceDetailsPolicy.ResolveSubscriber(
            normalized,
            "Taylor",
            "Applicant",
            new DateOnly(1985, 4, 5));

        Assert.Equal("Taylor", payload.SubscriberFirstName);
        Assert.Equal("Applicant", payload.SubscriberLastName);
        Assert.Equal(new DateOnly(1985, 4, 5), payload.SubscriberDateOfBirth);
        Assert.Equal("Primary", payload.CoveragePriority);
    }

    [Fact]
    public void ResolveSubscriber_uses_confirmed_non_self_identity()
    {
        var normalized = Normalize(NonSelfRequest("Parent"));

        var payload = TelehealthProspectiveMemberInsuranceDetailsPolicy.ResolveSubscriber(
            normalized,
            "Ignored",
            "Applicant",
            new DateOnly(1985, 4, 5));

        Assert.Equal("Morgan", payload.SubscriberFirstName);
        Assert.Equal("Example", payload.SubscriberLastName);
        Assert.Equal(new DateOnly(1980, 2, 3), payload.SubscriberDateOfBirth);
    }

    [Fact]
    public void Mask_exposes_only_the_last_four_characters()
    {
        Assert.Equal("••••1001", TelehealthProspectiveMemberInsuranceDetailsPolicy.Mask("SYN-HM-1001"));
    }

    [Fact]
    public void Protector_round_trips_for_exact_match_without_plaintext_ciphertext()
    {
        var protector = CreateProtector();
        var payload = Resolve(SelfRequest());

        var ciphertext = protector.Protect(payload);

        Assert.DoesNotContain(payload.MemberId, ciphertext, StringComparison.Ordinal);
        Assert.DoesNotContain(payload.GroupNumber!, ciphertext, StringComparison.Ordinal);
        Assert.DoesNotContain(payload.SubscriberFirstName, ciphertext, StringComparison.Ordinal);
        Assert.Equal(payload, protector.Unprotect(ciphertext));
        Assert.True(protector.Matches(ciphertext, payload));
    }

    [Fact]
    public void Protector_rejects_changed_semantic_content()
    {
        var protector = CreateProtector();
        var payload = Resolve(SelfRequest());
        var changed = payload with { MemberId = "SYN-HM-9999" };

        Assert.False(protector.Matches(protector.Protect(payload), changed));
    }

    [Fact]
    public void Protector_fails_closed_for_tampered_content()
    {
        var protector = CreateProtector();

        var problem = Assert.Throws<TelehealthProblem>(() => protector.Matches("tampered-payload", Resolve(SelfRequest())));

        Assert.Equal("telehealth_applicant_member_details_protection_invalid", problem.Code);
    }

    private static TelehealthProspectiveMemberInsuranceDetailsProtector CreateProtector() =>
        new(new EphemeralDataProtectionProvider());

    private static TelehealthProtectedMemberInsurancePayload Resolve(
        RecordTelehealthProspectiveMemberInsuranceDetailsRequest request) =>
        TelehealthProspectiveMemberInsuranceDetailsPolicy.ResolveSubscriber(
            Normalize(request),
            "Taylor",
            "Applicant",
            new DateOnly(1985, 4, 5));

    private static NormalizedTelehealthProspectiveMemberInsuranceDetails Normalize(
        RecordTelehealthProspectiveMemberInsuranceDetailsRequest request) =>
        TelehealthProspectiveMemberInsuranceDetailsPolicy.Normalize(request, Today);

    private static RecordTelehealthProspectiveMemberInsuranceDetailsRequest SelfRequest(
        int expectedVersion = 6,
        string memberId = "SYN-HM-1001",
        string? groupNumber = "SYN-GROUP-01",
        string relationship = "Self",
        bool detailsConfirmed = true,
        bool syntheticConfirmed = true) =>
        new(
            expectedVersion,
            memberId,
            groupNumber,
            relationship,
            null,
            null,
            null,
            detailsConfirmed,
            syntheticConfirmed);

    private static RecordTelehealthProspectiveMemberInsuranceDetailsRequest NonSelfRequest(string relationship) =>
        SelfRequest(relationship: relationship) with
        {
            SubscriberFirstName = " Morgan ",
            SubscriberLastName = " Example ",
            SubscriberDateOfBirth = "1980-02-03"
        };
}
