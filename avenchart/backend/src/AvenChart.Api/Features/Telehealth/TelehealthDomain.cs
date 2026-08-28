// SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Security.Cryptography;
using System.Text;

namespace AvenChart.Api.Features.Telehealth;

public enum TelehealthRequestStatus
{
    Draft,
    LocationConfirmed,
    Intake,
    Verification,
    OperationalReview,
    Redirected,
    Queued,
    Reserved,
    Connecting,
    InConsultation,
    WrapUp
}

public enum TelehealthTriageOutcome
{
    Emergency,
    UrgentInPerson,
    InPersonRequired,
    ClinicalReview,
    TelehealthEligible
}

public static class TelehealthRequestStateMachine
{
    private static readonly IReadOnlyDictionary<TelehealthRequestStatus, TelehealthRequestStatus[]> Allowed =
        new Dictionary<TelehealthRequestStatus, TelehealthRequestStatus[]>
        {
            [TelehealthRequestStatus.Draft] = [TelehealthRequestStatus.LocationConfirmed],
            [TelehealthRequestStatus.LocationConfirmed] = [TelehealthRequestStatus.Intake, TelehealthRequestStatus.Redirected],
            [TelehealthRequestStatus.Intake] = [TelehealthRequestStatus.Verification],
            [TelehealthRequestStatus.Verification] = [TelehealthRequestStatus.Verification, TelehealthRequestStatus.OperationalReview],
            [TelehealthRequestStatus.OperationalReview] = [TelehealthRequestStatus.Verification, TelehealthRequestStatus.OperationalReview, TelehealthRequestStatus.Queued],
            [TelehealthRequestStatus.Queued] = [TelehealthRequestStatus.Reserved],
            [TelehealthRequestStatus.Redirected] = [],
            [TelehealthRequestStatus.Reserved] = [TelehealthRequestStatus.Queued, TelehealthRequestStatus.Connecting],
            [TelehealthRequestStatus.Connecting] = [TelehealthRequestStatus.Queued, TelehealthRequestStatus.InConsultation],
            [TelehealthRequestStatus.InConsultation] = [TelehealthRequestStatus.WrapUp],
            [TelehealthRequestStatus.WrapUp] = []
        };

    public static bool CanTransition(TelehealthRequestStatus current, TelehealthRequestStatus next) =>
        Allowed[current].Contains(next);

    public static void RequireTransition(TelehealthRequestStatus current, TelehealthRequestStatus next)
    {
        if (!CanTransition(current, next))
        {
            throw TelehealthProblem.Conflict(
                "telehealth_invalid_transition",
                $"A request in {current} cannot transition to {next}.");
        }
    }
}

public enum TelehealthEligibilityStatus
{
    Active,
    Inactive,
    Unknown
}

public enum TelehealthNetworkStatus
{
    ConfirmedInNetwork,
    OutOfNetwork,
    Unknown
}

public enum TelehealthFinancialRoute
{
    ConfirmedInNetwork,
    CoverageActiveNetworkPending,
    OutOfNetworkOrSelfPay,
    UnableToVerify,
    CoverageInactive
}

public sealed class TelehealthProblem(int statusCode, string code, string title, string detail) : Exception(detail)
{
    public int StatusCode { get; } = statusCode;
    public string Code { get; } = code;
    public string Title { get; } = title;

    public static TelehealthProblem BadRequest(string code, string detail) =>
        new(StatusCodes.Status400BadRequest, code, "Telehealth request is invalid", detail);

    public static TelehealthProblem Unauthorized(string detail = "An active patient session is required.") =>
        new(StatusCodes.Status401Unauthorized, "telehealth_patient_session_required", "Patient authentication is required", detail);

    public static TelehealthProblem Forbidden(string code, string detail) =>
        new(StatusCodes.Status403Forbidden, code, "Telehealth access is not authorized", detail);

    public static TelehealthProblem NotFound() =>
        new(StatusCodes.Status404NotFound, "telehealth_request_not_found", "Telehealth request was not found", "The request was not found in the authorized scope.");

    public static TelehealthProblem ApplicantNotFound() =>
        new(StatusCodes.Status404NotFound, "telehealth_applicant_not_found", "Telehealth applicant was not found", "The applicant was not found in the authorized scope.");

    public static TelehealthProblem ApplicantUnauthorized() =>
        new(StatusCodes.Status401Unauthorized, "telehealth_applicant_access_key_required", "Applicant access key is required", "A valid applicant access key is required.");

    public static TelehealthProblem Gone(string code, string detail) =>
        new(StatusCodes.Status410Gone, code, "Telehealth applicant is no longer active", detail);

    public static TelehealthProblem Conflict(string code, string detail) =>
        new(StatusCodes.Status409Conflict, code, "Telehealth request conflict", detail);
}

public static class TelehealthCommandFingerprint
{
    public static string Create(params object?[] values)
    {
        var canonical = string.Join('\u001f', values.Select(value => value switch
        {
            null => string.Empty,
            bool boolean => boolean ? "true" : "false",
            _ => value.ToString()?.Trim() ?? string.Empty
        }));
        return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(canonical)));
    }

    public static string RequireIdempotencyKey(string? value)
    {
        var key = value?.Trim() ?? string.Empty;
        if (key.Length is < 8 or > 128 || key.Any(character => char.IsControl(character)))
        {
            throw TelehealthProblem.BadRequest(
                "telehealth_idempotency_key_invalid",
                "X-Idempotency-Key must contain 8 to 128 printable characters.");
        }

        return key;
    }
}

public static class TelehealthAuthorizationPolicy
{
    public static bool IsAdministratorRole(string? role) =>
        string.Equals(role, "frontdesk", StringComparison.OrdinalIgnoreCase)
        || (!string.IsNullOrWhiteSpace(role) && role.Contains("admin", StringComparison.OrdinalIgnoreCase));

    public static bool IsPhysicianRole(string? role) =>
        string.Equals(role, "provider", StringComparison.OrdinalIgnoreCase)
        || string.Equals(role, "physician", StringComparison.OrdinalIgnoreCase);

    public static bool IsConfiguredFacility(int selectedFacilityId, int configuredFacilityId) =>
        selectedFacilityId > 0 && selectedFacilityId == configuredFacilityId;
}
