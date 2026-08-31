// SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
// SPDX-License-Identifier: GPL-3.0-or-later

using AvenChart.Api.Data;
using AvenChart.Api.Features.Telehealth;
using AvenChart.Api.Security;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;

namespace AvenChart.Api.Tests.Telehealth;

public sealed class TelehealthSyntheticAfterVisitPlanPreviewServiceTests
{
    [Fact]
    public async Task PatientReadHidesAnUnconfiguredBrandedHostBeforeSessionOrRepositoryAccess()
    {
        var problem = await Assert.ThrowsAsync<TelehealthProblem>(() =>
            Service().GetForPatientAsync(Context("other.example.test"), Guid.NewGuid(), CancellationToken.None));

        Assert.Equal(StatusCodes.Status404NotFound, problem.StatusCode);
        Assert.Equal("telehealth_practice_not_found", problem.Code);
    }

    [Fact]
    public async Task ApplicantReadRequiresAValidAccessKeyBeforeRepositoryAccess()
    {
        var problem = await Assert.ThrowsAsync<TelehealthProblem>(() =>
            Service().GetForApplicantAsync(Context("localhost"), Guid.NewGuid(), Guid.NewGuid(), "too-short", CancellationToken.None));

        Assert.Equal(StatusCodes.Status401Unauthorized, problem.StatusCode);
        Assert.Equal("telehealth_applicant_access_key_required", problem.Code);
    }

    [Fact]
    public async Task PatientReadRequiresAnActivePortalSessionBeforeRepositoryAccess()
    {
        var problem = await Assert.ThrowsAsync<TelehealthProblem>(() =>
            Service().GetForPatientAsync(Context("localhost"), Guid.NewGuid(), CancellationToken.None));

        Assert.Equal(StatusCodes.Status401Unauthorized, problem.StatusCode);
        Assert.Equal("telehealth_patient_session_required", problem.Code);
    }

    private static TelehealthSyntheticAfterVisitPlanPreviewService Service() => new(
        null!,
        null!,
        new NoSessionIdentityAdapter(),
        Options.Create(new TelehealthOptions
        {
            PracticeId = "avenchart-synthetic-practice",
            FacilityId = 10,
            BrandedHosts = ["localhost"],
            SupportedStates = ["GA", "CA", "FL"]
        }));

    private static DefaultHttpContext Context(string host)
    {
        var context = new DefaultHttpContext();
        context.Request.Host = new HostString(host);
        return context;
    }

    private sealed class NoSessionIdentityAdapter : IPatientPortalIdentityAdapter
    {
        public Task<Guid?> ResolveSessionIdAsync(HttpContext httpContext, CancellationToken cancellationToken) => Task.FromResult<Guid?>(null);
    }
}
