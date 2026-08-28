// SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
// SPDX-License-Identifier: GPL-3.0-or-later

using AvenChart.Api.Features.Telehealth;
using AvenChart.Api.Models;
using AvenChart.Api.Security;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;

namespace AvenChart.Api.Tests.Telehealth;

public sealed class TelehealthConsultationServiceTests
{
    [Fact]
    public async Task StartRejectsANonPhysicianBeforeRepositoryAccess()
    {
        var problem = await Assert.ThrowsAsync<TelehealthProblem>(() =>
            Service().StartAsync(Session("frontdesk"), Access(), Guid.NewGuid(), ValidRequest(), "consultation-key", CancellationToken.None));

        Assert.Equal(StatusCodes.Status403Forbidden, problem.StatusCode);
        Assert.Equal("telehealth_physician_role_required", problem.Code);
    }

    [Fact]
    public async Task StartRejectsAnUnsupportedPatientLocationBeforeRepositoryAccess()
    {
        var problem = await Assert.ThrowsAsync<TelehealthProblem>(() =>
            Service().StartAsync(Session(), Access(), Guid.NewGuid(), ValidRequest() with { PatientLocationState = "NY" }, "consultation-key", CancellationToken.None));

        Assert.Equal(StatusCodes.Status400BadRequest, problem.StatusCode);
        Assert.Equal("telehealth_location_unsupported", problem.Code);
    }

    [Fact]
    public async Task StartRejectsAConcerningSymptomChangeBeforeRepositoryAccess()
    {
        var problem = await Assert.ThrowsAsync<TelehealthProblem>(() =>
            Service().StartAsync(Session(), Access(), Guid.NewGuid(), ValidRequest() with { NoConcerningSymptomChange = false }, "consultation-key", CancellationToken.None));

        Assert.Equal(StatusCodes.Status400BadRequest, problem.StatusCode);
        Assert.Equal("telehealth_consultation_safety_recheck_failed", problem.Code);
    }

    [Fact]
    public async Task StartRejectsAnyIncompleteAffirmativeChecklistBeforeRepositoryAccess()
    {
        var problem = await Assert.ThrowsAsync<TelehealthProblem>(() =>
            Service().StartAsync(Session(), Access(), Guid.NewGuid(), ValidRequest() with { CommunicationSufficient = false }, "consultation-key", CancellationToken.None));

        Assert.Equal(StatusCodes.Status400BadRequest, problem.StatusCode);
        Assert.Equal("telehealth_consultation_start_checklist_incomplete", problem.Code);
    }

    [Fact]
    public async Task WorkspaceRejectsANonPhysicianBeforeRepositoryAccess()
    {
        var problem = await Assert.ThrowsAsync<TelehealthProblem>(() =>
            Service().GetWorkspaceAsync(Session("frontdesk"), Access(), Guid.NewGuid(), CancellationToken.None));

        Assert.Equal(StatusCodes.Status403Forbidden, problem.StatusCode);
        Assert.Equal("telehealth_physician_role_required", problem.Code);
    }

    [Fact]
    public async Task WorkspaceRejectsAResourceFromAnotherFacilityBeforeRepositoryAccess()
    {
        var problem = await Assert.ThrowsAsync<TelehealthProblem>(() =>
            Service().GetWorkspaceAsync(Session(), new StaffAccessContext(20, "SYN2", "Other Facility", "treatment"), Guid.NewGuid(), CancellationToken.None));

        Assert.Equal(StatusCodes.Status404NotFound, problem.StatusCode);
    }

    [Fact]
    public async Task WorkspaceRequiresAnActiveStaffBindingBeforeRepositoryAccess()
    {
        var session = Session() with { StaffId = null };
        var problem = await Assert.ThrowsAsync<TelehealthProblem>(() =>
            Service().GetWorkspaceAsync(session, Access(), Guid.NewGuid(), CancellationToken.None));

        Assert.Equal(StatusCodes.Status403Forbidden, problem.StatusCode);
        Assert.Equal("telehealth_staff_record_required", problem.Code);
    }

    [Fact]
    public async Task DocumentationRejectsANonPhysicianBeforeRepositoryAccess()
    {
        var problem = await Assert.ThrowsAsync<TelehealthProblem>(() =>
            Service().SaveDocumentationDraftAsync(
                Session("frontdesk"),
                Access(),
                Guid.NewGuid(),
                ValidDocumentation(),
                CancellationToken.None));

        Assert.Equal(StatusCodes.Status403Forbidden, problem.StatusCode);
        Assert.Equal("telehealth_physician_role_required", problem.Code);
    }

