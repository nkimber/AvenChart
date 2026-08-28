// SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
// SPDX-License-Identifier: GPL-3.0-or-later

using AvenChart.Api.Models;
using AvenChart.Api.Security;
using Microsoft.Extensions.Options;

namespace AvenChart.Api.Features.Telehealth;

public sealed class TelehealthConsultationService(
    TelehealthConsultationRepository repository,
    TelehealthPharmacyRepository pharmacyRepository,
    TelehealthDispositionRepository dispositionRepository,
    TelehealthCompletionReviewRepository completionReviewRepository,
    IPharmacyDirectory pharmacyDirectory,
    IOptions<TelehealthOptions> options)
{
    private readonly TelehealthOptions _options = options.Value;

    public async Task<TelehealthConsultationStartResponse> StartAsync(
        AuthSessionResponse session,
        StaffAccessContext accessContext,
        Guid reservationId,
        StartTelehealthConsultationRequest request,
        string idempotencyKey,
        CancellationToken cancellationToken)
    {
        if (!TelehealthAuthorizationPolicy.IsPhysicianRole(session.Role))
        {
            throw TelehealthProblem.Forbidden(
                "telehealth_physician_role_required",
                "An eligible physician role is required to start a consultation.");
        }
        if (!TelehealthAuthorizationPolicy.IsConfiguredFacility(accessContext.FacilityId, _options.FacilityId))
        {
            throw TelehealthProblem.NotFound();
        }
        var physicianStaffId = session.StaffId
            ?? throw TelehealthProblem.Forbidden(
                "telehealth_staff_record_required",
                "The authenticated identity is not bound to an active staff record.");

        var normalized = Normalize(request);
        var key = TelehealthCommandFingerprint.RequireIdempotencyKey(idempotencyKey);
        var fingerprint = TelehealthCommandFingerprint.Create(
            "start-consultation",
            reservationId,
            normalized.ExpectedVersion,
            normalized.PatientLocationState,
            normalized.PatientIdentityDiscussed,
            normalized.CallbackConfirmed,
            normalized.PrivacyConfirmed,
            normalized.ConsentDiscussed,
            normalized.NoConcerningSymptomChange,
            normalized.EmergencyPlanConfirmed,
            normalized.CommunicationSufficient,
            normalized.SyntheticDataConfirmed);

        return await repository.StartAsync(
            _options.PracticeId,
            _options.FacilityId,
            physicianStaffId,
            reservationId,
            normalized,
            key,
            fingerprint,
            cancellationToken);
    }

    public async Task<TelehealthConsultationWorkspaceResponse> GetWorkspaceAsync(
        AuthSessionResponse session,
        StaffAccessContext accessContext,
        Guid consultationId,
        CancellationToken cancellationToken)
    {
        if (!TelehealthAuthorizationPolicy.IsPhysicianRole(session.Role))
        {
            throw TelehealthProblem.Forbidden(
                "telehealth_physician_role_required",
                "An eligible physician role is required to view a consultation workspace.");
        }
        if (!TelehealthAuthorizationPolicy.IsConfiguredFacility(accessContext.FacilityId, _options.FacilityId))
        {
            throw TelehealthProblem.NotFound();
        }
        var physicianStaffId = session.StaffId
            ?? throw TelehealthProblem.Forbidden(
                "telehealth_staff_record_required",
                "The authenticated identity is not bound to an active staff record.");

        return await repository.GetWorkspaceAsync(
            _options.PracticeId,
            _options.FacilityId,
            physicianStaffId,
            consultationId,
            cancellationToken)
            ?? throw TelehealthProblem.NotFound();
    }

    public async Task<TelehealthConsultationDocumentationDraftResponse> SaveDocumentationDraftAsync(
        AuthSessionResponse session,
        StaffAccessContext accessContext,
        Guid consultationId,
        TelehealthConsultationDocumentationDraftRequest request,
        CancellationToken cancellationToken)
    {
        if (!TelehealthAuthorizationPolicy.IsPhysicianRole(session.Role))
        {
            throw TelehealthProblem.Forbidden(
                "telehealth_physician_role_required",
                "An eligible physician role is required to save consultation documentation.");
        }
        if (!TelehealthAuthorizationPolicy.IsConfiguredFacility(accessContext.FacilityId, _options.FacilityId))
        {
            throw TelehealthProblem.NotFound();
        }
        var physicianStaffId = session.StaffId
            ?? throw TelehealthProblem.Forbidden(
                "telehealth_staff_record_required",
                "The authenticated identity is not bound to an active staff record.");
        if (request.ExpectedVersion < 0)
        {
            throw TelehealthProblem.BadRequest(
                "telehealth_documentation_version_invalid",
                "ExpectedVersion cannot be negative.");
        }

        try
        {
            return await repository.SaveDocumentationDraftAsync(
                _options.PracticeId,
                _options.FacilityId,
                physicianStaffId,
                consultationId,
                request,
                session.Username,
                cancellationToken)
                ?? throw TelehealthProblem.NotFound();
        }
        catch (EncounterSoapNoteConflictException exception)
        {
            throw TelehealthProblem.Conflict(
                exception.IsLocked
                    ? "telehealth_documentation_locked"
                    : "telehealth_documentation_version_conflict",
                exception.IsLocked
                    ? "This synthetic encounter has a locking signature. Ordinary draft changes are unavailable."
                    : $"The current draft is version {exception.CurrentVersion}. Reload it before making another change.");
        }
        catch (ArgumentException exception)
        {
            throw TelehealthProblem.BadRequest(
                "telehealth_documentation_invalid",
                exception.Message);
        }
    }

    public async Task<TelehealthConsultationWrapUpResponse> EnterWrapUpAsync(
        AuthSessionResponse session,
        StaffAccessContext accessContext,
        Guid consultationId,
        EnterTelehealthConsultationWrapUpRequest request,
        string idempotencyKey,
        CancellationToken cancellationToken)
    {
        if (!TelehealthAuthorizationPolicy.IsPhysicianRole(session.Role))
        {
            throw TelehealthProblem.Forbidden(
                "telehealth_physician_role_required",
                "An eligible physician role is required to enter consultation wrap-up.");
        }
        if (!TelehealthAuthorizationPolicy.IsConfiguredFacility(accessContext.FacilityId, _options.FacilityId))
        {
            throw TelehealthProblem.NotFound();
        }
        var physicianStaffId = session.StaffId
            ?? throw TelehealthProblem.Forbidden(
                "telehealth_staff_record_required",
                "The authenticated identity is not bound to an active staff record.");
        if (request.ExpectedVersion < 1)
        {
            throw TelehealthProblem.BadRequest(
                "telehealth_consultation_version_invalid",
                "ExpectedVersion must be positive.");
        }
        if (!request.SyntheticSessionEndedConfirmed
            || !request.DocumentationStillIncompleteAcknowledged
            || !request.WrapUpResponsibilityAcknowledged)
        {
            throw TelehealthProblem.BadRequest(
                "telehealth_wrap_up_acknowledgments_required",
                "Confirm the synthetic session end, unfinished documentation, and continuing physician responsibility before entering wrap-up.");
        }

        var key = TelehealthCommandFingerprint.RequireIdempotencyKey(idempotencyKey);
        var fingerprint = TelehealthCommandFingerprint.Create(
            "enter-consultation-wrap-up",
            consultationId,
            request.ExpectedVersion,
            request.SyntheticSessionEndedConfirmed,
            request.DocumentationStillIncompleteAcknowledged,
            request.WrapUpResponsibilityAcknowledged);

        return await repository.EnterWrapUpAsync(
            _options.PracticeId,
            _options.FacilityId,
            physicianStaffId,
            consultationId,
            request,
            key,
            fingerprint,
            cancellationToken)
            ?? throw TelehealthProblem.NotFound();
    }

    public async Task<TelehealthPharmacyChoiceWorkspaceResponse> GetPharmacyChoicesAsync(
        AuthSessionResponse session,
        StaffAccessContext accessContext,
        Guid consultationId,
        string? query,
        string? state,
        string? postalCode,
        string? originPostalCode,
        bool locationSearchAcknowledged,
        int limit,
        CancellationToken cancellationToken)
    {
        var physicianStaffId = RequireOwningPhysician(session, accessContext, "view synthetic pharmacy choices");
        var search = NormalizePharmacySearch(
            query,
            state,
            postalCode,
            originPostalCode,
            locationSearchAcknowledged,
            limit);
        try
        {
            return await pharmacyRepository.GetWorkspaceAsync(
                _options.PracticeId,
                _options.FacilityId,
                physicianStaffId,
                consultationId,
                search,
                pharmacyDirectory,
                cancellationToken)
                ?? throw TelehealthProblem.NotFound();
        }
        catch (ArgumentException exception)
        {
            throw TelehealthProblem.BadRequest("telehealth_pharmacy_search_invalid", exception.Message);
        }
    }

    public async Task<TelehealthPharmacyChoiceDraftResponse> RecordPharmacyChoiceAsync(
        AuthSessionResponse session,
        StaffAccessContext accessContext,
        Guid consultationId,
        RecordTelehealthPharmacyChoiceRequest request,
        string idempotencyKey,
        CancellationToken cancellationToken)
    {
        var physicianStaffId = RequireOwningPhysician(session, accessContext, "record a synthetic pharmacy choice");
        if (request.ExpectedVersion < 0)
        {
            throw TelehealthProblem.BadRequest(
                "telehealth_pharmacy_choice_version_invalid",
                "ExpectedVersion cannot be negative.");
        }
        if (request.DirectoryEntryId == Guid.Empty)
        {
            throw TelehealthProblem.BadRequest(
                "telehealth_pharmacy_choice_invalid",
                "Select an active synthetic pharmacy directory entry.");
        }
        if (!request.PatientChoiceConfirmed || !request.SyntheticDataConfirmed)
        {
            throw TelehealthProblem.BadRequest(
                "telehealth_pharmacy_choice_acknowledgments_required",
                "Confirm the patient's destination choice and synthetic-only data before recording it.");
        }

        var key = TelehealthCommandFingerprint.RequireIdempotencyKey(idempotencyKey);
        var fingerprint = TelehealthCommandFingerprint.Create(
            "record-consultation-pharmacy-choice",
            consultationId,
            request.ExpectedVersion,
            request.DirectoryEntryId,
            request.PatientChoiceConfirmed,
            request.SyntheticDataConfirmed,
            pharmacyDirectory.DatasetId,
            pharmacyDirectory.DatasetVersion);
        try
        {
            return await pharmacyRepository.RecordChoiceAsync(
                _options.PracticeId,
                _options.FacilityId,
                physicianStaffId,
                consultationId,
                request,
                key,
                fingerprint,
                pharmacyDirectory,
                cancellationToken)
                ?? throw TelehealthProblem.NotFound();
        }
        catch (TelehealthPharmacyChoiceConflictException exception)
        {
            throw TelehealthProblem.Conflict("telehealth_pharmacy_choice_conflict", exception.Message);
        }
        catch (ArgumentException exception)
        {
            throw TelehealthProblem.BadRequest("telehealth_pharmacy_choice_invalid", exception.Message);
        }
    }

    public async Task<TelehealthSafetyDispositionWorkspaceResponse> GetSafetyDispositionDraftAsync(
        AuthSessionResponse session,
        StaffAccessContext accessContext,
        Guid consultationId,
        CancellationToken cancellationToken)
    {
        var physicianStaffId = RequireOwningPhysician(session, accessContext, "view a synthetic safety-disposition draft");
        return await dispositionRepository.GetWorkspaceAsync(
            _options.PracticeId,
            _options.FacilityId,
            physicianStaffId,
            consultationId,
            cancellationToken)
            ?? throw TelehealthProblem.NotFound();
    }

    public async Task<TelehealthSafetyDispositionDraftResponse> RecordSafetyDispositionDraftAsync(
        AuthSessionResponse session,
        StaffAccessContext accessContext,
        Guid consultationId,
        RecordTelehealthSafetyDispositionDraftRequest request,
        string idempotencyKey,
        CancellationToken cancellationToken)
    {
        var physicianStaffId = RequireOwningPhysician(session, accessContext, "record a synthetic safety-disposition draft");
        try
        {
            var normalized = TelehealthSafetyDispositionRules.Normalize(request);
            var key = TelehealthCommandFingerprint.RequireIdempotencyKey(idempotencyKey);
            var fingerprint = TelehealthCommandFingerprint.Create(
                "record-consultation-safety-disposition-draft",
                consultationId,
                normalized.ExpectedVersion,
                normalized.DispositionCode,
                normalized.AdequateEvaluationCompleted,
                normalized.FollowUpOwner,
                normalized.FollowUpTimeframe,
                normalized.NextStepInstructions,
                normalized.WarningEscalationInstructions,
                normalized.CommunicationMethod,
                normalized.CommunicationCompleted,
                normalized.LocationCallbackReconfirmed,
                normalized.EmergencyInstructionProvided,
                normalized.EmergencyHandoffStatus,
                normalized.ContactAttemptSummary,
                normalized.SyntheticDataConfirmed);
            return await dispositionRepository.RecordAsync(
                _options.PracticeId,
                _options.FacilityId,
                physicianStaffId,
                consultationId,
                normalized,
                key,
                fingerprint,
                cancellationToken)
                ?? throw TelehealthProblem.NotFound();
        }
        catch (TelehealthSafetyDispositionConflictException exception)
        {
            throw TelehealthProblem.Conflict("telehealth_safety_disposition_conflict", exception.Message);
        }
        catch (ArgumentException exception)
        {
            throw TelehealthProblem.BadRequest("telehealth_safety_disposition_invalid", exception.Message);
        }
    }

    public async Task<TelehealthCompletionPrerequisitesResponse> GetCompletionPrerequisitesAsync(
        AuthSessionResponse session,
        StaffAccessContext accessContext,
        Guid consultationId,
        CancellationToken cancellationToken)
    {
        var physicianStaffId = RequireOwningPhysician(
            session,
            accessContext,
            "review synthetic completion prerequisites");
        return await completionReviewRepository.GetAsync(
            _options.PracticeId,
            _options.FacilityId,
            physicianStaffId,
            consultationId,
            cancellationToken)
            ?? throw TelehealthProblem.NotFound();
    }

    private int RequireOwningPhysician(
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

    private TelehealthPharmacyDirectorySearch NormalizePharmacySearch(
        string? query,
        string? state,
        string? postalCode,
        string? originPostalCode,
        bool locationSearchAcknowledged,
        int limit)
    {
        var normalizedQuery = NormalizeBounded(query, 64, "query");
        var normalizedState = NormalizeBounded(state, 2, "state")?.ToUpperInvariant();
        var normalizedPostalCode = NormalizePostalCode(postalCode, requiredLength: false, "postalCode");
        var normalizedOriginPostalCode = NormalizePostalCode(originPostalCode, requiredLength: true, "originPostalCode");
        if (normalizedState is not null
            && !_options.SupportedStates.Contains(normalizedState, StringComparer.Ordinal))
        {
            throw TelehealthProblem.BadRequest(
                "telehealth_pharmacy_search_state_invalid",
                "Pharmacy search state must be Georgia, California, or Florida.");
        }
        if (normalizedOriginPostalCode is not null && !locationSearchAcknowledged)
        {
            throw TelehealthProblem.BadRequest(
                "telehealth_pharmacy_location_acknowledgment_required",
                "Acknowledge the entered postal origin before requesting approximate distance.");
        }
        if (limit is < 1 or > 25)
        {
            throw TelehealthProblem.BadRequest(
                "telehealth_pharmacy_search_limit_invalid",
                "Pharmacy search limit must be between 1 and 25.");
        }
        return new TelehealthPharmacyDirectorySearch(
            normalizedQuery,
            normalizedState,
            normalizedPostalCode,
            normalizedOriginPostalCode,
            locationSearchAcknowledged,
            limit);
    }

    private static string? NormalizeBounded(string? value, int maxLength, string field)
    {
        var normalized = string.IsNullOrWhiteSpace(value) ? null : value.Trim();
        if (normalized?.Length > maxLength)
        {
            throw TelehealthProblem.BadRequest(
                "telehealth_pharmacy_search_invalid",
                $"{field} is longer than the allowed {maxLength} characters.");
        }
        return normalized;
    }

    private static string? NormalizePostalCode(string? value, bool requiredLength, string field)
    {
        var normalized = NormalizeBounded(value, 5, field);
        if (normalized is null)
        {
            return null;
        }
        if (!normalized.All(char.IsAsciiDigit) || (requiredLength && normalized.Length != 5))
        {
            throw TelehealthProblem.BadRequest(
                "telehealth_pharmacy_search_invalid",
                $"{field} must contain{(requiredLength ? " exactly" : " up to")} five digits.");
        }
        return normalized;
    }

    private StartTelehealthConsultationRequest Normalize(StartTelehealthConsultationRequest request)
    {
        if (request.ExpectedVersion < 1)
        {
            throw TelehealthProblem.BadRequest("telehealth_version_invalid", "ExpectedVersion must be positive.");
        }
        var state = request.PatientLocationState?.Trim().ToUpperInvariant() ?? string.Empty;
        if (!_options.SupportedStates.Contains(state, StringComparer.Ordinal))
        {
            throw TelehealthProblem.BadRequest(
                "telehealth_location_unsupported",
                "The reconfirmed patient location must be Georgia, California, or Florida.");
        }
        if (!request.SyntheticDataConfirmed)
        {
            throw TelehealthProblem.BadRequest(
                "telehealth_consultation_synthetic_confirmation_required",
                "Confirm that this consultation-start demonstration uses synthetic data only.");
        }
        if (!request.NoConcerningSymptomChange)
        {
            throw TelehealthProblem.BadRequest(
                "telehealth_consultation_safety_recheck_failed",
                "Do not start the synthetic consultation when symptoms changed or a red flag may be present. Reassess and follow the emergency or in-person pathway.");
        }
        if (!request.PatientIdentityDiscussed
            || !request.CallbackConfirmed
            || !request.PrivacyConfirmed
            || !request.ConsentDiscussed
            || !request.EmergencyPlanConfirmed
            || !request.CommunicationSufficient)
        {
            throw TelehealthProblem.BadRequest(
                "telehealth_consultation_start_checklist_incomplete",
                "Every synthetic consultation-start check must be affirmed before the lifecycle handoff.");
        }

        return request with { PatientLocationState = state };
    }
}
