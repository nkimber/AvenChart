// SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
// SPDX-License-Identifier: GPL-3.0-or-later

using AvenChart.Api.Features.Telehealth;
using AvenChart.Api.Models;
using AvenChart.Api.Security;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;

namespace AvenChart.Api.Tests.Telehealth;

public sealed class TelehealthPrescriptionServiceTests
{
    [Fact]
    public async Task CatalogRejectsANonPhysicianBeforeRepositoryAccess()
    {
        var problem = await Assert.ThrowsAsync<TelehealthProblem>(() => Service().GetWorkspaceAsync(
            Session("frontdesk"), Access(), Guid.NewGuid(), "metformin", CancellationToken.None));

        Assert.Equal(StatusCodes.Status403Forbidden, problem.StatusCode);
        Assert.Equal("telehealth_physician_role_required", problem.Code);
    }

    [Fact]
    public async Task CatalogHidesAnotherFacilityBeforeRepositoryAccess()
    {
        var problem = await Assert.ThrowsAsync<TelehealthProblem>(() => Service().GetWorkspaceAsync(
            Session(), new StaffAccessContext(20, "SYN2", "Other Facility", "treatment"),
            Guid.NewGuid(), "metformin", CancellationToken.None));

        Assert.Equal(StatusCodes.Status404NotFound, problem.StatusCode);
    }

    [Fact]
    public async Task CatalogRequiresAnActiveStaffBindingBeforeRepositoryAccess()
    {
        var problem = await Assert.ThrowsAsync<TelehealthProblem>(() => Service().GetWorkspaceAsync(
            Session() with { StaffId = null }, Access(), Guid.NewGuid(), "metformin", CancellationToken.None));

        Assert.Equal(StatusCodes.Status403Forbidden, problem.StatusCode);
        Assert.Equal("telehealth_staff_record_required", problem.Code);
    }

    [Fact]
    public async Task CatalogRequiresAnIntentionalSearchBeforeRepositoryAccess()
    {
        var problem = await Assert.ThrowsAsync<TelehealthProblem>(() => Service().GetWorkspaceAsync(
            Session(), Access(), Guid.NewGuid(), "m", CancellationToken.None));

        Assert.Equal(StatusCodes.Status400BadRequest, problem.StatusCode);
        Assert.Equal("telehealth_prescription_catalog_query_invalid", problem.Code);
    }

    [Fact]
    public async Task DraftRequiresEveryClinicalAcknowledgmentBeforeRepositoryAccess()
    {
        var problem = await Assert.ThrowsAsync<TelehealthProblem>(() => Service().RecordAsync(
            Session(), Access(), Guid.NewGuid(), ValidRequest() with { AllergyListReviewed = false },
            "prescription-draft-key", CancellationToken.None));

        Assert.Equal(StatusCodes.Status400BadRequest, problem.StatusCode);
        Assert.Equal("telehealth_prescription_draft_invalid", problem.Code);
    }

    [Fact]
    public async Task DraftRequiresBoundedExplicitStructuredDirectionsBeforeRepositoryAccess()
    {
        var problem = await Assert.ThrowsAsync<TelehealthProblem>(() => Service().RecordAsync(
            Session(), Access(), Guid.NewGuid(), ValidRequest() with { Directions = " " },
            "prescription-draft-key", CancellationToken.None));

        Assert.Equal(StatusCodes.Status400BadRequest, problem.StatusCode);
        Assert.Equal("telehealth_prescription_draft_invalid", problem.Code);
    }

    [Fact]
    public async Task SigningRequiresEveryFailClosedAttestationBeforeRepositoryAccess()
    {
        var problem = await Assert.ThrowsAsync<TelehealthProblem>(() => Service().SignAsync(
            Session(), Access(), Guid.NewGuid(), new SignTelehealthPrescriptionRequest(
                ExpectedDraftVersion: 1,
                NoCurrentMedicationsConfirmed: true,
                NoKnownAllergiesConfirmed: false,
                AdequateEvaluationConfirmed: true,
                SyntheticDataConfirmed: true),
            "prescription-signing-key", CancellationToken.None));

        Assert.Equal(StatusCodes.Status400BadRequest, problem.StatusCode);
        Assert.Equal("telehealth_prescription_signing_invalid", problem.Code);
    }

    [Theory]
    [InlineData(0, 0, true)]
    [InlineData(1, 0, false)]
    [InlineData(0, 1, false)]
    public void ConservativeSafetyGatewayPassesOnlyForConfirmedEmptyCanonicalLists(
        int activeMedications,
        int activeAllergies,
        bool expected)
    {
        var result = new SyntheticTelehealthPrescriptionSafetyGateway().Evaluate(
            new TelehealthPrescriptionSafetyInput(
                activeMedications,
                activeAllergies,
                NoCurrentMedicationsConfirmed: true,
                NoKnownAllergiesConfirmed: true));

        Assert.Equal(expected, result.Passed);
        Assert.False(result.ExternalSourceContacted);
    }

    [Fact]
    public void EPrescriptionGatewayPreparesTheCurrentTargetWithoutExternalCapability()
    {
        var result = new SyntheticEPrescriptionGateway().PrepareNewRx();

        Assert.Equal("NCPDP_SCRIPT_2023011", result.TargetStandard);
        Assert.Equal("NCPDP_SCRIPT_2017071_THROUGH_2027_12_31", result.TransitionStandard);
        Assert.Equal("PreparedOnly", result.TransmissionState);
        Assert.False(result.Certified);
        Assert.False(result.ExternalDestinationContacted);
    }

    private static TelehealthPrescriptionService Service() => new(
        null!,
        Options.Create(new TelehealthOptions
        {
            PracticeId = "avenchart-synthetic-practice",
            FacilityId = 10,
            SupportedStates = ["GA", "CA", "FL"]
        }));

    private static AuthSessionResponse Session(string role = "provider") => new(
        true, Guid.NewGuid(), "synthetic-physician", "Synthetic Physician", role, 101,
        DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddMinutes(10), null,
        null, "local");

    private static StaffAccessContext Access() => new(10, "SYN", "Synthetic Facility", "treatment");

    private static RecordTelehealthPrescriptionPreparationDraftRequest ValidRequest() => new(
        ExpectedVersion: 0,
        RxNormCode: "860975",
        DoseAmount: 500,
        DoseUnit: "mg",
        Frequency: "twice daily",
        QuantityValue: 60,
        QuantityUnit: "tablets",
        DurationDays: 30,
        Refills: 0,
        Indication: "Physician-entered synthetic indication.",
        Directions: "Physician-entered synthetic directions.",
        MedicationListReviewed: true,
        AllergyListReviewed: true,
        AdequateEvaluationCompleted: true,
        SyntheticDataConfirmed: true);
}
