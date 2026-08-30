// SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
// SPDX-License-Identifier: GPL-3.0-or-later

using AvenChart.Api.Models;
using AvenChart.Api.Security;
using Microsoft.Extensions.Options;

namespace AvenChart.Api.Features.Telehealth;

public sealed class TelehealthPrescriptionService(
    TelehealthPrescriptionRepository repository,
    IOptions<TelehealthOptions> options)
{
    private readonly TelehealthOptions _options = options.Value;

    public async Task<TelehealthPrescriptionPreparationWorkspaceResponse> GetWorkspaceAsync(
        AuthSessionResponse session,
        StaffAccessContext accessContext,
        Guid consultationId,
        string? query,
        CancellationToken cancellationToken)
    {
        var physicianStaffId = RequirePhysician(session, accessContext, "prepare a synthetic prescription draft");
        var normalizedQuery = NormalizeSearchQuery(query);
        return await repository.GetWorkspaceAsync(
            _options.PracticeId,
            _options.FacilityId,
            physicianStaffId,
            consultationId,
            normalizedQuery,
            cancellationToken)
            ?? throw TelehealthProblem.NotFound();
    }

    public async Task<TelehealthPrescriptionPreparationDraftResponse> RecordAsync(
        AuthSessionResponse session,
        StaffAccessContext accessContext,
        Guid consultationId,
        RecordTelehealthPrescriptionPreparationDraftRequest request,
        string idempotencyKey,
        CancellationToken cancellationToken)
    {
        var physicianStaffId = RequirePhysician(session, accessContext, "record a synthetic prescription draft");
        try
        {
            var normalized = Normalize(request);
            var key = TelehealthCommandFingerprint.RequireIdempotencyKey(idempotencyKey);
            var fingerprint = TelehealthCommandFingerprint.Create(
                "record-consultation-prescription-preparation-draft",
                consultationId,
                normalized.ExpectedVersion,
                normalized.RxNormCode,
                normalized.DoseAmount,
                normalized.DoseUnit,
                normalized.Frequency,
                normalized.QuantityValue,
                normalized.QuantityUnit,
                normalized.DurationDays,
                normalized.Refills,
                normalized.Indication,
                normalized.Directions,
                normalized.MedicationListReviewed,
                normalized.AllergyListReviewed,
                normalized.AdequateEvaluationCompleted,
                normalized.SyntheticDataConfirmed);
            return await repository.RecordAsync(
                _options.PracticeId,
                _options.FacilityId,
                physicianStaffId,
                consultationId,
                normalized,
                session.Username,
                key,
                fingerprint,
                cancellationToken)
                ?? throw TelehealthProblem.NotFound();
        }
        catch (TelehealthPrescriptionDraftConflictException exception)
        {
            throw TelehealthProblem.Conflict(
                "telehealth_prescription_draft_conflict",
                exception.Message);
        }
        catch (ArgumentException exception)
        {
            throw TelehealthProblem.BadRequest(
                "telehealth_prescription_draft_invalid",
                exception.Message);
        }
    }

    public async Task<TelehealthSignedPrescriptionResponse> SignAsync(
        AuthSessionResponse session,
        StaffAccessContext accessContext,
        Guid consultationId,
        SignTelehealthPrescriptionRequest request,
        string idempotencyKey,
        CancellationToken cancellationToken)
    {
        var physicianStaffId = RequirePhysician(session, accessContext, "sign a synthetic telehealth prescription");
        try
        {
            if (request.ExpectedDraftVersion < 1)
            {
                throw new ArgumentException("ExpectedDraftVersion must identify an existing draft version.");
            }
            if (!request.NoCurrentMedicationsConfirmed
                || !request.NoKnownAllergiesConfirmed
                || !request.AdequateEvaluationConfirmed
                || !request.SyntheticDataConfirmed)
            {
                throw new ArgumentException(
                    "Confirm the empty current medication list, empty known-allergy list, adequate evaluation, and synthetic-only effect before signing.");
            }

            var key = TelehealthCommandFingerprint.RequireIdempotencyKey(idempotencyKey);
            var fingerprint = TelehealthCommandFingerprint.Create(
                "sign-synthetic-telehealth-prescription",
                consultationId,
                request.ExpectedDraftVersion,
                request.NoCurrentMedicationsConfirmed,
                request.NoKnownAllergiesConfirmed,
                request.AdequateEvaluationConfirmed,
                request.SyntheticDataConfirmed);
            return await repository.SignAsync(
                _options.PracticeId,
                _options.FacilityId,
                physicianStaffId,
                consultationId,
                request,
                session.Username,
                key,
                fingerprint,
                cancellationToken)
                ?? throw TelehealthProblem.NotFound();
        }
        catch (TelehealthPrescriptionDraftConflictException exception)
        {
            throw TelehealthProblem.Conflict(
                "telehealth_prescription_signing_conflict",
                exception.Message);
        }
        catch (ArgumentException exception)
        {
            throw TelehealthProblem.BadRequest(
                "telehealth_prescription_signing_invalid",
                exception.Message);
        }
    }

    private int RequirePhysician(
        AuthSessionResponse session,
        StaffAccessContext accessContext,
        string action)
    {
        if (!TelehealthAuthorizationPolicy.IsPhysicianRole(session.Role))
        {
            throw TelehealthProblem.Forbidden(
                "telehealth_physician_role_required",
                $"An eligible physician role is required to {action}.");
        }
        if (!TelehealthAuthorizationPolicy.IsConfiguredFacility(accessContext.FacilityId, _options.FacilityId))
        {
            throw TelehealthProblem.NotFound();
        }
        return session.StaffId
            ?? throw TelehealthProblem.Forbidden(
                "telehealth_staff_record_required",
                "The authenticated identity is not bound to an active staff record.");
    }

    private static string? NormalizeSearchQuery(string? query)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return null;
        }
        var normalized = query.Trim();
        if (normalized.Length is < 2 or > 100)
        {
            throw TelehealthProblem.BadRequest(
                "telehealth_prescription_catalog_query_invalid",
                "Medication catalog search must contain between 2 and 100 characters.");
        }
        return normalized;
    }

    private static RecordTelehealthPrescriptionPreparationDraftRequest Normalize(
        RecordTelehealthPrescriptionPreparationDraftRequest request)
    {
        if (request.ExpectedVersion < 0)
        {
            throw new ArgumentException("ExpectedVersion cannot be negative.");
        }
        var rxNormCode = RequireText(request.RxNormCode, 64, "RxNorm code");
        if (!rxNormCode.All(char.IsLetterOrDigit))
        {
            throw new ArgumentException("RxNorm code must contain only letters and digits.");
        }
        if (request.DoseAmount is <= 0 or > 100000)
        {
            throw new ArgumentException("Dose amount must be greater than zero and no more than 100000.");
        }
        if (request.QuantityValue is <= 0 or > 100000)
        {
            throw new ArgumentException("Quantity must be greater than zero and no more than 100000.");
        }
        if (request.DurationDays is < 1 or > 365)
        {
            throw new ArgumentException("DurationDays must be between 1 and 365.");
        }
        if (request.Refills is < 0 or > 5)
        {
            throw new ArgumentException("Refills must be between 0 and 5.");
        }
        if (!request.MedicationListReviewed
            || !request.AllergyListReviewed
            || !request.AdequateEvaluationCompleted
            || !request.SyntheticDataConfirmed)
        {
            throw new ArgumentException(
                "Confirm current medication review, allergy review, adequate evaluation, and synthetic-only data before recording the draft.");
        }

        return request with
        {
            RxNormCode = rxNormCode,
            DoseUnit = RequireText(request.DoseUnit, 40, "Dose unit"),
            Frequency = RequireText(request.Frequency, 160, "Frequency"),
            QuantityUnit = RequireText(request.QuantityUnit, 40, "Quantity unit"),
            Indication = RequireText(request.Indication, 500, "Indication"),
            Directions = RequireText(request.Directions, 1000, "Directions")
        };
    }

    private static string RequireText(string? value, int maximumLength, string field)
    {
        var normalized = value?.Trim();
        if (string.IsNullOrEmpty(normalized) || normalized.Length > maximumLength)
        {
            throw new ArgumentException($"{field} is required and must not exceed {maximumLength} characters.");
        }
        return normalized;
    }
}
