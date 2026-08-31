// SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
// SPDX-License-Identifier: GPL-3.0-or-later

using AvenChart.Api.Features.Telehealth;
using Microsoft.AspNetCore.Http;

namespace AvenChart.Api.Tests.Telehealth;

public sealed class TelehealthLocalWebRtcPocRelayTests
{
    [Fact]
    public void ReadReturnsOnlyTheOtherParticipantTransientSignals()
    {
        var relay = new TelehealthLocalWebRtcPocRelay();
        var sessionId = Guid.NewGuid();
        var expiresAt = DateTimeOffset.UtcNow.AddMinutes(1);

        var sequence = relay.Append(sessionId, "physician", "offer", "{\"type\":\"offer\"}", expiresAt);
        var physicianRead = relay.Read(sessionId, "physician", 0, expiresAt);
        var patientRead = relay.Read(sessionId, "patient", 0, expiresAt);

        Assert.Equal(1, sequence);
        Assert.Empty(physicianRead.Signals);
        Assert.Equal(1, physicianRead.LatestSequence);
        var signal = Assert.Single(patientRead.Signals);
        Assert.Equal(1, signal.Sequence);
        Assert.Equal("offer", signal.Kind);
        Assert.Equal("{\"type\":\"offer\"}", signal.Payload);
    }

    [Fact]
    public void ExpiredGrantMaterialCannotBeAppendedOrRead()
    {
        var relay = new TelehealthLocalWebRtcPocRelay();
        var expiredAt = DateTimeOffset.UtcNow.AddSeconds(-1);

        var append = Assert.Throws<TelehealthProblem>(() =>
            relay.Append(Guid.NewGuid(), "patient", "candidate", "null", expiredAt));
        var read = Assert.Throws<TelehealthProblem>(() =>
            relay.Read(Guid.NewGuid(), "patient", 0, expiredAt));

        Assert.Equal(StatusCodes.Status404NotFound, append.StatusCode);
        Assert.Equal(StatusCodes.Status404NotFound, read.StatusCode);
    }
}
