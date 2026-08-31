// SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
// SPDX-License-Identifier: GPL-3.0-or-later

using AvenChart.Api.Features.Telehealth;
using AvenChart.Api.Models;
using AvenChart.Api.Security;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;

namespace AvenChart.Api.Tests.Telehealth;

public sealed class TelehealthConnectionAbandonServiceTests
{
    [Fact]
    public async Task AbandonRejectsANonPhysicianBeforeRepositoryAccess()
    {
        var problem = await Assert.ThrowsAsync<TelehealthProblem>(() =>
            Service().AbandonConnectionAsync(
                Session("frontdesk"), Access(), Guid.NewGuid(), ValidRequest(), "abandon-key", CancellationToken.None));

        Assert.Equal(StatusCodes.Status403Forbidden, problem.StatusCode);
        Assert.Equal("telehealth_physician_role_required", problem.Code);
    }

    [Fact]
    public async Task AbandonRejectsANonPositiveExpectedVersionBeforeRepositoryAccess()
    {
        var problem = await Assert.ThrowsAsync<TelehealthProblem>(() =>
            Service().AbandonConnectionAsync(
                Session(), Access(), Guid.NewGuid(), ValidRequest() with { ExpectedVersion = 0 }, "abandon-key", CancellationToken.None));

        Assert.Equal(StatusCodes.Status400BadRequest, problem.StatusCode);
        Assert.Equal("telehealth_connection_abandon_invalid", problem.Code);
    }

    [Fact]
    public async Task AbandonRequiresBothExplicitPreConsultationConfirmationsBeforeRepositoryAccess()
    {
        var problem = await Assert.ThrowsAsync<TelehealthProblem>(() =>
            Service().AbandonConnectionAsync(
                Session(), Access(), Guid.NewGuid(), ValidRequest() with { SyntheticConnectionAbandonConfirmed = false }, "abandon-key", CancellationToken.None));

        Assert.Equal(StatusCodes.Status400BadRequest, problem.StatusCode);
        Assert.Equal("telehealth_connection_abandon_invalid", problem.Code);
    }

    private static TelehealthService Service() => new(
        null!, null!, null!, null!, null!,
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

    private static AbandonTelehealthConnectionRequest ValidRequest() => new(15, true, true);
}
