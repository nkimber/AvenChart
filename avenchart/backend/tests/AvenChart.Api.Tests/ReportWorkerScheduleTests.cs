// SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
// SPDX-License-Identifier: GPL-3.0-or-later

using AvenChart.Api.Configuration;
using AvenChart.Api.Workflows;

namespace AvenChart.Api.Tests;

public sealed class ReportWorkerScheduleTests
{
    [Fact]
    public void MaintenanceRunsImmediatelyThenOnIndependentCadence()
    {
        var schedule = new ReportWorkerSchedule(new ReportExecutionOptions());
        var now = DateTimeOffset.UtcNow;
        Assert.True(schedule.MaintenanceDue(now));
        schedule.Succeeded(now, performedMaintenance: true);
        for (var seconds = 2; seconds < 15; seconds += 2)
        {
            Assert.False(schedule.MaintenanceDue(now.AddSeconds(seconds)));
            schedule.Succeeded(now.AddSeconds(seconds), performedMaintenance: false);
        }
        Assert.True(schedule.MaintenanceDue(now.AddSeconds(15)));
    }

    [Fact]
    public void FailuresBackOffToCapAndSuccessResetsTheDelay()
    {
        var schedule = new ReportWorkerSchedule(new ReportExecutionOptions());
        foreach (var expected in new[] { 2, 4, 8, 16, 30, 30, 30 })
            Assert.Equal(TimeSpan.FromSeconds(expected), schedule.NextDelay(failed: true));
        schedule.Succeeded(DateTimeOffset.UtcNow, performedMaintenance: false);
        Assert.Equal(TimeSpan.FromSeconds(2), schedule.NextDelay(failed: false));
        Assert.Equal(TimeSpan.FromSeconds(2), schedule.NextDelay(failed: true));
    }

    [Fact]
    public void FailedMaintenanceRemainsDueForRetry()
    {
        var schedule = new ReportWorkerSchedule(new ReportExecutionOptions());
        var now = DateTimeOffset.UtcNow;
        schedule.NextDelay(failed: true);
        Assert.True(schedule.MaintenanceDue(now.AddSeconds(2)));
    }
}
