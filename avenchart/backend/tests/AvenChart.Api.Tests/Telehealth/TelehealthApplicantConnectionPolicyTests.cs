// SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
// SPDX-License-Identifier: GPL-3.0-or-later

using AvenChart.Api.Features.Telehealth;

namespace AvenChart.Api.Tests.Telehealth;

public sealed class TelehealthApplicantConnectionPolicyTests
{
    [Fact]
    public void ParticipantHashIsStableDomainSeparatedAndApplicantBound()
    {
        var applicant = Guid.Parse("55000000-0000-4000-8000-000000000055");
        var accessHash = new string('a', 64);

        var first = TelehealthApplicantConnectionPolicy.CreateParticipantSubjectHash(applicant, accessHash);
        var replay = TelehealthApplicantConnectionPolicy.CreateParticipantSubjectHash(applicant, accessHash);
        var otherApplicant = TelehealthApplicantConnectionPolicy.CreateParticipantSubjectHash(Guid.NewGuid(), accessHash);
        var otherAccess = TelehealthApplicantConnectionPolicy.CreateParticipantSubjectHash(applicant, new string('b', 64));

        Assert.Equal(64, first.Length);
        Assert.Equal(first, replay);
        Assert.NotEqual(first, otherApplicant);
        Assert.NotEqual(first, otherAccess);
        Assert.DoesNotContain(applicant.ToString("D"), first, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(accessHash, first, StringComparison.OrdinalIgnoreCase);
    }
}