    [Fact]
    public async Task DocumentationHidesAResourceFromAnotherFacilityBeforeRepositoryAccess()
    {
        var problem = await Assert.ThrowsAsync<TelehealthProblem>(() =>
            Service().SaveDocumentationDraftAsync(
                Session(),
                new StaffAccessContext(20, "SYN2", "Other Facility", "treatment"),
                Guid.NewGuid(),
                ValidDocumentation(),
                CancellationToken.None));

        Assert.Equal(StatusCodes.Status404NotFound, problem.StatusCode);
    }

    [Fact]
    public async Task DocumentationRequiresAnActiveStaffBindingBeforeRepositoryAccess()
    {
        var problem = await Assert.ThrowsAsync<TelehealthProblem>(() =>
            Service().SaveDocumentationDraftAsync(
                Session() with { StaffId = null },
                Access(),
                Guid.NewGuid(),
                ValidDocumentation(),
                CancellationToken.None));

        Assert.Equal(StatusCodes.Status403Forbidden, problem.StatusCode);
        Assert.Equal("telehealth_staff_record_required", problem.Code);
    }

    [Fact]
    public async Task DocumentationRejectsANegativeExpectedVersionBeforeRepositoryAccess()
    {
        var problem = await Assert.ThrowsAsync<TelehealthProblem>(() =>
            Service().SaveDocumentationDraftAsync(
                Session(),
                Access(),
                Guid.NewGuid(),
                ValidDocumentation() with { ExpectedVersion = -1 },
                CancellationToken.None));

        Assert.Equal(StatusCodes.Status400BadRequest, problem.StatusCode);
        Assert.Equal("telehealth_documentation_version_invalid", problem.Code);
    }

    [Fact]
    public async Task WrapUpRejectsANonPhysicianBeforeRepositoryAccess()
    {
        var problem = await Assert.ThrowsAsync<TelehealthProblem>(() =>
            Service().EnterWrapUpAsync(
                Session("frontdesk"), Access(), Guid.NewGuid(), ValidWrapUp(), "wrap-up-key", CancellationToken.None));

        Assert.Equal(StatusCodes.Status403Forbidden, problem.StatusCode);
        Assert.Equal("telehealth_physician_role_required", problem.Code);
    }

    [Fact]
    public async Task WrapUpHidesAResourceFromAnotherFacilityBeforeRepositoryAccess()
    {
        var problem = await Assert.ThrowsAsync<TelehealthProblem>(() =>
            Service().EnterWrapUpAsync(
                Session(),
                new StaffAccessContext(20, "SYN2", "Other Facility", "treatment"),
                Guid.NewGuid(),
                ValidWrapUp(),
                "wrap-up-key",
                CancellationToken.None));

        Assert.Equal(StatusCodes.Status404NotFound, problem.StatusCode);
    }

    [Fact]
    public async Task WrapUpRequiresAnActiveStaffBindingBeforeRepositoryAccess()
    {
        var problem = await Assert.ThrowsAsync<TelehealthProblem>(() =>
            Service().EnterWrapUpAsync(
                Session() with { StaffId = null }, Access(), Guid.NewGuid(), ValidWrapUp(), "wrap-up-key", CancellationToken.None));

        Assert.Equal(StatusCodes.Status403Forbidden, problem.StatusCode);
        Assert.Equal("telehealth_staff_record_required", problem.Code);
    }

    [Fact]
    public async Task WrapUpRejectsANonPositiveExpectedVersionBeforeRepositoryAccess()
    {
        var problem = await Assert.ThrowsAsync<TelehealthProblem>(() =>
            Service().EnterWrapUpAsync(
                Session(), Access(), Guid.NewGuid(), ValidWrapUp() with { ExpectedVersion = 0 }, "wrap-up-key", CancellationToken.None));

        Assert.Equal(StatusCodes.Status400BadRequest, problem.StatusCode);
        Assert.Equal("telehealth_consultation_version_invalid", problem.Code);
    }

    [Fact]
    public async Task WrapUpRejectsAnIncompleteAcknowledgmentSetBeforeRepositoryAccess()
    {
        var problem = await Assert.ThrowsAsync<TelehealthProblem>(() =>
            Service().EnterWrapUpAsync(
                Session(),
                Access(),
                Guid.NewGuid(),
                ValidWrapUp() with { WrapUpResponsibilityAcknowledged = false },
                "wrap-up-key",
                CancellationToken.None));

        Assert.Equal(StatusCodes.Status400BadRequest, problem.StatusCode);
        Assert.Equal("telehealth_wrap_up_acknowledgments_required", problem.Code);
    }

