// SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Net;
using AvenChart.Api.Configuration;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.HttpOverrides;

namespace AvenChart.Api.Tests;

public sealed class RuntimeSafetyPolicyTests
{
    [Fact]
    public void ForwardedHeadersTrustOnlyConfiguredProxyAddresses()
    {
        var options = new ForwardedHeadersOptions();

        RuntimeSafetyPolicy.ConfigureForwardedHeaders(
            options,
            [IPAddress.Parse("203.0.113.25"), IPAddress.Parse("2001:db8::25")],
            forwardLimit: 2);

        Assert.Equal(ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto, options.ForwardedHeaders);
        Assert.Equal(2, options.ForwardLimit);
        Assert.Empty(options.KnownIPNetworks);
        Assert.Equal([IPAddress.Parse("203.0.113.25"), IPAddress.Parse("2001:db8::25")], options.KnownProxies);
    }

    [Theory]
    [InlineData(new[] { "203.0.113.25", "2001:db8::25" }, true)]
    [InlineData(new[] { "203.0.113.25", "not-an-address" }, false)]
    [InlineData(new[] { " " }, false)]
    public void TrustedProxyAddressesRequireExplicitIpAddresses(string[] addresses, bool expected) =>
        Assert.Equal(expected, RuntimeSafetyPolicy.HasValidTrustedProxyAddresses(addresses));

    [Fact]
    public void ProductionDataProtectionRequiresAllAbsoluteSettings()
    {
        var valid = new RuntimeSafetyOptions
        {
            DataProtectionKeyRingPath = Path.GetFullPath("keys"),
            DataProtectionApplicationName = "avenchart-production",
            DataProtectionCertificatePath = Path.GetFullPath("keys/avenchart-data-protection.pfx")
        };

        Assert.True(RuntimeSafetyPolicy.HasProductionDataProtectionConfiguration(valid));
        Assert.False(RuntimeSafetyPolicy.HasProductionDataProtectionConfiguration(new RuntimeSafetyOptions
        {
            DataProtectionKeyRingPath = valid.DataProtectionKeyRingPath,
            DataProtectionApplicationName = valid.DataProtectionApplicationName,
            DataProtectionCertificatePath = "relative.pfx"
        }));
        Assert.False(RuntimeSafetyPolicy.HasProductionDataProtectionConfiguration(new RuntimeSafetyOptions
        {
            DataProtectionKeyRingPath = valid.DataProtectionKeyRingPath,
            DataProtectionApplicationName = " ",
            DataProtectionCertificatePath = valid.DataProtectionCertificatePath
        }));
    }

    [Theory]
    [InlineData("api.avenchart.example;portal.avenchart.example", true)]
    [InlineData("*", false)]
    [InlineData("api.avenchart.example;*", false)]
    [InlineData("*.avenchart.example", false)]
    [InlineData("", false)]
    [InlineData(";;;", false)]
    public void ProductionHostFilteringRejectsWildcardOrMissingHosts(string allowedHosts, bool expected) =>
        Assert.Equal(expected, RuntimeSafetyPolicy.HasExplicitAllowedHosts(allowedHosts));
}
