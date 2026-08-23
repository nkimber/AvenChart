// SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
// SPDX-License-Identifier: GPL-3.0-or-later

using AvenChart.Api.Security;

namespace AvenChart.Api.Tests;

public sealed class StaffAccessContextServiceTests
{
    [Theory]
    [InlineData("APPT-100", "APPT-100")]
    [InlineData(" APPT-100 ", "APPT-100")]
    [InlineData("APPT-100::occurs::2026-12-30", "APPT-100")]
    [InlineData("APPT-100%3A%3Aoccurs%3A%3A2026-12-30", "APPT-100")]
    [InlineData("APPT-100@2026-12-30", "APPT-100")]
    public void AppointmentAuthorizationUsesPersistedSeriesRoot(string appointmentId, string expectedRootId) =>
        Assert.Equal(expectedRootId, StaffAccessContextService.GetAppointmentSeriesRootId(appointmentId));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("::occurs::2026-12-30")]
    public void AppointmentAuthorizationRejectsMissingSeriesRoot(string? appointmentId) =>
        Assert.Null(StaffAccessContextService.GetAppointmentSeriesRootId(appointmentId));
}
