// SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
// SPDX-License-Identifier: GPL-3.0-or-later

namespace AvenChart.Api.Features.Telehealth;

public sealed record TelehealthApplicantRequestLocationSnapshot(
    string CurrentLocationStateCode,
    string MaskedCallbackPhone,
    string Fingerprint);

public sealed record NormalizedTelehealthApplicantRequestLocationConfirmation(
    int ExpectedRequestVersion,
    string ContextSnapshotFingerprint,
    string CurrentLocationStateCode,
    bool CurrentLocationConfirmed,
    bool CallbackNumberConfirmed,
    bool ChangedLocationRequiresRestartAcknowledged,
    bool UrgentOrWorseningSymptomsRequireImmediateActionAcknowledged);

public static class TelehealthApplicantRequestLocationPolicy
{
    public const string PolicyKey = "SYNTHETIC_APPLICANT_REQUEST_LOCATION_CONFIRMATION";
    public const int PolicyVersion = 1;
    public const string EvidenceType = "APPLICANT_REQUEST_LOCATION_CALLBACK_CONFIRMATION";
    public const string ApplicantStatus = "SyntheticRequestCreated";
    public const string EntryRequestStatus = "Draft";
    public const int EntryRequestVersion = 1;
    public const string ResultingRequestStatus = "LocationConfirmed";
    public const int ResultingRequestVersion = 2;
    public static readonly IReadOnlyList<string> SupportedStates = ["GA", "CA", "FL"];

    public static TelehealthApplicantRequestLocationSnapshot Snapshot(
        Guid requestId,
        Guid requestCreationId,
        Guid communicationReadinessId,
        int requestVersion,
        string currentLocationStateCode,
        string callbackPhoneLast4) => new(
            currentLocationStateCode,
            $"***-***-{callbackPhoneLast4}",
            TelehealthCommandFingerprint.Create(
                "synthetic-applicant-request-location-context-v1",
                requestId,
                requestCreationId,
                communicationReadinessId,
                requestVersion,
                currentLocationStateCode,
                callbackPhoneLast4));

    public static NormalizedTelehealthApplicantRequestLocationConfirmation Normalize(
        ConfirmTelehealthApplicantRequestLocation request)
    {
        if (request.ExpectedRequestVersion < 1)
        {
            throw TelehealthProblem.BadRequest(
                "telehealth_applicant_request_location_version_invalid",
                "ExpectedRequestVersion must be positive.");
        }

        var fingerprint = (request.ContextSnapshotFingerprint ?? string.Empty)
            .Trim()
            .ToLowerInvariant();
        if (fingerprint.Length != 64 || fingerprint.Any(character => !Uri.IsHexDigit(character)))
        {
            throw TelehealthProblem.BadRequest(
                "telehealth_applicant_request_location_snapshot_invalid",
                "Reload the current location and callback step before continuing.");
        }

        var state = request.CurrentLocationStateCode?.Trim().ToUpperInvariant() ?? string.Empty;
        if (!SupportedStates.Contains(state, StringComparer.Ordinal))
        {
            throw TelehealthProblem.BadRequest(
                "telehealth_applicant_request_location_state_unsupported",
                "Current physical location must be Georgia, California, or Florida for this synthetic demonstration.");
        }

        if (!request.CurrentLocationConfirmed
            || !request.CallbackNumberConfirmed
            || !request.ChangedLocationRequiresRestartAcknowledged
            || !request.UrgentOrWorseningSymptomsRequireImmediateActionAcknowledged)
        {
            throw TelehealthProblem.BadRequest(
                "telehealth_applicant_request_location_acknowledgments_required",
                "Confirm every location, callback, changed-location, and safety statement before continuing.");
        }

        return new(
            request.ExpectedRequestVersion,
            fingerprint,
            state,
            request.CurrentLocationConfirmed,
            request.CallbackNumberConfirmed,
            request.ChangedLocationRequiresRestartAcknowledged,
            request.UrgentOrWorseningSymptomsRequireImmediateActionAcknowledged);
    }
}
