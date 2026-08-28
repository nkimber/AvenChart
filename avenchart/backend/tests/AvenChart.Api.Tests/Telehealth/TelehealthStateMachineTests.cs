// SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
// SPDX-License-Identifier: GPL-3.0-or-later

using AvenChart.Api.Features.Telehealth;

namespace AvenChart.Api.Tests.Telehealth;

public sealed class TelehealthStateMachineTests
{
    public static TheoryData<TelehealthRequestStatus, TelehealthRequestStatus> AllowedTransitions => new()
    {
        { TelehealthRequestStatus.Draft, TelehealthRequestStatus.LocationConfirmed },
        { TelehealthRequestStatus.LocationConfirmed, TelehealthRequestStatus.Intake },
        { TelehealthRequestStatus.LocationConfirmed, TelehealthRequestStatus.Redirected },
        { TelehealthRequestStatus.Intake, TelehealthRequestStatus.Verification },
        { TelehealthRequestStatus.Verification, TelehealthRequestStatus.Verification },
        { TelehealthRequestStatus.Verification, TelehealthRequestStatus.OperationalReview },
        { TelehealthRequestStatus.OperationalReview, TelehealthRequestStatus.Verification },
        { TelehealthRequestStatus.OperationalReview, TelehealthRequestStatus.OperationalReview },
        { TelehealthRequestStatus.OperationalReview, TelehealthRequestStatus.Queued },
        { TelehealthRequestStatus.Queued, TelehealthRequestStatus.Reserved },
        { TelehealthRequestStatus.Reserved, TelehealthRequestStatus.Queued },
        { TelehealthRequestStatus.Reserved, TelehealthRequestStatus.Connecting },
        { TelehealthRequestStatus.Connecting, TelehealthRequestStatus.Queued },
        { TelehealthRequestStatus.Connecting, TelehealthRequestStatus.InConsultation },
        { TelehealthRequestStatus.InConsultation, TelehealthRequestStatus.WrapUp }
    };

    [Theory]
    [MemberData(nameof(AllowedTransitions))]
    public void AllowsOnlyDeclaredTransition(TelehealthRequestStatus current, TelehealthRequestStatus next)
    {
        Assert.True(TelehealthRequestStateMachine.CanTransition(current, next));
        TelehealthRequestStateMachine.RequireTransition(current, next);
    }

    [Fact]
    public void RejectsEveryUndeclaredTransition()
    {
        var allowed = AllowedTransitions.Select(row => ((TelehealthRequestStatus)row[0], (TelehealthRequestStatus)row[1])).ToHashSet();
        foreach (var current in Enum.GetValues<TelehealthRequestStatus>())
            foreach (var next in Enum.GetValues<TelehealthRequestStatus>())
            {
                if (allowed.Contains((current, next))) continue;
                var problem = Assert.Throws<TelehealthProblem>(() => TelehealthRequestStateMachine.RequireTransition(current, next));
                Assert.Equal("telehealth_invalid_transition", problem.Code);
                Assert.Equal(409, problem.StatusCode);
            }
    }
}