    [Fact]
    public async Task PharmacySearchRejectsANonPhysicianBeforeRepositoryAccess()
    {
        var problem = await Assert.ThrowsAsync<TelehealthProblem>(() =>
            Service().GetPharmacyChoicesAsync(
                Session("frontdesk"), Access(), Guid.NewGuid(), null, "GA", null, null, false, 25, CancellationToken.None));

        Assert.Equal(StatusCodes.Status403Forbidden, problem.StatusCode);
        Assert.Equal("telehealth_physician_role_required", problem.Code);
    }

    [Fact]
    public async Task PharmacySearchRequiresLocationAcknowledgmentBeforeRepositoryAccess()
    {
        var problem = await Assert.ThrowsAsync<TelehealthProblem>(() =>
            Service().GetPharmacyChoicesAsync(
                Session(), Access(), Guid.NewGuid(), null, "GA", null, "30303", false, 25, CancellationToken.None));

        Assert.Equal(StatusCodes.Status400BadRequest, problem.StatusCode);
        Assert.Equal("telehealth_pharmacy_location_acknowledgment_required", problem.Code);
    }

    [Fact]
    public async Task PharmacySearchRejectsUnsupportedStateBeforeRepositoryAccess()
    {
        var problem = await Assert.ThrowsAsync<TelehealthProblem>(() =>
            Service().GetPharmacyChoicesAsync(
                Session(), Access(), Guid.NewGuid(), null, "NY", null, null, false, 25, CancellationToken.None));

        Assert.Equal(StatusCodes.Status400BadRequest, problem.StatusCode);
        Assert.Equal("telehealth_pharmacy_search_state_invalid", problem.Code);
    }

    [Fact]
    public async Task PharmacyChoiceRequiresPatientAndSyntheticConfirmationsBeforeRepositoryAccess()
    {
        var problem = await Assert.ThrowsAsync<TelehealthProblem>(() =>
            Service().RecordPharmacyChoiceAsync(
                Session(),
                Access(),
                Guid.NewGuid(),
                new RecordTelehealthPharmacyChoiceRequest(0, Guid.NewGuid(), false, true),
                "pharmacy-choice-key",
                CancellationToken.None));

        Assert.Equal(StatusCodes.Status400BadRequest, problem.StatusCode);
        Assert.Equal("telehealth_pharmacy_choice_acknowledgments_required", problem.Code);
    }

    [Fact]
    public async Task CompletionPrerequisitesRejectANonPhysicianBeforeRepositoryAccess()
    {
        var problem = await Assert.ThrowsAsync<TelehealthProblem>(() =>
            Service().GetCompletionPrerequisitesAsync(
                Session("frontdesk"), Access(), Guid.NewGuid(), CancellationToken.None));

        Assert.Equal(StatusCodes.Status403Forbidden, problem.StatusCode);
        Assert.Equal("telehealth_physician_role_required", problem.Code);
    }

    [Fact]
    public async Task CompletionPrerequisitesHideAResourceFromAnotherFacilityBeforeRepositoryAccess()
    {
        var problem = await Assert.ThrowsAsync<TelehealthProblem>(() =>
            Service().GetCompletionPrerequisitesAsync(
                Session(),
                new StaffAccessContext(20, "SYN2", "Other Facility", "treatment"),
                Guid.NewGuid(),
                CancellationToken.None));

        Assert.Equal(StatusCodes.Status404NotFound, problem.StatusCode);
    }

    [Fact]
    public async Task CompletionPrerequisitesRequireAnActiveStaffBindingBeforeRepositoryAccess()
    {
        var problem = await Assert.ThrowsAsync<TelehealthProblem>(() =>
            Service().GetCompletionPrerequisitesAsync(
                Session() with { StaffId = null }, Access(), Guid.NewGuid(), CancellationToken.None));

        Assert.Equal(StatusCodes.Status403Forbidden, problem.StatusCode);
        Assert.Equal("telehealth_staff_record_required", problem.Code);
    }

    private static TelehealthConsultationService Service() => new(
        null!,
        null!,
        null!,
        null!,
        new SyntheticTelehealthPharmacyDirectory(),
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

    private static StartTelehealthConsultationRequest ValidRequest() => new(
        10, "GA", true, true, true, true, true, true, true, true);

    private static TelehealthConsultationDocumentationDraftRequest ValidDocumentation() => new(
        0, "Synthetic patient history entered by the physician.", null, null, null);

    private static EnterTelehealthConsultationWrapUpRequest ValidWrapUp() => new(1, true, true, true);
}
