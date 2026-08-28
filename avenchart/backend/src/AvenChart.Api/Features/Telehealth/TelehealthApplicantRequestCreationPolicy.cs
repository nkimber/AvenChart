// SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
// SPDX-License-Identifier: GPL-3.0-or-later

namespace AvenChart.Api.Features.Telehealth;

public sealed record NormalizedTelehealthApplicantRequestCreation(
    int ExpectedApplicantVersion,
    int AuthorizationPolicyVersion,
    bool RequestCreationConfirmed,
    bool NoQueueOrCareAcknowledged,
    bool UrgentOrWorseningSymptomsRequireImmediateActionAcknowledged);

public static class TelehealthApplicantRequestCreationPolicy
{
    public const string PolicyKey = "SYNTHETIC_APPLICANT_TELEHEALTH_REQUEST_CREATION";
    public const int PolicyVersion = 1;
    public const int AuthorizationPolicyVersion = 1;
    public const string EntryStatus = "SyntheticPracticeReviewAuthorized";
    public const string ResultingStatus = "SyntheticRequestCreated";
    public const string EvidenceType = "APPLICANT_CONFIRMATION_WITH_AUTHORIZED_SOURCE_PROVENANCE";

    public static NormalizedTelehealthApplicantRequestCreation Normalize(
        CreateTelehealthApplicantRequest request)
    {
        if (request.ExpectedApplicantVersion < 1)
        {
            throw TelehealthProblem.BadRequest(
                "telehealth_applicant_request_creation_version_invalid",
                "ExpectedApplicantVersion must be positive.");
        }
        if (request.AuthorizationPolicyVersion != AuthorizationPolicyVersion)
        {
            throw TelehealthProblem.BadRequest(
                "telehealth_applicant_request_creation_authorization_policy_invalid",
                "Reload the authorized request-creation step before continuing.");
        }
        if (!request.RequestCreationConfirmed
            || !request.NoQueueOrCareAcknowledged
            || !request.UrgentOrWorseningSymptomsRequireImmediateActionAcknowledged)
        {
            throw TelehealthProblem.BadRequest(
                "telehealth_applicant_request_creation_acknowledgments_required",
                "Confirm request creation and every safety and workflow limitation before continuing.");
        }

        return new(
            request.ExpectedApplicantVersion,
            request.AuthorizationPolicyVersion,
            request.RequestCreationConfirmed,
            request.NoQueueOrCareAcknowledged,
            request.UrgentOrWorseningSymptomsRequireImmediateActionAcknowledged);
    }
}
