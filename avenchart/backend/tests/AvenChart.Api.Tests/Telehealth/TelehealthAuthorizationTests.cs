// SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
// SPDX-License-Identifier: GPL-3.0-or-later

using AvenChart.Api.Features.Telehealth;

namespace AvenChart.Api.Tests.Telehealth;

public sealed class TelehealthAuthorizationTests
{
    [Theory]
    [InlineData("frontdesk", true)]
    [InlineData("administrator", true)]
    [InlineData("provider", false)]
    [InlineData("billing", false)]
    [InlineData(null, false)]
    public void OperationalAuthorizationRequiresAdministratorRole(string? role, bool expected) =>
        Assert.Equal(expected, TelehealthAuthorizationPolicy.IsAdministratorRole(role));

    [Theory]
    [InlineData("provider", true)]
    [InlineData("physician", true)]
    [InlineData("frontdesk", false)]
    [InlineData("nurse", false)]
    [InlineData(null, false)]
    public void ReservationRequiresPhysicianRole(string? role, bool expected) =>
        Assert.Equal(expected, TelehealthAuthorizationPolicy.IsPhysicianRole(role));

    [Theory]
    [InlineData(10, 10, true)]
    [InlineData(11, 10, false)]
    [InlineData(0, 10, false)]
    public void StaffActionsRemainInConfiguredFacility(int selected, int configured, bool expected) =>
        Assert.Equal(expected, TelehealthAuthorizationPolicy.IsConfiguredFacility(selected, configured));
}
