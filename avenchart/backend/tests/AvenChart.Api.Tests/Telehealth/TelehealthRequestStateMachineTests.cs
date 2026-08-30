// SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
// SPDX-License-Identifier: GPL-3.0-or-later

using AvenChart.Api.Features.Telehealth;

namespace AvenChart.Api.Tests.Telehealth;

public sealed class TelehealthRequestStateMachineTests
{
    [Theory]
    [InlineData(TelehealthRequestStatus.Draft)]
    [InlineData(TelehealthRequestStatus.LocationConfirmed)]
    [InlineData(TelehealthRequestStatus.SafetyScreening)]
    [InlineData(TelehealthRequestStatus.Intake)]
    [InlineData(TelehealthRequestStatus.Verification)]
    [InlineData(TelehealthRequestStatus.OperationalReview)]
    public void AllowsSyntheticCancellationOnlyBeforeQueueAuthorization(TelehealthRequestStatus current)
    {
        Assert.True(TelehealthRequestStateMachine.CanTransition(current, TelehealthRequestStatus.Cancelled));
    }

    [Theory]
    [InlineData(TelehealthRequestStatus.Queued)]
    [InlineData(TelehealthRequestStatus.Reserved)]
    [InlineData(TelehealthRequestStatus.Connecting)]
    [InlineData(TelehealthRequestStatus.InConsultation)]
    [InlineData(TelehealthRequestStatus.WrapUp)]
    [InlineData(TelehealthRequestStatus.Closed)]
    public void RejectsCancellationOncePracticeQueueOrConsultationWorkExists(TelehealthRequestStatus current)
    {
        Assert.False(TelehealthRequestStateMachine.CanTransition(current, TelehealthRequestStatus.Cancelled));
    }
}
