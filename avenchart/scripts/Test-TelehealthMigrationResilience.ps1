# SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
# SPDX-License-Identifier: GPL-3.0-or-later

[CmdletBinding()]
param(
    [switch]$SkipBaseRehearsal,
    [ValidatePattern('^avenchart(?:_test_[a-z0-9_]+)?$')]
    [string]$DatabaseName = 'avenchart'
)

$ErrorActionPreference = 'Stop'
$solutionRoot = Resolve-Path (Join-Path $PSScriptRoot '..')
$artifactsRoot = Join-Path $solutionRoot 'artifacts/telehealth'
$resultPath = Join-Path $artifactsRoot 'latest-telehealth-migration-resilience.json'
New-Item -ItemType Directory -Force $artifactsRoot | Out-Null
$checks = [System.Collections.Generic.List[object]]::new()
$passed = $true

function Add-Check([string]$Name, [bool]$Result, [object]$Details = $null) {
    $script:checks.Add([ordered]@{ name=$Name; status=$(if ($Result) { 'passed' } else { 'failed' }); details=$Details })
    if (-not $Result) { $script:passed = $false }
}

function Invoke-Scalar([string]$Sql) {
    Push-Location $solutionRoot
    try {
        $value = docker compose exec -T postgres psql -X -U avenchart -d $DatabaseName -t -A -v ON_ERROR_STOP=1 -c $Sql
        if ($LASTEXITCODE -ne 0) { throw 'PostgreSQL telehealth migration query failed.' }
        return ($value | Select-Object -Last 1).Trim()
    }
    finally { Pop-Location }
}

