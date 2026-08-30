// SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
// SPDX-License-Identifier: GPL-3.0-or-later

using Npgsql;

namespace AvenChart.Api.Features.Telehealth;

/// <summary>
/// Private, read-only claim-preparation projection. It deliberately exposes
/// only structural blockers and never creates a billing, claim, or transport
/// record.
/// </summary>
public sealed class TelehealthProfessionalClaimPreparationRepository(NpgsqlDataSource dataSource)
{
    public async Task<TelehealthProfessionalClaimPreparationWorkspaceResponse?> GetWorkspaceAsync(
        string practiceId, int facilityId, int physicianStaffId, Guid consultationId, CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            select now(),
                   exists(select 1 from telehealth_consultation_final_clinical_review_versions review
                          where review.consultation_id=context.consultation_id),
                   exists(select 1 from encounter_signatures signature
                          where signature.encounter=context.encounter_id and signature.is_lock)
            from telehealth_consultation_contexts context
            join telehealth_requests request on request.request_id=context.request_id
            join telehealth_reservations reservation on reservation.reservation_id=context.reservation_id
            join telehealth_clinician_shifts shift on shift.shift_id=context.shift_id
            join appointments appointment on appointment.id=context.appointment_id
            join encounters encounter on encounter.encounter=context.encounter_id
            where context.consultation_id=@consultationId
              and context.practice_id=@practiceId and context.facility_id=@facilityId
              and context.physician_staff_id=@physician and context.status='MediaEnded'
              and request.practice_id=@practiceId and request.facility_id=@facilityId and request.status='WrapUp'
              and reservation.clinician_staff_id=@physician and reservation.status='Released'
              and shift.clinician_staff_id=@physician and shift.status='WrapUp'
              and appointment.status='>' and encounter.provider_id=@physician and encounter.facility_id=@facilityId;
            """;
        command.Parameters.AddWithValue("consultationId", consultationId);
        command.Parameters.AddWithValue("practiceId", practiceId);
        command.Parameters.AddWithValue("facilityId", facilityId);
        command.Parameters.AddWithValue("physician", physicianStaffId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken)) return null;

        var reviewed = reader.GetBoolean(1);
        var signed = reader.GetBoolean(2);
        var blockers = new List<string>();
        if (!reviewed) blockers.Add("A current source-bound synthetic final clinical-review record is required before any future claim preparation.");
        if (!signed) blockers.Add("A governed encounter signature/finalization is required; no telehealth signature action is available in this slice.");
        blockers.Add("No physician-confirmed coding evidence (diagnosis, service, modifiers, and rule versions) is recorded.");
        blockers.Add("No billing-provider, payer/product, fee-schedule, or confirmed service-location evidence is recorded.");
        blockers.Add("No human billing approval is recorded. Autonomous claim preparation and submission are prohibited.");
        return new TelehealthProfessionalClaimPreparationWorkspaceResponse(
            consultationId, reader.GetFieldValue<DateTimeOffset>(0), reviewed, signed,
            CodingEvidenceRecorded: false, BillingProviderEvidenceRecorded: false, FeeScheduleEvidenceRecorded: false,
            HumanBillingApprovalRecorded: false, SyntheticProfessionalClaimGateway.AdapterMode,
            SyntheticProfessionalClaimGateway.TargetStandard, ClaimPreparationEnabled: false, ClaimSubmissionEnabled: false,
            blockers,
            [
                "This is a read-only NON_PRODUCTION preparation assessment. It creates no claim, billing item, transaction, or gateway call.",
                "A future claim must use a versioned canonical packet and certified clearinghouse tooling; bespoke X12 serialization is not enabled.",
                "Prepared, submitted, acknowledged, accepted, adjudicated, paid, and patient-billed are separate states."
            ]);
    }
}
