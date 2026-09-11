// SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
// SPDX-License-Identifier: GPL-3.0-or-later

using AvenChart.Api.Infrastructure;

namespace AvenChart.Api.Tests;

public sealed class AzureRevisionSelectionTests
{
    [Fact]
    public void RollbackSelectsHealthyRetainedRevisionNotCurrentOrFailedRevision()
    {
        const string revisions = """
            [
              {"name":"current","properties":{"healthState":"Healthy","trafficWeight":100,"createdTime":"2026-09-11"}},
              {"name":"failed","properties":{"healthState":"Unhealthy","trafficWeight":0,"createdTime":"2026-09-10"}},
              {"name":"retained","properties":{"active":false,"healthState":"Healthy","trafficWeight":0,"createdTime":"2026-09-09"}},
              {"name":"older","properties":{"healthState":"Healthy","trafficWeight":0,"createdTime":"2026-09-08"}}
            ]
            """;
        Assert.Equal("retained", AzureDeploymentCoordinator.SelectPreviousRevision(revisions));
    }

    [Fact]
    public void RollbackRefusesUnknownHealth()
    {
        Assert.Throws<InvalidOperationException>(() => AzureDeploymentCoordinator.SelectPreviousRevision(
            """[{"name":"unknown","properties":{"trafficWeight":0}}]"""));
    }
}
