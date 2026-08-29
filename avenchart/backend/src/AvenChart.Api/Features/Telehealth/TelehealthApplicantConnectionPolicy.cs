// SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
// SPDX-License-Identifier: GPL-3.0-or-later

namespace AvenChart.Api.Features.Telehealth;

public static class TelehealthApplicantConnectionPolicy
{
    public const string PolicyKey = "SYNTHETIC_APPLICANT_CONNECTION_ROOM";
    public const int PolicyVersion = 1;
    public const string SourceMode = "NON_PRODUCTION";

    public static string CreateParticipantSubjectHash(Guid applicantId, string accessKeyHash) =>
        TelehealthProspectiveApplicantPolicy.Hash(
            $"telehealth-video-applicant-participant-v1\u001f{applicantId:D}\u001f{accessKeyHash}");
}
