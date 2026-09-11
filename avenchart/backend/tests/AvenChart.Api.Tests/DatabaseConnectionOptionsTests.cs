// SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
// SPDX-License-Identifier: GPL-3.0-or-later

using AvenChart.Api.Configuration;
using Npgsql;

namespace AvenChart.Api.Tests;

public sealed class DatabaseConnectionOptionsTests
{
    [Theory]
    [InlineData(15, 100, 15)]
    [InlineData(100, 15, 15)]
    [InlineData(8, 15, 8)]
    public void EffectivePoolNeverExceedsEitherConfiguredCeiling(int connectionLimit, int optionLimit, int expected)
    {
        var options = new DatabaseConnectionOptions { MaximumPoolSize = optionLimit };
        var result = new NpgsqlConnectionStringBuilder(options.BuildConnectionString(
            $"Host=localhost;Maximum Pool Size={connectionLimit};SSL Mode=VerifyFull;Connection Idle Lifetime=60"));
        Assert.Equal(expected, result.MaxPoolSize);
        Assert.Equal(SslMode.VerifyFull, result.SslMode);
        Assert.Equal(60, result.ConnectionIdleLifetime);
    }

    [Fact]
    public void MissingPoolSettingUsesSafeApplicationDefault()
    {
        var result = new NpgsqlConnectionStringBuilder(new DatabaseConnectionOptions().BuildConnectionString("Host=localhost"));
        Assert.Equal(15, result.MaxPoolSize);
        Assert.Equal(0, result.MinPoolSize);
    }

    [Fact]
    public void RejectsMinimumThatCannotFitEffectivePool()
    {
        var options = new DatabaseConnectionOptions { MinimumPoolSize = 20, MaximumPoolSize = 100 };
        Assert.Throws<InvalidOperationException>(() => options.BuildConnectionString("Host=localhost;Maximum Pool Size=15"));
    }
}
