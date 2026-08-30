// SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
// SPDX-License-Identifier: GPL-3.0-or-later

using AvenChart.Api.Data;
using AvenChart.Api.Features.Telehealth;
using AvenChart.Api.Models;
using AvenChart.Api.Security;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;

namespace AvenChart.Api.Tests.Telehealth;

public sealed class TelehealthConversationServiceTests
{
    [Fact]
    public async Task PhysicianTranscriptRejectsNonPhysicianBeforeRepositoryAccess()
    {
        var problem = await Assert.ThrowsAsync<TelehealthProblem>(() => Service().GetForPhysicianAsync(
            Session("frontdesk"), Access(), Guid.NewGuid(), CancellationToken.None));

        Assert.Equal(StatusCodes.Status403Forbidden, problem.StatusCode);
        Assert.Equal("telehealth_physician_role_required", problem.Code);
    }

    [Fact]
    public async Task PhysicianTranscriptHidesAnotherFacilityBeforeRepositoryAccess()
    {
        var problem = await Assert.ThrowsAsync<TelehealthProblem>(() => Service().GetForPhysicianAsync(
            Session(), new StaffAccessContext(20, "SYN2", "Other Facility", "treatment"), Guid.NewGuid(), CancellationToken.None));

        Assert.Equal(StatusCodes.Status404NotFound, problem.StatusCode);
        Assert.Equal("telehealth_request_not_found", problem.Code);
    }

    [Fact]
    public async Task MessageRequiresExplicitSyntheticConfirmationBeforeRepositoryAccess()
    {
        var problem = await Assert.ThrowsAsync<TelehealthProblem>(() => Service().AddForPhysicianAsync(
            Session(), Access(), Guid.NewGuid(), new TelehealthConversationMessageRequest("Synthetic hello", false), CancellationToken.None));

        Assert.Equal(StatusCodes.Status400BadRequest, problem.StatusCode);
        Assert.Equal("telehealth_conversation_synthetic_confirmation_required", problem.Code);
    }

    [Fact]
    public async Task MessageRejectsControlCharactersBeforeRepositoryAccess()
    {
        var problem = await Assert.ThrowsAsync<TelehealthProblem>(() => Service().AddForPhysicianAsync(
            Session(), Access(), Guid.NewGuid(), new TelehealthConversationMessageRequest("Synthetic\nhello", true), CancellationToken.None));

        Assert.Equal(StatusCodes.Status400BadRequest, problem.StatusCode);
        Assert.Equal("telehealth_conversation_message_invalid", problem.Code);
    }

    private static TelehealthConversationService Service() => new(
        null!, null!, null!,
        Options.Create(new TelehealthOptions
        {
            PracticeId = "avenchart-synthetic-practice",
            FacilityId = 10,
            BrandedHosts = ["localhost"],
            SupportedStates = ["GA", "CA", "FL"]
        }));

    private static AuthSessionResponse Session(string role = "provider") => new(
        true, Guid.NewGuid(), "synthetic-physician", "Synthetic Physician", role, 101,
        DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddMinutes(10), null,
        null, "local");

    private static StaffAccessContext Access() => new(10, "SYN", "Synthetic Facility", "treatment");
}
