// SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
// SPDX-License-Identifier: GPL-3.0-or-later

using AvenChart.Api.Features.Telehealth;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace AvenChart.Api.Tests.Telehealth;

public sealed class TelehealthRuntimeSafetyPolicyTests
{
    [Fact]
    public void FeatureIsClosedByDefaultInEveryEnvironment()
    {
        var options = new TelehealthOptions();
        Assert.False(options.Enabled);
        Assert.False(options.LocalWebRtcPocEnabled);
        Assert.True(TelehealthRuntimeSafetyPolicy.IsSafe(options, Environment(Environments.Production)));
    }

    [Fact]
    public void LocalWebRtcPocRemainsNonProductionSyntheticOnly()
    {
        var options = ValidEnabledOptions(localWebRtcPocEnabled: true);

        Assert.True(TelehealthRuntimeSafetyPolicy.IsSafe(options, Environment(Environments.Development)));
        Assert.False(TelehealthRuntimeSafetyPolicy.IsSafe(options, Environment(Environments.Production)));
    }

    [Fact]
    public void ProductionRejectsEnabledSyntheticFeature()
    {
        var options = ValidEnabledOptions();
        Assert.False(TelehealthRuntimeSafetyPolicy.IsSafe(options, Environment(Environments.Production)));
    }

