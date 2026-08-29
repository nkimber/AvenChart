// SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
// SPDX-License-Identifier: GPL-3.0-or-later

using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using Npgsql;

namespace AvenChart.Api.Features.Telehealth;

public sealed class TelehealthReadinessHealthCheck(
    NpgsqlDataSource dataSource,
    IOptions<TelehealthOptions> options,
    ILogger<TelehealthReadinessHealthCheck> logger) : IHealthCheck
{
    private const int RequiredTableCount = 66;

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        var configuration = options.Value;
        var safeData = new Dictionary<string, object>
        {
            ["enabled"] = configuration.Enabled,
            ["mode"] = configuration.Mode
        };
        if (!configuration.Enabled)
        {
            return HealthCheckResult.Healthy("Telehealth is disabled by configuration.", safeData);
        }

        try
        {
            await using var command = dataSource.CreateCommand("""
                select count(*)
                from unnest(array[
                  'telehealth_requests',
                  'telehealth_protocol_versions',
                  'telehealth_patient_locations',
                  'telehealth_triage_assessments',
                  'telehealth_request_events',
                  'telehealth_queue_entries',
                  'telehealth_clinician_shifts',
                  'telehealth_reservations',
                  'telehealth_patient_confirmations',
                  'telehealth_intake_snapshots',
                  'telehealth_demonstration_acknowledgments',
                  'telehealth_coverage_selections',
                  'telehealth_coverage_verifications',
                  'telehealth_prospective_applicants',
                  'telehealth_applicant_contact_challenges',
                  'telehealth_applicant_verification_attempts',
                  'telehealth_applicant_events',
                  'telehealth_applicant_identity_review_decisions',
                  'telehealth_applicant_safety_triage_evaluations',
                  'telehealth_applicant_visit_purposes',
                  'telehealth_applicant_practice_network_prechecks',
                  'telehealth_applicant_member_insurance_details',
                  'telehealth_applicant_eligibility_results',
                  'telehealth_applicant_practice_network_determinations',
                  'telehealth_applicant_identity_proofing_results',
                  'telehealth_applicant_promotion_authorization_decisions',
                  'telehealth_applicant_synthetic_promotions',
                  'telehealth_applicant_notice_acknowledgments',
                  'telehealth_applicant_registration_details_confirmations',
                  'telehealth_applicant_insurance_handoff_confirmations',
                  'telehealth_applicant_communication_access_readiness',
                  'telehealth_applicant_device_preparations',
                  'telehealth_applicant_clinical_information_inventories',
                  'telehealth_applicant_medication_information_receipts',
                  'telehealth_applicant_reported_medication_items',
                  'telehealth_applicant_allergy_information_receipts',
                  'telehealth_applicant_reported_allergy_items',
                  'telehealth_applicant_health_history_information_receipts',
                  'telehealth_applicant_reported_health_history_topics',
                  'telehealth_applicant_clinical_information_summary_confirmations',
                  'telehealth_applicant_pre_request_readiness_acknowledgments',
                  'telehealth_prospective_practice_review_cases',
                  'telehealth_applicant_practice_review_submissions',
                  'telehealth_practice_review_claims',
                  'telehealth_practice_review_authorizations',
                  'telehealth_applicant_request_creations',
                  'telehealth_applicant_request_location_confirmations',
                  'telehealth_applicant_request_universal_safety_assessments',
                  'telehealth_applicant_request_complaint_triage_assessments',
                  'telehealth_applicant_request_intake_snapshots',
                  'telehealth_applicant_request_insurance_source_confirmations',
                  'telehealth_applicant_request_eligibility_verifications',
                  'telehealth_applicant_request_practice_network_verifications',
                  'telehealth_video_sessions',
                  'telehealth_video_preflights',
                  'telehealth_video_participant_grants',
                  'telehealth_video_events',
                  'telehealth_consultation_contexts',
                  'telehealth_consultation_events',
                  'telehealth_patient_pharmacy_preferences',
                  'telehealth_consultation_pharmacy_choice_versions',
                  'telehealth_consultation_pharmacy_choice_events',
                  'telehealth_consultation_disposition_draft_versions',
                  'telehealth_consultation_disposition_draft_events',
                  'telehealth_consultation_prescription_draft_versions',
                  'telehealth_consultation_prescription_draft_events'
                ]) as required(table_name)
                where to_regclass('public.' || required.table_name) is not null;
                """);
            var presentTableCount = Convert.ToInt32(await command.ExecuteScalarAsync(cancellationToken));
            safeData["requiredTableCount"] = RequiredTableCount;
            safeData["presentTableCount"] = presentTableCount;

            return presentTableCount == RequiredTableCount
                ? HealthCheckResult.Healthy("Synthetic telehealth schema is ready.", safeData)
                : HealthCheckResult.Unhealthy("Synthetic telehealth schema is incomplete.", data: safeData);
        }
        catch (Exception exception) when (exception is NpgsqlException or InvalidOperationException)
        {
            logger.LogWarning("Synthetic telehealth readiness check could not verify its schema.");
            return HealthCheckResult.Unhealthy("Synthetic telehealth readiness could not be verified.", data: safeData);
        }
    }
}
