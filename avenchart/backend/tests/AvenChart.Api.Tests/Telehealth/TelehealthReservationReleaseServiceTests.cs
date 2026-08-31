// SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
// SPDX-License-Identifier: GPL-3.0-or-later

using AvenChart.Api.Features.Telehealth;
using AvenChart.Api.Models;
using AvenChart.Api.Security;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;

namespace AvenChart.Api.Tests.Telehealth;

public sealed class TelehealthReservationReleaseServiceTests
{
    [Fact]
    public async Task ReleaseRejectsANonPhysicianBeforeRepositoryAccess()
    {
        var problem = await Assert.ThrowsAsync<TelehealthProblem>(() =>
            Service().ReleaseReservationAsync(
                Session("frontdesk"), Access(), Guid.NewGuid(), ValidRequest(), "release-key", CancellationToken.None));

        Assert.Equal(StatusCodes.Status403Forbidden, problem.StatusCode);
        Assert.Equal("telehealth_physician_role_required", problem.Code);
    }

    [Fact]
    public async Task ReleaseRejectsANonPositiveExpectedVersionBeforeRepositoryAccess()
    {
        var problem = await Assert.ThrowsAsync<TelehealthProblem>(() =>
            Service().ReleaseReservationAsync(
                Session(), Access(), Guid.NewGuid(), ValidRequest() with { ExpectedVersion = 0 }, "release-key", CancellationToken.None));

        Assert.Equal(StatusCodes.Status400BadRequest, problem.StatusCode);
        Assert.Equal("telehealth_reservation_release_invalid", problem.Code);
    }

    [Fact]
    public async Task ReleaseRequiresBothExplicitPreConnectionConfirmationsBeforeRepositoryAccess()
    {
        var problem = await Assert.ThrowsAsync<TelehealthProblem>(() =>
            Service().ReleaseReservationAsync(
                Session(), Access(), Guid.NewGuid(), ValidRequest() with { SyntheticReleaseConfirmed = false }, "release-key", CancellationToken.None));

        Assert.Equal(StatusCodes.Status400BadRequest, problem.StatusCode);
        Assert.Equal("telehealth_reservation_release_invalid", problem.Code);
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

    private static ReleaseTelehealthReservationRequest ValidRequest() => new(14, true, true);
}