    [Fact]
    public async Task ProductionHostStartupRejectsEnabledSyntheticFeature()
    {
        var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings
        {
            EnvironmentName = Environments.Production,
            ApplicationName = "AvenChart.Telehealth.RuntimeSafety.Tests"
        });
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Telehealth:Enabled"] = "true",
            ["Telehealth:Mode"] = "Synthetic",
            ["Telehealth:PracticeId"] = "avenchart-synthetic-practice",
            ["Telehealth:FacilityId"] = "10",
            ["Telehealth:BrandedHosts:0"] = "localhost",
            ["Telehealth:SupportedStates:0"] = "GA",
            ["Telehealth:SupportedStates:1"] = "CA",
            ["Telehealth:SupportedStates:2"] = "FL",
            ["Telehealth:ReservationLeaseSeconds"] = "120",
            ["Telehealth:VideoAdapterMode"] = "NON_PRODUCTION",
            ["Telehealth:PharmacyDirectoryAdapterMode"] = "NON_PRODUCTION",
            ["Telehealth:ProfessionalClaimAdapterMode"] = "NON_PRODUCTION"
        });
        builder.Services.AddTelehealth(builder.Configuration, builder.Environment);
        using var host = builder.Build();

        var exception = await Assert.ThrowsAsync<OptionsValidationException>(() => host.StartAsync());
        Assert.Contains("cannot run in Production", exception.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("*")]
    [InlineData("*.example.test")]
    [InlineData("example.test:443")]
    [InlineData("https://example.test")]
    public void EnabledFeatureRejectsPermissiveOrNonHostMappings(string host)
    {
        var options = ValidEnabledOptions([host]);
        Assert.False(TelehealthRuntimeSafetyPolicy.IsSafe(options, Environment(Environments.Development)));
    }

    [Fact]
    public void DevelopmentAcceptsOnlyExactSyntheticConfiguration()
    {
        Assert.True(TelehealthRuntimeSafetyPolicy.IsSafe(ValidEnabledOptions(), Environment(Environments.Development)));
    }

    [Fact]
    public void EnabledFeatureRejectsAnyNonSimulatorVideoAdapter()
    {
        var options = WithVideoAdapter(ValidEnabledOptions(), "ManagedVendor");
        Assert.False(TelehealthRuntimeSafetyPolicy.IsSafe(options, Environment(Environments.Development)));
    }

    [Fact]
    public void EnabledFeatureRejectsAnyNonSyntheticPharmacyDirectoryAdapter()
    {
        var options = WithPharmacyDirectoryAdapter(ValidEnabledOptions(), "ManagedVendor");
        Assert.False(TelehealthRuntimeSafetyPolicy.IsSafe(options, Environment(Environments.Development)));
    }

    [Fact]
    public void EnabledFeatureRejectsAnyNonSyntheticProfessionalClaimAdapter()
    {
        Assert.False(TelehealthRuntimeSafetyPolicy.IsSafe(
            WithProfessionalClaimAdapter(ValidEnabledOptions(), "ClearinghouseVendor"), Environment(Environments.Development)));
    }

    private static TelehealthOptions ValidEnabledOptions(string[]? hosts = null, bool localWebRtcPocEnabled = false) => new()
    {
        Enabled = true,
        Mode = "Synthetic",
        PracticeId = "avenchart-synthetic-practice",
        FacilityId = 10,
        BrandedHosts = hosts ?? ["localhost"],
        SupportedStates = ["GA", "CA", "FL"],
        ReservationLeaseSeconds = 120,
        VideoAdapterMode = "NON_PRODUCTION",
        LocalWebRtcPocEnabled = localWebRtcPocEnabled,
        PharmacyDirectoryAdapterMode = "NON_PRODUCTION",
        ProfessionalClaimAdapterMode = "NON_PRODUCTION"
    };

    private static TelehealthOptions WithVideoAdapter(TelehealthOptions options, string mode) => new()
    {
        Enabled = options.Enabled,
        Mode = options.Mode,
        PracticeId = options.PracticeId,
        PracticeDisplayName = options.PracticeDisplayName,
        FacilityId = options.FacilityId,
        BrandedHosts = options.BrandedHosts,
        SupportedStates = options.SupportedStates,
        ReservationLeaseSeconds = options.ReservationLeaseSeconds,
        VideoAdapterMode = mode,
        LocalWebRtcPocEnabled = options.LocalWebRtcPocEnabled,
        PharmacyDirectoryAdapterMode = options.PharmacyDirectoryAdapterMode,
        ProfessionalClaimAdapterMode = options.ProfessionalClaimAdapterMode
    };

    private static TelehealthOptions WithPharmacyDirectoryAdapter(TelehealthOptions options, string mode) => new()
    {
        Enabled = options.Enabled,
        Mode = options.Mode,
        PracticeId = options.PracticeId,
        PracticeDisplayName = options.PracticeDisplayName,
        FacilityId = options.FacilityId,
        BrandedHosts = options.BrandedHosts,
        SupportedStates = options.SupportedStates,
        ReservationLeaseSeconds = options.ReservationLeaseSeconds,
        VideoAdapterMode = options.VideoAdapterMode,
        LocalWebRtcPocEnabled = options.LocalWebRtcPocEnabled,
        PharmacyDirectoryAdapterMode = mode,
        ProfessionalClaimAdapterMode = options.ProfessionalClaimAdapterMode
    };

    private static TelehealthOptions WithProfessionalClaimAdapter(TelehealthOptions options, string mode) => new()
    {
        Enabled = options.Enabled,
        Mode = options.Mode,
        PracticeId = options.PracticeId,
        PracticeDisplayName = options.PracticeDisplayName,
        FacilityId = options.FacilityId,
        BrandedHosts = options.BrandedHosts,
        SupportedStates = options.SupportedStates,
        ReservationLeaseSeconds = options.ReservationLeaseSeconds,
        VideoAdapterMode = options.VideoAdapterMode,
        LocalWebRtcPocEnabled = options.LocalWebRtcPocEnabled,
        PharmacyDirectoryAdapterMode = options.PharmacyDirectoryAdapterMode,
        ProfessionalClaimAdapterMode = mode
    };

    private static IHostEnvironment Environment(string environmentName) => new TestHostEnvironment
    {
        EnvironmentName = environmentName
    };

    private sealed class TestHostEnvironment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = Environments.Development;
        public string ApplicationName { get; set; } = "AvenChart.Api.Tests";
        public string ContentRootPath { get; set; } = Directory.GetCurrentDirectory();
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
