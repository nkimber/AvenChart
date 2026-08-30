// SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
// SPDX-License-Identifier: GPL-3.0-or-later

using AvenChart.Api.Data;
using AvenChart.Api.Models;
using Npgsql;

namespace AvenChart.Api.Features.Telehealth;

public sealed class TelehealthEncounterFinalizationRepository(NpgsqlDataSource dataSource, EncounterRepository encounters)
{
    public async Task<TelehealthEncounterFinalizationResponse?> FinalizeAsync(
        string practiceId, int facilityId, int physicianStaffId, Guid consultationId,
        FinalizeTelehealthEncounterRequest request, string actor, CancellationToken cancellationToken)
    {
        var encounter = await ReadTargetEncounterAsync(practiceId, facilityId, physicianStaffId, consultationId, cancellationToken);
        if (encounter is null) return null;
        Source? source = null;
        var signature = await encounters.SignAsync(encounter.Value, new EncounterSignRequest(IsLock: true, Amendment: null), actor,
            cancellationToken, async (connection, transaction, token) =>
            {
                source = await ReadAndLockSourceAsync(connection, transaction, practiceId, facilityId, physicianStaffId, consultationId, token);
                if (source is null) throw new TelehealthEncounterFinalizationConflictException("The consultation is no longer eligible for synthetic encounter finalization.");
                Validate(source, request);
            });
        if (signature is null || source is null) return null;
        return new TelehealthEncounterFinalizationResponse(
            signature.Id, DateTimeOffset.UtcNow, source.DocumentationVersion, source.DispositionVersion!.Value, source.FinalReviewVersion!.Value,
            EncounterLocked: true, LegalEffect: false, CompletionCreated: false, PatientDeliveryCreated: false,
            BillingCreated: false, ClaimCreated: false, ExternalDestinationContacted: false,
            [
                "This NON_PRODUCTION synthetic encounter lock is not a legal signature or final clinical, billing, or claim determination.",
                "The encounter is locked for ordinary draft changes; any later change must use the governed amendment workflow.",
                "Visit completion, patient delivery, billing, claims, integration, and external actions remain unavailable."
            ]);
    }

    private async Task<int?> ReadTargetEncounterAsync(string practiceId, int facilityId, int physicianStaffId, Guid consultationId, CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            select context.encounter_id from telehealth_consultation_contexts context
            join telehealth_requests request on request.request_id=context.request_id
            join telehealth_reservations reservation on reservation.reservation_id=context.reservation_id
            join telehealth_clinician_shifts shift on shift.shift_id=context.shift_id
            join appointments appointment on appointment.id=context.appointment_id
            join encounters encounter on encounter.encounter=context.encounter_id
            where context.consultation_id=@consultationId and context.practice_id=@practiceId and context.facility_id=@facilityId
              and context.physician_staff_id=@physician and context.status='MediaEnded'
              and request.practice_id=@practiceId and request.facility_id=@facilityId and request.status='WrapUp'
              and reservation.clinician_staff_id=@physician and reservation.status='Released'
              and shift.clinician_staff_id=@physician and shift.status='WrapUp'
              and appointment.status='>' and encounter.provider_id=@physician and encounter.facility_id=@facilityId;
            """;
        command.Parameters.AddWithValue("consultationId", consultationId); command.Parameters.AddWithValue("practiceId", practiceId);
        command.Parameters.AddWithValue("facilityId", facilityId); command.Parameters.AddWithValue("physician", physicianStaffId);
        var result = await command.ExecuteScalarAsync(cancellationToken);
        return result is null or DBNull ? null : Convert.ToInt32(result);
    }

    private static async Task<Source?> ReadAndLockSourceAsync(NpgsqlConnection connection, NpgsqlTransaction transaction,
        string practiceId, int facilityId, int physicianStaffId, Guid consultationId, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand(); command.Transaction = transaction;
        command.CommandText = """
            select coalesce(note.version,0),coalesce(length(trim(coalesce(note.subjective,'')))>0,false),coalesce(length(trim(coalesce(note.objective,'')))>0,false),
                   coalesce(length(trim(coalesce(note.assessment,'')))>0,false),coalesce(length(trim(coalesce(note.plan,'')))>0,false),
                   disposition.version,review.version
            from telehealth_consultation_contexts context
            join telehealth_requests request on request.request_id=context.request_id
            join telehealth_reservations reservation on reservation.reservation_id=context.reservation_id
            join telehealth_clinician_shifts shift on shift.shift_id=context.shift_id
            join telehealth_video_sessions session on session.session_id=context.session_id
            join appointments appointment on appointment.id=context.appointment_id
            join encounters encounter on encounter.encounter=context.encounter_id
            left join lateral (select version,subjective,objective,assessment,plan from clinical_notes where encounter=context.encounter_id order by version desc,id desc limit 1) note on true
            left join lateral (select version from telehealth_consultation_disposition_draft_versions where consultation_id=context.consultation_id order by version desc limit 1) disposition on true
            left join lateral (select review.version from telehealth_consultation_final_clinical_review_versions review where review.consultation_id=context.consultation_id and review.documentation_version=coalesce(note.version,0) and review.disposition_version=disposition.version order by review.version desc limit 1) review on true
            where context.consultation_id=@consultationId and context.practice_id=@practiceId and context.facility_id=@facilityId and context.physician_staff_id=@physician and context.status='MediaEnded'
              and request.practice_id=@practiceId and request.facility_id=@facilityId and request.status='WrapUp' and reservation.clinician_staff_id=@physician and reservation.status='Released'
              and shift.clinician_staff_id=@physician and shift.status='WrapUp' and session.status='Ended' and appointment.status='>' and encounter.provider_id=@physician and encounter.facility_id=@facilityId
              and not exists(select 1 from encounter_signatures signature where signature.encounter=encounter.encounter and signature.is_lock)
            for update of context,request,reservation,shift,session,appointment,encounter;
            """;
        command.Parameters.AddWithValue("consultationId", consultationId); command.Parameters.AddWithValue("practiceId", practiceId);
        command.Parameters.AddWithValue("facilityId", facilityId); command.Parameters.AddWithValue("physician", physicianStaffId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken)) return null;
        return new Source(reader.GetInt32(0), reader.GetBoolean(1), reader.GetBoolean(2), reader.GetBoolean(3), reader.GetBoolean(4),
            reader.IsDBNull(5) ? null : reader.GetInt32(5), reader.IsDBNull(6) ? null : reader.GetInt32(6));
    }

    private static void Validate(Source source, FinalizeTelehealthEncounterRequest request)
    {
        if (!request.SourceReviewConfirmed || !request.SyntheticOnlyConfirmed) throw new ArgumentException("Confirm the current source review and synthetic-only effect before finalization.");
        if (!source.Subjective || !source.Objective || !source.Assessment || !source.Plan || source.DispositionVersion is null || source.FinalReviewVersion is null)
            throw new ArgumentException("Current complete SOAP, safety-disposition, and final clinical-review evidence are required before finalization.");
        if (request.ExpectedDocumentationVersion != source.DocumentationVersion || request.ExpectedDispositionVersion != source.DispositionVersion || request.ExpectedFinalClinicalReviewVersion != source.FinalReviewVersion)
            throw new TelehealthEncounterFinalizationConflictException("Current source evidence changed. Reload before finalizing the synthetic encounter.");
    }

    private sealed record Source(int DocumentationVersion, bool Subjective, bool Objective, bool Assessment, bool Plan, int? DispositionVersion, int? FinalReviewVersion);
}

public sealed class TelehealthEncounterFinalizationConflictException(string message) : Exception(message);
