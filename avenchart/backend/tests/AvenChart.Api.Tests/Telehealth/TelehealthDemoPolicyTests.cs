// SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
// SPDX-License-Identifier: GPL-3.0-or-later

using AvenChart.Api.Features.Telehealth;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;

namespace AvenChart.Api.Tests.Telehealth;

public sealed class TelehealthDemoPolicyTests
{
    [Theory]
    [InlineData("Production", true, true, "Synthetic", false)]
    [InlineData("Staging", false, true, "Synthetic", false)]
    [InlineData("Staging", true, false, "Synthetic", false)]
    [InlineData("Staging", true, true, "Live", false)]
    [InlineData("Staging", true, true, "Synthetic", true)]
    public void LauncherFailsClosedOutsideExplicitSyntheticOptIn(string environment, bool telehealth, bool launcher, string mode, bool expected)
    {
        Assert.Equal(expected, TelehealthDemoService.IsEnabled(new TelehealthOptions { Enabled = telehealth, DemoLauncherEnabled = launcher, Mode = mode }, new TestEnvironment(environment)));
    }

    [Fact]
    public void InvitationsAreStableForRetryButDistinctForEveryRoomAndRole()
    {
        var key = new string('a', 64);
        var run = Guid.NewGuid();
        var patient = TelehealthDemoPolicy.Invitation(key, run, "patient");
        Assert.Equal(patient, TelehealthDemoPolicy.Invitation(key, run, "patient"));
        Assert.NotEqual(patient, TelehealthDemoPolicy.Invitation(key, run, "physician"));
        Assert.NotEqual(patient, TelehealthDemoPolicy.Invitation(key, Guid.NewGuid(), "patient"));
        Assert.NotEqual(patient, TelehealthDemoPolicy.Invitation(new string('b', 64), run, "patient"));
        Assert.NotEqual(patient, TelehealthDemoPolicy.Hash(patient));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("ordinary-password")]
    [InlineData("aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa/")]
    public void MalformedKeysCannotAuthorizeARoom(string? key) => Assert.Throws<TelehealthProblem>(() => TelehealthDemoPolicy.RequireKey(key));

    [Theory]
    [InlineData("administrator")]
    [InlineData("observer")]
    [InlineData("Patient")]
    public void InvitationsCannotGrantClinicalOrObserverRoles(string role) => Assert.Throws<TelehealthProblem>(() => TelehealthDemoPolicy.RequireRole(role));

    private sealed class TestEnvironment(string name) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = name;
        public string ApplicationName { get; set; } = "SyntheticTelehealthTest";
        public string ContentRootPath { get; set; } = ".";
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
