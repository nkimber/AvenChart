// SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
// SPDX-License-Identifier: GPL-3.0-or-later

namespace AvenChart.Api.Features.Telehealth;

public sealed record TelehealthApplicantRegistrationDetailsSnapshot(
    string LegalFirstName,
    string LegalLastName,
    DateOnly DateOfBirth,
    string MaskedEmail,
    string MaskedPhone,
    string ResidenceStateCode,
    string PostalCode,
    string Fingerprint);

public sealed record NormalizedTelehealthApplicantRegistrationDetailsConfirmation(
    int ExpectedVersion,
    string DetailsFingerprint,
    bool LegalNameAndBirthDateConfirmed,
    bool ContactChannelsConfirmed,
    bool ResidenceRegionConfirmed,
    bool NoCorrectionsNeededConfirmed,
    bool SyntheticDataConfirmed);

public static class TelehealthApplicantRegistrationDetailsPolicy
{
    public const string PolicyKey = "SYNTHETIC_MINIMUM_REGISTRATION_DETAILS_CONFIRMATION";
    public const int PolicyVersion = 1;
    public const string EvidenceType = "PROMOTED_PATIENT_MINIMUM_DETAILS_NO_EDIT_CONFIRMATION";
    public const string ResultingStatus = "SyntheticMinimumRegistrationDetailsConfirmed";

    public static TelehealthApplicantRegistrationDetailsSnapshot Snapshot(
        string legalFirstName,
        string legalLastName,
        DateOnly dateOfBirth,
        string email,
        string phone,
        string residenceStateCode,
        string postalCode) => new(
            legalFirstName,
            legalLastName,
            dateOfBirth,
            TelehealthProspectiveApplicantPolicy.MaskEmail(email),
            TelehealthProspectiveApplicantPolicy.MaskPhone(phone),
            residenceStateCode,
            postalCode,
            TelehealthCommandFingerprint.Create(
                "synthetic-minimum-registration-details-snapshot-v1",
                legalFirstName,
                legalLastName,
                dateOfBirth,
                email,
                phone,
                residenceStateCode,
                postalCode));

    public static NormalizedTelehealthApplicantRegistrationDetailsConfirmation Normalize(
        ConfirmTelehealthApplicantRegistrationDetailsRequest request)
    {
        if (request.ExpectedVersion <= 0)
        {
            throw TelehealthProblem.BadRequest(
                "telehealth_applicant_registration_details_version_invalid",
                "ExpectedVersion must be a positive applicant version.");
        }

        var fingerprint = (request.DetailsFingerprint ?? string.Empty).Trim().ToLowerInvariant();
        if (fingerprint.Length != 64 || fingerprint.Any(character => !Uri.IsHexDigit(character)))
        {
            throw TelehealthProblem.BadRequest(
                "telehealth_applicant_registration_details_fingerprint_invalid",
                "Reload the minimum registration details before confirming them.");
        }

        if (!request.LegalNameAndBirthDateConfirmed
            || !request.ContactChannelsConfirmed
            || !request.ResidenceRegionConfirmed
            || !request.NoCorrectionsNeededConfirmed
            || !request.SyntheticDataConfirmed)
        {
            throw TelehealthProblem.BadRequest(
                "telehealth_applicant_registration_details_acknowledgments_required",
                "Confirm every minimum registration-details statement before continuing.");
        }

        return new(
            request.ExpectedVersion,
            fingerprint,
            request.LegalNameAndBirthDateConfirmed,
            request.ContactChannelsConfirmed,
            request.ResidenceRegionConfirmed,
            request.NoCorrectionsNeededConfirmed,
            request.SyntheticDataConfirmed);
    }
}
