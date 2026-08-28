// SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Security.Cryptography;
using System.Text;
using AvenChart.Api.Features.Telehealth;

namespace AvenChart.Api.Tests.Telehealth;

public sealed class SyntheticTelehealthVideoProviderTests
{
    [Fact]
    public void ExactGrantReplayIsStableWithinOneProcessAndStoredHashMatches()
    {
        var provider = new SyntheticTelehealthVideoProvider();
        var sessionId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        var grantId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
        var expiry = new DateTimeOffset(2026, 8, 27, 5, 0, 0, TimeSpan.Zero);

        var first = provider.Prepare(sessionId, grantId, "patient", expiry);
        var replay = provider.Prepare(sessionId, grantId, "patient", expiry.AddSeconds(10));

        Assert.Equal("NON_PRODUCTION", first.AdapterMode);
        Assert.Equal(first.ProviderInstanceId, replay.ProviderInstanceId);
        Assert.Equal(first.ProviderSessionReference, replay.ProviderSessionReference);
        Assert.Equal(first.JoinCredential, replay.JoinCredential);
        Assert.Equal(43, first.JoinCredential.Length);
        Assert.DoesNotContain('=', first.JoinCredential);
        Assert.Equal(
            Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(first.JoinCredential))),
            first.JoinCredentialHash);
    }

    [Fact]
    public void RoleAndGrantAreCryptographicallySeparated()
    {
        var provider = new SyntheticTelehealthVideoProvider();
        var sessionId = Guid.NewGuid();
        var grantId = Guid.NewGuid();
        var expiry = DateTimeOffset.UtcNow.AddMinutes(2);

        var patient = provider.Prepare(sessionId, grantId, "patient", expiry);
        var physician = provider.Prepare(sessionId, grantId, "physician", expiry);
        var otherGrant = provider.Prepare(sessionId, Guid.NewGuid(), "patient", expiry);

        Assert.NotEqual(patient.JoinCredential, physician.JoinCredential);
        Assert.NotEqual(patient.JoinCredential, otherGrant.JoinCredential);
        Assert.Equal(patient.ProviderSessionReference, physician.ProviderSessionReference);
    }

    [Fact]
    public void InvalidParticipantRoleIsRejected()
    {
        var provider = new SyntheticTelehealthVideoProvider();
        Assert.Throws<ArgumentOutOfRangeException>(() => provider.Prepare(
            Guid.NewGuid(), Guid.NewGuid(), "observer", DateTimeOffset.UtcNow.AddMinutes(1)));
    }
}