try {
    if (-not $SkipBaseRehearsal) {
        & (Join-Path $PSScriptRoot 'Test-AvenChartMigrationResilience.ps1')
        Add-Check 'Repository empty, populated, interruption, and recovery rehearsal includes V0282 through V0327' ($LASTEXITCODE -eq 0)
    }
    else {
        Add-Check 'Repository migration rehearsal supplied by the immediately preceding runtime-evidence step' $true
    }

    $migration = Join-Path $solutionRoot 'database/migrations/V0282__telehealth_foundation.sql'
    $source = Get-Content -Raw $migration
    Add-Check 'V0282 is additive and contains no destructive table operation' (
        $source -notmatch '(?im)^\s*(drop\s+table|truncate\s+|delete\s+from)' -and
        $source -match 'create\s+table\s+(?:if\s+not\s+exists\s+)?telehealth_requests')
    Add-Check 'V0282 is recorded in the live migration ledger' ((Invoke-Scalar "select count(*) from schema_migrations where migration_id='V0282__telehealth_foundation';") -eq '1')
    $readinessMigration = Join-Path $solutionRoot 'database/migrations/V0283__telehealth_established_patient_readiness.sql'
    $readinessSource = Get-Content -Raw $readinessMigration
    Add-Check 'V0283 is additive and contains no destructive table or row operation' (
        $readinessSource -notmatch '(?im)^\s*(drop\s+table|truncate\s+|delete\s+from)' -and
        $readinessSource -match 'create\s+table\s+(?:if\s+not\s+exists\s+)?telehealth_patient_confirmations')
    Add-Check 'V0283 is recorded in the live migration ledger' ((Invoke-Scalar "select count(*) from schema_migrations where migration_id='V0283__telehealth_established_patient_readiness';") -eq '1')
    $applicantMigration = Join-Path $solutionRoot 'database/migrations/V0284__telehealth_prospective_patient_identity.sql'
    $applicantSource = Get-Content -Raw $applicantMigration
    Add-Check 'V0284 is additive and contains no destructive table or row operation' (
        $applicantSource -notmatch '(?im)^\s*(drop\s+table|truncate\s+|delete\s+from)' -and
        $applicantSource -match 'create\s+table\s+(?:if\s+not\s+exists\s+)?telehealth_prospective_applicants')
    Add-Check 'V0284 is recorded in the live migration ledger' ((Invoke-Scalar "select count(*) from schema_migrations where migration_id='V0284__telehealth_prospective_patient_identity';") -eq '1')
    $videoMigration = Join-Path $solutionRoot 'database/migrations/V0285__telehealth_connection_room_shell.sql'
    $videoSource = Get-Content -Raw $videoMigration
    Add-Check 'V0285 is additive and contains no destructive table or row operation' (
        $videoSource -notmatch '(?im)^\s*(drop\s+table|truncate\s+|delete\s+from)' -and
        $videoSource -match 'create\s+table\s+(?:if\s+not\s+exists\s+)?telehealth_video_sessions')
    Add-Check 'V0285 is recorded in the live migration ledger' ((Invoke-Scalar "select count(*) from schema_migrations where migration_id='V0285__telehealth_connection_room_shell';") -eq '1')
    $consultationMigration = Join-Path $solutionRoot 'database/migrations/V0286__telehealth_consultation_start_handoff.sql'
    $consultationSource = Get-Content -Raw $consultationMigration
    Add-Check 'V0286 adds linkage only and contains no destructive table or row operation' (
        $consultationSource -notmatch '(?im)^\s*(drop\s+table|truncate\s+|delete\s+from)' -and
        $consultationSource -match 'create\s+table\s+(?:if\s+not\s+exists\s+)?telehealth_consultation_contexts')
    Add-Check 'V0286 is recorded in the live migration ledger' ((Invoke-Scalar "select count(*) from schema_migrations where migration_id='V0286__telehealth_consultation_start_handoff';") -eq '1')
    $wrapUpMigration = Join-Path $solutionRoot 'database/migrations/V0287__telehealth_consultation_wrap_up_handoff.sql'
    $wrapUpSource = Get-Content -Raw $wrapUpMigration
    Add-Check 'V0287 evolves lifecycle constraints without destructive table or row operations' (
        $wrapUpSource -notmatch '(?im)^\s*(drop\s+table|truncate\s+|delete\s+from)' -and
        $wrapUpSource -match 'add column if not exists media_ended_at' -and
        $wrapUpSource -match 'govern_telehealth_consultation_context_mutation')
    Add-Check 'V0287 is recorded in the live migration ledger' ((Invoke-Scalar "select count(*) from schema_migrations where migration_id='V0287__telehealth_consultation_wrap_up_handoff';") -eq '1')
    $pharmacyMigration = Join-Path $solutionRoot 'database/migrations/V0288__telehealth_synthetic_pharmacy_choice.sql'
    $pharmacySource = Get-Content -Raw $pharmacyMigration
    Add-Check 'V0288 adds append-only synthetic pharmacy-choice evidence without destructive or downstream clinical operations' (
        $pharmacySource -notmatch '(?im)^\s*(drop\s+table|truncate\s+|delete\s+from)' -and
        $pharmacySource -match 'create\s+table\s+(?:if\s+not\s+exists\s+)?telehealth_consultation_pharmacy_choice_versions' -and
        $pharmacySource -notmatch '(?i)insert\s+into\s+(prescriptions|medications|encounter_signatures|claims|billing)')
    Add-Check 'V0288 is recorded in the live migration ledger' ((Invoke-Scalar "select count(*) from schema_migrations where migration_id='V0288__telehealth_synthetic_pharmacy_choice';") -eq '1')
    $dispositionMigration = Join-Path $solutionRoot 'database/migrations/V0289__telehealth_synthetic_safety_disposition_draft.sql'
    $dispositionSource = Get-Content -Raw $dispositionMigration
    Add-Check 'V0289 adds append-only synthetic safety-disposition draft evidence without destructive or downstream clinical operations' (
        $dispositionSource -notmatch '(?im)^\s*(drop\s+table|truncate\s+|delete\s+from)' -and
        $dispositionSource -match 'create\s+table\s+(?:if\s+not\s+exists\s+)?telehealth_consultation_disposition_draft_versions' -and
        $dispositionSource -match 'legal_effect boolean not null default false' -and
        $dispositionSource -notmatch '(?i)insert\s+into\s+(prescriptions|medications|encounter_signatures|claims|billing|orders|diagnoses|messages|integration_outbox)')
    Add-Check 'V0289 is recorded in the live migration ledger' ((Invoke-Scalar "select count(*) from schema_migrations where migration_id='V0289__telehealth_synthetic_safety_disposition_draft';") -eq '1')
    $prescriptionMigration = Join-Path $solutionRoot 'database/migrations/V0290__telehealth_synthetic_prescription_preparation_draft.sql'
    $prescriptionSource = Get-Content -Raw $prescriptionMigration
    Add-Check 'V0290 adds append-only synthetic prescription-preparation evidence without destructive, canonical, or downstream clinical operations' (
        $prescriptionSource -notmatch '(?im)^\s*(drop\s+table|truncate\s+|delete\s+from)' -and
        $prescriptionSource -match 'create\s+table\s+(?:if\s+not\s+exists\s+)?telehealth_consultation_prescription_draft_versions' -and
        $prescriptionSource -match 'controlled_substance_schedule_snapshot is null' -and
        $prescriptionSource -match 'not legal_effect and not safety_checked and not signed and not transmission_queued' -and
        $prescriptionSource -notmatch '(?i)insert\s+into\s+(prescriptions|medications|encounter_signatures|claims|billing|orders|diagnoses|messages|integration_outbox)')
    Add-Check 'V0290 is recorded in the live migration ledger' ((Invoke-Scalar "select count(*) from schema_migrations where migration_id='V0290__telehealth_synthetic_prescription_preparation_draft';") -eq '1')
    $identityReviewMigration = Join-Path $solutionRoot 'database/migrations/V0291__telehealth_synthetic_applicant_identity_review.sql'
    $identityReviewSource = Get-Content -Raw $identityReviewMigration
    Add-Check 'V0291 adds bounded append-only applicant review evidence without patient promotion or downstream operations' (
        $identityReviewSource -notmatch '(?im)^\s*(drop\s+table|truncate\s+|delete\s+from)' -and
        $identityReviewSource -match 'create\s+table\s+(?:if\s+not\s+exists\s+)?telehealth_applicant_identity_review_decisions' -and
        $identityReviewSource -match 'not identity_proofed and not canonical_patient_created and not chart_linked' -and
        $identityReviewSource -notmatch '(?i)insert\s+into\s+(patients|patient_portal_accounts|telehealth_requests|telehealth_queue_entries|appointments|encounters|claims|billing|prescriptions|messages|integration_outbox)')
    Add-Check 'V0291 is recorded in the live migration ledger' ((Invoke-Scalar "select count(*) from schema_migrations where migration_id='V0291__telehealth_synthetic_applicant_identity_review';") -eq '1')
    $prospectiveSafetyMigration = Join-Path $solutionRoot 'database/migrations/V0292__telehealth_prospective_safety_triage.sql'
    $prospectiveSafetySource = Get-Content -Raw $prospectiveSafetyMigration
    Add-Check 'V0292 adds one bounded append-only prospective safety evaluation without patient promotion or downstream operations' (
        $prospectiveSafetySource -notmatch '(?im)^\s*(drop\s+table|truncate\s+|delete\s+from)' -and
        $prospectiveSafetySource -match 'create\s+table\s+(?:if\s+not\s+exists\s+)?telehealth_applicant_safety_triage_evaluations' -and
        $prospectiveSafetySource -match 'not identity_proofed and not clinical_review_performed' -and
        $prospectiveSafetySource -match 'not request_created and not queue_enabled and not care_enabled' -and
        $prospectiveSafetySource -notmatch '(?i)insert\s+into\s+(patients|patient_portal_accounts|telehealth_requests|telehealth_queue_entries|appointments|encounters|claims|billing|prescriptions|messages|integration_outbox)')
    Add-Check 'V0292 is recorded in the live migration ledger' ((Invoke-Scalar "select count(*) from schema_migrations where migration_id='V0292__telehealth_prospective_safety_triage';") -eq '1')
    $visitPurposeMigration = Join-Path $solutionRoot 'database/migrations/V0293__telehealth_prospective_visit_purpose.sql'
    $visitPurposeSource = Get-Content -Raw $visitPurposeMigration
    Add-Check 'V0293 adds one bounded append-only controlled visit purpose without clinical eligibility, patient promotion, or downstream operations' (
        $visitPurposeSource -notmatch '(?im)^\s*(drop\s+table|truncate\s+|delete\s+from)' -and
        $visitPurposeSource -match 'create\s+table\s+(?:if\s+not\s+exists\s+)?telehealth_applicant_visit_purposes' -and
        $visitPurposeSource -match "purpose_category='migraine'" -and
        $visitPurposeSource -match "purpose_category='sleep'" -and
        $visitPurposeSource -match 'not clinical_protocol_published and not clinical_eligibility_determined' -and
        $visitPurposeSource -match 'not request_created\s+and not queue_enabled and not care_enabled' -and
        $visitPurposeSource -notmatch '(?i)insert\s+into\s+(patients|patient_portal_accounts|telehealth_requests|telehealth_queue_entries|appointments|encounters|claims|billing|prescriptions|messages|integration_outbox)')
    Add-Check 'V0293 is recorded in the live migration ledger' ((Invoke-Scalar "select count(*) from schema_migrations where migration_id='V0293__telehealth_prospective_visit_purpose';") -eq '1')
    $practiceNetworkMigration = Join-Path $solutionRoot 'database/migrations/V0294__telehealth_prospective_practice_network_precheck.sql'
    $practiceNetworkSource = Get-Content -Raw $practiceNetworkMigration
    Add-Check 'V0294 adds one bounded append-only synthetic practice-network fixture without member eligibility, coverage, promotion, or downstream operations' (
        $practiceNetworkSource -notmatch '(?im)^\s*(drop\s+table|truncate\s+|delete\s+from)' -and
        $practiceNetworkSource -match 'create\s+table\s+(?:if\s+not\s+exists\s+)?telehealth_applicant_practice_network_prechecks' -and
        $practiceNetworkSource -match "plan_key='harbor-mutual-hd'" -and
        $practiceNetworkSource -match "plan_key='blue-valley-standard'" -and
        $practiceNetworkSource -match "plan_key='pine-state-choice'" -and
        $practiceNetworkSource -match "adapter_mode='NON_PRODUCTION'" -and
        $practiceNetworkSource -match 'not member_eligibility_checked' -and
        $practiceNetworkSource -match 'not exact_network_confirmed' -and
        $practiceNetworkSource -match 'not integration_enabled and not external_call_performed' -and
        $practiceNetworkSource -notmatch '(?i)insert\s+into\s+(patients|patient_portal_accounts|insurance_records|telehealth_requests|telehealth_queue_entries|appointments|encounters|claims|billing|prescriptions|messages|integration_outbox)')
    Add-Check 'V0294 is recorded in the live migration ledger' ((Invoke-Scalar "select count(*) from schema_migrations where migration_id='V0294__telehealth_prospective_practice_network_precheck';") -eq '1')
    $memberDetailsMigration = Join-Path $solutionRoot 'database/migrations/V0295__telehealth_prospective_member_insurance_details.sql'
    $memberDetailsSource = Get-Content -Raw $memberDetailsMigration
    Add-Check 'V0295 adds one protected append-only synthetic member-details receipt without eligibility, canonical coverage, promotion, or downstream operations' (
        $memberDetailsSource -notmatch '(?im)^\s*(drop\s+table|truncate\s+|delete\s+from)' -and
        $memberDetailsSource -match 'create\s+table\s+(?:if\s+not\s+exists\s+)?telehealth_applicant_member_insurance_details' -and
        $memberDetailsSource -match "protection_scheme='ASP.NET_CORE_DATA_PROTECTION'" -and
        $memberDetailsSource -match "protection_purpose='AvenChart.Telehealth.ProspectiveMemberInsuranceDetails.v1'" -and
        $memberDetailsSource -match 'review_row\.decision_id is null' -and
        $memberDetailsSource -match 'safety_row\.evaluation_id is null' -and
        $memberDetailsSource -match 'purpose_row\.purpose_id is null' -and
        $memberDetailsSource -match 'precheck_row\.precheck_id is null' -and
        $memberDetailsSource -match 'not member_matched and not member_eligibility_checked' -and
        $memberDetailsSource -match 'not coverage_verified and not exact_network_confirmed' -and
        $memberDetailsSource -match 'not integration_enabled and not external_call_performed' -and
        $memberDetailsSource -notmatch '(?i)insert\s+into\s+(patients|patient_portal_accounts|insurance_records|telehealth_requests|telehealth_queue_entries|appointments|encounters|claims|billing|prescriptions|messages|integration_outbox)')
    Add-Check 'V0295 is recorded in the live migration ledger' ((Invoke-Scalar "select count(*) from schema_migrations where migration_id='V0295__telehealth_prospective_member_insurance_details';") -eq '1')
    $eligibilityMigration = Join-Path $solutionRoot 'database/migrations/V0296__telehealth_prospective_synthetic_eligibility.sql'
    $eligibilitySource = Get-Content -Raw $eligibilityMigration
    Add-Check 'V0296 adds one normalized append-only NON_PRODUCTION eligibility result without raw X12, exact network, canonical coverage, promotion, or downstream operations' (
        $eligibilitySource -notmatch '(?im)^\s*(drop\s+table|truncate\s+|delete\s+from)' -and
        $eligibilitySource -match 'create\s+table\s+(?:if\s+not\s+exists\s+)?telehealth_applicant_eligibility_results' -and
        $eligibilitySource -match "adapter_mode='NON_PRODUCTION'" -and
        $eligibilitySource -match "compatibility_target='ASC_X12N_270_271_005010X279A1'" -and
        $eligibilitySource -match "business_outcome='EligibleBenefitsReported'" -and
        $eligibilitySource -match "business_outcome='CoverageInactive'" -and
        $eligibilitySource -match "business_outcome='SubscriberNotFound'" -and
        $eligibilitySource -match "business_outcome='UnableToDetermine'" -and
        $eligibilitySource -match 'not raw_transaction_created and not exact_network_confirmed' -and
        $eligibilitySource -match 'not integration_enabled and not external_call_performed' -and
        $eligibilitySource -notmatch '(?i)insert\s+into\s+(patients|patient_portal_accounts|insurance_records|telehealth_requests|telehealth_queue_entries|appointments|encounters|claims|billing|prescriptions|messages|integration_outbox)')
    Add-Check 'V0296 is recorded in the live migration ledger' ((Invoke-Scalar "select count(*) from schema_migrations where migration_id='V0296__telehealth_prospective_synthetic_eligibility';") -eq '1')
    $practiceNetworkDeterminationMigration = Join-Path $solutionRoot 'database/migrations/V0297__telehealth_prospective_synthetic_practice_network.sql'
    $practiceNetworkDeterminationSource = Get-Content -Raw $practiceNetworkDeterminationMigration
    Add-Check 'V0297 adds one normalized append-only NON_PRODUCTION practice-network determination without FHIR resources, physician participation, coverage, promotion, or downstream operations' (
        $practiceNetworkDeterminationSource -notmatch '(?im)^\s*(drop\s+table|truncate\s+|delete\s+from)' -and
        $practiceNetworkDeterminationSource -match 'create\s+table\s+(?:if\s+not\s+exists\s+)?telehealth_applicant_practice_network_determinations' -and
        $practiceNetworkDeterminationSource -match "adapter_mode='NON_PRODUCTION'" -and
        $practiceNetworkDeterminationSource -match "compatibility_target='HL7_FHIR_R4_DAVINCI_PDEX_PLAN_NET_1_2_0'" -and
        $practiceNetworkDeterminationSource -match "business_outcome='PracticeInNetworkAcceptingNewPatients'" -and
        $practiceNetworkDeterminationSource -match "business_outcome='PracticeOutOfNetwork'" -and
        $practiceNetworkDeterminationSource -match "business_outcome='UnableToDetermine'" -and
        $practiceNetworkDeterminationSource -match 'not fhir_resource_created and not live_directory_queried' -and
        $practiceNetworkDeterminationSource -match 'not rendering_physician_network_checked' -and
        $practiceNetworkDeterminationSource -match 'not exact_network_confirmed and not coverage_verified' -and
        $practiceNetworkDeterminationSource -match 'not integration_enabled' -and
        $practiceNetworkDeterminationSource -match 'not external_call_performed' -and
        $practiceNetworkDeterminationSource -notmatch '(?i)insert\s+into\s+(patients|patient_portal_accounts|insurance_records|telehealth_requests|telehealth_queue_entries|appointments|encounters|claims|billing|prescriptions|messages|integration_outbox)')
    Add-Check 'V0297 is recorded in the live migration ledger' ((Invoke-Scalar "select count(*) from schema_migrations where migration_id='V0297__telehealth_prospective_synthetic_practice_network';") -eq '1')
    $identityProofingMigration = Join-Path $solutionRoot 'database/migrations/V0298__telehealth_prospective_synthetic_identity_proofing.sql'
    $identityProofingSource = Get-Content -Raw $identityProofingMigration
    Add-Check 'V0298 adds one normalized append-only NON_PRODUCTION identity-proofing process fixture without evidence, government identifiers, biometrics, IAL, promotion, or downstream operations' (
        $identityProofingSource -notmatch '(?im)^\s*(drop\s+table|truncate\s+|delete\s+from)' -and
        $identityProofingSource -match 'create\s+table\s+(?:if\s+not\s+exists\s+)?telehealth_applicant_identity_proofing_results' -and
        $identityProofingSource -match "adapter_mode='NON_PRODUCTION'" -and
        $identityProofingSource -match "compatibility_target='NIST_SP_800_63A_4_PROCESS_CONCEPTS_ONLY'" -and
        $identityProofingSource -match "business_outcome='SyntheticProofingPassed'" -and
        $identityProofingSource -match "assurance_level_achieved='None'" -and
        $identityProofingSource -match 'not identity_evidence_collected and not government_identifier_collected' -and
        $identityProofingSource -match 'not biometric_data_collected and not authoritative_source_queried' -and
        $identityProofingSource -match 'not authenticator_bound and not identity_proofed' -and
        $identityProofingSource -match 'not integration_enabled' -and
        $identityProofingSource -match 'not external_call_performed' -and
        $identityProofingSource -notmatch '(?i)insert\s+into\s+(patients|patient_portal_accounts|insurance_records|telehealth_requests|telehealth_queue_entries|appointments|encounters|claims|billing|prescriptions|messages|integration_outbox)')
    Add-Check 'V0298 is recorded in the live migration ledger' ((Invoke-Scalar "select count(*) from schema_migrations where migration_id='V0298__telehealth_prospective_synthetic_identity_proofing';") -eq '1')
    $promotionAuthorizationMigration = Join-Path $solutionRoot 'database/migrations/V0299__telehealth_synthetic_promotion_authorization.sql'
    $promotionAuthorizationSource = Get-Content -Raw $promotionAuthorizationMigration
    Add-Check 'V0299 adds one append-only staff governance decision without identity proofing, patient promotion, downstream, or outbound operations' (
        $promotionAuthorizationSource -notmatch '(?im)^\s*(drop\s+table|truncate\s+|delete\s+from)' -and
        $promotionAuthorizationSource -match 'create\s+table\s+(?:if\s+not\s+exists\s+)?telehealth_applicant_promotion_authorization_decisions' -and
        $promotionAuthorizationSource -match "assurance_level_achieved='None'" -and
        $promotionAuthorizationSource -match 'not proofing_identity_proofed' -and
        $promotionAuthorizationSource -match 'none_assurance_acknowledged and synthetic_data_confirmed' -and
        $promotionAuthorizationSource -match 'not real_identity_proofed and not canonical_patient_created and not chart_linked' -and
        $promotionAuthorizationSource -match 'not portal_account_created and not prospective_intake_completed' -and
        $promotionAuthorizationSource -match 'not request_created' -and
        $promotionAuthorizationSource -match 'not queue_enabled' -and
        $promotionAuthorizationSource -match 'not integration_enabled' -and
        $promotionAuthorizationSource -match 'not external_call_performed' -and
        $promotionAuthorizationSource -notmatch '(?i)insert\s+into\s+(patients|patient_portal_accounts|insurance_records|telehealth_requests|telehealth_queue_entries|appointments|encounters|claims|billing|prescriptions|messages|integration_outbox)')
    Add-Check 'V0299 is recorded in the live migration ledger' ((Invoke-Scalar "select count(*) from schema_migrations where migration_id='V0299__telehealth_synthetic_promotion_authorization';") -eq '1')
    $syntheticPromotionMigration = Join-Path $solutionRoot 'database/migrations/V0300__telehealth_atomic_synthetic_patient_promotion.sql'
    $syntheticPromotionSource = Get-Content -Raw $syntheticPromotionMigration
    Add-Check 'V0300 adds one atomic duplicate-rechecked patient-shell promotion with append-only provenance and no downstream capability' (
        $syntheticPromotionSource -notmatch '(?im)^\s*(drop\s+table|truncate\s+|delete\s+from)' -and
        $syntheticPromotionSource -match 'create\s+table\s+(?:if\s+not\s+exists\s+)?telehealth_applicant_synthetic_promotions' -and
        $syntheticPromotionSource -match "command='PromoteAuthorizedSyntheticApplicant'" -and
        $syntheticPromotionSource -match "outcome='SyntheticPatientCreated'" -and
        $syntheticPromotionSource -match "outcome='BlockedPossiblePatientMatch'" -and
        $syntheticPromotionSource -match "assurance_level_achieved='None'" -and
        $syntheticPromotionSource -match 'canonical_patient_creation_acknowledged and no_portal_no_care_acknowledged' -and
        $syntheticPromotionSource -match 'not portal_account_created' -and
        $syntheticPromotionSource -match 'not request_created' -and
        $syntheticPromotionSource -match 'not queue_enabled' -and
        $syntheticPromotionSource -match 'not care_enabled' -and
        $syntheticPromotionSource -match 'not external_call_performed' -and
        $syntheticPromotionSource -notmatch '(?i)insert\s+into\s+(patient_portal_accounts|insurance_records|telehealth_requests|telehealth_queue_entries|appointments|encounters|claims|billing|prescriptions|messages|integration_outbox)')
    Add-Check 'V0300 is recorded in the live migration ledger' ((Invoke-Scalar "select count(*) from schema_migrations where migration_id='V0300__telehealth_atomic_synthetic_patient_promotion';") -eq '1')
    $noticeMigration = Join-Path $solutionRoot 'database/migrations/V0301__telehealth_state_notice_acknowledgment.sql'
    $noticeSource = Get-Content -Raw $noticeMigration
    Add-Check 'V0301 adds one promotion-bound state-specific synthetic acknowledgment without legal consent, portal, downstream, care, or outbound operations' (
        $noticeSource -notmatch '(?im)^\s*(drop\s+table|truncate\s+|delete\s+from)' -and
        $noticeSource -match 'create\s+table\s+(?:if\s+not\s+exists\s+)?telehealth_applicant_notice_acknowledgments' -and
        $noticeSource -match "notice_key='GA_TELEHEALTH_NOTICE_V1'" -and
        $noticeSource -match "notice_key='CA_TELEHEALTH_NOTICE_V1'" -and
        $noticeSource -match "notice_key='FL_TELEHEALTH_NOTICE_V1'" -and
        $noticeSource -match "legal_review_status='PendingIndependentReview'" -and
        $noticeSource -match 'current_location_confirmed and mode_of_care_acknowledged' -and
        $noticeSource -match 'clinician_reconfirmation_required_acknowledged' -and
        $noticeSource -match 'not legal_consent_established' -and
        $noticeSource -match 'not clinician_consent_documented' -and
        $noticeSource -match 'not portal_account_created' -and
        $noticeSource -match 'not request_created' -and
        $noticeSource -match 'not queue_enabled' -and
        $noticeSource -match 'not care_enabled' -and
        $noticeSource -match 'not external_call_performed' -and
        $noticeSource -notmatch '(?i)insert\s+into\s+(patient_portal_accounts|insurance_records|telehealth_requests|telehealth_queue_entries|appointments|encounters|claims|billing|prescriptions|messages|integration_outbox)')
    Add-Check 'V0301 is recorded in the live migration ledger' ((Invoke-Scalar "select count(*) from schema_migrations where migration_id='V0301__telehealth_state_notice_acknowledgment';") -eq '1')
    $registrationDetailsMigration = Join-Path $solutionRoot 'database/migrations/V0302__telehealth_minimum_registration_details_confirmation.sql'
    $registrationDetailsSource = Get-Content -Raw $registrationDetailsMigration
    Add-Check 'V0302 adds one notice-bound no-edit minimum registration-details confirmation without identity, patient mutation, intake, insurance, downstream, care, or outbound consequences' (
        $registrationDetailsSource -notmatch '(?im)^\s*(drop\s+table|truncate\s+|delete\s+from)' -and
        $registrationDetailsSource -match 'create\s+table\s+(?:if\s+not\s+exists\s+)?telehealth_applicant_registration_details_confirmations' -and
        $registrationDetailsSource -match 'legal_name_birth_date_confirmed' -and
        $registrationDetailsSource -match 'contact_channels_confirmed' -and
        $registrationDetailsSource -match 'residence_region_confirmed' -and
        $registrationDetailsSource -match 'no_corrections_needed_confirmed' -and
        $registrationDetailsSource -match "policy_key='SYNTHETIC_MINIMUM_REGISTRATION_DETAILS_CONFIRMATION'" -and
        $registrationDetailsSource -match "evidence_type='PROMOTED_PATIENT_MINIMUM_DETAILS_NO_EDIT_CONFIRMATION'" -and
        $registrationDetailsSource -match 'not identity_assurance_established' -and
        $registrationDetailsSource -match 'not patient_record_changed' -and
        $registrationDetailsSource -match 'not correction_completed' -and
        $registrationDetailsSource -match 'not insurance_confirmed' -and
        $registrationDetailsSource -match 'not request_created' -and
        $registrationDetailsSource -match 'not queue_enabled' -and
        $registrationDetailsSource -match 'not care_enabled' -and
        $registrationDetailsSource -match 'not external_call_performed' -and
        $registrationDetailsSource -notmatch '(?i)insert\s+into\s+(patient_portal_accounts|insurance_records|telehealth_requests|telehealth_queue_entries|appointments|encounters|claims|billing|prescriptions|messages|integration_outbox)')
    Add-Check 'V0302 is recorded in the live migration ledger' ((Invoke-Scalar "select count(*) from schema_migrations where migration_id='V0302__telehealth_minimum_registration_details_confirmation';") -eq '1')
    $insuranceHandoffMigration = Join-Path $solutionRoot 'database/migrations/V0303__telehealth_insurance_handoff_confirmation.sql'
    $insuranceHandoffSource = Get-Content -Raw $insuranceHandoffMigration
    Add-Check 'V0303 adds one evidence-bound no-edit synthetic insurance handoff confirmation without canonical coverage, exact network, downstream, care, or outbound consequences' (
        $insuranceHandoffSource -notmatch '(?im)^\s*(drop\s+table|truncate\s+|delete\s+from)' -and
        $insuranceHandoffSource -match 'create\s+table\s+(?:if\s+not\s+exists\s+)?telehealth_applicant_insurance_handoff_confirmations' -and
        $insuranceHandoffSource -match 'payer_product_confirmed' -and
        $insuranceHandoffSource -match 'masked_member_details_confirmed' -and
        $insuranceHandoffSource -match 'subscriber_relationship_confirmed' -and
        $insuranceHandoffSource -match 'evidence_limitations_acknowledged' -and
        $insuranceHandoffSource -match "policy_key='SYNTHETIC_INSURANCE_HANDOFF_CONFIRMATION'" -and
        $insuranceHandoffSource -match "evidence_type='PROMOTED_PATIENT_INSURANCE_HANDOFF_NO_EDIT_CONFIRMATION'" -and
        $insuranceHandoffSource -match 'not rendering_physician_network_checked' -and
        $insuranceHandoffSource -match 'not coverage_verified' -and
        $insuranceHandoffSource -match 'not exact_network_confirmed' -and
        $insuranceHandoffSource -match 'not canonical_coverage_created' -and
        $insuranceHandoffSource -match 'not request_created' -and
        $insuranceHandoffSource -match 'not queue_enabled' -and
        $insuranceHandoffSource -match 'not care_enabled' -and
        $insuranceHandoffSource -match 'not external_call_performed' -and
        $insuranceHandoffSource -notmatch '(?i)insert\s+into\s+(patient_portal_accounts|insurance_records|telehealth_requests|telehealth_queue_entries|appointments|encounters|claims|billing|prescriptions|messages|integration_outbox)')
    Add-Check 'V0303 is recorded in the live migration ledger' ((Invoke-Scalar "select count(*) from schema_migrations where migration_id='V0303__telehealth_insurance_handoff_confirmation';") -eq '1')
    $communicationAccessMigration = Join-Path $solutionRoot 'database/migrations/V0304__telehealth_communication_access_readiness.sql'
    $communicationAccessSource = Get-Content -Raw $communicationAccessMigration
    Add-Check 'V0304 adds one provenance-bound communication/access-readiness receipt without arranging services or enabling downstream, care, or outbound consequences' (
        $communicationAccessSource -notmatch '(?im)^\s*(drop\s+table|truncate\s+|delete\s+from)' -and
        $communicationAccessSource -match 'create\s+table\s+(?:if\s+not\s+exists\s+)?telehealth_applicant_communication_access_readiness' -and
        $communicationAccessSource -match 'preferred_spoken_language' -and
        $communicationAccessSource -match 'interpreter_requested' -and
        $communicationAccessSource -match 'accessibility_support_requested' -and
        $communicationAccessSource -match 'current_location_confirmed' -and
        $communicationAccessSource -match 'callback_number_confirmed' -and
        $communicationAccessSource -match 'safe_private_communication_confirmed' -and
        $communicationAccessSource -match 'disconnection_emergency_plan_acknowledged' -and
        $communicationAccessSource -match 'enforce_telehealth_communication_access_readiness' -and
        $communicationAccessSource -match 'not interpreter_assigned' -and
        $communicationAccessSource -match 'not accessibility_accommodation_arranged' -and
        $communicationAccessSource -match 'not communication_arrangement_completed' -and
        $communicationAccessSource -match 'not support_request_created' -and
        $communicationAccessSource -match 'not technology_readiness_completed' -and
        $communicationAccessSource -match 'not patient_record_changed' -and
        $communicationAccessSource -match 'not request_created' -and
        $communicationAccessSource -match 'not queue_enabled' -and
        $communicationAccessSource -match 'not care_enabled' -and
        $communicationAccessSource -match 'not external_call_performed' -and
        $communicationAccessSource -notmatch '(?i)insert\s+into\s+(patients|patient_portal_accounts|insurance_records|telehealth_requests|telehealth_queue_entries|appointments|encounters|claims|billing|prescriptions|messages|integration_outbox)')
    Add-Check 'V0304 is recorded in the live migration ledger' ((Invoke-Scalar "select count(*) from schema_migrations where migration_id='V0304__telehealth_communication_access_readiness';") -eq '1')
    $devicePreparationMigration = Join-Path $solutionRoot 'database/migrations/V0305__telehealth_applicant_device_preparation.sql'
    $devicePreparationSource = Get-Content -Raw $devicePreparationMigration
    Add-Check 'V0305 adds one provenance-bound coarse client-reported device-preparation receipt without media, technology readiness, downstream, care, or outbound consequences' (
        $devicePreparationSource -notmatch '(?im)^\s*(drop\s+table|truncate\s+|delete\s+from)' -and
        $devicePreparationSource -match 'create\s+table\s+(?:if\s+not\s+exists\s+)?telehealth_applicant_device_preparations' -and
        $devicePreparationSource -match 'communication_access_readiness_id' -and
        $devicePreparationSource -match 'browser_supported' -and
        $devicePreparationSource -match 'camera_available' -and
        $devicePreparationSource -match 'microphone_available' -and
        $devicePreparationSource -match 'speaker_available' -and
        $devicePreparationSource -match "network_quality in \('Unknown','Good'\)" -and
        $devicePreparationSource -match 'client_reported_result_acknowledged' -and
        $devicePreparationSource -match 'no_readiness_guarantee_acknowledged' -and
        $devicePreparationSource -match 'recheck_before_consultation_acknowledged' -and
        $devicePreparationSource -match 'enforce_telehealth_applicant_device_preparation' -and
        $devicePreparationSource -match 'not technology_ready' -and
        $devicePreparationSource -match 'not waiting_room_created' -and
        $devicePreparationSource -match 'not media_session_created' -and
        $devicePreparationSource -match 'not communication_started' -and
        $devicePreparationSource -match 'not patient_record_changed' -and
        $devicePreparationSource -match 'not request_created' -and
        $devicePreparationSource -match 'not queue_entered' -and
        $devicePreparationSource -match 'not care_authorized' -and
        $devicePreparationSource -match 'not external_call_performed' -and
        $devicePreparationSource -notmatch '(?i)insert\s+into\s+(patients|patient_portal_accounts|insurance_records|telehealth_requests|telehealth_queue_entries|telehealth_video_sessions|appointments|encounters|claims|billing|prescriptions|messages|integration_outbox)')
    Add-Check 'V0305 is recorded in the live migration ledger' ((Invoke-Scalar "select count(*) from schema_migrations where migration_id='V0305__telehealth_applicant_device_preparation';") -eq '1')
    $clinicalInventoryMigration = Join-Path $solutionRoot 'database/migrations/V0306__telehealth_applicant_clinical_information_inventory.sql'
    $clinicalInventorySource = Get-Content -Raw $clinicalInventoryMigration
    Add-Check 'V0306 adds one provenance-bound coarse patient-reported clinical-information inventory without details, reconciliation, review, intake, eligibility, request, queue, prescribing, or care consequences' (
        $clinicalInventorySource -notmatch '(?im)^\s*(drop\s+table|truncate\s+|delete\s+from)' -and
        $clinicalInventorySource -match 'create\s+table\s+(?:if\s+not\s+exists\s+)?telehealth_applicant_clinical_information_inventories' -and
        $clinicalInventorySource -match 'device_preparation_id' -and
        $clinicalInventorySource -match "medications_status in \('PatientReportsNone','ItemsToReview','Unsure'\)" -and
        $clinicalInventorySource -match 'allergies_or_intolerances_status' -and
        $clinicalInventorySource -match 'other_health_history_status' -and
        $clinicalInventorySource -match "then 'DetailedCollectionRequired'" -and
        $clinicalInventorySource -match "then 'AssistedReviewRequired'" -and
        $clinicalInventorySource -match "else 'PendingClinicianReconciliation'" -and
        $clinicalInventorySource -match 'patient_reported_may_be_incomplete_acknowledged' -and
        $clinicalInventorySource -match 'no_clinical_details_captured_acknowledged' -and
        $clinicalInventorySource -match 'clinician_reconciliation_required_acknowledged' -and
        $clinicalInventorySource -match 'enforce_telehealth_applicant_clinical_information_inventory' -and
        $clinicalInventorySource -match 'not medication_list_reconciled' -and
        $clinicalInventorySource -match 'not allergy_list_reconciled' -and
        $clinicalInventorySource -match 'not health_history_reconciled' -and
        $clinicalInventorySource -match 'not clinical_intake_completed' -and
        $clinicalInventorySource -match 'not clinical_eligibility_established' -and
        $clinicalInventorySource -match 'not clinician_review_created' -and
        $clinicalInventorySource -match 'not patient_record_changed' -and
        $clinicalInventorySource -match 'not request_created' -and
        $clinicalInventorySource -match 'not queue_entered' -and
        $clinicalInventorySource -match 'not care_authorized' -and
        $clinicalInventorySource -match 'not prescribing_enabled' -and
        $clinicalInventorySource -notmatch '(?i)insert\s+into\s+(patients|medications|lists|allergies|problems|diagnoses|encounters|telehealth_requests|telehealth_queue_entries|appointments|claims|billing|prescriptions|messages|integration_outbox)')
    Add-Check 'V0306 is recorded in the live migration ledger' ((Invoke-Scalar "select count(*) from schema_migrations where migration_id='V0306__telehealth_applicant_clinical_information_inventory';") -eq '1')
    $medicationInformationMigration = Join-Path $solutionRoot 'database/migrations/V0307__telehealth_applicant_medication_information.sql'
    $medicationInformationSource = Get-Content -Raw $medicationInformationMigration
    Add-Check 'V0307 adds immutable parent-child patient-reported synthetic medication information without detailed, canonical, reconciliation, interaction, review, intake, request, queue, prescribing, or care consequences' (
        $medicationInformationSource -notmatch '(?im)^\s*(drop\s+table|truncate\s+|delete\s+from)' -and
        $medicationInformationSource -match 'create\s+table\s+(?:if\s+not\s+exists\s+)?telehealth_applicant_medication_information_receipts' -and
        $medicationInformationSource -match 'create\s+table\s+(?:if\s+not\s+exists\s+)?telehealth_applicant_reported_medication_items' -and
        $medicationInformationSource -match "coding_system='LOCAL_SYNTHETIC_ONLY'" -and
        $medicationInformationSource -match 'not rxnorm_mapped' -and
        $medicationInformationSource -match "reported_use_status in \('Taking','NotTaking','Unsure'\)" -and
        $medicationInformationSource -match 'enforce_telehealth_medication_information_item_count' -and
        $medicationInformationSource -match 'deferrable initially deferred' -and
        $medicationInformationSource -match 'not medication_statement_created' -and
        $medicationInformationSource -match 'not medication_request_created' -and
        $medicationInformationSource -match 'not medication_list_reconciled' -and
        $medicationInformationSource -match 'not interaction_check_performed' -and
        $medicationInformationSource -match 'not clinician_review_created' -and
        $medicationInformationSource -match 'not clinical_intake_completed' -and
        $medicationInformationSource -match 'not patient_record_changed' -and
        $medicationInformationSource -match 'not request_created' -and
        $medicationInformationSource -match 'not queue_entered' -and
        $medicationInformationSource -match 'not care_authorized' -and
        $medicationInformationSource -match 'not prescribing_enabled' -and
        $medicationInformationSource -notmatch '(?im)^\s+(dose|directions|route|frequency|timing|indication|prescriber|pharmacy|note|attachment|free_text|rxnorm_code|ndc_code|snomed_code)\s+')
    Add-Check 'V0307 is recorded in the live migration ledger' ((Invoke-Scalar "select count(*) from schema_migrations where migration_id='V0307__telehealth_applicant_medication_information';") -eq '1')
    $allergyInformationMigration = Join-Path $solutionRoot 'database/migrations/V0308__telehealth_applicant_allergy_information.sql'
    $allergyInformationSource = Get-Content -Raw $allergyInformationMigration
    Add-Check 'V0308 adds immutable parent-child patient-reported synthetic allergy information without reaction, status, criticality, canonical, reconciliation, review, intake, request, queue, prescribing, or care consequences' (
        $allergyInformationSource -notmatch '(?im)^\s*(drop\s+table|truncate\s+|delete\s+from)' -and
        $allergyInformationSource -match 'create\s+table\s+(?:if\s+not\s+exists\s+)?telehealth_applicant_allergy_information_receipts' -and
        $allergyInformationSource -match 'create\s+table\s+(?:if\s+not\s+exists\s+)?telehealth_applicant_reported_allergy_items' -and
        $allergyInformationSource -match "coding_system='LOCAL_SYNTHETIC_ONLY'" -and
        $allergyInformationSource -match 'not snomed_ct_mapped' -and
        $allergyInformationSource -match 'not rxnorm_mapped' -and
        $allergyInformationSource -match "\('amoxicillin','Amoxicillin','Medication'\)" -and
        $allergyInformationSource -match "\('bee-venom','Bee venom','Environment'\)" -and
        $allergyInformationSource -match 'enforce_telehealth_allergy_information_item_count' -and
        $allergyInformationSource -match 'deferrable initially deferred' -and
        $allergyInformationSource -match 'not allergy_intolerance_created' -and
        $allergyInformationSource -match 'not allergy_list_reconciled' -and
        $allergyInformationSource -match 'not reaction_assessed' -and
        $allergyInformationSource -match 'not criticality_assessed' -and
        $allergyInformationSource -match 'not contraindication_check_performed' -and
        $allergyInformationSource -match 'not clinician_review_created' -and
        $allergyInformationSource -match 'not clinical_intake_completed' -and
        $allergyInformationSource -match 'not patient_record_changed' -and
        $allergyInformationSource -match 'not request_created' -and
        $allergyInformationSource -match 'not queue_entered' -and
        $allergyInformationSource -match 'not care_authorized' -and
        $allergyInformationSource -match 'not prescribing_enabled' -and
        $allergyInformationSource -notmatch '(?im)^\s+(reaction|manifestation|allergy_type|clinical_status|verification_status|severity|criticality|onset|occurrence|note|attachment|free_text|snomed_code|rxnorm_code)\s+')
    Add-Check 'V0308 is recorded in the live migration ledger' ((Invoke-Scalar "select count(*) from schema_migrations where migration_id='V0308__telehealth_applicant_allergy_information';") -eq '1')
    $healthHistoryInformationMigration = Join-Path $solutionRoot 'database/migrations/V0309__telehealth_applicant_health_history_information.sql'
    $healthHistoryInformationSource = Get-Content -Raw $healthHistoryInformationMigration
    Add-Check 'V0309 adds immutable parent-child broad synthetic health-history topics without diagnosis, status, timing, terminology, canonical, reconciliation, review, intake, request, queue, prescribing, or care consequences' (
        $healthHistoryInformationSource -notmatch '(?im)^\s*(drop\s+table|truncate\s+|delete\s+from)' -and
        $healthHistoryInformationSource -match 'create\s+table\s+(?:if\s+not\s+exists\s+)?telehealth_applicant_health_history_information_receipts' -and
        $healthHistoryInformationSource -match 'create\s+table\s+(?:if\s+not\s+exists\s+)?telehealth_applicant_reported_health_history_topics' -and
        $healthHistoryInformationSource -match "coding_system='LOCAL_SYNTHETIC_ONLY'" -and
        $healthHistoryInformationSource -match 'not snomed_ct_mapped' -and
        $healthHistoryInformationSource -match 'not icd10_cm_mapped' -and
        $healthHistoryInformationSource -match 'not loinc_mapped' -and
        $healthHistoryInformationSource -match "\('ongoing-health-conditions','Ongoing health conditions','ConditionOrConcern'\)" -and
        $healthHistoryInformationSource -match "\('family-health-history','Family health history','FamilyHistory'\)" -and
        $healthHistoryInformationSource -match 'enforce_telehealth_health_history_information_topic_count' -and
        $healthHistoryInformationSource -match 'deferrable initially deferred' -and
        $healthHistoryInformationSource -match 'not condition_created' -and
        $healthHistoryInformationSource -match 'not procedure_created' -and
        $healthHistoryInformationSource -match 'not observation_created' -and
        $healthHistoryInformationSource -match 'not family_member_history_created' -and
        $healthHistoryInformationSource -match 'not questionnaire_response_created' -and
        $healthHistoryInformationSource -match 'not health_history_reconciled' -and
        $healthHistoryInformationSource -match 'not risk_modifier_evaluated' -and
        $healthHistoryInformationSource -match 'not clinical_triage_changed' -and
        $healthHistoryInformationSource -match 'not clinician_review_created' -and
        $healthHistoryInformationSource -match 'not clinical_intake_completed' -and
        $healthHistoryInformationSource -match 'not patient_record_changed' -and
        $healthHistoryInformationSource -match 'not request_created' -and
        $healthHistoryInformationSource -match 'not queue_entered' -and
        $healthHistoryInformationSource -match 'not care_authorized' -and
        $healthHistoryInformationSource -match 'not prescribing_enabled' -and
        $healthHistoryInformationSource -notmatch '(?im)^\s+(diagnosis|symptom|procedure_date|clinical_status|verification_status|severity|onset|occurrence|note|attachment|free_text|snomed_code|icd10_code|loinc_code)\s+')
    Add-Check 'V0309 is recorded in the live migration ledger' ((Invoke-Scalar "select count(*) from schema_migrations where migration_id='V0309__telehealth_applicant_health_history_information';") -eq '1')
    $clinicalInformationSummaryMigration = Join-Path $solutionRoot 'database/migrations/V0310__telehealth_applicant_clinical_information_summary.sql'
    $clinicalInformationSummarySource = Get-Content -Raw $clinicalInformationSummaryMigration
    Add-Check 'V0310 adds one immutable no-edit clinical-information summary confirmation without clinical detail, reconciliation, intake, eligibility, review task, request, queue, prescribing, or care consequences' (
        $clinicalInformationSummarySource -notmatch '(?im)^\s*(drop\s+table|truncate\s+|delete\s+from)' -and
        $clinicalInformationSummarySource -match 'create\s+table\s+(?:if\s+not\s+exists\s+)?telehealth_applicant_clinical_information_summary_confirmations' -and
        $clinicalInformationSummarySource -match 'health_history_information_id' -and
        $clinicalInformationSummarySource -match 'medication_item_count' -and
        $clinicalInformationSummarySource -match 'allergy_item_count' -and
        $clinicalInformationSummarySource -match 'health_history_topic_count' -and
        $clinicalInformationSummarySource -match 'AdditionalClinicalInformationCollectionRequired' -and
        $clinicalInformationSummarySource -match 'AssistedClinicalInformationReviewRequired' -and
        $clinicalInformationSummarySource -match 'ClinicianClinicalInformationReviewRequired' -and
        $clinicalInformationSummarySource -match 'PendingClinicianReconciliationOfPatientReportedNone' -and
        $clinicalInformationSummarySource -match 'not questionnaire_response_created' -and
        $clinicalInformationSummarySource -match 'not medication_list_reconciled' -and
        $clinicalInformationSummarySource -match 'not allergy_list_reconciled' -and
        $clinicalInformationSummarySource -match 'not health_history_reconciled' -and
        $clinicalInformationSummarySource -match 'not confirmed_negative_established' -and
        $clinicalInformationSummarySource -match 'not clinician_review_created' -and
        $clinicalInformationSummarySource -match 'not clinical_intake_completed' -and
        $clinicalInformationSummarySource -match 'not clinical_eligibility_established' -and
        $clinicalInformationSummarySource -match 'not clinical_triage_changed' -and
        $clinicalInformationSummarySource -match 'not patient_record_changed' -and
        $clinicalInformationSummarySource -match 'not practice_accepted' -and
        $clinicalInformationSummarySource -match 'not request_created' -and
        $clinicalInformationSummarySource -match 'not queue_entered' -and
        $clinicalInformationSummarySource -match 'not care_authorized' -and
        $clinicalInformationSummarySource -match 'not prescribing_enabled' -and
        $clinicalInformationSummarySource -notmatch '(?im)^\s+(legal_name|date_of_birth|email|phone|address|member_id|payer|diagnosis|symptom|dose|reaction|note|attachment|free_text)\s+')
    Add-Check 'V0310 is recorded in the live migration ledger' ((Invoke-Scalar "select count(*) from schema_migrations where migration_id='V0310__telehealth_applicant_clinical_information_summary';") -eq '1')
    $preRequestReadinessMigration = Join-Path $solutionRoot 'database/migrations/V0311__telehealth_applicant_pre_request_readiness.sql'
    $preRequestReadinessSource = Get-Content -Raw $preRequestReadinessMigration
    Add-Check 'V0311 adds one immutable five-section pre-request readiness acknowledgment without completion, eligibility, task, acceptance, request, queue, financial, integration, or care consequences' (
        $preRequestReadinessSource -notmatch '(?im)^\s*(drop\s+table|truncate\s+|delete\s+from)' -and
        $preRequestReadinessSource -match 'create\s+table\s+(?:if\s+not\s+exists\s+)?telehealth_applicant_pre_request_readiness_acknowledgments' -and
        $preRequestReadinessSource -match 'registration_details_confirmation_id' -and
        $preRequestReadinessSource -match 'insurance_handoff_confirmation_id' -and
        $preRequestReadinessSource -match 'communication_access_readiness_id' -and
        $preRequestReadinessSource -match 'device_preparation_id' -and
        $preRequestReadinessSource -match 'clinical_information_summary_confirmation_id' -and
        $preRequestReadinessSource -match 'AdditionalClinicalInformationRequired' -and
        $preRequestReadinessSource -match 'AssistedPreRequestSupportRequired' -and
        $preRequestReadinessSource -match 'PendingPracticePreRequestReview' -and
        $preRequestReadinessSource -match 'not identity_assurance_established' -and
        $preRequestReadinessSource -match 'not coverage_guaranteed' -and
        $preRequestReadinessSource -match 'not staff_review_created' -and
        $preRequestReadinessSource -match 'not clinician_review_created' -and
        $preRequestReadinessSource -match 'not practice_accepted' -and
        $preRequestReadinessSource -match 'not request_created' -and
        $preRequestReadinessSource -match 'not queue_entered' -and
        $preRequestReadinessSource -match 'not care_authorized' -and
        $preRequestReadinessSource -match 'not billing_enabled' -and
        $preRequestReadinessSource -match 'not claim_created' -and
        $preRequestReadinessSource -match 'not integration_enabled' -and
        $preRequestReadinessSource -match 'not external_call_performed' -and
        $preRequestReadinessSource -notmatch '(?im)^\s+(legal_name|date_of_birth|email|phone|address|language|callback|member_id|payer|diagnosis|symptom|dose|reaction|note|attachment|free_text)\s+')
    Add-Check 'V0311 is recorded in the live migration ledger' ((Invoke-Scalar "select count(*) from schema_migrations where migration_id='V0311__telehealth_applicant_pre_request_readiness';") -eq '1')
    $practiceReviewMigration = Join-Path $solutionRoot 'database/migrations/V0312__telehealth_applicant_practice_review_submission.sql'
    $practiceReviewSource = Get-Content -Raw $practiceReviewMigration
    Add-Check 'V0312 adds one immutable staff-review work item without practice acceptance, telehealth request, patient or clinician queue, appointment, encounter, financial, integration, or care consequences' (
        $practiceReviewSource -notmatch '(?im)^\s*(drop\s+table|truncate\s+|delete\s+from)' -and
        $practiceReviewSource -match 'create\s+table\s+(?:if\s+not\s+exists\s+)?telehealth_prospective_practice_review_cases' -and
        $practiceReviewSource -match 'create\s+table\s+(?:if\s+not\s+exists\s+)?telehealth_applicant_practice_review_submissions' -and
        $practiceReviewSource -match "case_status='PendingPracticeReview'" -and
        $practiceReviewSource -match 'staff_review_created' -and
        $practiceReviewSource -match 'not clinician_review_created' -and
        $practiceReviewSource -match 'not practice_accepted' -and
        $practiceReviewSource -match 'not telehealth_request_created' -and
        $practiceReviewSource -match 'not patient_care_queue_entered' -and
        $practiceReviewSource -match 'not clinician_queue_entered' -and
        $practiceReviewSource -match 'not care_authorized' -and
        $practiceReviewSource -match 'not billing_enabled' -and
        $practiceReviewSource -match 'not claim_created' -and
        $practiceReviewSource -match 'not integration_enabled' -and
        $practiceReviewSource -match 'not external_call_performed' -and
        $practiceReviewSource -notmatch '(?im)^\s+(legal_name|date_of_birth|email|phone|address|member_id|payer|diagnosis|symptom|dose|reaction|note|attachment|free_text|priority|assigned_to|queue_position|doctor_id)\s+')
    Add-Check 'V0312 is recorded in the live migration ledger' ((Invoke-Scalar "select count(*) from schema_migrations where migration_id='V0312__telehealth_applicant_practice_review_submission';") -eq '1')
    $practiceReviewClaimMigration = Join-Path $solutionRoot 'database/migrations/V0313__telehealth_practice_review_claim.sql'
    $practiceReviewClaimSource = Get-Content -Raw $practiceReviewClaimMigration
    Add-Check 'V0313 adds only immutable short staff review claims without priority, decision, request, queue, care, financial, integration, or external consequences' (
        $practiceReviewClaimSource -notmatch '(?im)^\s*(drop\s+table|truncate\s+|delete\s+from)' -and
        $practiceReviewClaimSource -match 'create\s+table\s+(?:if\s+not\s+exists\s+)?telehealth_practice_review_claims' -and
        $practiceReviewClaimSource -match "lease_expires_at=assigned_at\+interval '120 seconds'" -and
        $practiceReviewClaimSource -match 'staff_action_taken' -and
        $practiceReviewClaimSource -match 'not priority_assigned' -and
        $practiceReviewClaimSource -match 'not practice_accepted' -and
        $practiceReviewClaimSource -match 'not practice_declined' -and
        $practiceReviewClaimSource -match 'not telehealth_request_created' -and
        $practiceReviewClaimSource -match 'not patient_care_queue_entered' -and
        $practiceReviewClaimSource -match 'not clinician_queue_entered' -and
        $practiceReviewClaimSource -match 'not care_authorized' -and
        $practiceReviewClaimSource -match 'not billing_enabled' -and
        $practiceReviewClaimSource -match 'not claim_created' -and
        $practiceReviewClaimSource -match 'not integration_enabled' -and
        $practiceReviewClaimSource -match 'not external_call_performed')
    Add-Check 'V0313 is recorded in the live migration ledger' ((Invoke-Scalar "select count(*) from schema_migrations where migration_id='V0313__telehealth_practice_review_claim';") -eq '1')
    Add-Check 'Practice-review claim actor, 120-second lease, acknowledgments, policy, replay, no-consequence, provenance guard, and append-only constraints are database-enforced' (
        [int](Invoke-Scalar @"
select count(*) from pg_constraint where conname in (
'uq_telehealth_practice_review_claim_idempotency','chk_telehealth_practice_review_claim_actor',
'chk_telehealth_practice_review_claim_lease','chk_telehealth_practice_review_claim_acknowledgments',
'chk_telehealth_practice_review_claim_policy','chk_telehealth_practice_review_claim_hash',
'chk_telehealth_practice_review_claim_consequences');
"@) -eq 7 -and
        [int](Invoke-Scalar @"
select count(*) from pg_trigger where not tgisinternal and tgname in (
'trg_enforce_telehealth_practice_review_claim','trg_telehealth_practice_review_claims_append_only');
"@) -eq 2 -and
        [int](Invoke-Scalar "select count(*) from pg_proc where proname='enforce_telehealth_practice_review_claim';") -eq 1)
    $practiceReviewAuthorizationMigration = Join-Path $solutionRoot 'database/migrations/V0314__telehealth_practice_review_authorization.sql'
    $practiceReviewAuthorizationSource = Get-Content -Raw $practiceReviewAuthorizationMigration
    Add-Check 'V0314 adds one positive-only claimant-bound authorization without case/claim rewrite or request, queue, contact, care, financial, integration, or external consequences' (
        $practiceReviewAuthorizationSource -notmatch '(?im)^\s*(drop\s+table|truncate\s+|delete\s+from)' -and
        $practiceReviewAuthorizationSource -match 'create\s+table\s+(?:if\s+not\s+exists\s+)?telehealth_practice_review_authorizations' -and
        $practiceReviewAuthorizationSource -match "decision='AuthorizedForSyntheticRequestCreation'" -and
        $practiceReviewAuthorizationSource -match "rationale_code='OperationalPrerequisitesReviewed'" -and
        $practiceReviewAuthorizationSource -match 'request_creation_authorized' -and
        $practiceReviewAuthorizationSource -match 'not practice_accepted' -and
        $practiceReviewAuthorizationSource -match 'not patient_contacted' -and
        $practiceReviewAuthorizationSource -match 'not telehealth_request_created' -and
        $practiceReviewAuthorizationSource -match 'not patient_care_queue_entered' -and
        $practiceReviewAuthorizationSource -match 'not clinician_queue_entered' -and
        $practiceReviewAuthorizationSource -match 'not appointment_created' -and
        $practiceReviewAuthorizationSource -match 'not encounter_created' -and
        $practiceReviewAuthorizationSource -match 'not consent_created' -and
        $practiceReviewAuthorizationSource -match 'not care_authorized' -and
        $practiceReviewAuthorizationSource -match 'not billing_enabled' -and
        $practiceReviewAuthorizationSource -match 'not claim_created' -and
        $practiceReviewAuthorizationSource -match 'not integration_enabled' -and
        $practiceReviewAuthorizationSource -match 'not external_call_performed')
    Add-Check 'V0314 is recorded in the live migration ledger' ((Invoke-Scalar "select count(*) from schema_migrations where migration_id='V0314__telehealth_practice_review_authorization';") -eq '1')
    Add-Check 'Practice-review authorization decision, acknowledgments, packet binding, actor, replay, no-consequence, provenance guard, and append-only constraints are database-enforced' (
        [int](Invoke-Scalar @"
select count(*) from pg_constraint where conname in (
'uq_telehealth_practice_review_authorization_idempotency',
'chk_telehealth_practice_review_authorization_scope',
'chk_telehealth_practice_review_authorization_version',
'chk_telehealth_practice_review_authorization_decision',
'chk_telehealth_practice_review_authorization_packet',
'chk_telehealth_practice_review_authorization_acknowledgments',
'chk_telehealth_practice_review_authorization_policy',
'chk_telehealth_practice_review_authorization_actor',
'chk_telehealth_practice_review_authorization_idempotency',
'chk_telehealth_practice_review_authorization_fingerprint',
'chk_telehealth_practice_review_authorization_no_consequence');
"@) -eq 11 -and
        [int](Invoke-Scalar @"
select count(*) from pg_trigger where not tgisinternal and tgname in (
'trg_telehealth_practice_review_authorization_guard',
'trg_telehealth_practice_review_authorizations_append_only');
"@) -eq 2 -and
        [int](Invoke-Scalar "select count(*) from pg_proc where proname='enforce_telehealth_practice_review_authorization';") -eq 1)
    $applicantRequestCreationMigration = Join-Path $solutionRoot 'database/migrations/V0315__telehealth_applicant_request_creation.sql'
    $applicantRequestCreationSource = Get-Content -Raw $applicantRequestCreationMigration
    Add-Check 'V0315 adds one applicant-bound Draft request with immutable source provenance and no queue, care, financial, integration, or external consequence' (
        $applicantRequestCreationSource -notmatch '(?im)^\s*(drop\s+table|truncate\s+|delete\s+from)' -and
        $applicantRequestCreationSource -match 'create\s+table\s+(?:if\s+not\s+exists\s+)?telehealth_applicant_request_creations' -and
        $applicantRequestCreationSource -match "resulting_applicant_status='SyntheticRequestCreated'" -and
        $applicantRequestCreationSource -match "request_status='Draft'" -and
        $applicantRequestCreationSource -match 'source_practice_review_authorization_id' -and
        $applicantRequestCreationSource -match 'telehealth_request_created and not patient_contacted' -and
        $applicantRequestCreationSource -match 'not patient_care_queue_entered' -and
        $applicantRequestCreationSource -match 'not clinician_queue_entered' -and
        $applicantRequestCreationSource -match 'not doctor_search_started' -and
        $applicantRequestCreationSource -match 'not queue_position_assigned' -and
        $applicantRequestCreationSource -match 'not appointment_created' -and
        $applicantRequestCreationSource -match 'not encounter_created' -and
        $applicantRequestCreationSource -match 'not consent_created' -and
        $applicantRequestCreationSource -match 'not care_authorized' -and
        $applicantRequestCreationSource -match 'not billing_enabled' -and
        $applicantRequestCreationSource -match 'not claim_created' -and
        $applicantRequestCreationSource -match 'not integration_enabled' -and
        $applicantRequestCreationSource -match 'not external_call_performed')
    Add-Check 'V0315 is recorded in the live migration ledger' ((Invoke-Scalar "select count(*) from schema_migrations where migration_id='V0315__telehealth_applicant_request_creation';") -eq '1')
    Add-Check 'Applicant request version, policy, acknowledgments, no-consequence, provenance, uniqueness, and append-only constraints are database-enforced' (
        [int](Invoke-Scalar @"
select count(*) from pg_constraint where conname in (
'uq_telehealth_applicant_request_creation_idempotency',
'chk_telehealth_applicant_request_creation_scope',
'chk_telehealth_applicant_request_creation_version',
'chk_telehealth_applicant_request_creation_request',
'chk_telehealth_applicant_request_creation_acknowledgments',
'chk_telehealth_applicant_request_creation_policy',
'chk_telehealth_applicant_request_creation_idempotency',
'chk_telehealth_applicant_request_creation_fingerprint',
'chk_telehealth_applicant_request_creation_no_consequence',
'chk_telehealth_request_applicant_provenance');
"@) -eq 10 -and
        [int](Invoke-Scalar @"
select count(*) from pg_trigger where not tgisinternal and tgname in (
'trg_telehealth_applicant_request_creation_guard',
'trg_telehealth_applicant_request_creations_append_only',
'trg_telehealth_request_applicant_provenance');
"@) -eq 3 -and
        [int](Invoke-Scalar "select count(*) from pg_proc where proname in ('enforce_telehealth_applicant_request_creation','protect_telehealth_request_applicant_provenance');") -eq 2)
    $applicantRequestLocationMigration = Join-Path $solutionRoot 'database/migrations/V0316__telehealth_applicant_request_location_confirmation.sql'
    $applicantRequestLocationSource = Get-Content -Raw $applicantRequestLocationMigration
    Add-Check 'V0316 adds one append-only applicant request location and callback confirmation without triage, queue, care, financial, integration, or external consequence' (
        $applicantRequestLocationSource -notmatch '(?im)^\s*(drop\s+table|truncate\s+|delete\s+from)' -and
        $applicantRequestLocationSource -match 'create\s+table\s+(?:if\s+not\s+exists\s+)?telehealth_applicant_request_location_confirmations' -and
        $applicantRequestLocationSource -match "source_request_version=1" -and
        $applicantRequestLocationSource -match "resulting_request_version=2" -and
        $applicantRequestLocationSource -match "resulting_request_status='LocationConfirmed'" -and
        $applicantRequestLocationSource -match "current_location_state_code in \('GA','CA','FL'\)" -and
        $applicantRequestLocationSource -match 'current_location_confirmed and callback_number_confirmed' -and
        $applicantRequestLocationSource -match 'not triage_assessment_created' -and
        $applicantRequestLocationSource -match 'not patient_care_queue_entered' -and
        $applicantRequestLocationSource -match 'not clinician_queue_entered' -and
        $applicantRequestLocationSource -match 'not doctor_search_started' -and
        $applicantRequestLocationSource -match 'not appointment_created' -and
        $applicantRequestLocationSource -match 'not care_authorized' -and
        $applicantRequestLocationSource -match 'not claim_created' -and
        $applicantRequestLocationSource -match 'not integration_enabled' -and
        $applicantRequestLocationSource -match 'not external_call_performed')
    Add-Check 'V0316 is recorded in the live migration ledger' ((Invoke-Scalar "select count(*) from schema_migrations where migration_id='V0316__telehealth_applicant_request_location_confirmation';") -eq '1')
    Add-Check 'Applicant request-location scope, versions, state, acknowledgments, policy, replay, no-consequence, provenance, and append-only constraints are database-enforced' (
        [int](Invoke-Scalar @"
select count(*) from pg_constraint where conname in (
'uq_telehealth_applicant_request_location_idempotency',
'chk_telehealth_applicant_request_location_scope',
'chk_telehealth_applicant_request_location_versions',
'chk_telehealth_applicant_request_location_context',
'chk_telehealth_applicant_request_location_acknowledgments',
'chk_telehealth_applicant_request_location_policy',
'chk_telehealth_applicant_request_location_idempotency',
'chk_telehealth_applicant_request_location_fingerprints',
'chk_telehealth_applicant_request_location_no_consequence');
"@) -eq 9 -and
        [int](Invoke-Scalar @"
select count(*) from pg_trigger where not tgisinternal and tgname in (
'trg_telehealth_applicant_request_location_guard',
'trg_telehealth_applicant_request_location_append_only');
"@) -eq 2 -and
        [int](Invoke-Scalar "select count(*) from pg_proc where proname='enforce_telehealth_applicant_request_location_confirmation';") -eq 1)
    Add-Check 'The applicant request-location confirmation table exists' ([int](Invoke-Scalar "select count(*) from information_schema.tables where table_schema='public' and table_name='telehealth_applicant_request_location_confirmations';") -eq 1)
    $applicantRequestSafetyMigration = Join-Path $solutionRoot 'database/migrations/V0317__telehealth_applicant_request_universal_safety_assessment.sql'
    $applicantRequestSafetySource = Get-Content -Raw $applicantRequestSafetyMigration
    Add-Check 'V0317 adds one append-only applicant request universal-safety receipt with exact outcome mapping and no review-work-item, queue, care, financial, integration, or external consequence' (
        $applicantRequestSafetySource -notmatch '(?im)^\s*(drop\s+table|truncate\s+|delete\s+from)' -and
        $applicantRequestSafetySource -match 'create\s+table\s+(?:if\s+not\s+exists\s+)?telehealth_applicant_request_universal_safety_assessments' -and
        $applicantRequestSafetySource -match 'source_request_version=2' -and
        $applicantRequestSafetySource -match 'resulting_request_version=3' -and
        $applicantRequestSafetySource -match "resulting_request_status='SafetyScreening'" -and
        $applicantRequestSafetySource -match "resulting_request_status='EmergencyRedirected'" -and
        $applicantRequestSafetySource -match "resulting_request_status='InPersonRecommended'" -and
        $applicantRequestSafetySource -match "resulting_request_status='ClinicalReview'" -and
        $applicantRequestSafetySource -match 'complaint_specific_triage_required' -and
        $applicantRequestSafetySource -match 'not complaint_specific_triage_created' -and
        $applicantRequestSafetySource -match 'not clinical_review_created' -and
        $applicantRequestSafetySource -match 'not patient_care_queue_entered' -and
        $applicantRequestSafetySource -match 'not clinician_queue_entered' -and
        $applicantRequestSafetySource -match 'not doctor_search_started' -and
        $applicantRequestSafetySource -match 'not appointment_created' -and
        $applicantRequestSafetySource -match 'not care_authorized' -and
        $applicantRequestSafetySource -match 'not claim_created' -and
        $applicantRequestSafetySource -match 'not integration_enabled' -and
        $applicantRequestSafetySource -match 'not external_call_performed')
    Add-Check 'V0317 is recorded in the live migration ledger' ((Invoke-Scalar "select count(*) from schema_migrations where migration_id='V0317__telehealth_applicant_request_universal_safety_assessment';") -eq '1')
    Add-Check 'Applicant request universal-safety versions, context, freshness, fixture priority, outcome, policy, replay, no-consequence, provenance, and append-only constraints are database-enforced' (
        [int](Invoke-Scalar @"
select count(*) from pg_constraint where conname in (
'uq_th_app_req_safety_idempotency','chk_th_app_req_safety_scope',
'chk_th_app_req_safety_versions','chk_th_app_req_safety_result',
'chk_th_app_req_safety_context','chk_th_app_req_safety_freshness',
'chk_th_app_req_safety_protocol','chk_th_app_req_safety_priority',
'chk_th_app_req_safety_policy','chk_th_app_req_safety_hashes',
'chk_th_app_req_safety_idem','chk_th_app_req_safety_no_consequence');
"@) -eq 12 -and
        [int](Invoke-Scalar @"
select count(*) from pg_trigger where not tgisinternal and tgname in (
'trg_th_app_request_universal_safety_guard',
'trg_th_app_request_universal_safety_append');
"@) -eq 2 -and
        [int](Invoke-Scalar "select count(*) from pg_proc where proname='enforce_th_app_request_universal_safety';") -eq 1)
    Add-Check 'The applicant request universal-safety assessment table exists' ([int](Invoke-Scalar "select count(*) from information_schema.tables where table_schema='public' and table_name='telehealth_applicant_request_universal_safety_assessments';") -eq 1)
    $applicantRequestComplaintTriageMigration = Join-Path $solutionRoot 'database/migrations/V0318__telehealth_applicant_request_complaint_triage.sql'
    $applicantRequestComplaintTriageSource = Get-Content -Raw $applicantRequestComplaintTriageMigration
    Add-Check 'V0318 adds one append-only complaint-triage receipt with ordered rule evidence, Unsupported, and a false-only production publication gate' (
        $applicantRequestComplaintTriageSource -notmatch '(?im)^\s*(drop\s+table|truncate\s+|delete\s+from)' -and
        $applicantRequestComplaintTriageSource -match 'create\s+table\s+(?:if\s+not\s+exists\s+)?telehealth_applicant_request_complaint_triage_assessments' -and
        $applicantRequestComplaintTriageSource -match 'source_request_version=3' -and
        $applicantRequestComplaintTriageSource -match 'resulting_request_version=4' -and
        $applicantRequestComplaintTriageSource -match "resulting_request_status='Unsupported'" -and
        $applicantRequestComplaintTriageSource -match "resulting_request_status='Intake'" -and
        $applicantRequestComplaintTriageSource -match 'fired_rule_codes' -and
        $applicantRequestComplaintTriageSource -match "clinical_content_status='UNAPPROVED_SYNTHETIC'" -and
        $applicantRequestComplaintTriageSource -match 'not medical_director_approval_recorded' -and
        $applicantRequestComplaintTriageSource -match 'not clinical_golden_case_pack_approved' -and
        $applicantRequestComplaintTriageSource -match 'not production_publication_allowed' -and
        $applicantRequestComplaintTriageSource -match 'not clinical_review_created' -and
        $applicantRequestComplaintTriageSource -match 'not intake_snapshot_created' -and
        $applicantRequestComplaintTriageSource -match 'not patient_care_queue_entered' -and
        $applicantRequestComplaintTriageSource -match 'not clinician_queue_entered' -and
        $applicantRequestComplaintTriageSource -match 'not doctor_search_started' -and
        $applicantRequestComplaintTriageSource -match 'not appointment_created' -and
        $applicantRequestComplaintTriageSource -match 'not care_authorized' -and
        $applicantRequestComplaintTriageSource -match 'not claim_created' -and
        $applicantRequestComplaintTriageSource -match 'not integration_enabled' -and
        $applicantRequestComplaintTriageSource -match 'not external_call_performed')
    Add-Check 'V0318 is recorded in the live migration ledger' ((Invoke-Scalar "select count(*) from schema_migrations where migration_id='V0318__telehealth_applicant_request_complaint_triage';") -eq '1')
    Add-Check 'Complaint-triage versions, context, answers, ordered evidence, protocol, publication gate, outcome, policy, replay, no-consequence, provenance, and append-only constraints are database-enforced' (
        [int](Invoke-Scalar @"
select count(*) from pg_constraint where conname in (
'uq_th_app_req_complaint_triage_idempotency','chk_th_app_req_complaint_triage_scope',
'chk_th_app_req_complaint_triage_versions','chk_th_app_req_complaint_triage_category',
'chk_th_app_req_complaint_triage_context','chk_th_app_req_complaint_triage_freshness',
'chk_th_app_req_complaint_triage_answers','chk_th_app_req_complaint_triage_rule_evidence',
'chk_th_app_req_complaint_triage_protocol','chk_th_app_req_complaint_triage_publication_gate',
'chk_th_app_req_complaint_triage_result','chk_th_app_req_complaint_triage_policy',
'chk_th_app_req_complaint_triage_hashes','chk_th_app_req_complaint_triage_idem',
'chk_th_app_req_complaint_triage_no_consequence');
"@) -eq 15 -and
        [int](Invoke-Scalar @"
select count(*) from pg_trigger where not tgisinternal and tgname in (
'trg_th_app_request_complaint_triage_guard',
'trg_th_app_request_complaint_triage_append');
"@) -eq 2 -and
        [int](Invoke-Scalar "select count(*) from pg_proc where proname='enforce_th_app_request_complaint_triage';") -eq 1)
    Add-Check 'The applicant request complaint-triage assessment table exists' ([int](Invoke-Scalar "select count(*) from information_schema.tables where table_schema='public' and table_name='telehealth_applicant_request_complaint_triage_assessments';") -eq 1)
    $applicantRequestIntakeMigration = Join-Path $solutionRoot 'database/migrations/V0319__telehealth_applicant_request_intake_snapshot.sql'
    $applicantRequestIntakeSource = Get-Content -Raw $applicantRequestIntakeMigration
    Add-Check 'V0319 adds one no-free-text applicant intake receipt with exact version, eight-confirmation, publication, and no-consequence gates' (
        $applicantRequestIntakeSource -notmatch '(?im)^\s*(drop\s+table|truncate\s+|delete\s+from)' -and
        $applicantRequestIntakeSource -notmatch 'create\s+unique\s+index[^;]+telehealth_intake_snapshots\s*\(\s*request_id\s*\)' -and
        $applicantRequestIntakeSource -match 'create\s+table\s+(?:if\s+not\s+exists\s+)?telehealth_applicant_request_intake_snapshots' -and
        $applicantRequestIntakeSource -match 'source_request_version=4' -and
        $applicantRequestIntakeSource -match 'resulting_request_version=5' -and
        $applicantRequestIntakeSource -match "resulting_request_status='Verification'" -and
        $applicantRequestIntakeSource -match "complaint_summary='Synthetic migraine intake demonstration'" -and
        $applicantRequestIntakeSource -match "complaint_summary='Synthetic sleep intake demonstration'" -and
        $applicantRequestIntakeSource -match 'prior_information_reviewed and insurance_limitations_acknowledged' -and
        $applicantRequestIntakeSource -match 'pending_consent_acknowledged and pending_verification_acknowledged' -and
        $applicantRequestIntakeSource -match 'complaint_result_acknowledged and synthetic_data_confirmed' -and
        $applicantRequestIntakeSource -match "clinical_content_status='UNAPPROVED_SYNTHETIC'" -and
        $applicantRequestIntakeSource -match 'not coverage_record_created and not coverage_verified' -and
        $applicantRequestIntakeSource -match 'not operational_review_created' -and
        $applicantRequestIntakeSource -match 'not patient_care_queue_entered' -and
        $applicantRequestIntakeSource -match 'not care_authorized' -and
        $applicantRequestIntakeSource -match 'not integration_enabled' -and
        $applicantRequestIntakeSource -match 'not external_call_performed')
    Add-Check 'V0319 is recorded in the live migration ledger' ((Invoke-Scalar "select count(*) from schema_migrations where migration_id='V0319__telehealth_applicant_request_intake_snapshot';") -eq '1')
    Add-Check 'Applicant intake versions, fixed summary, duration, eight confirmations, publication, replay, no-consequence, provenance, and append-only constraints are database-enforced' (
        [int](Invoke-Scalar @"
select count(*) from pg_constraint where conname in (
'uq_th_app_req_intake_idempotency','chk_th_app_req_intake_scope',
'chk_th_app_req_intake_versions','chk_th_app_req_intake_complaint',
'chk_th_app_req_intake_duration','chk_th_app_req_intake_context',
'chk_th_app_req_intake_freshness','chk_th_app_req_intake_publication_gate',
'chk_th_app_req_intake_policy','chk_th_app_req_intake_hashes',
'chk_th_app_req_intake_idem','chk_th_app_req_intake_no_consequence');
"@) -eq 12 -and
        [int](Invoke-Scalar @"
select count(*) from pg_trigger where not tgisinternal and tgname in (
'trg_th_app_request_intake_snapshot_guard',
'trg_th_app_request_intake_snapshot_append');
"@) -eq 2 -and
        [int](Invoke-Scalar "select count(*) from pg_proc where proname='enforce_th_app_request_intake_snapshot';") -eq 1)
    Add-Check 'The applicant request intake snapshot table exists' ([int](Invoke-Scalar "select count(*) from information_schema.tables where table_schema='public' and table_name='telehealth_applicant_request_intake_snapshots';") -eq 1)
    $requestInsuranceSourceMigration = Join-Path $solutionRoot 'database/migrations/V0320__telehealth_applicant_request_insurance_source_confirmation.sql'
    $requestInsuranceSourceSource = Get-Content -Raw $requestInsuranceSourceMigration
    Add-Check 'V0320 adds one masked protected-source reference with exact same-status version, seven-confirmation, historical-only, and no-consequence gates' (
        $requestInsuranceSourceSource -notmatch '(?im)^\s*(drop\s+table|truncate\s+|delete\s+from)' -and
        $requestInsuranceSourceSource -match 'create\s+table\s+(?:if\s+not\s+exists\s+)?telehealth_applicant_request_insurance_source_confirmations' -and
        $requestInsuranceSourceSource -match 'source_request_version=5' -and
        $requestInsuranceSourceSource -match 'resulting_request_version=6' -and
        $requestInsuranceSourceSource -match "source_request_status='Verification'" -and
        $requestInsuranceSourceSource -match "resulting_request_status='Verification'" -and
        $requestInsuranceSourceSource -match 'primary_coverage_source_confirmed' -and
        $requestInsuranceSourceSource -match 'fresh_verification_requested' -and
        $requestInsuranceSourceSource -match 'protected_payload_referenced and not protected_payload_copied' -and
        $requestInsuranceSourceSource -match 'not protected_payload_decrypted and not prior_result_reused' -and
        $requestInsuranceSourceSource -match 'not eligibility_verification_created and not network_verification_created' -and
        $requestInsuranceSourceSource -match 'not rendering_physician_network_checked and not coverage_verified' -and
        $requestInsuranceSourceSource -match 'not financial_acknowledgment_created and not operational_review_created' -and
        $requestInsuranceSourceSource -match 'not patient_care_queue_entered and not clinician_queue_entered' -and
        $requestInsuranceSourceSource -match 'not care_authorized' -and
        $requestInsuranceSourceSource -match 'not integration_enabled and not external_call_performed')
    Add-Check 'V0320 is recorded in the live migration ledger' ((Invoke-Scalar "select count(*) from schema_migrations where migration_id='V0320__telehealth_applicant_request_insurance_source_confirmation';") -eq '1')
    Add-Check 'Insurance-source versions, masks, history, seven confirmations, protection, replay, no-consequence, provenance, and append-only constraints are database-enforced' (
        [int](Invoke-Scalar @"
select count(*) from pg_constraint where conname in (
'uq_th_app_req_ins_source_idempotency','chk_th_app_req_ins_source_scope',
'chk_th_app_req_ins_source_versions','chk_th_app_req_ins_source_masks',
'chk_th_app_req_ins_source_relationship','chk_th_app_req_ins_source_history',
'chk_th_app_req_ins_source_freshness','chk_th_app_req_ins_source_confirmations',
'chk_th_app_req_ins_source_protection','chk_th_app_req_ins_source_policy',
'chk_th_app_req_ins_source_hashes','chk_th_app_req_ins_source_idem',
'chk_th_app_req_ins_source_no_consequence');
"@) -eq 13 -and
        [int](Invoke-Scalar @"
select count(*) from pg_trigger where not tgisinternal and tgname in (
'trg_th_app_request_insurance_source_guard',
'trg_th_app_request_insurance_source_append');
"@) -eq 2 -and
        [int](Invoke-Scalar "select count(*) from pg_proc where proname='enforce_th_app_request_insurance_source';") -eq 1)
    Add-Check 'The applicant request insurance-source confirmation table exists' ([int](Invoke-Scalar "select count(*) from information_schema.tables where table_schema='public' and table_name='telehealth_applicant_request_insurance_source_confirmations';") -eq 1)
    $requestEligibilityMigration = Join-Path $solutionRoot 'database/migrations/V0321__telehealth_applicant_request_eligibility_verification.sql'
    $requestEligibilitySource = Get-Content -Raw $requestEligibilityMigration
    Add-Check 'V0321 adds one fresh request eligibility result with protected-memory use, exact same-status version, X12-shaped adapter, and no-consequence gates' (
        $requestEligibilitySource -notmatch '(?im)^\s*(drop\s+table|truncate\s+|delete\s+from)' -and
        $requestEligibilitySource -match 'create\s+table\s+(?:if\s+not\s+exists\s+)?telehealth_applicant_request_eligibility_verifications' -and
        $requestEligibilitySource -match 'source_request_version=6' -and
        $requestEligibilitySource -match 'resulting_request_version=7' -and
        $requestEligibilitySource -match "source_request_status='Verification'" -and
        $requestEligibilitySource -match "resulting_request_status='Verification'" -and
        $requestEligibilitySource -match "compatibility_target='ASC_X12N_270_271_005010X279A1'" -and
        $requestEligibilitySource -match 'protected_payload_decrypted_in_server_memory' -and
        $requestEligibilitySource -match 'not prior_eligibility_result_reused' -and
        $requestEligibilitySource -match 'current_eligibility_evidence_created' -and
        $requestEligibilitySource -match 'not network_verification_created and not rendering_physician_network_checked' -and
        $requestEligibilitySource -match 'not coverage_verified and not exact_network_confirmed' -and
        $requestEligibilitySource -match 'not financial_acknowledgment_created and not operational_review_created' -and
        $requestEligibilitySource -match 'not patient_care_queue_entered and not clinician_queue_entered' -and
        $requestEligibilitySource -match 'not care_authorized' -and
        $requestEligibilitySource -match 'not integration_enabled and not external_call_performed')
    Add-Check 'V0321 is recorded in the live migration ledger' ((Invoke-Scalar "select count(*) from schema_migrations where migration_id='V0321__telehealth_applicant_request_eligibility_verification';") -eq '1')
    Add-Check 'Request eligibility source, adapter, outcome, freshness, protection, replay, no-consequence, provenance, and append-only constraints are database-enforced' (
        [int](Invoke-Scalar @"
select count(*) from pg_constraint where conname in (
'uq_th_app_req_eligibility_idempotency','chk_th_app_req_eligibility_scope',
'chk_th_app_req_eligibility_versions','chk_th_app_req_eligibility_source',
'chk_th_app_req_eligibility_adapter','chk_th_app_req_eligibility_outcome_vocabulary',
'chk_th_app_req_eligibility_outcome_mapping','chk_th_app_req_eligibility_freshness',
'chk_th_app_req_eligibility_acknowledgments','chk_th_app_req_eligibility_protection',
'chk_th_app_req_eligibility_policy','chk_th_app_req_eligibility_hashes',
'chk_th_app_req_eligibility_idem','chk_th_app_req_eligibility_no_consequence');
"@) -eq 14 -and
        [int](Invoke-Scalar @"
select count(*) from pg_trigger where not tgisinternal and tgname in (
'trg_th_app_request_eligibility_guard','trg_th_app_request_eligibility_append');
"@) -eq 2 -and
        [int](Invoke-Scalar "select count(*) from pg_proc where proname='enforce_th_app_request_eligibility';") -eq 1)
    Add-Check 'The applicant request eligibility verification table exists' ([int](Invoke-Scalar "select count(*) from information_schema.tables where table_schema='public' and table_name='telehealth_applicant_request_eligibility_verifications';") -eq 1)
    $requestPracticeNetworkMigration = Join-Path $solutionRoot 'database/migrations/V0322__telehealth_applicant_request_practice_network_verification.sql'
    $requestPracticeNetworkSource = Get-Content -Raw $requestPracticeNetworkMigration
    Add-Check 'V0322 adds one fresh practice-only network result with same-status version advance and exact-network/downstream gates closed' (
        $requestPracticeNetworkSource -notmatch '(?im)^\s*(drop\s+table|truncate\s+|delete\s+from)' -and
        $requestPracticeNetworkSource -match 'create\s+table\s+(?:if\s+not\s+exists\s+)?telehealth_applicant_request_practice_network_verifications' -and
        $requestPracticeNetworkSource -match 'source_request_version=7' -and
        $requestPracticeNetworkSource -match 'resulting_request_version=8' -and
        $requestPracticeNetworkSource -match "source_request_status='Verification'" -and
        $requestPracticeNetworkSource -match "resulting_request_status='Verification'" -and
        $requestPracticeNetworkSource -match 'HL7_FHIR_R4_DAVINCI_PDEX_PLAN_NET_1_2_0' -and
        $requestPracticeNetworkSource -match 'current_eligibility_evidence_referenced' -and
        $requestPracticeNetworkSource -match 'not rendering_physician_selected' -and
        $requestPracticeNetworkSource -match 'not rendering_physician_network_checked' -and
        $requestPracticeNetworkSource -match 'not exact_network_confirmed' -and
        $requestPracticeNetworkSource -match 'not financial_acknowledgment_created and not operational_review_created' -and
        $requestPracticeNetworkSource -match 'not patient_care_queue_entered and not clinician_queue_entered' -and
        $requestPracticeNetworkSource -match 'not care_authorized' -and
        $requestPracticeNetworkSource -match 'not integration_enabled and not external_call_performed')
    Add-Check 'V0322 is recorded in the live migration ledger' ((Invoke-Scalar "select count(*) from schema_migrations where migration_id='V0322__telehealth_applicant_request_practice_network_verification';") -eq '1')
    Add-Check 'Request practice-network source, adapter, outcome, freshness, practice-only, replay, no-consequence, provenance, and append-only constraints are database-enforced' (
        [int](Invoke-Scalar @"
select count(*) from pg_constraint where conname in (
'uq_th_app_req_practice_network_idem','chk_th_app_req_practice_network_scope',
'chk_th_app_req_practice_network_versions','chk_th_app_req_practice_network_source',
'chk_th_app_req_practice_network_adapter','chk_th_app_req_practice_network_outcome_vocabulary',
'chk_th_app_req_practice_network_outcome_mapping','chk_th_app_req_practice_network_freshness',
'chk_th_app_req_practice_network_ack','chk_th_app_req_practice_network_boundary',
'chk_th_app_req_practice_network_policy','chk_th_app_req_practice_network_hashes',
'chk_th_app_req_practice_network_idem','chk_th_app_req_practice_network_no_consequence');
"@) -eq 14 -and
        [int](Invoke-Scalar @"
select count(*) from pg_trigger where not tgisinternal and tgname in (
'trg_th_app_request_practice_network_guard','trg_th_app_request_practice_network_append');
"@) -eq 2 -and
        [int](Invoke-Scalar "select count(*) from pg_proc where proname='enforce_th_app_request_practice_network';") -eq 1)
    Add-Check 'The applicant request practice-network verification table exists' ([int](Invoke-Scalar "select count(*) from information_schema.tables where table_schema='public' and table_name='telehealth_applicant_request_practice_network_verifications';") -eq 1)
    $requestRenderingCandidateMigration = Join-Path $solutionRoot 'database/migrations/V0323__telehealth_applicant_request_rendering_candidate_selection.sql'
    $requestRenderingCandidateSource = Get-Content -Raw $requestRenderingCandidateMigration
    Add-Check 'V0323 adds one state-bound candidate-only roster selection with same-status version advance and assignment/network/downstream gates closed' (
        $requestRenderingCandidateSource -notmatch '(?im)^\s*(drop\s+table|truncate\s+|delete\s+from)' -and
        $requestRenderingCandidateSource -match 'create\s+table\s+(?:if\s+not\s+exists\s+)?telehealth_applicant_request_rendering_candidate_selections' -and
        $requestRenderingCandidateSource -match 'source_request_version=8' -and
        $requestRenderingCandidateSource -match 'resulting_request_version=9' -and
        $requestRenderingCandidateSource -match "source_request_status='Verification'" -and
        $requestRenderingCandidateSource -match "resulting_request_status='Verification'" -and
        $requestRenderingCandidateSource -match "candidate_purpose='NETWORK_EVALUATION_ONLY'" -and
        $requestRenderingCandidateSource -match 'candidate_only_scope_acknowledged' -and
        $requestRenderingCandidateSource -match 'no_assignment_acknowledged' -and
        $requestRenderingCandidateSource -match 'network_check_still_required_acknowledged' -and
        $requestRenderingCandidateSource -match 'not rendering_physician_assigned' -and
        $requestRenderingCandidateSource -match 'not rendering_physician_network_checked' -and
        $requestRenderingCandidateSource -match 'not exact_network_confirmed' -and
        $requestRenderingCandidateSource -match 'not patient_care_queue_entered and not clinician_queue_entered' -and
        $requestRenderingCandidateSource -match 'not integration_enabled and not external_call_performed')
    Add-Check 'V0323 is recorded in the live migration ledger' ((Invoke-Scalar "select count(*) from schema_migrations where migration_id='V0323__telehealth_applicant_request_rendering_candidate_selection';") -eq '1')
    Add-Check 'Rendering-candidate roster, freshness, acknowledgment, candidate-only, replay, no-consequence, provenance, and append-only constraints are database-enforced' (
        [int](Invoke-Scalar @"
select count(*) from pg_constraint where conname in (
'uq_th_app_req_render_candidate_idem','chk_th_app_req_render_candidate_scope',
'chk_th_app_req_render_candidate_versions','chk_th_app_req_render_candidate_source',
'chk_th_app_req_render_candidate_roster','chk_th_app_req_render_candidate_freshness',
'chk_th_app_req_render_candidate_ack','chk_th_app_req_render_candidate_boundary',
'chk_th_app_req_render_candidate_policy','chk_th_app_req_render_candidate_hashes',
'chk_th_app_req_render_candidate_idem','chk_th_app_req_render_candidate_no_consequence');
"@) -eq 12 -and
        [int](Invoke-Scalar @"
select count(*) from pg_trigger where not tgisinternal and tgname in (
'trg_th_app_request_render_candidate_guard','trg_th_app_request_render_candidate_append');
"@) -eq 2 -and
        [int](Invoke-Scalar "select count(*) from pg_proc where proname='enforce_th_app_request_render_candidate';") -eq 1)
    Add-Check 'The applicant request rendering-candidate selection table exists' ([int](Invoke-Scalar "select count(*) from information_schema.tables where table_schema='public' and table_name='telehealth_applicant_request_rendering_candidate_selections';") -eq 1)
    $requestParticipationContextMigration = Join-Path $solutionRoot 'database/migrations/V0324__telehealth_applicant_request_participation_context.sql'
    $requestParticipationContextSource = Get-Content -Raw $requestParticipationContextMigration
    Add-Check 'V0324 adds one effective-dated prerequisite-only context with same-status version advance and verification/network/downstream gates closed' (
        $requestParticipationContextSource -notmatch '(?im)^\s*(drop\s+table|truncate\s+|delete\s+from)' -and
        $requestParticipationContextSource -match 'create\s+table\s+(?:if\s+not\s+exists\s+)?telehealth_applicant_request_participation_contexts' -and
        $requestParticipationContextSource -match 'source_request_version=9' -and
        $requestParticipationContextSource -match 'resulting_request_version=10' -and
        $requestParticipationContextSource -match "source_request_status='Verification'" -and
        $requestParticipationContextSource -match "resulting_request_status='Verification'" -and
        $requestParticipationContextSource -match "context_purpose='PARTICIPATION_EVALUATION_PREREQUISITES_ONLY'" -and
        $requestParticipationContextSource -match 'npi_not_credential_acknowledged' -and
        $requestParticipationContextSource -match 'real_authority_not_verified_acknowledged' -and
        $requestParticipationContextSource -match 'exact_participation_still_required_acknowledged' -and
        $requestParticipationContextSource -match 'not real_state_authority_verified' -and
        $requestParticipationContextSource -match 'not real_credentialing_verified' -and
        $requestParticipationContextSource -match 'not rendering_physician_network_checked' -and
        $requestParticipationContextSource -match 'not exact_network_confirmed' -and
        $requestParticipationContextSource -match 'not patient_care_queue_entered and not clinician_queue_entered' -and
        $requestParticipationContextSource -match 'not integration_enabled and not external_call_performed')
    Add-Check 'V0324 is recorded in the live migration ledger' ((Invoke-Scalar "select count(*) from schema_migrations where migration_id='V0324__telehealth_applicant_request_participation_context';") -eq '1')
    Add-Check 'Participation-context source, matrix, freshness, acknowledgment, boundary, replay, no-consequence, provenance, and append-only constraints are database-enforced' (
        [int](Invoke-Scalar @"
select count(*) from pg_constraint where conname in (
'uq_th_app_req_part_context_idem','chk_th_app_req_part_context_scope',
'chk_th_app_req_part_context_versions','chk_th_app_req_part_context_source',
'chk_th_app_req_part_context_matrix','chk_th_app_req_part_context_freshness',
'chk_th_app_req_part_context_ack','chk_th_app_req_part_context_boundary',
'chk_th_app_req_part_context_policy','chk_th_app_req_part_context_hashes',
'chk_th_app_req_part_context_idem','chk_th_app_req_part_context_no_consequence');
"@) -eq 12 -and
        [int](Invoke-Scalar @"
select count(*) from pg_trigger where not tgisinternal and tgname in (
'trg_th_app_request_part_context_guard','trg_th_app_request_part_context_append');
"@) -eq 2 -and
        [int](Invoke-Scalar "select count(*) from pg_proc where proname='enforce_th_app_request_part_context';") -eq 1)
    Add-Check 'The applicant request participation-context table exists' ([int](Invoke-Scalar "select count(*) from information_schema.tables where table_schema='public' and table_name='telehealth_applicant_request_participation_contexts';") -eq 1)
    $requestParticipationEvaluationMigration = Join-Path $solutionRoot 'database/migrations/V0325__telehealth_applicant_request_participation_evaluation.sql'
    $requestParticipationEvaluationSource = Get-Content -Raw $requestParticipationEvaluationMigration
    Add-Check 'V0325 adds one exact synthetic participation evaluation with same-status version advance and every real/downstream gate closed' (
        $requestParticipationEvaluationSource -notmatch '(?im)^\s*(drop\s+table|truncate\s+|delete\s+from)' -and
        $requestParticipationEvaluationSource -match 'create\s+table\s+(?:if\s+not\s+exists\s+)?telehealth_applicant_request_participation_evaluations' -and
        $requestParticipationEvaluationSource -match 'source_request_version=10' -and
        $requestParticipationEvaluationSource -match 'resulting_request_version=11' -and
        $requestParticipationEvaluationSource -match "source_request_status='Verification'" -and
        $requestParticipationEvaluationSource -match "resulting_request_status='Verification'" -and
        $requestParticipationEvaluationSource -match "source_mode='NON_PRODUCTION'" -and
        $requestParticipationEvaluationSource -match "compatibility_target='HL7_FHIR_R4_DAVINCI_PDEX_PLAN_NET_1_2_0'" -and
        $requestParticipationEvaluationSource -match 'synthetic_billing_entity_in_network' -and
        $requestParticipationEvaluationSource -match 'synthetic_rendering_provider_in_network' -and
        $requestParticipationEvaluationSource -match 'synthetic_new_patients_accepted' -and
        $requestParticipationEvaluationSource -match 'synthetic_exact_network_matched' -and
        $requestParticipationEvaluationSource -match 'not real_state_authority_verified' -and
        $requestParticipationEvaluationSource -match 'not real_credentialing_verified' -and
        $requestParticipationEvaluationSource -match 'not rendering_physician_network_checked' -and
        $requestParticipationEvaluationSource -match 'not exact_network_confirmed' -and
        $requestParticipationEvaluationSource -match 'not patient_care_queue_entered and not clinician_queue_entered' -and
        $requestParticipationEvaluationSource -match 'not integration_enabled and not external_call_performed')
    Add-Check 'V0325 is recorded in the live migration ledger' ((Invoke-Scalar "select count(*) from schema_migrations where migration_id='V0325__telehealth_applicant_request_participation_evaluation';") -eq '1')
    Add-Check 'Participation-evaluation source, matrix, freshness, acknowledgment, result, replay, no-consequence, provenance, and append-only constraints are database-enforced' (
        [int](Invoke-Scalar @"
select count(*) from pg_constraint where conname in (
'uq_th_app_req_part_eval_idem','chk_th_app_req_part_eval_scope',
'chk_th_app_req_part_eval_versions','chk_th_app_req_part_eval_source',
'chk_th_app_req_part_eval_matrix','chk_th_app_req_part_eval_freshness',
'chk_th_app_req_part_eval_ack','chk_th_app_req_part_eval_result',
'chk_th_app_req_part_eval_policy','chk_th_app_req_part_eval_hashes',
'chk_th_app_req_part_eval_idem','chk_th_app_req_part_eval_no_consequence');
"@) -eq 12 -and
        [int](Invoke-Scalar @"
select count(*) from pg_trigger where not tgisinternal and tgname in (
'trg_th_app_request_part_eval_guard','trg_th_app_request_part_eval_append');
"@) -eq 2 -and
        [int](Invoke-Scalar "select count(*) from pg_proc where proname='enforce_th_app_request_part_eval';") -eq 1)
    Add-Check 'The applicant request participation-evaluation table exists' ([int](Invoke-Scalar "select count(*) from information_schema.tables where table_schema='public' and table_name='telehealth_applicant_request_participation_evaluations';") -eq 1)
    $requestOperationalReviewSubmissionMigration = Join-Path $solutionRoot 'database/migrations/V0326__telehealth_applicant_request_operational_review_submission.sql'
    $requestOperationalReviewSubmissionSource = Get-Content -Raw $requestOperationalReviewSubmissionMigration
    Add-Check 'V0326 adds one append-only operational-review submission and closes every acceptance, financial, queue, care, integration, and external consequence' (
        $requestOperationalReviewSubmissionSource -notmatch '(?im)^\s*(drop\s+table|truncate\s+|delete\s+from)' -and
        $requestOperationalReviewSubmissionSource -match 'create\s+table\s+(?:if\s+not\s+exists\s+)?telehealth_applicant_request_operational_review_submissions' -and
        $requestOperationalReviewSubmissionSource -match 'source_request_version=11' -and
        $requestOperationalReviewSubmissionSource -match 'resulting_request_version=12' -and
        $requestOperationalReviewSubmissionSource -match "source_request_status='Verification'" -and
        $requestOperationalReviewSubmissionSource -match "resulting_request_status='OperationalReview'" -and
        $requestOperationalReviewSubmissionSource -match "source_mode='NON_PRODUCTION'" -and
        $requestOperationalReviewSubmissionSource -match 'synthetic_automated_checks_complete and operational_review_created' -and
        $requestOperationalReviewSubmissionSource -match 'not financial_route_created and not practice_accepted' -and
        $requestOperationalReviewSubmissionSource -match 'not patient_care_queue_entered and not clinician_queue_entered' -and
        $requestOperationalReviewSubmissionSource -match 'not care_authorized' -and
        $requestOperationalReviewSubmissionSource -match 'not claim_created and not integration_enabled and not external_call_performed')
    Add-Check 'V0326 is recorded in the live migration ledger' ((Invoke-Scalar "select count(*) from schema_migrations where migration_id='V0326__telehealth_applicant_request_operational_review_submission';") -eq '1')
    Add-Check 'Operational-review submission scope, version, source, freshness, acknowledgment, replay, no-consequence, provenance, and append-only constraints are database-enforced' (
        [int](Invoke-Scalar @"
select count(*) from pg_constraint where conname in (
'uq_th_app_req_op_review_submission_idem','chk_th_app_req_op_review_submission_scope',
'chk_th_app_req_op_review_submission_versions','chk_th_app_req_op_review_submission_source',
'chk_th_app_req_op_review_submission_freshness','chk_th_app_req_op_review_submission_ack',
'chk_th_app_req_op_review_submission_result','chk_th_app_req_op_review_submission_policy',
'chk_th_app_req_op_review_submission_hashes','chk_th_app_req_op_review_submission_idem',
'chk_th_app_req_op_review_submission_no_consequence');
"@) -eq 11 -and
        [int](Invoke-Scalar @"
select count(*) from pg_trigger where not tgisinternal and tgname in (
'trg_th_app_request_op_review_submission_guard','trg_th_app_request_op_review_submission_append');
"@) -eq 2 -and
        [int](Invoke-Scalar "select count(*) from pg_proc where proname='enforce_th_app_request_op_review_submission';") -eq 1)
    Add-Check 'The applicant request operational-review-submission table exists' ([int](Invoke-Scalar "select count(*) from information_schema.tables where table_schema='public' and table_name='telehealth_applicant_request_operational_review_submissions';") -eq 1)
    $requestQueueAuthorizationMigration = Join-Path $solutionRoot 'database/migrations/V0327__telehealth_applicant_request_queue_authorization.sql'
    $requestQueueAuthorizationSource = Get-Content -Raw $requestQueueAuthorizationMigration
    Add-Check 'V0327 adds one append-only applicant queue authorization with exact positive queue and hard-false real-care consequences' (
        $requestQueueAuthorizationSource -notmatch '(?im)^\s*(drop\s+table|truncate\s+|delete\s+from)' -and
        $requestQueueAuthorizationSource -match 'create\s+table\s+(?:if\s+not\s+exists\s+)?telehealth_applicant_request_queue_authorizations' -and
        $requestQueueAuthorizationSource -match 'source_request_version=12' -and
        $requestQueueAuthorizationSource -match 'resulting_request_version=13' -and
        $requestQueueAuthorizationSource -match "source_request_status='OperationalReview'" -and
        $requestQueueAuthorizationSource -match "resulting_request_status='Queued'" -and
        $requestQueueAuthorizationSource -match 'practice_accepted and patient_care_queue_entered and clinician_queue_entered' -and
        $requestQueueAuthorizationSource -match 'not rendering_physician_assigned' -and
        $requestQueueAuthorizationSource -match 'not coverage_verified' -and
        $requestQueueAuthorizationSource -match 'not encounter_created and not consent_created and not care_authorized' -and
        $requestQueueAuthorizationSource -match 'not integration_enabled and not external_call_performed')
    Add-Check 'V0327 is recorded in the live migration ledger' ((Invoke-Scalar "select count(*) from schema_migrations where migration_id='V0327__telehealth_applicant_request_queue_authorization';") -eq '1')
    Add-Check 'Applicant queue-authorization scope, versions, source, time, acknowledgments, actor, replay, result, no-consequence, provenance, and append-only constraints are database-enforced' (
        [int](Invoke-Scalar @"
select count(*) from pg_constraint where conname in (
'uq_th_app_req_queue_auth_idem','chk_th_app_req_queue_auth_scope',
'chk_th_app_req_queue_auth_versions','chk_th_app_req_queue_auth_source',
'chk_th_app_req_queue_auth_time','chk_th_app_req_queue_auth_ack',
'chk_th_app_req_queue_auth_result','chk_th_app_req_queue_auth_policy',
'chk_th_app_req_queue_auth_actor','chk_th_app_req_queue_auth_hashes',
'chk_th_app_req_queue_auth_idem','chk_th_app_req_queue_auth_no_consequence');
"@) -eq 12 -and
        [int](Invoke-Scalar @"
select count(*) from pg_trigger where not tgisinternal and tgname in (
'trg_th_app_request_queue_auth_guard','trg_th_app_request_queue_auth_append');
"@) -eq 2 -and
        [int](Invoke-Scalar "select count(*) from pg_proc where proname='enforce_th_app_request_queue_authorization';") -eq 1)
    Add-Check 'The applicant request queue-authorization table exists' ([int](Invoke-Scalar "select count(*) from information_schema.tables where table_schema='public' and table_name='telehealth_applicant_request_queue_authorizations';") -eq 1)
    Add-Check 'All eight foundation tables exist' ([int](Invoke-Scalar @"
select count(*) from information_schema.tables
where table_schema='public' and table_name in (
'telehealth_requests','telehealth_request_events','telehealth_patient_locations','telehealth_protocol_versions',
'telehealth_triage_assessments','telehealth_queue_entries','telehealth_clinician_shifts','telehealth_reservations');
"@) -eq 8)
    Add-Check 'All five established-patient readiness evidence tables exist' ([int](Invoke-Scalar @"
select count(*) from information_schema.tables
where table_schema='public' and table_name in (
'telehealth_patient_confirmations','telehealth_intake_snapshots','telehealth_demonstration_acknowledgments',
'telehealth_coverage_selections','telehealth_coverage_verifications');
"@) -eq 5)
    Add-Check 'All thirty prospective-applicant identity, review, safety, purpose, practice-network, protected-member-detail, eligibility, network, proofing, governance, promotion, notice, registration, insurance-handoff, communication-readiness, device-preparation, clinical-inventory, medication-information, allergy-information, health-history-information, summary-confirmation, pre-request-readiness, and practice-review tables exist' ([int](Invoke-Scalar @"
select count(*) from information_schema.tables
where table_schema='public' and table_name in (
'telehealth_prospective_applicants','telehealth_applicant_contact_challenges',
'telehealth_applicant_verification_attempts','telehealth_applicant_events',
'telehealth_applicant_identity_review_decisions','telehealth_applicant_safety_triage_evaluations',
'telehealth_applicant_visit_purposes','telehealth_applicant_practice_network_prechecks',
'telehealth_applicant_member_insurance_details','telehealth_applicant_eligibility_results',
'telehealth_applicant_practice_network_determinations','telehealth_applicant_identity_proofing_results',
'telehealth_applicant_promotion_authorization_decisions','telehealth_applicant_synthetic_promotions',
'telehealth_applicant_notice_acknowledgments','telehealth_applicant_registration_details_confirmations',
'telehealth_applicant_insurance_handoff_confirmations','telehealth_applicant_communication_access_readiness',
  'telehealth_applicant_device_preparations','telehealth_applicant_clinical_information_inventories',
  'telehealth_applicant_medication_information_receipts','telehealth_applicant_reported_medication_items',
  'telehealth_applicant_allergy_information_receipts','telehealth_applicant_reported_allergy_items',
  'telehealth_applicant_health_history_information_receipts','telehealth_applicant_reported_health_history_topics',
  'telehealth_applicant_clinical_information_summary_confirmations',
  'telehealth_applicant_pre_request_readiness_acknowledgments',
  'telehealth_prospective_practice_review_cases',
  'telehealth_applicant_practice_review_submissions');
"@) -eq 30)
    Add-Check 'All four connection-room tables exist' ([int](Invoke-Scalar @"
select count(*) from information_schema.tables
where table_schema='public' and table_name in (
'telehealth_video_sessions','telehealth_video_preflights',
'telehealth_video_participant_grants','telehealth_video_events');
"@) -eq 4)
    Add-Check 'Both consultation-start linkage tables exist' ([int](Invoke-Scalar @"
select count(*) from information_schema.tables
where table_schema='public' and table_name in (
'telehealth_consultation_contexts','telehealth_consultation_events');
"@) -eq 2)
    Add-Check 'All three synthetic pharmacy-choice evidence tables exist' ([int](Invoke-Scalar @"
select count(*) from information_schema.tables
where table_schema='public' and table_name in (
'telehealth_patient_pharmacy_preferences','telehealth_consultation_pharmacy_choice_versions',
'telehealth_consultation_pharmacy_choice_events');
"@) -eq 3)
    Add-Check 'Both synthetic safety-disposition draft evidence tables exist' ([int](Invoke-Scalar @"
select count(*) from information_schema.tables
where table_schema='public' and table_name in (
'telehealth_consultation_disposition_draft_versions','telehealth_consultation_disposition_draft_events');
"@) -eq 2)
    Add-Check 'Both synthetic prescription-preparation draft evidence tables exist' ([int](Invoke-Scalar @"
select count(*) from information_schema.tables
where table_schema='public' and table_name in (
'telehealth_consultation_prescription_draft_versions','telehealth_consultation_prescription_draft_events');
"@) -eq 2)
    Add-Check 'The synthetic signed-prescription evidence table exists' ([int](Invoke-Scalar "select count(*) from information_schema.tables where table_schema='public' and table_name='telehealth_consultation_prescription_orders';") -eq 1)
    Add-Check 'All telehealth append-only evidence triggers exist' ([int](Invoke-Scalar "select count(*) from pg_trigger where not tgisinternal and tgname like 'trg_telehealth_%_append_only';") -eq 54)
    Add-Check 'Pre-request readiness route, acknowledgment, no-consequence, provenance, replay, and append-only constraints are database-enforced' (
        [int](Invoke-Scalar @"
select count(*) from pg_constraint where conname in (
'chk_telehealth_pre_request_readiness_overall_route',
'chk_telehealth_pre_request_readiness_acknowledgments',
'chk_telehealth_pre_request_readiness_no_consequence',
'chk_telehealth_pre_request_readiness_policy',
'uq_telehealth_pre_request_readiness_idempotency');
"@) -eq 5 -and
        [int](Invoke-Scalar @"
select count(*) from pg_trigger where not tgisinternal and tgname in (
'trg_telehealth_pre_request_readiness_guard',
'trg_telehealth_pre_request_readiness_append_only');
"@) -eq 2 -and
        [int](Invoke-Scalar "select count(*) from pg_proc where proname='enforce_telehealth_applicant_pre_request_readiness';") -eq 1)
    Add-Check 'Practice-review route, acknowledgment, one-staff-work-item, no-care-consequence, provenance, replay, and append-only constraints are database-enforced' (
        [int](Invoke-Scalar @"
select count(*) from pg_constraint where conname in (
'chk_telehealth_practice_review_case_route','chk_telehealth_practice_review_case_status',
'chk_telehealth_practice_review_submission_acknowledgments',
'chk_telehealth_practice_review_submission_consequences',
'chk_telehealth_practice_review_submission_policy',
'uq_telehealth_practice_review_submission_idempotency');
"@) -eq 6 -and
        [int](Invoke-Scalar @"
select count(*) from pg_trigger where not tgisinternal and tgname in (
'trg_enforce_telehealth_applicant_practice_review_submission',
'trg_telehealth_practice_review_cases_append_only',
'trg_telehealth_practice_review_submissions_append_only');
"@) -eq 3 -and
        [int](Invoke-Scalar "select count(*) from pg_proc where proname='enforce_telehealth_applicant_practice_review_submission';") -eq 1)
    Add-Check 'Applicant aggregate deletion and evidence mutation are database-blocked' ([int](Invoke-Scalar @"
select count(*) from pg_trigger where not tgisinternal and tgname in (
'trg_telehealth_applicants_no_delete','trg_telehealth_applicant_challenges_append_only',
'trg_telehealth_applicant_attempts_append_only','trg_telehealth_applicant_events_append_only');
"@) -eq 4)
    Add-Check 'Applicant ownership, hash, state, attempt, and idempotency constraints are database-enforced' ([int](Invoke-Scalar @"
select count(*) from pg_constraint where conname in (
'chk_telehealth_applicant_hashes','chk_telehealth_applicant_review_state',
'uq_telehealth_applicant_create_idempotency','uq_telehealth_applicant_attempt_idempotency',
'uq_telehealth_applicant_attempt_ordinal');
"@) -eq 5)
    Add-Check 'Applicant identity-review outcome, provenance, actor, no-promotion, snapshot, and append-only constraints are database-enforced' (
        [int](Invoke-Scalar @"
select count(*) from pg_constraint where conname in (
'chk_telehealth_applicant_identity_decision_outcome',
'chk_telehealth_applicant_identity_decision_policy',
'chk_telehealth_applicant_identity_decision_actor_role',
'chk_telehealth_applicant_identity_decision_no_promotion',
'uq_telehealth_applicant_identity_decision_idempotency');
"@) -eq 5 -and
        [int](Invoke-Scalar @"
select count(*) from pg_trigger where not tgisinternal and tgname in (
'trg_telehealth_applicant_identity_decision_guard',
'trg_telehealth_applicant_identity_decisions_append_only');
"@) -eq 2 -and
        [int](Invoke-Scalar "select count(*) from pg_proc where proname='enforce_telehealth_applicant_identity_review_decision';") -eq 1)
    Add-Check 'Prospective safety priority, protocol, location, replay, no-consequence, snapshot, and append-only constraints are database-enforced' (
        [int](Invoke-Scalar @"
select count(*) from pg_constraint where conname in (
'chk_telehealth_applicant_safety_triage_status_outcome',
'chk_telehealth_applicant_safety_triage_location',
'chk_telehealth_applicant_safety_triage_protocol',
'chk_telehealth_applicant_safety_triage_priority',
'chk_telehealth_applicant_safety_triage_no_consequence',
'uq_telehealth_applicant_safety_triage_idempotency');
"@) -eq 6 -and
        [int](Invoke-Scalar @"
select count(*) from pg_trigger where not tgisinternal and tgname in (
'trg_telehealth_applicant_safety_triage_guard',
'trg_telehealth_applicant_safety_triage_append_only');
"@) -eq 2 -and
        [int](Invoke-Scalar "select count(*) from pg_proc where proname='enforce_telehealth_applicant_safety_triage_evaluation';") -eq 1)
    Add-Check 'Prospective visit-purpose vocabulary, source snapshot, replay, no-consequence, guard, and append-only constraints are database-enforced' (
        [int](Invoke-Scalar @"
select count(*) from pg_constraint where conname in (
'chk_telehealth_applicant_visit_purpose_category',
'chk_telehealth_applicant_visit_purpose_source',
'chk_telehealth_applicant_visit_purpose_no_consequence',
'uq_telehealth_applicant_visit_purpose_idempotency');
"@) -eq 4 -and
        [int](Invoke-Scalar @"
select count(*) from pg_trigger where not tgisinternal and tgname in (
'trg_telehealth_applicant_visit_purpose_guard',
'trg_telehealth_applicant_visit_purpose_append_only');
"@) -eq 2 -and
        [int](Invoke-Scalar "select count(*) from pg_proc where proname='enforce_telehealth_applicant_visit_purpose';") -eq 1)
    Add-Check 'Prospective practice-network plan mapping, catalog window, replay, no-consequence, guard, and append-only constraints are database-enforced' (
        [int](Invoke-Scalar @"
select count(*) from pg_constraint where conname in (
'chk_telehealth_applicant_network_precheck_plan_status',
'chk_telehealth_applicant_network_precheck_catalog',
'chk_telehealth_applicant_network_precheck_location',
'chk_telehealth_applicant_network_precheck_purpose',
'chk_telehealth_applicant_network_precheck_no_consequence',
'uq_telehealth_applicant_network_precheck_idempotency');
"@) -eq 6 -and
        [int](Invoke-Scalar @"
select count(*) from pg_trigger where not tgisinternal and tgname in (
'trg_telehealth_applicant_network_precheck_guard',
'trg_telehealth_applicant_network_precheck_append_only');
"@) -eq 2 -and
        [int](Invoke-Scalar "select count(*) from pg_proc where proname='enforce_telehealth_applicant_practice_network_precheck';") -eq 1)
    Add-Check 'Prospective member-detail plan binding, masks, protection, replay, no-consequence, guard, and append-only constraints are database-enforced' (
        [int](Invoke-Scalar @"
select count(*) from pg_constraint where conname in (
'chk_telehealth_applicant_member_details_plan_status',
'chk_telehealth_applicant_member_details_relationship',
'chk_telehealth_applicant_member_details_priority',
'chk_telehealth_applicant_member_details_masks',
'chk_telehealth_applicant_member_details_confirmations',
'chk_telehealth_applicant_member_details_protection',
'chk_telehealth_applicant_member_details_no_consequence',
'uq_telehealth_applicant_member_details_idempotency');
"@) -eq 8 -and
        [int](Invoke-Scalar @"
select count(*) from pg_trigger where not tgisinternal and tgname in (
'trg_telehealth_applicant_member_details_guard',
'trg_telehealth_applicant_member_details_append_only');
"@) -eq 2 -and
        [int](Invoke-Scalar "select count(*) from pg_proc where proname='enforce_telehealth_applicant_member_insurance_details';") -eq 1)
    Add-Check 'Prospective eligibility mapping, compatibility, freshness, replay, no-consequence, provenance guard, and append-only constraints are database-enforced' (
        [int](Invoke-Scalar @"
select count(*) from pg_constraint where conname in (
'chk_telehealth_applicant_eligibility_plan_status',
'chk_telehealth_applicant_eligibility_masks',
'chk_telehealth_applicant_eligibility_subscriber',
'chk_telehealth_applicant_eligibility_inquiry',
'chk_telehealth_applicant_eligibility_outcome_vocabulary',
'chk_telehealth_applicant_eligibility_outcome_mapping',
'chk_telehealth_applicant_eligibility_freshness',
'chk_telehealth_applicant_eligibility_no_consequence',
'uq_telehealth_applicant_eligibility_idempotency');
"@) -eq 9 -and
        [int](Invoke-Scalar @"
select count(*) from pg_trigger where not tgisinternal and tgname in (
'trg_telehealth_applicant_eligibility_result_guard',
'trg_telehealth_applicant_eligibility_result_append_only');
"@) -eq 2 -and
        [int](Invoke-Scalar "select count(*) from pg_proc where proname='enforce_telehealth_applicant_eligibility_result';") -eq 1)
    Add-Check 'Prospective practice-network mapping, Plan-Net compatibility metadata, freshness, replay, no-consequence, provenance guard, and append-only constraints are database-enforced' (
        [int](Invoke-Scalar @"
select count(*) from pg_constraint where conname in (
'chk_telehealth_applicant_practice_network_practice',
'chk_telehealth_applicant_practice_network_plan_status',
'chk_telehealth_applicant_practice_network_eligibility',
'chk_telehealth_applicant_practice_network_adapter',
'chk_telehealth_applicant_practice_network_vocabulary',
'chk_telehealth_applicant_practice_network_mapping',
'chk_telehealth_applicant_practice_network_freshness',
'chk_telehealth_applicant_practice_network_no_consequence',
'uq_telehealth_applicant_practice_network_idempotency');
"@) -eq 9 -and
        [int](Invoke-Scalar @"
select count(*) from pg_trigger where not tgisinternal and tgname in (
'trg_telehealth_applicant_practice_network_determination_guard',
'trg_telehealth_applicant_practice_network_result_append_only');
"@) -eq 2 -and
        [int](Invoke-Scalar "select count(*) from pg_proc where proname='enforce_telehealth_applicant_practice_network_determination';") -eq 1)
    Add-Check 'Prospective identity-proofing scope, NIST-concepts-only metadata, normalized outcomes, freshness, replay, no-consequence, provenance guard, and append-only constraints are database-enforced' (
        [int](Invoke-Scalar @"
select count(*) from pg_constraint where conname in (
'uq_telehealth_applicant_identity_proofing_idempotency',
'chk_telehealth_applicant_identity_proofing_practice',
'chk_telehealth_applicant_identity_proofing_version',
'chk_telehealth_applicant_identity_proofing_status',
'chk_telehealth_applicant_identity_proofing_scope',
'chk_telehealth_applicant_identity_proofing_notice',
'chk_telehealth_applicant_identity_proofing_adapter',
'chk_telehealth_applicant_identity_proofing_outcome',
'chk_telehealth_applicant_identity_proofing_references',
'chk_telehealth_applicant_identity_proofing_freshness',
'chk_telehealth_applicant_identity_proofing_idempotency',
'chk_telehealth_applicant_identity_proofing_fingerprint',
'chk_telehealth_applicant_identity_proofing_no_consequence');
"@) -eq 13 -and
        [int](Invoke-Scalar @"
select count(*) from pg_trigger where not tgisinternal and tgname in (
'trg_telehealth_applicant_identity_proofing_guard',
'trg_telehealth_identity_proofing_append_only');
"@) -eq 2 -and
        [int](Invoke-Scalar "select count(*) from pg_proc where proname='enforce_telehealth_applicant_identity_proofing_result';") -eq 1)
    Add-Check 'Prospective promotion authorization scope, acknowledgments, policy, evidence, replay, actor, no-consequence, provenance guard, and append-only constraints are database-enforced' (
        [int](Invoke-Scalar @"
select count(*) from pg_constraint where conname in (
'uq_telehealth_applicant_promotion_authorization_idempotency',
'chk_telehealth_applicant_promotion_authorization_practice',
'chk_telehealth_applicant_promotion_authorization_version',
'chk_telehealth_applicant_promotion_authorization_scope',
'chk_telehealth_applicant_promotion_authorization_outcome',
'chk_telehealth_applicant_promotion_authorization_evidence',
'chk_telehealth_applicant_promotion_authorization_reason',
'chk_telehealth_applicant_promotion_authorization_acknowledgments',
'chk_telehealth_applicant_promotion_authorization_policy',
'chk_telehealth_applicant_promotion_authorization_actor',
'chk_telehealth_applicant_promotion_authorization_actor_role',
'chk_telehealth_applicant_promotion_authorization_idempotency',
'chk_telehealth_applicant_promotion_authorization_fingerprint',
'chk_telehealth_applicant_promotion_authorization_no_consequence');
"@) -eq 14 -and
        [int](Invoke-Scalar @"
select count(*) from pg_trigger where not tgisinternal and tgname in (
'trg_telehealth_applicant_promotion_authorization_guard',
'trg_telehealth_applicant_promotion_authorizations_append_only');
"@) -eq 2 -and
        [int](Invoke-Scalar "select count(*) from pg_proc where proname='enforce_telehealth_applicant_promotion_authorization';") -eq 1)
    Add-Check 'Atomic synthetic promotion outcome, acknowledgments, policy, actor, replay, no-downstream, provenance guard, and append-only constraints are database-enforced' (
        [int](Invoke-Scalar @"
select count(*) from pg_constraint where conname in (
'uq_telehealth_applicant_synthetic_promotion_idempotency',
'chk_telehealth_applicant_synthetic_promotion_scope',
'chk_telehealth_applicant_synthetic_promotion_version',
'chk_telehealth_applicant_synthetic_promotion_command',
'chk_telehealth_applicant_synthetic_promotion_outcome',
'chk_telehealth_applicant_synthetic_promotion_acknowledgments',
'chk_telehealth_applicant_synthetic_promotion_reason',
'chk_telehealth_applicant_synthetic_promotion_policy',
'chk_telehealth_applicant_synthetic_promotion_actor',
'chk_telehealth_applicant_synthetic_promotion_idempotency',
'chk_telehealth_applicant_synthetic_promotion_fingerprint',
'chk_telehealth_applicant_synthetic_promotion_no_downstream');
"@) -eq 12 -and
        [int](Invoke-Scalar @"
select count(*) from pg_trigger where not tgisinternal and tgname in (
'trg_telehealth_applicant_synthetic_promotion_guard',
'trg_telehealth_applicant_synthetic_promotions_append_only');
"@) -eq 2 -and
        [int](Invoke-Scalar "select count(*) from pg_proc where proname='enforce_telehealth_applicant_synthetic_promotion';") -eq 1)
    Add-Check 'State-notice mapping, affirmations, pending legal review, replay, no-consequence, provenance guard, and append-only constraints are database-enforced' (
        [int](Invoke-Scalar @"
select count(*) from pg_constraint where conname in (
'uq_telehealth_applicant_notice_acknowledgment_idempotency',
'chk_telehealth_applicant_notice_acknowledgment_scope',
'chk_telehealth_applicant_notice_acknowledgment_result',
'chk_telehealth_applicant_notice_acknowledgment_state_notice',
'chk_telehealth_applicant_notice_acknowledgment_version',
'chk_telehealth_applicant_notice_acknowledgment_affirmations',
'chk_telehealth_applicant_notice_acknowledgment_policy',
'chk_telehealth_applicant_notice_acknowledgment_freshness',
'chk_telehealth_applicant_notice_acknowledgment_idempotency',
'chk_telehealth_applicant_notice_acknowledgment_fingerprint',
'chk_telehealth_applicant_notice_acknowledgment_no_consequence');
"@) -eq 11 -and
        [int](Invoke-Scalar @"
select count(*) from pg_trigger where not tgisinternal and tgname in (
'trg_telehealth_applicant_notice_acknowledgment_guard',
'trg_telehealth_applicant_notice_acknowledgments_append_only');
"@) -eq 2 -and
        [int](Invoke-Scalar "select count(*) from pg_proc where proname='enforce_telehealth_applicant_notice_acknowledgment';") -eq 1)
    Add-Check 'Minimum registration-details affirmations, snapshot, policy, expiry, no-consequence, provenance guard, and append-only constraints are database-enforced' (
        [int](Invoke-Scalar @"
select count(*) from pg_constraint where conname in (
'uq_telehealth_registration_details_practice_key',
'chk_telehealth_registration_details_result',
'chk_telehealth_registration_details_fingerprints',
'chk_telehealth_registration_details_affirmations',
'chk_telehealth_registration_details_policy',
'chk_telehealth_registration_details_expiry',
'chk_telehealth_registration_details_no_consequence');
"@) -eq 7 -and
        [int](Invoke-Scalar @"
select count(*) from pg_trigger where not tgisinternal and tgname in (
'trg_telehealth_registration_details_confirmation_guard',
'trg_telehealth_registration_details_confirmations_append_only');
"@) -eq 2 -and
        [int](Invoke-Scalar "select count(*) from pg_proc where proname='enforce_telehealth_registration_details_confirmation';") -eq 1)
    Add-Check 'Synthetic insurance-handoff masks, affirmations, evidence, policy, expiry, no-consequence, provenance guard, and append-only constraints are database-enforced' (
        [int](Invoke-Scalar @"
select count(*) from pg_constraint where conname in (
'chk_telehealth_insurance_handoff_result',
'chk_telehealth_insurance_handoff_fingerprints',
'chk_telehealth_insurance_handoff_masks',
'chk_telehealth_insurance_handoff_relationship',
'chk_telehealth_insurance_handoff_evidence',
'chk_telehealth_insurance_handoff_affirmations',
'chk_telehealth_insurance_handoff_policy',
'chk_telehealth_insurance_handoff_expiry',
'chk_telehealth_insurance_handoff_no_consequence');
"@) -eq 9 -and
        [int](Invoke-Scalar @"
select count(*) from pg_trigger where not tgisinternal and tgname in (
'trg_telehealth_insurance_handoff_confirmation_guard',
'trg_telehealth_insurance_handoff_confirmations_append_only');
"@) -eq 2 -and
        [int](Invoke-Scalar "select count(*) from pg_proc where proname='enforce_telehealth_insurance_handoff_confirmation';") -eq 1)
    Add-Check 'Coverage ownership and NON_PRODUCTION constraints are database-enforced' ([int](Invoke-Scalar @"
select count(*) from pg_constraint where conname in (
'fk_telehealth_coverage_selection_insurance_patient','chk_telehealth_verification_adapter',
'chk_telehealth_verification_eligibility','chk_telehealth_verification_network');
"@) -eq 4)
    Add-Check 'Active reservation uniqueness is database-enforced' ([int](Invoke-Scalar "select count(*) from pg_indexes where schemaname='public' and indexname in ('uq_telehealth_active_reservation_request','uq_telehealth_active_reservation_clinician');") -eq 2)
    Add-Check 'Connection-room isolation, no-capture, role, expiry, and idempotency constraints are database-enforced' ([int](Invoke-Scalar @"
select count(*) from pg_constraint where conname in (
'fk_telehealth_video_session_reservation_request','chk_telehealth_video_session_adapter',
'chk_telehealth_video_session_no_capture','chk_telehealth_video_preflight_passed',
'chk_telehealth_video_grant_role','chk_telehealth_video_grant_expiry',
'uq_telehealth_video_grant_idempotency');
"@) -eq 7)
    Add-Check 'Connection-room evidence and aggregates reject destructive mutation' ([int](Invoke-Scalar @"
select count(*) from pg_trigger where not tgisinternal and tgname in (
'trg_telehealth_video_preflights_append_only','trg_telehealth_video_events_append_only',
'trg_telehealth_video_sessions_no_delete','trg_telehealth_video_grants_no_delete');
"@) -eq 4)
    Add-Check 'Consultation start is request-scoped one-to-one, affirmative-only, opaque, and append-only at the database boundary' (
        [int](Invoke-Scalar @"
select count(*) from pg_constraint where conname in (
'fk_telehealth_request_appointment','fk_telehealth_consultation_reservation_request',
'chk_telehealth_consultation_start_gate','chk_telehealth_consultation_modality',
'chk_telehealth_consultation_state','uq_telehealth_consultation_event_idempotency');
"@) -eq 6 -and
        [int](Invoke-Scalar @"
select count(*) from pg_trigger where not tgisinternal and tgname in (
'trg_telehealth_consultation_events_append_only','trg_telehealth_consultation_contexts_append_only');
"@) -eq 2)
    Add-Check 'A clinician shift can own sequential consultations without weakening patient-scoped uniqueness' (
        [int](Invoke-Scalar @"
select count(*) from pg_constraint where conrelid='telehealth_consultation_contexts'::regclass
and contype='u' and conname in (
'telehealth_consultation_contexts_request_id_key','telehealth_consultation_contexts_reservation_id_key',
'telehealth_consultation_contexts_session_id_key','telehealth_consultation_contexts_appointment_id_key',
'telehealth_consultation_contexts_encounter_id_key');
"@) -eq 5 -and
        [int](Invoke-Scalar "select count(*) from pg_constraint where conrelid='telehealth_consultation_contexts'::regclass and contype='u' and conname='telehealth_consultation_contexts_shift_id_key';") -eq 0 -and
        [int](Invoke-Scalar "select count(*) from pg_indexes where schemaname='public' and indexname='ix_telehealth_consultation_contexts_shift';") -eq 1)
    Add-Check 'Wrap-up timing and the single governed Started-to-MediaEnded context mutation are database-enforced' (
        [int](Invoke-Scalar "select count(*) from information_schema.columns where table_schema='public' and table_name='telehealth_consultation_contexts' and column_name='media_ended_at' and data_type='timestamp with time zone';") -eq 1 -and
        [int](Invoke-Scalar "select count(*) from pg_constraint where conrelid='telehealth_consultation_contexts'::regclass and conname in ('chk_telehealth_consultation_status','chk_telehealth_consultation_media_end');") -eq 2 -and
        [int](Invoke-Scalar "select count(*) from pg_proc where proname='govern_telehealth_consultation_context_mutation';") -eq 1 -and
        [int](Invoke-Scalar "select count(*) from pg_trigger where not tgisinternal and tgname='trg_telehealth_consultation_contexts_append_only';") -eq 1)
    $activeBusyIndex = Invoke-Scalar "select indexdef from pg_indexes where schemaname='public' and indexname='uq_telehealth_active_shift_clinician';"
    Add-Check 'A clinician can own at most one active, busy, or wrap-up telehealth shift' (
        $activeBusyIndex -match "status" -and $activeBusyIndex -match "Active" -and $activeBusyIndex -match "Busy" -and $activeBusyIndex -match "WrapUp")
    Add-Check 'Pharmacy choice versions, replay keys, patient confirmation, source snapshots, and synthetic-only routing are database-enforced' (
        [int](Invoke-Scalar @"
select count(*) from pg_constraint where conname in (
'uq_telehealth_pharmacy_choice_version','uq_telehealth_pharmacy_choice_event_version',
'uq_telehealth_pharmacy_choice_event_idempotency','chk_telehealth_pharmacy_choice_confirmed',
'chk_telehealth_pharmacy_choice_routing','chk_telehealth_pharmacy_choice_snapshot');
"@) -eq 6 -and
        [int](Invoke-Scalar @"
select count(*) from pg_trigger where not tgisinternal and tgname in (
'trg_telehealth_patient_pharmacy_preferences_append_only',
'trg_telehealth_pharmacy_choice_versions_append_only',
'trg_telehealth_consultation_pharmacy_choice_events_append_only');
"@) -eq 3)
    Add-Check 'Safety-disposition versions, replay identity, conditional safety facts, and append-only evidence are database-enforced' (
        [int](Invoke-Scalar @"
select count(*) from pg_constraint where conname in (
'uq_telehealth_disposition_draft_version','uq_telehealth_disposition_event_version',
'uq_telehealth_disposition_event_idempotency','chk_telehealth_disposition_code',
'chk_telehealth_disposition_evaluation','chk_telehealth_disposition_communication_state',
'chk_telehealth_disposition_location','chk_telehealth_disposition_emergency',
'chk_telehealth_disposition_interrupted','chk_telehealth_disposition_legal_effect');
"@) -eq 10 -and
        [int](Invoke-Scalar @"
select count(*) from pg_trigger where not tgisinternal and tgname in (
'trg_telehealth_disposition_versions_append_only','trg_telehealth_disposition_events_append_only');
"@) -eq 2)
    Add-Check 'Prescription-preparation versions, catalog/pharmacy binding, review gates, nonlegal state, replay identity, and append-only evidence are database-enforced' (
        [int](Invoke-Scalar @"
select count(*) from pg_constraint where conname in (
'uq_telehealth_prescription_draft_version','fk_telehealth_prescription_draft_pharmacy_choice',
'chk_telehealth_prescription_draft_catalog_snapshot','chk_telehealth_prescription_draft_directions',
'chk_telehealth_prescription_draft_reviews','chk_telehealth_prescription_draft_standard',
'chk_telehealth_prescription_draft_nonlegal','uq_telehealth_prescription_draft_event_version',
'uq_telehealth_prescription_draft_event_idempotency','chk_telehealth_prescription_draft_event_fingerprint');
"@) -eq 10 -and
        [int](Invoke-Scalar @"
select count(*) from pg_trigger where not tgisinternal and tgname in (
'trg_telehealth_prescription_draft_catalog','trg_telehealth_prescription_draft_versions_append_only',
'trg_telehealth_prescription_draft_events_append_only');
"@) -eq 3 -and
        [int](Invoke-Scalar "select count(*) from pg_proc where proname='enforce_telehealth_prescription_draft_catalog';") -eq 1)
    $prescriptionSigningMigration = Join-Path $solutionRoot 'database/migrations/V0328__telehealth_synthetic_prescription_signing.sql'
    $prescriptionSigningSource = Get-Content -Raw $prescriptionSigningMigration
    Add-Check 'V0328 adds one safety-gated immutable prepared-only prescription seam without destructive or outbound SQL' (
        $prescriptionSigningSource -notmatch '(?im)^\s*(drop\s+table|truncate\s+|delete\s+from)' -and
        $prescriptionSigningSource -match 'SYNTHETIC_ZERO_LIST_GATE_PASSED' -and
        $prescriptionSigningSource -match "target_standard='NCPDP_SCRIPT_2023011'" -and
        $prescriptionSigningSource -match "transmission_state='PreparedOnly'" -and
        $prescriptionSigningSource -match 'not certified' -and
        $prescriptionSigningSource -match 'not external_destination_contacted' -and
        $prescriptionSigningSource -match 'not legal_effect')
    Add-Check 'V0328 signed prescription constraints, append-only evidence, and canonical-row mutation rejection are database-enforced' (
        [int](Invoke-Scalar "select count(*) from pg_constraint where conname in ('uq_telehealth_prescription_order_idempotency','fk_telehealth_prescription_order_draft_version','fk_telehealth_prescription_order_pharmacy_choice','chk_telehealth_prescription_order_snapshots','chk_telehealth_prescription_order_safety','chk_telehealth_prescription_order_integrity','chk_telehealth_prescription_order_stub');") -eq 7 -and
        [int](Invoke-Scalar "select count(*) from pg_trigger where not tgisinternal and tgname in ('trg_telehealth_prescription_orders_append_only','trg_prescriptions_reject_signed_telehealth_mutation');") -eq 2 -and
        [int](Invoke-Scalar "select count(*) from pg_proc where proname='reject_signed_telehealth_prescription_mutation';") -eq 1)
    Add-Check 'V0328 is recorded in the live migration ledger' ((Invoke-Scalar "select count(*) from schema_migrations where migration_id='V0328__telehealth_synthetic_prescription_signing';") -eq '1')
    $finalClinicalReviewMigration = Join-Path $solutionRoot 'database/migrations/V0329__telehealth_synthetic_final_clinical_review.sql'
    $finalClinicalReviewSource = Get-Content -Raw $finalClinicalReviewMigration
    Add-Check 'V0329 adds immutable final clinical-review evidence without signature, completion, billing, claim, or outbound SQL' (
        $finalClinicalReviewSource -notmatch '(?im)^\s*(drop\s+table|truncate\s+|delete\s+from)' -and
        $finalClinicalReviewSource -match 'telehealth_consultation_final_clinical_review_versions' -and
        $finalClinicalReviewSource -match 'documentation_reviewed and physician_responsibility_confirmed' -and
        $finalClinicalReviewSource -match 'not legal_effect and not encounter_signature_created' -and
        $finalClinicalReviewSource -match 'not billing_created and not claim_created' -and
        $finalClinicalReviewSource -match 'not external_destination_contacted')
    Add-Check 'V0329 final clinical-review source snapshots, idempotency, and append-only evidence are database-enforced' (
        [int](Invoke-Scalar "select count(*) from pg_constraint where conname in ('uq_telehealth_final_clinical_review_version','chk_telehealth_final_clinical_review_version','chk_telehealth_final_clinical_review_attestations','chk_telehealth_final_clinical_review_hash','chk_telehealth_final_clinical_review_no_effect','uq_telehealth_final_clinical_review_event_version','uq_telehealth_final_clinical_review_event_idempotency','chk_telehealth_final_clinical_review_event');") -eq 8 -and
        [int](Invoke-Scalar "select count(*) from pg_trigger where not tgisinternal and tgname in ('trg_telehealth_final_clinical_review_versions_append_only','trg_telehealth_final_clinical_review_events_append_only');") -eq 2)
    Add-Check 'V0329 is recorded in the live migration ledger' ((Invoke-Scalar "select count(*) from schema_migrations where migration_id='V0329__telehealth_synthetic_final_clinical_review';") -eq '1')
}
catch {
    Add-Check 'Telehealth migration resilience execution' $false $_.Exception.Message
}
finally {
    $result = [ordered]@{ status=$(if ($passed) { 'passed' } else { 'failed' }); generatedAtUtc=(Get-Date).ToUniversalTime().ToString('O'); decisions=@('TH-DEC-0003','TH-DEC-0005','TH-DEC-0006','TH-DEC-0007','TH-DEC-0008','TH-DEC-0009','TH-DEC-0010','TH-DEC-0011','TH-DEC-0012','TH-DEC-0013','TH-DEC-0014','TH-DEC-0015','TH-DEC-0016','TH-DEC-0017','TH-DEC-0018','TH-DEC-0019','TH-DEC-0020','TH-DEC-0021','TH-DEC-0022','TH-DEC-0023','TH-DEC-0024','TH-DEC-0025','TH-DEC-0026','TH-DEC-0027','TH-DEC-0028','TH-DEC-0029','TH-DEC-0030','TH-DEC-0031','TH-DEC-0032','TH-DEC-0033','TH-DEC-0034','TH-DEC-0035','TH-DEC-0036','TH-DEC-0037','TH-DEC-0038','TH-DEC-0039','TH-DEC-0040','TH-DEC-0041','TH-DEC-0042','TH-DEC-0043','TH-DEC-0044','TH-DEC-0045','TH-DEC-0046','TH-DEC-0047','TH-DEC-0048','TH-DEC-0049','TH-DEC-0050','TH-DEC-0051','TH-DEC-0052','TH-DEC-0053','TH-DEC-0054','TH-DEC-0055','TH-DEC-0056','TH-DEC-0057','TH-DEC-0058','TH-DEC-0059','TH-DEC-0060','TH-DEC-0061','TH-DEC-0062'); checks=$checks }
    $result | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $resultPath -Encoding utf8
    $result | ConvertTo-Json -Depth 8
}
if (-not $passed) { exit 1 }
