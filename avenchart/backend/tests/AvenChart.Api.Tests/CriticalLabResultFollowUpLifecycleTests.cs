// SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
// SPDX-License-Identifier: GPL-3.0-or-later

using AvenChart.Api.Infrastructure;

namespace AvenChart.Api.Tests;

public sealed class CriticalLabResultFollowUpLifecycleTests
{
    [Theory]
    [InlineData("open", "accepted")]
    [InlineData("accepted", "ownership-transferred")]
    [InlineData("accepted", "communication-recorded")]
    [InlineData("actioned", "clinical-action-recorded")]
    [InlineData("accepted", "escalated")]
    [InlineData("actioned", "closed")]
    public void AllowsOnlyDocumentedLifecycleTransitions(string status, string action) =>
        Assert.True(CriticalLabResultFollowUpLifecycle.AllowsAction(status, action));

    [Theory]
    [InlineData("open", "closed")]
    [InlineData("open", "communication-recorded")]
    [InlineData("accepted", "closed")]
    [InlineData("closed", "accepted")]
    [InlineData("closed", "escalated")]
    [InlineData("actioned", "accepted")]
    public void RejectsTerminalAndSkippedTransitions(string status, string action) =>
        Assert.False(CriticalLabResultFollowUpLifecycle.AllowsAction(status, action));
}
