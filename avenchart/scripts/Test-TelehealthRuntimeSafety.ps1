# SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
# SPDX-License-Identifier: GPL-3.0-or-later

[CmdletBinding()]
param([string]$ApiBaseUrl = '')

$ErrorActionPreference = 'Stop'
$solutionRoot = Resolve-Path (Join-Path $PSScriptRoot '..')
$repositoryRoot = Resolve-Path (Join-Path $solutionRoot '..')
$artifactsRoot = Join-Path $solutionRoot 'artifacts/telehealth'
$resultPath = Join-Path $artifactsRoot 'latest-telehealth-runtime-safety.json'
New-Item -ItemType Directory -Force $artifactsRoot | Out-Null
$checks = [System.Collections.Generic.List[object]]::new()
$passed = $true

function Add-Check([string]$Name, [bool]$Result, [object]$Details = $null) {
    $script:checks.Add([ordered]@{ name=$Name; status=$(if ($Result) { 'passed' } else { 'failed' }); details=$Details })
    if (-not $Result) { $script:passed = $false }
}

try {
    $settings = Get-Content -Raw (Join-Path $solutionRoot 'backend/src/AvenChart.Api/appsettings.json') | ConvertFrom-Json
    $development = Get-Content -Raw (Join-Path $solutionRoot 'backend/src/AvenChart.Api/appsettings.Development.json') | ConvertFrom-Json
    Add-Check 'Telehealth defaults off in base and Development configuration' (
        $settings.Telehealth.Enabled -eq $false -and $development.Telehealth.Enabled -eq $false) @{
        base=$settings.Telehealth.Enabled; development=$development.Telehealth.Enabled
    }

    & dotnet test (Join-Path $solutionRoot 'backend/tests/AvenChart.Api.Tests/AvenChart.Api.Tests.csproj') `
        -c Release --no-restore --filter 'FullyQualifiedName~TelehealthRuntimeSafetyPolicyTests' | Out-Host
    Add-Check 'Runtime-safety policy tests reject Production enablement' ($LASTEXITCODE -eq 0)

    $featureSource = Get-Content -Raw (Join-Path $solutionRoot 'backend/src/AvenChart.Api/Features/Telehealth/TelehealthOptions.cs')
    $endpointSource = Get-Content -Raw (Join-Path $solutionRoot 'backend/src/AvenChart.Api/Features/Telehealth/TelehealthEndpoints.cs')
    Add-Check 'Route registration is absent when the feature is disabled' (
        $endpointSource -match 'if \(!app\.Services.*TelehealthOptions.*\.Enabled\)' -and
        $endpointSource -match 'return app;')
    Add-Check 'Enabled configuration is explicitly synthetic-only and Production-denied' (
        $featureSource -match '!environment\.IsProduction\(\)' -and
        $featureSource -match 'string\.Equals\(options\.Mode, "Synthetic"' -and
        $featureSource -match 'SyntheticTelehealthVideoProvider\.AdapterMode' -and
        $featureSource -match 'SyntheticTelehealthPharmacyDirectory\.Mode' -and
        $featureSource -match 'SyntheticProfessionalClaimGateway\.AdapterMode')
    $applicantServiceSource = Get-Content -Raw (Join-Path $solutionRoot 'backend/src/AvenChart.Api/Features/Telehealth/TelehealthProspectiveApplicantService.cs')
    $applicantRepositorySource = Get-Content -Raw (Join-Path $solutionRoot 'backend/src/AvenChart.Api/Features/Telehealth/TelehealthProspectiveApplicantRepository.cs')
    Add-Check 'Prospective identity shell has no delivery integration or canonical-patient mutation' (
        $applicantServiceSource -notmatch 'HttpClient|SmtpClient|SendAsync|SendMail' -and
        $applicantRepositorySource -notmatch '(?i)insert\s+into\s+(patients|patient_portal_accounts|telehealth_requests|telehealth_queue_entries)' -and
        $applicantRepositorySource -match 'PossibleMatchManualReview' -and
        $applicantRepositorySource -notmatch 'PatientDuplicateCandidate')
    $identityReviewRepositorySource = Get-Content -Raw (Join-Path $solutionRoot 'backend/src/AvenChart.Api/Features/Telehealth/TelehealthApplicantIdentityReviewRepository.cs')
    $identityReviewServiceSource = Get-Content -Raw (Join-Path $solutionRoot 'backend/src/AvenChart.Api/Features/Telehealth/TelehealthApplicantIdentityReviewService.cs')
    $identityReviewMigrationSource = Get-Content -Raw (Join-Path $solutionRoot 'database/migrations/V0291__telehealth_synthetic_applicant_identity_review.sql')
    Add-Check 'Applicant identity review remains a bounded synthetic decision with no proofing, patient promotion, downstream, or outbound path' (
        $identityReviewRepositorySource -match 'IdentityReviewPending' -and
        $identityReviewRepositorySource -match 'ApprovedForProspectiveIntake' -and
        $identityReviewRepositorySource -match 'ManualReviewRequired' -and
        $identityReviewRepositorySource -notmatch '(?i)insert\s+into\s+(patients|patient_portal_accounts|telehealth_requests|telehealth_queue_entries|appointments|encounters|claims|billing|prescriptions|messages|integration_outbox)' -and
        $identityReviewRepositorySource -notmatch 'HttpClient|ClientWebSocket|SmtpClient|HubConnection|WebRequest|SendAsync' -and
        $identityReviewServiceSource -match 'TelehealthAuthorizationPolicy\.IsAdministratorRole' -and
        $identityReviewMigrationSource -match 'not identity_proofed and not canonical_patient_created and not chart_linked' -and
        $identityReviewMigrationSource -notmatch '(?im)^\s*(drop\s+table|truncate\s+|delete\s+from)' -and
        $endpointSource -match 'TelehealthApplicantIdentityReview')
    $prospectiveSafetyRepositorySource = Get-Content -Raw (Join-Path $solutionRoot 'backend/src/AvenChart.Api/Features/Telehealth/TelehealthProspectiveSafetyTriageRepository.cs')
    $prospectiveSafetyServiceSource = Get-Content -Raw (Join-Path $solutionRoot 'backend/src/AvenChart.Api/Features/Telehealth/TelehealthProspectiveSafetyTriageService.cs')
    $prospectiveSafetyMigrationSource = Get-Content -Raw (Join-Path $solutionRoot 'database/migrations/V0292__telehealth_prospective_safety_triage.sql')
    Add-Check 'Prospective safety triage remains an emergency-first one-shot synthetic evaluation with no clinical review, promotion, downstream, or outbound path' (
        $prospectiveSafetyRepositorySource -match 'IdentityReviewApproved' -and
        $prospectiveSafetyRepositorySource -match 'ApprovedForProspectiveIntake' -and
        $prospectiveSafetyRepositorySource -notmatch '(?i)insert\s+into\s+(patients|patient_portal_accounts|telehealth_requests|telehealth_queue_entries|appointments|encounters|claims|billing|prescriptions|messages|integration_outbox)' -and
        $prospectiveSafetyRepositorySource -notmatch 'HttpClient|ClientWebSocket|SmtpClient|HubConnection|WebRequest|SendAsync' -and
        $prospectiveSafetyServiceSource -match 'prospective-safety-triage-v1' -and
        $prospectiveSafetyServiceSource -match 'No clinician reviewed these answers' -and
        $prospectiveSafetyMigrationSource -match 'not identity_proofed and not clinical_review_performed' -and
        $prospectiveSafetyMigrationSource -match 'not request_created and not queue_enabled and not care_enabled' -and
        $prospectiveSafetyMigrationSource -notmatch '(?im)^\s*(drop\s+table|truncate\s+|delete\s+from)' -and
        $endpointSource -match '\{applicantId:guid\}/safety-triage')
    $visitPurposeRepositorySource = Get-Content -Raw (Join-Path $solutionRoot 'backend/src/AvenChart.Api/Features/Telehealth/TelehealthProspectiveVisitPurposeRepository.cs')
    $visitPurposeServiceSource = Get-Content -Raw (Join-Path $solutionRoot 'backend/src/AvenChart.Api/Features/Telehealth/TelehealthProspectiveVisitPurposeService.cs')
    $visitPurposeMigrationSource = Get-Content -Raw (Join-Path $solutionRoot 'database/migrations/V0293__telehealth_prospective_visit_purpose.sql')
    Add-Check 'Prospective visit purpose remains a two-value navigation classification with no clinical protocol, eligibility, promotion, downstream, or outbound path' (
        $visitPurposeRepositorySource -match 'SafetyScreenPassed' -and
        $visitPurposeRepositorySource -match 'TelehealthEligible' -and
        $visitPurposeRepositorySource -notmatch '(?i)insert\s+into\s+(patients|patient_portal_accounts|telehealth_requests|telehealth_queue_entries|appointments|encounters|claims|billing|prescriptions|messages|integration_outbox)' -and
        $visitPurposeRepositorySource -notmatch 'HttpClient|ClientWebSocket|SmtpClient|HubConnection|WebRequest|SendAsync|ClinicalTriage|EligibilityEvaluator' -and
        $visitPurposeServiceSource -match 'not diagnoses or approved clinical protocols' -and
        $visitPurposeServiceSource -notmatch 'HttpClient|ClientWebSocket|SmtpClient|HubConnection|WebRequest|SendAsync|ClinicalTriage|EligibilityEvaluator' -and
        $visitPurposeMigrationSource -match "purpose_category='migraine'" -and
        $visitPurposeMigrationSource -match "purpose_category='sleep'" -and
        $visitPurposeMigrationSource -match 'not clinical_protocol_published and not clinical_eligibility_determined' -and
        $visitPurposeMigrationSource -notmatch '(?im)^\s*(drop\s+table|truncate\s+|delete\s+from)' -and
        $endpointSource -match '\{applicantId:guid\}/visit-purpose')
    $practiceNetworkRepositorySource = Get-Content -Raw (Join-Path $solutionRoot 'backend/src/AvenChart.Api/Features/Telehealth/TelehealthProspectivePracticeNetworkPrecheckRepository.cs')
    $practiceNetworkServiceSource = Get-Content -Raw (Join-Path $solutionRoot 'backend/src/AvenChart.Api/Features/Telehealth/TelehealthProspectivePracticeNetworkPrecheckService.cs')
    $practiceNetworkCatalogSource = Get-Content -Raw (Join-Path $solutionRoot 'backend/src/AvenChart.Api/Features/Telehealth/SyntheticTelehealthProspectivePracticeNetworkCatalog.cs')
    $practiceNetworkMigrationSource = Get-Content -Raw (Join-Path $solutionRoot 'database/migrations/V0294__telehealth_prospective_practice_network_precheck.sql')
    Add-Check 'Prospective practice-network precheck remains a deterministic plan catalog with no member eligibility, exact network, coverage, promotion, downstream, gateway, or outbound path' (
        $practiceNetworkRepositorySource -match 'VisitPurposeRecorded' -and
        $practiceNetworkRepositorySource -match 'PracticeNetworkPrecheckRecorded' -and
        $practiceNetworkRepositorySource -notmatch '(?i)insert\s+into\s+(patients|patient_portal_accounts|insurance_records|telehealth_requests|telehealth_queue_entries|appointments|encounters|claims|billing|prescriptions|messages|integration_outbox)' -and
        $practiceNetworkRepositorySource -notmatch 'ITelehealthCoverageGateway|SyntheticTelehealthCoverageGateway|HttpClient|ClientWebSocket|SmtpClient|HubConnection|WebRequest|SendAsync' -and
        $practiceNetworkServiceSource -match 'No member eligibility, benefits, rendering-physician participation, exact network status, coverage, estimate, or payment guarantee was established' -and
        $practiceNetworkServiceSource -notmatch 'ITelehealthCoverageGateway|SyntheticTelehealthCoverageGateway|HttpClient|ClientWebSocket|SmtpClient|HubConnection|WebRequest|SendAsync' -and
        $practiceNetworkCatalogSource -match 'public const string AdapterMode = "NON_PRODUCTION"' -and
        $practiceNetworkCatalogSource -match 'harbor-mutual-hd' -and
        $practiceNetworkCatalogSource -match 'blue-valley-standard' -and
        $practiceNetworkCatalogSource -match 'pine-state-choice' -and
        $practiceNetworkCatalogSource -notmatch 'ITelehealthCoverageGateway|SyntheticTelehealthCoverageGateway|HttpClient|ClientWebSocket|SmtpClient|HubConnection|WebRequest|SendAsync' -and
        $practiceNetworkMigrationSource -match 'not member_eligibility_checked' -and
        $practiceNetworkMigrationSource -match 'not exact_network_confirmed' -and
        $practiceNetworkMigrationSource -match 'not integration_enabled and not external_call_performed' -and
        $practiceNetworkMigrationSource -notmatch '(?im)^\s*(drop\s+table|truncate\s+|delete\s+from)' -and
        $endpointSource -match '\{applicantId:guid\}/practice-network-precheck/options' -and
        $endpointSource -match '\{applicantId:guid\}/practice-network-precheck')
    $memberDetailsRepositorySource = Get-Content -Raw (Join-Path $solutionRoot 'backend/src/AvenChart.Api/Features/Telehealth/TelehealthProspectiveMemberInsuranceDetailsRepository.cs')
    $memberDetailsServiceSource = Get-Content -Raw (Join-Path $solutionRoot 'backend/src/AvenChart.Api/Features/Telehealth/TelehealthProspectiveMemberInsuranceDetailsService.cs')
    $memberDetailsPolicySource = Get-Content -Raw (Join-Path $solutionRoot 'backend/src/AvenChart.Api/Features/Telehealth/TelehealthProspectiveMemberInsuranceDetailsPolicy.cs')
    $memberDetailsProtectorSource = Get-Content -Raw (Join-Path $solutionRoot 'backend/src/AvenChart.Api/Features/Telehealth/TelehealthProspectiveMemberInsuranceDetailsProtector.cs')
    $memberDetailsMigrationSource = Get-Content -Raw (Join-Path $solutionRoot 'database/migrations/V0295__telehealth_prospective_member_insurance_details.sql')
    Add-Check 'Prospective member-details receipt remains SYN-only, purpose-protected, mask-only, and without eligibility, canonical coverage, downstream, gateway, or outbound path' (
        $memberDetailsRepositorySource -match 'PracticeNetworkPrecheckRecorded' -and
        $memberDetailsRepositorySource -match 'MemberInsuranceDetailsRecorded' -and
        $memberDetailsRepositorySource -match 'protectedPayload' -and
        $memberDetailsRepositorySource -notmatch 'AddWithValue\("memberId"|AddWithValue\("groupNumber"|AddWithValue\("subscriberFirstName"|AddWithValue\("subscriberLastName"|AddWithValue\("subscriberDateOfBirth"' -and
        $memberDetailsRepositorySource -notmatch '(?i)insert\s+into\s+(patients|patient_portal_accounts|insurance_records|telehealth_requests|telehealth_queue_entries|appointments|encounters|claims|billing|prescriptions|messages|integration_outbox)' -and
        $memberDetailsRepositorySource -notmatch 'ITelehealthCoverageGateway|SyntheticTelehealthCoverageGateway|HttpClient|ClientWebSocket|SmtpClient|HubConnection|WebRequest|SendAsync' -and
        $memberDetailsServiceSource -match 'No canonical insurance, coverage, patient, chart, portal, consent, request, queue, appointment, encounter, estimate, payment, prescribing, billing, claim, communication, integration, external action, or care capability was created' -and
        $memberDetailsServiceSource -notmatch 'ITelehealthCoverageGateway|SyntheticTelehealthCoverageGateway|HttpClient|ClientWebSocket|SmtpClient|HubConnection|WebRequest|SendAsync' -and
        $memberDetailsPolicySource -match '\^SYN-' -and
        $memberDetailsProtectorSource -match 'AvenChart.Telehealth.ProspectiveMemberInsuranceDetails.v1' -and
        $memberDetailsProtectorSource -match 'CryptographicOperations.FixedTimeEquals' -and
        $memberDetailsMigrationSource -match "protection_scheme='ASP.NET_CORE_DATA_PROTECTION'" -and
        $memberDetailsMigrationSource -match 'not member_matched and not member_eligibility_checked' -and
        $memberDetailsMigrationSource -match 'not coverage_verified and not exact_network_confirmed' -and
        $memberDetailsMigrationSource -match 'not integration_enabled and not external_call_performed' -and
        $memberDetailsMigrationSource -notmatch '(?im)^\s*(drop\s+table|truncate\s+|delete\s+from)' -and
        $endpointSource -match '\{applicantId:guid\}/member-insurance-details')
    $eligibilityRepositorySource = Get-Content -Raw (Join-Path $solutionRoot 'backend/src/AvenChart.Api/Features/Telehealth/TelehealthProspectiveEligibilityRepository.cs')
    $eligibilityServiceSource = Get-Content -Raw (Join-Path $solutionRoot 'backend/src/AvenChart.Api/Features/Telehealth/TelehealthProspectiveEligibilityService.cs')
    $eligibilityGatewaySource = Get-Content -Raw (Join-Path $solutionRoot 'backend/src/AvenChart.Api/Features/Telehealth/TelehealthProspectiveEligibilityGateway.cs')
    $eligibilityMigrationSource = Get-Content -Raw (Join-Path $solutionRoot 'database/migrations/V0296__telehealth_prospective_synthetic_eligibility.sql')
    Add-Check 'Prospective eligibility remains normalized NON_PRODUCTION evidence with in-memory unprotection, replay-before-resolution, and no raw transaction, exact network, canonical coverage, downstream, or outbound path' (
        $eligibilityRepositorySource -match 'MemberInsuranceDetailsRecorded' -and
        $eligibilityRepositorySource -match 'SyntheticEligibilityRecorded' -and
        $eligibilityRepositorySource.IndexOf('var replay = await LoadByIdempotencyAsync', [StringComparison]::Ordinal) -lt $eligibilityRepositorySource.IndexOf('var adapterResult = await resolveEligibility', [StringComparison]::Ordinal) -and
        $eligibilityRepositorySource -notmatch '(?i)insert\s+into\s+(patients|patient_portal_accounts|insurance_records|telehealth_requests|telehealth_queue_entries|appointments|encounters|claims|billing|prescriptions|messages|integration_outbox)' -and
        $eligibilityRepositorySource -notmatch 'HttpClient|ClientWebSocket|SmtpClient|HubConnection|WebRequest|SendAsync' -and
        $eligibilityServiceSource -match 'protector\.Unprotect\(candidate\.ProtectedPayload\)' -and
        $eligibilityServiceSource -match 'Eligibility and reported benefit information are separate from exact practice-and-rendering-physician network participation' -and
        $eligibilityServiceSource -notmatch 'HttpClient|ClientWebSocket|SmtpClient|HubConnection|WebRequest|SendAsync' -and
        $eligibilityGatewaySource -match 'public const string AdapterMode = "NON_PRODUCTION"' -and
        $eligibilityGatewaySource -match 'ASC_X12N_270_271_005010X279A1' -and
        $eligibilityGatewaySource -match 'SYN-HM-1001' -and
        $eligibilityGatewaySource -match 'SYN-BV-2002' -and
        $eligibilityGatewaySource -match 'SYN-PS-3003' -and
        $eligibilityGatewaySource -notmatch 'HttpClient|ClientWebSocket|SmtpClient|HubConnection|WebRequest|SendAsync' -and
        $eligibilityMigrationSource -match 'not raw_transaction_created and not exact_network_confirmed' -and
        $eligibilityMigrationSource -match 'not coverage_verified and not canonical_patient_created' -and
        $eligibilityMigrationSource -match 'not integration_enabled and not external_call_performed' -and
        $eligibilityMigrationSource -notmatch '(?im)^\s*(drop\s+table|truncate\s+|delete\s+from)' -and
        $endpointSource -match '\{applicantId:guid\}/eligibility')
    $practiceNetworkDeterminationRepositorySource = Get-Content -Raw (Join-Path $solutionRoot 'backend/src/AvenChart.Api/Features/Telehealth/TelehealthProspectivePracticeNetworkRepository.cs')
    $practiceNetworkDeterminationServiceSource = Get-Content -Raw (Join-Path $solutionRoot 'backend/src/AvenChart.Api/Features/Telehealth/TelehealthProspectivePracticeNetworkService.cs')
    $practiceNetworkDeterminationGatewaySource = Get-Content -Raw (Join-Path $solutionRoot 'backend/src/AvenChart.Api/Features/Telehealth/TelehealthProspectivePracticeNetworkGateway.cs')
    $practiceNetworkDeterminationMigrationSource = Get-Content -Raw (Join-Path $solutionRoot 'database/migrations/V0297__telehealth_prospective_synthetic_practice_network.sql')
    Add-Check 'Prospective practice-network determination remains server-bound normalized NON_PRODUCTION evidence with replay-before-adapter, no member disclosure, no FHIR resource, no physician claim, and no downstream or outbound path' (
        $practiceNetworkDeterminationRepositorySource -match 'SyntheticEligibilityRecorded' -and
        $practiceNetworkDeterminationRepositorySource -match 'SyntheticPracticeNetworkRecorded' -and
        $practiceNetworkDeterminationRepositorySource.IndexOf('var replay = await LoadByIdempotencyAsync', [StringComparison]::Ordinal) -lt $practiceNetworkDeterminationRepositorySource.IndexOf('var adapterResult = await resolveNetwork', [StringComparison]::Ordinal) -and
        $practiceNetworkDeterminationRepositorySource -notmatch '(?i)insert\s+into\s+(patients|patient_portal_accounts|insurance_records|telehealth_requests|telehealth_queue_entries|appointments|encounters|claims|billing|prescriptions|messages|integration_outbox)' -and
        $practiceNetworkDeterminationRepositorySource -notmatch 'HttpClient|ClientWebSocket|SmtpClient|HubConnection|WebRequest|SendAsync' -and
        $practiceNetworkDeterminationServiceSource -match 'Rendering-physician participation remains unchecked' -and
        $practiceNetworkDeterminationServiceSource -notmatch 'Unprotect|ProtectedPayload|MemberId|Subscriber|HttpClient|ClientWebSocket|SmtpClient|HubConnection|WebRequest|SendAsync' -and
        $practiceNetworkDeterminationGatewaySource -match 'public const string AdapterMode = "NON_PRODUCTION"' -and
        $practiceNetworkDeterminationGatewaySource -match 'HL7_FHIR_R4_DAVINCI_PDEX_PLAN_NET_1_2_0' -and
        $practiceNetworkDeterminationGatewaySource -notmatch 'MemberId|Subscriber|Npi|RenderingPhysician|HttpClient|ClientWebSocket|SmtpClient|HubConnection|WebRequest|SendAsync' -and
        $practiceNetworkDeterminationMigrationSource -match 'not fhir_resource_created and not live_directory_queried' -and
        $practiceNetworkDeterminationMigrationSource -match 'not rendering_physician_network_checked' -and
        $practiceNetworkDeterminationMigrationSource -match 'not exact_network_confirmed and not coverage_verified' -and
        $practiceNetworkDeterminationMigrationSource -match 'not integration_enabled' -and
        $practiceNetworkDeterminationMigrationSource -match 'not external_call_performed' -and
        $practiceNetworkDeterminationMigrationSource -notmatch '(?im)^\s*(drop\s+table|truncate\s+|delete\s+from)' -and
        $endpointSource -match '\{applicantId:guid\}/practice-network-determination')
    $identityProofingRepositorySource = Get-Content -Raw (Join-Path $solutionRoot 'backend/src/AvenChart.Api/Features/Telehealth/TelehealthProspectiveIdentityProofingRepository.cs')
    $identityProofingServiceSource = Get-Content -Raw (Join-Path $solutionRoot 'backend/src/AvenChart.Api/Features/Telehealth/TelehealthProspectiveIdentityProofingService.cs')
    $identityProofingGatewaySource = Get-Content -Raw (Join-Path $solutionRoot 'backend/src/AvenChart.Api/Features/Telehealth/TelehealthProspectiveIdentityProofingGateway.cs')
    $identityProofingMigrationSource = Get-Content -Raw (Join-Path $solutionRoot 'database/migrations/V0298__telehealth_prospective_synthetic_identity_proofing.sql')
    Add-Check 'Prospective identity proofing remains opaque-reference-only normalized NON_PRODUCTION process evidence with positive upstream gates, replay-before-adapter, no IAL claim, and no downstream or outbound path' (
        $identityProofingRepositorySource -match 'SyntheticPracticeNetworkRecorded' -and
        $identityProofingRepositorySource -match 'SyntheticIdentityProofingRecorded' -and
        $identityProofingRepositorySource -match 'EligibleBenefitsReported' -and
        $identityProofingRepositorySource -match 'PracticeInNetworkAcceptingNewPatients' -and
        $identityProofingRepositorySource.IndexOf('var replay = await LoadByIdempotencyAsync', [StringComparison]::Ordinal) -lt $identityProofingRepositorySource.IndexOf('var adapterResult = await resolveAsync', [StringComparison]::Ordinal) -and
        $identityProofingRepositorySource -notmatch '(?i)insert\s+into\s+(patients|patient_portal_accounts|insurance_records|telehealth_requests|telehealth_queue_entries|appointments|encounters|claims|billing|prescriptions|messages|integration_outbox)' -and
        $identityProofingRepositorySource -notmatch 'HttpClient|ClientWebSocket|SmtpClient|HubConnection|WebRequest|SendAsync' -and
        $identityProofingServiceSource -match 'AssuranceLevelAchieved: "None"' -and
        $identityProofingServiceSource -match 'IdentityEvidenceCollected: false' -and
        $identityProofingServiceSource -match 'BiometricDataCollected: false' -and
        $identityProofingServiceSource -notmatch 'LegalFirstName|LegalLastName|DateOfBirth|Email|Phone|MemberId|Subscriber|HttpClient|ClientWebSocket|SmtpClient|HubConnection|WebRequest|SendAsync' -and
        $identityProofingGatewaySource -match 'public const string AdapterMode = "NON_PRODUCTION"' -and
        $identityProofingGatewaySource -match 'NIST_SP_800_63A_4_PROCESS_CONCEPTS_ONLY' -and
        $identityProofingGatewaySource -notmatch 'LegalFirstName|LegalLastName|DateOfBirth|Email|Phone|Address|MemberId|Subscriber|GovernmentIdentifier(?:Value|Number)|Biometric(?:Data|Sample|Template)|HttpClient|ClientWebSocket|SmtpClient|HubConnection|WebRequest|SendAsync' -and
        $identityProofingMigrationSource -match "assurance_level_achieved='None'" -and
        $identityProofingMigrationSource -match 'not identity_evidence_collected and not government_identifier_collected' -and
        $identityProofingMigrationSource -match 'not authenticator_bound and not identity_proofed' -and
        $identityProofingMigrationSource -match 'not external_call_performed' -and
        $identityProofingMigrationSource -notmatch '(?im)^\s*(drop\s+table|truncate\s+|delete\s+from)' -and
        $endpointSource -match '\{applicantId:guid\}/identity-proofing')
    $promotionAuthorizationRepositorySource = Get-Content -Raw (Join-Path $solutionRoot 'backend/src/AvenChart.Api/Features/Telehealth/TelehealthApplicantPromotionAuthorizationRepository.cs')
    $promotionAuthorizationServiceSource = Get-Content -Raw (Join-Path $solutionRoot 'backend/src/AvenChart.Api/Features/Telehealth/TelehealthApplicantPromotionAuthorizationService.cs')
    $promotionAuthorizationPolicySource = Get-Content -Raw (Join-Path $solutionRoot 'backend/src/AvenChart.Api/Features/Telehealth/TelehealthApplicantPromotionAuthorizationPolicy.cs')
    $promotionAuthorizationMigrationSource = Get-Content -Raw (Join-Path $solutionRoot 'database/migrations/V0299__telehealth_synthetic_promotion_authorization.sql')
    Add-Check 'Applicant promotion authorization remains a staff-governed synthetic decision over complete normalized evidence with None assurance and no canonical, downstream, or outbound path' (
        $promotionAuthorizationRepositorySource -match 'SyntheticIdentityProofingRecorded' -and
        $promotionAuthorizationPolicySource -match 'SyntheticPromotionAuthorized' -and
        $promotionAuthorizationPolicySource -match 'SyntheticPromotionDenied' -and
        $promotionAuthorizationRepositorySource -match 'EligibleBenefitsReported' -and
        $promotionAuthorizationRepositorySource -match 'PracticeInNetworkAcceptingNewPatients' -and
        $promotionAuthorizationRepositorySource -match 'SyntheticProofingPassed' -and
        $promotionAuthorizationRepositorySource -match 'AssuranceLevelAchieved != "None"' -and
        $promotionAuthorizationRepositorySource -notmatch '(?i)insert\s+into\s+(patients|patient_portal_accounts|insurance_records|telehealth_requests|telehealth_queue_entries|appointments|encounters|claims|billing|prescriptions|messages|integration_outbox)' -and
        $promotionAuthorizationRepositorySource -notmatch 'HttpClient|ClientWebSocket|SmtpClient|HubConnection|WebRequest|SendAsync' -and
        $promotionAuthorizationServiceSource -match 'TelehealthAuthorizationPolicy\.IsAdministratorRole' -and
        $promotionAuthorizationServiceSource -match 'assurance remains None and identity was not proved' -and
        $promotionAuthorizationMigrationSource -match 'none_assurance_acknowledged and synthetic_data_confirmed' -and
        $promotionAuthorizationMigrationSource -match 'not real_identity_proofed and not canonical_patient_created and not chart_linked' -and
        $promotionAuthorizationMigrationSource -match 'not portal_account_created and not prospective_intake_completed' -and
        $promotionAuthorizationMigrationSource -match 'not request_created' -and
        $promotionAuthorizationMigrationSource -match 'not queue_enabled' -and
        $promotionAuthorizationMigrationSource -match 'not external_call_performed' -and
        $promotionAuthorizationMigrationSource -notmatch '(?im)^\s*(drop\s+table|truncate\s+|delete\s+from)' -and
        $endpointSource -match 'applicant-promotion-authorization' -and
        $endpointSource -match 'promotion-authorization-decision')
    $syntheticPromotionRepositorySource = Get-Content -Raw (Join-Path $solutionRoot 'backend/src/AvenChart.Api/Features/Telehealth/TelehealthApplicantSyntheticPromotionRepository.cs')
    $syntheticPromotionServiceSource = Get-Content -Raw (Join-Path $solutionRoot 'backend/src/AvenChart.Api/Features/Telehealth/TelehealthApplicantSyntheticPromotionService.cs')
    $syntheticPromotionPolicySource = Get-Content -Raw (Join-Path $solutionRoot 'backend/src/AvenChart.Api/Features/Telehealth/TelehealthApplicantSyntheticPromotionPolicy.cs')
    $syntheticPromotionMigrationSource = Get-Content -Raw (Join-Path $solutionRoot 'database/migrations/V0300__telehealth_atomic_synthetic_patient_promotion.sql')
    Add-Check 'Atomic synthetic promotion serializes canonical creation, rechecks duplicates, discloses no candidate, and cannot create portal, care, downstream, or outbound capability' (
        $syntheticPromotionRepositorySource -match 'pg_advisory_xact_lock\(873421986\)' -and
        $syntheticPromotionRepositorySource -match 'PossiblePatientMatchExistsAsync' -and
        $syntheticPromotionRepositorySource -match 'PatientIdentifierExistsAsync' -and
        $syntheticPromotionRepositorySource -match 'SyntheticPromotionAuthorized' -and
        $syntheticPromotionRepositorySource -match 'insert into patients' -and
        $syntheticPromotionRepositorySource -match 'provider_id,facility_id,portal_enabled,registration_date' -and
        $syntheticPromotionRepositorySource -match 'null,@facilityId,false' -and
        $syntheticPromotionRepositorySource -notmatch '(?i)insert\s+into\s+(patient_portal_accounts|patient_portal_external_identity_mappings|insurance_records|telehealth_requests|telehealth_queue_entries|telehealth_intake_snapshots|appointments|encounters|claims|billing|prescriptions|messages|integration_outbox)' -and
        $syntheticPromotionRepositorySource -notmatch 'HttpClient|ClientWebSocket|SmtpClient|HubConnection|WebRequest|SendAsync' -and
        $syntheticPromotionServiceSource -match 'RequireAdministrator' -and
        $syntheticPromotionServiceSource -match 'canonical synthetic patient shell' -and
        $syntheticPromotionServiceSource -notmatch 'CanonicalPatientId|CanonicalLegacyPid' -and
        $syntheticPromotionPolicySource -match 'PromoteAuthorizedSyntheticApplicant' -and
        $syntheticPromotionPolicySource -match 'BlockedPossiblePatientMatch' -and
        $syntheticPromotionMigrationSource -match 'enforce_telehealth_applicant_synthetic_promotion' -and
        $syntheticPromotionMigrationSource -match 'canonical_patient_creation_acknowledged and no_portal_no_care_acknowledged' -and
        $syntheticPromotionMigrationSource -match 'not portal_account_created' -and
        $syntheticPromotionMigrationSource -match 'not request_created' -and
        $syntheticPromotionMigrationSource -match 'not queue_enabled' -and
        $syntheticPromotionMigrationSource -match 'not care_enabled' -and
        $syntheticPromotionMigrationSource -match 'not external_call_performed' -and
        $endpointSource -match 'applicant-synthetic-promotion' -and
        $endpointSource -match 'synthetic-promotion')
    $noticeRepositorySource = Get-Content -Raw (Join-Path $solutionRoot 'backend/src/AvenChart.Api/Features/Telehealth/TelehealthApplicantNoticeRepository.cs')
    $noticeServiceSource = Get-Content -Raw (Join-Path $solutionRoot 'backend/src/AvenChart.Api/Features/Telehealth/TelehealthApplicantNoticeService.cs')
    $noticePolicySource = Get-Content -Raw (Join-Path $solutionRoot 'backend/src/AvenChart.Api/Features/Telehealth/TelehealthApplicantNoticePolicy.cs')
    $noticeMigrationSource = Get-Content -Raw (Join-Path $solutionRoot 'database/migrations/V0301__telehealth_state_notice_acknowledgment.sql')
    Add-Check 'State-specific notice acknowledgment is applicant-owned, server-selected, promotion-bound, legally nonfinal, and cannot create portal, downstream, care, or outbound capability' (
        $noticeRepositorySource -match 'SyntheticPatientPromoted' -and
        $noticeRepositorySource -match 'TelehealthApplicantNoticePolicy\.ResultingStatus' -and
        $noticeRepositorySource -match 'portal_enabled' -and
        $noticeRepositorySource -notmatch '(?i)insert\s+into\s+(patient_portal_accounts|patient_portal_external_identity_mappings|insurance_records|telehealth_requests|telehealth_queue_entries|telehealth_intake_snapshots|appointments|encounters|claims|billing|prescriptions|messages|integration_outbox)' -and
        $noticeRepositorySource -notmatch 'HttpClient|ClientWebSocket|SmtpClient|HubConnection|WebRequest|SendAsync' -and
        $noticeServiceSource -match 'LegalConsentEstablished: false' -and
        $noticeServiceSource -match 'ClinicianConsentDocumented: false' -and
        $noticeServiceSource -match 'ClinicianReconfirmationRequired: true' -and
        $noticePolicySource -match 'GA_TELEHEALTH_NOTICE_V1' -and
        $noticePolicySource -match 'CA_TELEHEALTH_NOTICE_V1' -and
        $noticePolicySource -match 'FL_TELEHEALTH_NOTICE_V1' -and
        $noticeMigrationSource -match 'enforce_telehealth_applicant_notice_acknowledgment' -and
        $noticeMigrationSource -match 'legal_review_status=''PendingIndependentReview''' -and
        $noticeMigrationSource -match 'not legal_consent_established' -and
        $noticeMigrationSource -match 'not clinician_consent_documented' -and
        $noticeMigrationSource -match 'not portal_account_created' -and
        $noticeMigrationSource -match 'not request_created' -and
        $noticeMigrationSource -match 'not queue_enabled' -and
        $noticeMigrationSource -match 'not care_enabled' -and
        $noticeMigrationSource -match 'not external_call_performed' -and
        $noticeMigrationSource -notmatch '(?im)^\s*(drop\s+table|truncate\s+|delete\s+from)' -and
        $endpointSource -match '\{applicantId:guid\}/telehealth-notice' -and
        $endpointSource -match 'telehealth-notice/acknowledgment')
    $registrationDetailsRepositorySource = Get-Content -Raw (Join-Path $solutionRoot 'backend/src/AvenChart.Api/Features/Telehealth/TelehealthApplicantRegistrationDetailsRepository.cs')
    $registrationDetailsServiceSource = Get-Content -Raw (Join-Path $solutionRoot 'backend/src/AvenChart.Api/Features/Telehealth/TelehealthApplicantRegistrationDetailsService.cs')
    $registrationDetailsPolicySource = Get-Content -Raw (Join-Path $solutionRoot 'backend/src/AvenChart.Api/Features/Telehealth/TelehealthApplicantRegistrationDetailsPolicy.cs')
    $registrationDetailsMigrationSource = Get-Content -Raw (Join-Path $solutionRoot 'database/migrations/V0302__telehealth_minimum_registration_details_confirmation.sql')
    Add-Check 'Minimum registration-details confirmation is applicant-owned, no-edit, notice-and-promotion-bound, masked, and cannot create identity, downstream, care, or outbound capability' (
        $registrationDetailsRepositorySource -match 'SyntheticTelehealthNoticeAcknowledged' -and
        $registrationDetailsRepositorySource -match 'TelehealthApplicantRegistrationDetailsPolicy\.ResultingStatus' -and
        $registrationDetailsRepositorySource -match 'portal_enabled' -and
        $registrationDetailsRepositorySource -match 'merged_into_patient_id' -and
        $registrationDetailsRepositorySource -notmatch '(?i)insert\s+into\s+(patients|patient_portal_accounts|patient_portal_external_identity_mappings|insurance_records|telehealth_requests|telehealth_queue_entries|telehealth_intake_snapshots|appointments|encounters|claims|billing|prescriptions|messages|integration_outbox)' -and
        $registrationDetailsRepositorySource -notmatch 'HttpClient|ClientWebSocket|SmtpClient|HubConnection|WebRequest|SendAsync' -and
        $registrationDetailsServiceSource -match 'MaskedEmail: snapshot\.MaskedEmail' -and
        $registrationDetailsServiceSource -match 'MaskedPhone: snapshot\.MaskedPhone' -and
        $registrationDetailsServiceSource -match 'IdentityAssuranceEstablished: false' -and
        $registrationDetailsServiceSource -match 'PatientRecordChanged: false' -and
        $registrationDetailsServiceSource -match 'InsuranceConfirmed: false' -and
        $registrationDetailsServiceSource -match 'RequestCreated: false' -and
        $registrationDetailsServiceSource -match 'QueueEnabled: false' -and
        $registrationDetailsServiceSource -match 'CareEnabled: false' -and
        $registrationDetailsPolicySource -match 'SYNTHETIC_MINIMUM_REGISTRATION_DETAILS_CONFIRMATION' -and
        $registrationDetailsPolicySource -match 'PROMOTED_PATIENT_MINIMUM_DETAILS_NO_EDIT_CONFIRMATION' -and
        $registrationDetailsMigrationSource -match 'enforce_telehealth_registration_details_confirmation' -and
        $registrationDetailsMigrationSource -match 'not identity_assurance_established' -and
        $registrationDetailsMigrationSource -match 'not patient_record_changed' -and
        $registrationDetailsMigrationSource -match 'not correction_completed' -and
        $registrationDetailsMigrationSource -match 'not insurance_confirmed' -and
        $registrationDetailsMigrationSource -match 'not request_created' -and
        $registrationDetailsMigrationSource -match 'not queue_enabled' -and
        $registrationDetailsMigrationSource -match 'not care_enabled' -and
        $registrationDetailsMigrationSource -match 'not external_call_performed' -and
        $registrationDetailsMigrationSource -notmatch '(?im)^\s*(drop\s+table|truncate\s+|delete\s+from)' -and
        $endpointSource -match '\{applicantId:guid\}/registration-details' -and
        $endpointSource -match 'registration-details/confirmation')
    $insuranceHandoffRepositorySource = Get-Content -Raw (Join-Path $solutionRoot 'backend/src/AvenChart.Api/Features/Telehealth/TelehealthApplicantInsuranceHandoffRepository.cs')
    $insuranceHandoffServiceSource = Get-Content -Raw (Join-Path $solutionRoot 'backend/src/AvenChart.Api/Features/Telehealth/TelehealthApplicantInsuranceHandoffService.cs')
    $insuranceHandoffPolicySource = Get-Content -Raw (Join-Path $solutionRoot 'backend/src/AvenChart.Api/Features/Telehealth/TelehealthApplicantInsuranceHandoffPolicy.cs')
    $insuranceHandoffMigrationSource = Get-Content -Raw (Join-Path $solutionRoot 'database/migrations/V0303__telehealth_insurance_handoff_confirmation.sql')
    Add-Check 'Synthetic insurance handoff is applicant-owned, no-edit, masked, evidence-bound, and cannot create coverage, downstream, care, or outbound capability' (
        $insuranceHandoffRepositorySource -match 'SyntheticMinimumRegistrationDetailsConfirmed' -and
        $insuranceHandoffRepositorySource -match 'TelehealthApplicantInsuranceHandoffPolicy\.ResultingStatus' -and
        $insuranceHandoffRepositorySource -match 'portal_enabled' -and
        $insuranceHandoffRepositorySource -match 'merged_into_patient_id' -and
        $insuranceHandoffRepositorySource -match 'CanonicalInsuranceRecordCount != 0' -and
        $insuranceHandoffRepositorySource -notmatch '(?i)insert\s+into\s+(patients|patient_portal_accounts|patient_portal_external_identity_mappings|insurance_records|telehealth_requests|telehealth_queue_entries|telehealth_intake_snapshots|appointments|encounters|claims|billing|prescriptions|messages|integration_outbox)' -and
        $insuranceHandoffRepositorySource -notmatch 'HttpClient|ClientWebSocket|SmtpClient|HubConnection|WebRequest|SendAsync' -and
        $insuranceHandoffServiceSource -match 'RenderingPhysicianNetworkChecked: snapshot\.RenderingPhysicianNetworkChecked' -and
        $insuranceHandoffServiceSource -match 'CoverageVerified: false' -and
        $insuranceHandoffServiceSource -match 'ExactNetworkConfirmed: false' -and
        $insuranceHandoffServiceSource -match 'CanonicalCoverageCreated: false' -and
        $insuranceHandoffServiceSource -match 'RequestCreated: false' -and
        $insuranceHandoffServiceSource -match 'QueueEnabled: false' -and
        $insuranceHandoffServiceSource -match 'CareEnabled: false' -and
        $insuranceHandoffPolicySource -match 'SYNTHETIC_INSURANCE_HANDOFF_CONFIRMATION' -and
        $insuranceHandoffPolicySource -match 'PROMOTED_PATIENT_INSURANCE_HANDOFF_NO_EDIT_CONFIRMATION' -and
        $insuranceHandoffMigrationSource -match 'enforce_telehealth_insurance_handoff_confirmation' -and
        $insuranceHandoffMigrationSource -match 'not rendering_physician_network_checked' -and
        $insuranceHandoffMigrationSource -match 'not coverage_verified' -and
        $insuranceHandoffMigrationSource -match 'not exact_network_confirmed' -and
        $insuranceHandoffMigrationSource -match 'not canonical_coverage_created' -and
        $insuranceHandoffMigrationSource -match 'not request_created' -and
        $insuranceHandoffMigrationSource -match 'not queue_enabled' -and
        $insuranceHandoffMigrationSource -match 'not care_enabled' -and
        $insuranceHandoffMigrationSource -match 'not external_call_performed' -and
        $insuranceHandoffMigrationSource -notmatch '(?im)^\s*(drop\s+table|truncate\s+|delete\s+from)' -and
        $endpointSource -match '\{applicantId:guid\}/insurance-handoff' -and
        $endpointSource -match 'insurance-handoff/confirmation')
    $communicationAccessRepositorySource = Get-Content -Raw (Join-Path $solutionRoot 'backend/src/AvenChart.Api/Features/Telehealth/TelehealthApplicantCommunicationAccessRepository.cs')
    $communicationAccessServiceSource = Get-Content -Raw (Join-Path $solutionRoot 'backend/src/AvenChart.Api/Features/Telehealth/TelehealthApplicantCommunicationAccessService.cs')
    $communicationAccessPolicySource = Get-Content -Raw (Join-Path $solutionRoot 'backend/src/AvenChart.Api/Features/Telehealth/TelehealthApplicantCommunicationAccessPolicy.cs')
    $communicationAccessMigrationSource = Get-Content -Raw (Join-Path $solutionRoot 'database/migrations/V0304__telehealth_communication_access_readiness.sql')
    Add-Check 'Synthetic communication/access readiness is applicant-owned, server-context-bound, preference-only, and cannot arrange support or enable downstream, care, or outbound capability' (
        $communicationAccessRepositorySource -match 'SyntheticInsuranceDetailsConfirmed' -and
        $communicationAccessRepositorySource -match 'TelehealthApplicantCommunicationAccessPolicy\.ResultingStatus' -and
        $communicationAccessRepositorySource -match 'portal_enabled' -and
        $communicationAccessRepositorySource -match 'merged_into_patient_id' -and
        $communicationAccessRepositorySource -match 'CanonicalInsuranceRecordCount != 0' -and
        $communicationAccessRepositorySource -notmatch '(?i)insert\s+into\s+(patients|patient_portal_accounts|patient_portal_external_identity_mappings|insurance_records|telehealth_requests|telehealth_queue_entries|telehealth_intake_snapshots|telehealth_video_sessions|appointments|encounters|claims|billing|prescriptions|messages|integration_outbox)' -and
        $communicationAccessRepositorySource -notmatch 'HttpClient|ClientWebSocket|SmtpClient|HubConnection|WebRequest|SendAsync' -and
        $communicationAccessServiceSource -match 'InterpreterAssigned: false' -and
        $communicationAccessServiceSource -match 'AccessibilityAccommodationArranged: false' -and
        $communicationAccessServiceSource -match 'CommunicationArrangementCompleted: false' -and
        $communicationAccessServiceSource -match 'SupportRequestCreated: false' -and
        $communicationAccessServiceSource -match 'TechnologyReadinessCompleted: false' -and
        $communicationAccessServiceSource -match 'RequestCreated: false' -and
        $communicationAccessServiceSource -match 'QueueEnabled: false' -and
        $communicationAccessServiceSource -match 'CareEnabled: false' -and
        $communicationAccessPolicySource -match 'SYNTHETIC_COMMUNICATION_ACCESS_READINESS' -and
        $communicationAccessPolicySource -match 'PROMOTED_PATIENT_COMMUNICATION_ACCESS_READINESS_RECEIPT' -and
        $communicationAccessPolicySource -match 'English' -and $communicationAccessPolicySource -match 'Spanish' -and
        $communicationAccessMigrationSource -match 'enforce_telehealth_communication_access_readiness' -and
        $communicationAccessMigrationSource -match 'not interpreter_assigned' -and
        $communicationAccessMigrationSource -match 'not accessibility_accommodation_arranged' -and
        $communicationAccessMigrationSource -match 'not communication_arrangement_completed' -and
        $communicationAccessMigrationSource -match 'not support_request_created' -and
        $communicationAccessMigrationSource -match 'not technology_readiness_completed' -and
        $communicationAccessMigrationSource -match 'not patient_record_changed' -and
        $communicationAccessMigrationSource -match 'not request_created' -and
        $communicationAccessMigrationSource -match 'not queue_enabled' -and
        $communicationAccessMigrationSource -match 'not care_enabled' -and
        $communicationAccessMigrationSource -match 'not external_call_performed' -and
        $communicationAccessMigrationSource -notmatch '(?im)^\s*(drop\s+table|truncate\s+|delete\s+from)' -and
        $endpointSource -match '\{applicantId:guid\}/communication-access-readiness')
    $devicePreparationRepositorySource = Get-Content -Raw (Join-Path $solutionRoot 'backend/src/AvenChart.Api/Features/Telehealth/TelehealthApplicantDevicePreparationRepository.cs')
    $devicePreparationServiceSource = Get-Content -Raw (Join-Path $solutionRoot 'backend/src/AvenChart.Api/Features/Telehealth/TelehealthApplicantDevicePreparationService.cs')
    $devicePreparationPolicySource = Get-Content -Raw (Join-Path $solutionRoot 'backend/src/AvenChart.Api/Features/Telehealth/TelehealthApplicantDevicePreparationPolicy.cs')
    $devicePreparationMigrationSource = Get-Content -Raw (Join-Path $solutionRoot 'database/migrations/V0305__telehealth_applicant_device_preparation.sql')
    Add-Check 'Synthetic device preparation is applicant-owned, communication-readiness-bound, coarse, acknowledgment-gated, and cannot create media, readiness, downstream, care, or outbound capability' (
        $devicePreparationRepositorySource -match 'SyntheticCommunicationAccessReadinessRecorded' -and
        $devicePreparationRepositorySource -match 'TelehealthApplicantDevicePreparationPolicy\.ResultingStatus' -and
        $devicePreparationRepositorySource -match 'portal_enabled' -and
        $devicePreparationRepositorySource -match 'merged_into_patient_id' -and
        $devicePreparationRepositorySource -match 'CanonicalInsuranceRecordCount != 0' -and
        $devicePreparationRepositorySource -notmatch '(?i)insert\s+into\s+(patients|patient_portal_accounts|patient_portal_external_identity_mappings|insurance_records|telehealth_requests|telehealth_queue_entries|telehealth_intake_snapshots|telehealth_video_sessions|appointments|encounters|claims|billing|prescriptions|messages|integration_outbox)' -and
        $devicePreparationRepositorySource -notmatch 'HttpClient|ClientWebSocket|SmtpClient|HubConnection|WebRequest|SendAsync|RTCPeerConnection|MediaStream' -and
        $devicePreparationServiceSource -match 'TechnologyReady: false' -and
        $devicePreparationServiceSource -match 'WaitingRoomCreated: false' -and
        $devicePreparationServiceSource -match 'MediaSessionCreated: false' -and
        $devicePreparationServiceSource -match 'CommunicationStarted: false' -and
        $devicePreparationServiceSource -match 'SupportArrangementCompleted: false' -and
        $devicePreparationServiceSource -match 'RequestCreated: false' -and
        $devicePreparationServiceSource -match 'QueueEntered: false' -and
        $devicePreparationServiceSource -match 'CareAuthorized: false' -and
        $devicePreparationPolicySource -match 'SYNTHETIC_APPLICANT_DEVICE_PREPARATION' -and
        $devicePreparationPolicySource -match 'PROMOTED_PATIENT_DEVICE_PREPARATION_RECEIPT' -and
        $devicePreparationPolicySource -match 'Unknown' -and $devicePreparationPolicySource -match 'Good' -and
        $devicePreparationPolicySource -match 'NoReadinessGuaranteeAcknowledged' -and
        $devicePreparationPolicySource -match 'RecheckBeforeConsultationAcknowledged' -and
        $devicePreparationMigrationSource -match 'enforce_telehealth_applicant_device_preparation' -and
        $devicePreparationMigrationSource -match 'not technology_ready' -and
        $devicePreparationMigrationSource -match 'not waiting_room_created' -and
        $devicePreparationMigrationSource -match 'not media_session_created' -and
        $devicePreparationMigrationSource -match 'not communication_started' -and
        $devicePreparationMigrationSource -match 'not patient_record_changed' -and
        $devicePreparationMigrationSource -match 'not request_created' -and
        $devicePreparationMigrationSource -match 'not queue_entered' -and
        $devicePreparationMigrationSource -match 'not care_authorized' -and
        $devicePreparationMigrationSource -match 'not external_call_performed' -and
        $devicePreparationMigrationSource -notmatch '(?im)^\s*(drop\s+table|truncate\s+|delete\s+from)' -and
        $endpointSource -match '\{applicantId:guid\}/device-preparation')
    $clinicalInventoryRepositorySource = Get-Content -Raw (Join-Path $solutionRoot 'backend/src/AvenChart.Api/Features/Telehealth/TelehealthApplicantClinicalInformationInventoryRepository.cs')
    $clinicalInventoryServiceSource = Get-Content -Raw (Join-Path $solutionRoot 'backend/src/AvenChart.Api/Features/Telehealth/TelehealthApplicantClinicalInformationInventoryService.cs')
    $clinicalInventoryPolicySource = Get-Content -Raw (Join-Path $solutionRoot 'backend/src/AvenChart.Api/Features/Telehealth/TelehealthApplicantClinicalInformationInventoryPolicy.cs')
    $clinicalInventoryMigrationSource = Get-Content -Raw (Join-Path $solutionRoot 'database/migrations/V0306__telehealth_applicant_clinical_information_inventory.sql')
    Add-Check 'Synthetic clinical-information inventory is applicant-owned, device-preparation-bound, coarse, acknowledgment-gated, server-routed, and cannot reconcile a chart or create review, intake, eligibility, request, queue, prescribing, or care capability' (
        $clinicalInventoryRepositorySource -match 'SyntheticDevicePreparationRecorded' -and
        $clinicalInventoryRepositorySource -match 'TelehealthApplicantClinicalInformationInventoryPolicy\.ResultingStatus' -and
        $clinicalInventoryRepositorySource -match 'portal_enabled' -and
        $clinicalInventoryRepositorySource -match 'merged_into_patient_id' -and
        $clinicalInventoryRepositorySource -match 'CanonicalInsuranceRecordCount != 0' -and
        $clinicalInventoryRepositorySource -notmatch '(?i)insert\s+into\s+(patients|medications|allergies|problems|diagnoses|patient_portal_accounts|insurance_records|telehealth_requests|telehealth_queue_entries|telehealth_intake_snapshots|appointments|encounters|claims|billing|prescriptions|messages|integration_outbox)' -and
        $clinicalInventoryRepositorySource -notmatch 'HttpClient|ClientWebSocket|SmtpClient|HubConnection|WebRequest|SendAsync' -and
        $clinicalInventoryServiceSource -match 'MedicationListReconciled: false' -and
        $clinicalInventoryServiceSource -match 'AllergyListReconciled: false' -and
        $clinicalInventoryServiceSource -match 'HealthHistoryReconciled: false' -and
        $clinicalInventoryServiceSource -match 'ClinicalIntakeCompleted: false' -and
        $clinicalInventoryServiceSource -match 'ClinicalEligibilityEstablished: false' -and
        $clinicalInventoryServiceSource -match 'ClinicianReviewCreated: false' -and
        $clinicalInventoryServiceSource -match 'PatientRecordChanged: false' -and
        $clinicalInventoryServiceSource -match 'RequestCreated: false' -and
        $clinicalInventoryServiceSource -match 'QueueEntered: false' -and
        $clinicalInventoryServiceSource -match 'CareAuthorized: false' -and
        $clinicalInventoryServiceSource -match 'PrescribingEnabled: false' -and
        $clinicalInventoryPolicySource -match 'SYNTHETIC_APPLICANT_CLINICAL_INFORMATION_INVENTORY' -and
        $clinicalInventoryPolicySource -match 'PatientReportsNone' -and
        $clinicalInventoryPolicySource -match 'ItemsToReview' -and
        $clinicalInventoryPolicySource -match 'Unsure' -and
        $clinicalInventoryPolicySource -match 'DetailedCollectionRequired' -and
        $clinicalInventoryPolicySource -match 'AssistedReviewRequired' -and
        $clinicalInventoryPolicySource -match 'PendingClinicianReconciliation' -and
        $clinicalInventoryMigrationSource -match 'enforce_telehealth_applicant_clinical_information_inventory' -and
        $clinicalInventoryMigrationSource -match 'not medication_list_reconciled' -and
        $clinicalInventoryMigrationSource -match 'not allergy_list_reconciled' -and
        $clinicalInventoryMigrationSource -match 'not health_history_reconciled' -and
        $clinicalInventoryMigrationSource -match 'not clinical_intake_completed' -and
        $clinicalInventoryMigrationSource -match 'not clinical_eligibility_established' -and
        $clinicalInventoryMigrationSource -match 'not clinician_review_created' -and
        $clinicalInventoryMigrationSource -match 'not patient_record_changed' -and
        $clinicalInventoryMigrationSource -match 'not request_created' -and
        $clinicalInventoryMigrationSource -match 'not queue_entered' -and
        $clinicalInventoryMigrationSource -match 'not care_authorized' -and
        $clinicalInventoryMigrationSource -match 'not prescribing_enabled' -and
        $clinicalInventoryMigrationSource -notmatch '(?im)^\s+(medication_name|substance|reaction|dose|diagnosis|symptom|procedure|narrative|clinical_date|clinical_identifier|free_text)\s+' -and
        $clinicalInventoryMigrationSource -notmatch '(?im)^\s*(drop\s+table|truncate\s+|delete\s+from)' -and
        $endpointSource -match '\{applicantId:guid\}/clinical-information-inventory')
    $medicationInformationRepositorySource = Get-Content -Raw (Join-Path $solutionRoot 'backend/src/AvenChart.Api/Features/Telehealth/TelehealthApplicantMedicationInformationRepository.cs')
    $medicationInformationServiceSource = Get-Content -Raw (Join-Path $solutionRoot 'backend/src/AvenChart.Api/Features/Telehealth/TelehealthApplicantMedicationInformationService.cs')
    $medicationInformationPolicySource = Get-Content -Raw (Join-Path $solutionRoot 'backend/src/AvenChart.Api/Features/Telehealth/TelehealthApplicantMedicationInformationPolicy.cs')
    $medicationInformationMigrationSource = Get-Content -Raw (Join-Path $solutionRoot 'database/migrations/V0307__telehealth_applicant_medication_information.sql')
    Add-Check 'Synthetic medication information is applicant-owned, inventory-bound, fixed-catalog, acknowledgment-gated, server-routed, and cannot create canonical medication, reconciliation, interaction, review, intake, request, queue, prescribing, or care capability' (
        $medicationInformationRepositorySource -match 'SyntheticClinicalInformationInventoryRecorded' -and
        $medicationInformationRepositorySource -match 'CanonicalMedicationCount' -and
        $medicationInformationRepositorySource -match 'CanonicalPrescriptionCount' -and
        $medicationInformationRepositorySource -notmatch '(?i)insert\s+into\s+(patients|medications|prescriptions|allergies|problems|diagnoses|patient_portal_accounts|insurance_records|telehealth_requests|telehealth_queue_entries|telehealth_intake_snapshots|appointments|encounters|claims|billing|messages|integration_outbox)' -and
        $medicationInformationRepositorySource -notmatch 'HttpClient|ClientWebSocket|SmtpClient|HubConnection|WebRequest|SendAsync' -and
        $medicationInformationServiceSource -match 'MedicationStatementCreated: false' -and
        $medicationInformationServiceSource -match 'MedicationRequestCreated: false' -and
        $medicationInformationServiceSource -match 'MedicationListReconciled: false' -and
        $medicationInformationServiceSource -match 'InteractionCheckPerformed: false' -and
        $medicationInformationServiceSource -match 'ClinicianReviewCreated: false' -and
        $medicationInformationServiceSource -match 'ClinicalIntakeCompleted: false' -and
        $medicationInformationServiceSource -match 'PatientRecordChanged: false' -and
        $medicationInformationServiceSource -match 'RequestCreated: false' -and
        $medicationInformationServiceSource -match 'QueueEntered: false' -and
        $medicationInformationServiceSource -match 'CareAuthorized: false' -and
        $medicationInformationServiceSource -match 'PrescribingEnabled: false' -and
        $medicationInformationPolicySource -match 'LOCAL_SYNTHETIC_ONLY' -and
        $medicationInformationPolicySource -match 'RxNormMapped: false' -and
        $medicationInformationPolicySource -match 'Taking' -and
        $medicationInformationPolicySource -match 'NotTaking' -and
        $medicationInformationPolicySource -match 'Unsure' -and
        $medicationInformationMigrationSource -match 'enforce_telehealth_medication_information_item_count' -and
        $medicationInformationMigrationSource -match 'deferrable initially deferred' -and
        $medicationInformationMigrationSource -notmatch '(?im)^\s+(dose|directions|route|frequency|timing|indication|prescriber|pharmacy|note|attachment|free_text|rxnorm_code|ndc_code|snomed_code)\s+' -and
        $medicationInformationMigrationSource -notmatch '(?im)^\s*(drop\s+table|truncate\s+|delete\s+from)' -and
        $endpointSource -match '\{applicantId:guid\}/medication-information')
    $allergyInformationRepositorySource = Get-Content -Raw (Join-Path $solutionRoot 'backend/src/AvenChart.Api/Features/Telehealth/TelehealthApplicantAllergyInformationRepository.cs')
    $allergyInformationServiceSource = Get-Content -Raw (Join-Path $solutionRoot 'backend/src/AvenChart.Api/Features/Telehealth/TelehealthApplicantAllergyInformationService.cs')
    $allergyInformationPolicySource = Get-Content -Raw (Join-Path $solutionRoot 'backend/src/AvenChart.Api/Features/Telehealth/TelehealthApplicantAllergyInformationPolicy.cs')
    $allergyInformationMigrationSource = Get-Content -Raw (Join-Path $solutionRoot 'database/migrations/V0308__telehealth_applicant_allergy_information.sql')
    Add-Check 'Synthetic allergy information is applicant-owned, inventory-and-medication-bound, fixed-catalog, acknowledgment-gated, server-routed, and cannot create canonical allergy, confirmed negation, reaction, criticality, reconciliation, review, intake, request, queue, prescribing, or care capability' (
        $allergyInformationRepositorySource -match 'SyntheticMedicationInformationRecorded' -and
        $allergyInformationRepositorySource -match 'CanonicalMedicationCount' -and
        $allergyInformationRepositorySource -match 'CanonicalPrescriptionCount' -and
        $allergyInformationRepositorySource -match 'CanonicalAllergyCount' -and
        $allergyInformationRepositorySource -notmatch '(?i)insert\s+into\s+(patients|medications|prescriptions|allergies|problems|diagnoses|patient_portal_accounts|insurance_records|telehealth_requests|telehealth_queue_entries|telehealth_intake_snapshots|appointments|encounters|claims|billing|messages|integration_outbox)' -and
        $allergyInformationRepositorySource -notmatch 'HttpClient|ClientWebSocket|SmtpClient|HubConnection|WebRequest|SendAsync' -and
        $allergyInformationServiceSource -match 'AllergyIntoleranceCreated: false' -and
        $allergyInformationServiceSource -match 'AllergyListReconciled: false' -and
        $allergyInformationServiceSource -match 'ReactionAssessed: false' -and
        $allergyInformationServiceSource -match 'CriticalityAssessed: false' -and
        $allergyInformationServiceSource -match 'ContraindicationCheckPerformed: false' -and
        $allergyInformationServiceSource -match 'ClinicianReviewCreated: false' -and
        $allergyInformationServiceSource -match 'ClinicalIntakeCompleted: false' -and
        $allergyInformationServiceSource -match 'PatientRecordChanged: false' -and
        $allergyInformationServiceSource -match 'RequestCreated: false' -and
        $allergyInformationServiceSource -match 'QueueEntered: false' -and
        $allergyInformationServiceSource -match 'CareAuthorized: false' -and
        $allergyInformationServiceSource -match 'PrescribingEnabled: false' -and
        $allergyInformationPolicySource -match 'LOCAL_SYNTHETIC_ONLY' -and
        $allergyInformationPolicySource -match 'SnomedCtMapped: false' -and
        $allergyInformationPolicySource -match 'RxNormMapped: false' -and
        $allergyInformationPolicySource -match 'AdditionalAllergyCollectionRequired' -and
        $allergyInformationPolicySource -match 'ClinicianAllergyReviewRequired' -and
        $allergyInformationPolicySource -match 'AssistedAllergyReviewRequired' -and
        $allergyInformationPolicySource -match 'PendingClinicianConfirmationOfPatientReportedNone' -and
        $allergyInformationMigrationSource -match 'enforce_telehealth_allergy_information_item_count' -and
        $allergyInformationMigrationSource -match 'deferrable initially deferred' -and
        $allergyInformationMigrationSource -notmatch '(?im)^\s+(reaction|manifestation|allergy_type|clinical_status|verification_status|severity|criticality|onset|occurrence|note|attachment|free_text|snomed_code|rxnorm_code)\s+' -and
        $allergyInformationMigrationSource -notmatch '(?im)^\s*(drop\s+table|truncate\s+|delete\s+from)' -and
        $endpointSource -match '\{applicantId:guid\}/allergy-information')
    $healthHistoryInformationRepositorySource = Get-Content -Raw (Join-Path $solutionRoot 'backend/src/AvenChart.Api/Features/Telehealth/TelehealthApplicantHealthHistoryInformationRepository.cs')
    $healthHistoryInformationServiceSource = Get-Content -Raw (Join-Path $solutionRoot 'backend/src/AvenChart.Api/Features/Telehealth/TelehealthApplicantHealthHistoryInformationService.cs')
    $healthHistoryInformationPolicySource = Get-Content -Raw (Join-Path $solutionRoot 'backend/src/AvenChart.Api/Features/Telehealth/TelehealthApplicantHealthHistoryInformationPolicy.cs')
    $healthHistoryInformationMigrationSource = Get-Content -Raw (Join-Path $solutionRoot 'database/migrations/V0309__telehealth_applicant_health_history_information.sql')
    Add-Check 'Synthetic health-history topics are applicant-owned, allergy-and-medication-bound, fixed-catalog, acknowledgment-gated, server-routed, and cannot create findings, risk decisions, reconciliation, review, intake, request, queue, prescribing, or care capability' (
        $healthHistoryInformationRepositorySource -match 'SyntheticAllergyInformationRecorded' -and
        $healthHistoryInformationRepositorySource -match 'CanonicalMedicationCount' -and
        $healthHistoryInformationRepositorySource -match 'CanonicalPrescriptionCount' -and
        $healthHistoryInformationRepositorySource -match 'CanonicalAllergyCount' -and
        $healthHistoryInformationRepositorySource -match 'CanonicalProblemCount' -and
        $healthHistoryInformationRepositorySource -notmatch '(?i)insert\s+into\s+(patients|medications|prescriptions|allergies|problems|diagnoses|patient_portal_accounts|insurance_records|telehealth_requests|telehealth_queue_entries|telehealth_intake_snapshots|appointments|encounters|claims|billing|messages|integration_outbox)' -and
        $healthHistoryInformationRepositorySource -notmatch 'HttpClient|ClientWebSocket|SmtpClient|HubConnection|WebRequest|SendAsync' -and
        $healthHistoryInformationServiceSource -match 'ConditionCreated: false' -and
        $healthHistoryInformationServiceSource -match 'ProcedureCreated: false' -and
        $healthHistoryInformationServiceSource -match 'ObservationCreated: false' -and
        $healthHistoryInformationServiceSource -match 'FamilyMemberHistoryCreated: false' -and
        $healthHistoryInformationServiceSource -match 'QuestionnaireResponseCreated: false' -and
        $healthHistoryInformationServiceSource -match 'HealthHistoryReconciled: false' -and
        $healthHistoryInformationServiceSource -match 'RiskModifierEvaluated: false' -and
        $healthHistoryInformationServiceSource -match 'ClinicalTriageChanged: false' -and
        $healthHistoryInformationServiceSource -match 'ClinicianReviewCreated: false' -and
        $healthHistoryInformationServiceSource -match 'ClinicalIntakeCompleted: false' -and
        $healthHistoryInformationServiceSource -match 'PatientRecordChanged: false' -and
        $healthHistoryInformationServiceSource -match 'RequestCreated: false' -and
        $healthHistoryInformationServiceSource -match 'QueueEntered: false' -and
        $healthHistoryInformationServiceSource -match 'CareAuthorized: false' -and
        $healthHistoryInformationServiceSource -match 'PrescribingEnabled: false' -and
        $healthHistoryInformationPolicySource -match 'LOCAL_SYNTHETIC_ONLY' -and
        $healthHistoryInformationPolicySource -match 'SnomedCtMapped: false' -and
        $healthHistoryInformationPolicySource -match 'Icd10CmMapped: false' -and
        $healthHistoryInformationPolicySource -match 'LoincMapped: false' -and
        $healthHistoryInformationPolicySource -match 'AdditionalHealthHistoryCollectionRequired' -and
        $healthHistoryInformationPolicySource -match 'ClinicianHealthHistoryReviewRequired' -and
        $healthHistoryInformationPolicySource -match 'AssistedHealthHistoryReviewRequired' -and
        $healthHistoryInformationPolicySource -match 'PendingClinicianConfirmationOfPatientReportedNone' -and
        $healthHistoryInformationMigrationSource -match 'enforce_telehealth_health_history_information_topic_count' -and
        $healthHistoryInformationMigrationSource -match 'deferrable initially deferred' -and
        $healthHistoryInformationMigrationSource -notmatch '(?im)^\s+(diagnosis|symptom|procedure_date|clinical_status|verification_status|severity|onset|occurrence|note|attachment|free_text|snomed_code|icd10_code|loinc_code)\s+' -and
        $healthHistoryInformationMigrationSource -notmatch '(?im)^\s*(drop\s+table|truncate\s+|delete\s+from)' -and
        $endpointSource -match '\{applicantId:guid\}/health-history-information')
    $clinicalInformationSummaryRepositorySource = Get-Content -Raw (Join-Path $solutionRoot 'backend/src/AvenChart.Api/Features/Telehealth/TelehealthApplicantClinicalInformationSummaryRepository.cs')
    $clinicalInformationSummaryServiceSource = Get-Content -Raw (Join-Path $solutionRoot 'backend/src/AvenChart.Api/Features/Telehealth/TelehealthApplicantClinicalInformationSummaryService.cs')
    $clinicalInformationSummaryPolicySource = Get-Content -Raw (Join-Path $solutionRoot 'backend/src/AvenChart.Api/Features/Telehealth/TelehealthApplicantClinicalInformationSummaryPolicy.cs')
    $clinicalInformationSummaryMigrationSource = Get-Content -Raw (Join-Path $solutionRoot 'database/migrations/V0310__telehealth_applicant_clinical_information_summary.sql')
    Add-Check 'Synthetic clinical-information summary is applicant-owned, exact-source-bound, no-edit, acknowledgment-gated, server-routed, minimized, and cannot create reconciliation, intake, eligibility, review, request, queue, prescribing, or care capability' (
        $clinicalInformationSummaryRepositorySource -match 'SyntheticHealthHistoryInformationRecorded' -and
        $clinicalInformationSummaryRepositorySource -match 'CanonicalMedicationCount' -and
        $clinicalInformationSummaryRepositorySource -match 'CanonicalPrescriptionCount' -and
        $clinicalInformationSummaryRepositorySource -match 'CanonicalAllergyCount' -and
        $clinicalInformationSummaryRepositorySource -match 'CanonicalProblemCount' -and
        $clinicalInformationSummaryRepositorySource -notmatch '(?i)insert\s+into\s+(patients|medications|prescriptions|allergies|problems|diagnoses|patient_portal_accounts|insurance_records|telehealth_requests|telehealth_queue_entries|telehealth_intake_snapshots|appointments|encounters|claims|billing|messages|integration_outbox)' -and
        $clinicalInformationSummaryRepositorySource -notmatch 'HttpClient|ClientWebSocket|SmtpClient|HubConnection|WebRequest|SendAsync' -and
        $clinicalInformationSummaryServiceSource -match 'QuestionnaireResponseCreated: false' -and
        $clinicalInformationSummaryServiceSource -match 'MedicationListReconciled: false' -and
        $clinicalInformationSummaryServiceSource -match 'AllergyListReconciled: false' -and
        $clinicalInformationSummaryServiceSource -match 'HealthHistoryReconciled: false' -and
        $clinicalInformationSummaryServiceSource -match 'ConfirmedNegativeEstablished: false' -and
        $clinicalInformationSummaryServiceSource -match 'ClinicianReviewCreated: false' -and
        $clinicalInformationSummaryServiceSource -match 'ClinicalIntakeCompleted: false' -and
        $clinicalInformationSummaryServiceSource -match 'ClinicalEligibilityEstablished: false' -and
        $clinicalInformationSummaryServiceSource -match 'ClinicalTriageChanged: false' -and
        $clinicalInformationSummaryServiceSource -match 'PatientRecordChanged: false' -and
        $clinicalInformationSummaryServiceSource -match 'PracticeAccepted: false' -and
        $clinicalInformationSummaryServiceSource -match 'RequestCreated: false' -and
        $clinicalInformationSummaryServiceSource -match 'QueueEntered: false' -and
        $clinicalInformationSummaryServiceSource -match 'CareAuthorized: false' -and
        $clinicalInformationSummaryServiceSource -match 'PrescribingEnabled: false' -and
        $clinicalInformationSummaryPolicySource -match 'SYNTHETIC_APPLICANT_CLINICAL_INFORMATION_SUMMARY' -and
        $clinicalInformationSummaryPolicySource -match 'PatientReportedMayBeIncompleteAcknowledged' -and
        $clinicalInformationSummaryPolicySource -match 'CorrectionRequiresSeparateWorkflowAcknowledged' -and
        $clinicalInformationSummaryPolicySource -match 'AdditionalClinicalInformationCollectionRequired' -and
        $clinicalInformationSummaryPolicySource -match 'AssistedClinicalInformationReviewRequired' -and
        $clinicalInformationSummaryPolicySource -match 'ClinicianClinicalInformationReviewRequired' -and
        $clinicalInformationSummaryPolicySource -match 'PendingClinicianReconciliationOfPatientReportedNone' -and
        $clinicalInformationSummaryMigrationSource -match 'enforce_telehealth_applicant_clinical_information_summary' -and
        $clinicalInformationSummaryMigrationSource -match 'not questionnaire_response_created' -and
        $clinicalInformationSummaryMigrationSource -notmatch '(?im)^\s+(legal_name|date_of_birth|email|phone|address|member_id|payer|diagnosis|symptom|dose|reaction|note|attachment|free_text)\s+' -and
        $clinicalInformationSummaryMigrationSource -notmatch '(?im)^\s*(drop\s+table|truncate\s+|delete\s+from)' -and
        $endpointSource -match '\{applicantId:guid\}/clinical-information-summary')
    $preRequestReadinessRepositorySource = Get-Content -Raw (Join-Path $solutionRoot 'backend/src/AvenChart.Api/Features/Telehealth/TelehealthApplicantPreRequestReadinessRepository.cs')
    $preRequestReadinessServiceSource = Get-Content -Raw (Join-Path $solutionRoot 'backend/src/AvenChart.Api/Features/Telehealth/TelehealthApplicantPreRequestReadinessService.cs')
    $preRequestReadinessPolicySource = Get-Content -Raw (Join-Path $solutionRoot 'backend/src/AvenChart.Api/Features/Telehealth/TelehealthApplicantPreRequestReadinessPolicy.cs')
    $preRequestReadinessMigrationSource = Get-Content -Raw (Join-Path $solutionRoot 'database/migrations/V0311__telehealth_applicant_pre_request_readiness.sql')
    Add-Check 'Synthetic pre-request readiness is applicant-owned, exact-source-bound, five-section minimized, acknowledgment-gated, server-routed, and cannot create assurance, support fulfillment, task, acceptance, request, queue, financial, integration, or care capability' (
        $preRequestReadinessRepositorySource -match 'SyntheticClinicalInformationSummaryConfirmed' -and
        $preRequestReadinessRepositorySource -match 'CanonicalInsuranceCount' -and
        $preRequestReadinessRepositorySource -match 'CanonicalMedicationCount' -and
        $preRequestReadinessRepositorySource -match 'CanonicalPrescriptionCount' -and
        $preRequestReadinessRepositorySource -match 'CanonicalAllergyCount' -and
        $preRequestReadinessRepositorySource -match 'CanonicalProblemCount' -and
        $preRequestReadinessRepositorySource -notmatch '(?i)insert\s+into\s+(patients|medications|prescriptions|allergies|problems|diagnoses|patient_portal_accounts|insurance_records|telehealth_requests|telehealth_queue_entries|telehealth_intake_snapshots|appointments|encounters|claims|billing|messages|integration_outbox)' -and
        $preRequestReadinessRepositorySource -notmatch 'HttpClient|ClientWebSocket|SmtpClient|HubConnection|WebRequest|SendAsync' -and
        $preRequestReadinessServiceSource -match 'new\("Registration", "ReceiptRecorded"' -and
        $preRequestReadinessServiceSource -match 'new\("Insurance", "ReceiptRecorded"' -and
        $preRequestReadinessServiceSource -match 'new\(\s*"CommunicationAccess",' -and
        $preRequestReadinessServiceSource -match 'new\("DevicePreparation",' -and
        $preRequestReadinessServiceSource -match 'new\("ClinicalInformation",' -and
        $preRequestReadinessServiceSource -match 'IdentityAssuranceEstablished: false' -and
        $preRequestReadinessServiceSource -match 'CoverageGuaranteed: false' -and
        $preRequestReadinessServiceSource -match 'InterpreterOrAccommodationArranged: false' -and
        $preRequestReadinessServiceSource -match 'TechnologyReady: false' -and
        $preRequestReadinessServiceSource -match 'ClinicalInformationReconciled: false' -and
        $preRequestReadinessServiceSource -match 'StaffReviewCreated: false' -and
        $preRequestReadinessServiceSource -match 'ClinicianReviewCreated: false' -and
        $preRequestReadinessServiceSource -match 'PracticeAccepted: false' -and
        $preRequestReadinessServiceSource -match 'RequestCreated: false' -and
        $preRequestReadinessServiceSource -match 'QueueEntered: false' -and
        $preRequestReadinessServiceSource -match 'BillingEnabled: false' -and
        $preRequestReadinessServiceSource -match 'ClaimCreated: false' -and
        $preRequestReadinessServiceSource -match 'IntegrationEnabled: false' -and
        $preRequestReadinessServiceSource -match 'ExternalCallPerformed: false' -and
        $preRequestReadinessPolicySource -match 'SYNTHETIC_APPLICANT_PRE_REQUEST_READINESS' -and
        $preRequestReadinessPolicySource -match 'AdditionalClinicalInformationRequired' -and
        $preRequestReadinessPolicySource -match 'AssistedPreRequestSupportRequired' -and
        $preRequestReadinessPolicySource -match 'PendingPracticePreRequestReview' -and
        $preRequestReadinessMigrationSource -match 'enforce_telehealth_applicant_pre_request_readiness' -and
        $preRequestReadinessMigrationSource -match 'not identity_assurance_established' -and
        $preRequestReadinessMigrationSource -match 'not external_call_performed' -and
        $preRequestReadinessMigrationSource -notmatch '(?im)^\s+(legal_name|date_of_birth|email|phone|address|language|callback|member_id|payer|diagnosis|symptom|dose|reaction|note|attachment|free_text)\s+' -and
        $preRequestReadinessMigrationSource -notmatch '(?im)^\s*(drop\s+table|truncate\s+|delete\s+from)' -and
        $endpointSource -match '\{applicantId:guid\}/pre-request-readiness')
    $practiceReviewServiceSource = Get-Content -Raw (Join-Path $solutionRoot 'backend/src/AvenChart.Api/Features/Telehealth/TelehealthApplicantPracticeReviewSubmissionService.cs')
    $practiceReviewPolicySource = Get-Content -Raw (Join-Path $solutionRoot 'backend/src/AvenChart.Api/Features/Telehealth/TelehealthApplicantPracticeReviewSubmissionPolicy.cs')
    $practiceReviewMigrationSource = Get-Content -Raw (Join-Path $solutionRoot 'database/migrations/V0312__telehealth_applicant_practice_review_submission.sql')
    Add-Check 'Synthetic practice-review submission creates exactly one staff work item while request, patient/clinician queue, care, financial, integration, and external gates remain closed' (
        $preRequestReadinessRepositorySource -match 'SubmitPracticeReviewAsync' -and
        $preRequestReadinessRepositorySource -match 'telehealth_prospective_practice_review_cases' -and
        $preRequestReadinessRepositorySource -match 'telehealth_applicant_practice_review_submissions' -and
        $preRequestReadinessRepositorySource -notmatch '(?i)insert\s+into\s+(patients|medications|prescriptions|allergies|problems|diagnoses|patient_portal_accounts|insurance_records|telehealth_requests|telehealth_queue_entries|appointments|encounters|claims|billing|messages|integration_outbox)' -and
        $preRequestReadinessRepositorySource -notmatch 'HttpClient|ClientWebSocket|SmtpClient|HubConnection|WebRequest|SendAsync' -and
        $practiceReviewServiceSource -match 'StaffReviewCreated: submitted' -and
        $practiceReviewServiceSource -match 'TelehealthRequestCreated: false' -and
        $practiceReviewServiceSource -match 'PatientCareQueueEntered: false' -and
        $practiceReviewServiceSource -match 'ClinicianQueueEntered: false' -and
        $practiceReviewServiceSource -match 'PracticeAccepted: false' -and
        $practiceReviewServiceSource -match 'CareAuthorized: false' -and
        $practiceReviewServiceSource -match 'ExternalCallPerformed: false' -and
        $practiceReviewPolicySource -match 'SYNTHETIC_APPLICANT_PRACTICE_REVIEW_SUBMISSION' -and
        $practiceReviewPolicySource -match 'SyntheticPreRequestReadinessAcknowledged' -and
        $practiceReviewPolicySource -match 'SyntheticPracticeReviewSubmitted' -and
        $practiceReviewPolicySource -match 'PendingPracticeReview' -and
        $practiceReviewMigrationSource -match 'enforce_telehealth_applicant_practice_review_submission' -and
        $practiceReviewMigrationSource -match 'staff_review_created' -and
        $practiceReviewMigrationSource -match 'not telehealth_request_created' -and
        $practiceReviewMigrationSource -match 'not patient_care_queue_entered' -and
        $practiceReviewMigrationSource -match 'not clinician_queue_entered' -and
        $practiceReviewMigrationSource -notmatch '(?im)^\s+(legal_name|date_of_birth|email|phone|address|member_id|payer|diagnosis|symptom|dose|reaction|note|attachment|free_text|priority|assigned_to|queue_position|doctor_id)\s+' -and
        $practiceReviewMigrationSource -notmatch '(?im)^\s*(drop\s+table|truncate\s+|delete\s+from)' -and
        $endpointSource -match '\{applicantId:guid\}/practice-review-submission')
    $practiceReviewInboxRepositorySource = Get-Content -Raw (Join-Path $solutionRoot 'backend/src/AvenChart.Api/Features/Telehealth/TelehealthApplicantPracticeReviewInboxRepository.cs')
    $practiceReviewInboxServiceSource = Get-Content -Raw (Join-Path $solutionRoot 'backend/src/AvenChart.Api/Features/Telehealth/TelehealthApplicantPracticeReviewInboxService.cs')
    $practiceReviewInboxPolicySource = Get-Content -Raw (Join-Path $solutionRoot 'backend/src/AvenChart.Api/Features/Telehealth/TelehealthApplicantPracticeReviewInboxPolicy.cs')
    $practiceReviewInboxUiSource = Get-Content -Raw (Join-Path $repositoryRoot 'avenchart-ui/src/features/telehealth/AdminTelehealthQueue.tsx')
    Add-Check 'Practice-review inbox is a bounded PHI-audited read model with masking and only an opaque active-claim projection' (
        $practiceReviewInboxRepositorySource -match 'ListAsync' -and
        $practiceReviewInboxRepositorySource -match 'PendingPracticeReview' -and
        $practiceReviewInboxRepositorySource -match 'limit 100' -and
        $practiceReviewInboxRepositorySource -notmatch '(?i)\b(insert\s+into|update\s+|delete\s+from|truncate\s+|for\s+update)\b' -and
        $practiceReviewInboxRepositorySource -notmatch 'HttpClient|ClientWebSocket|SmtpClient|HubConnection|WebRequest|SendAsync' -and
        $practiceReviewInboxServiceSource -match 'TelehealthAuthorizationPolicy\.IsAdministratorRole' -and
        $practiceReviewInboxServiceSource -match 'MaskEmail' -and
        $practiceReviewInboxServiceSource -match 'MaskPhone' -and
        $practiceReviewInboxServiceSource -match 'StaffActionTaken: assigned' -and
        $practiceReviewInboxServiceSource -match 'AssignedToCurrentUser:' -and
        $practiceReviewInboxServiceSource -match 'TelehealthRequestCreated: false' -and
        $practiceReviewInboxServiceSource -match 'CareAuthorized: false' -and
        $practiceReviewInboxServiceSource -match 'ExternalCallPerformed: false' -and
        $practiceReviewInboxPolicySource -match 'SYNTHETIC_ADMIN_PRACTICE_REVIEW_INBOX' -and
        $practiceReviewInboxPolicySource -match 'MaximumItems = 100' -and
        $endpointSource -match 'MapGet\("/applicant-practice-review"' -and
        $endpointSource -match 'TelehealthApplicantPracticeReviewInbox", "queue"' -and
        $practiceReviewInboxUiSource -match 'Pending practice review' -and
        $practiceReviewInboxUiSource -match 'Read-only operational awareness' -and
        $practiceReviewInboxUiSource -notmatch 'acceptApplicant|declineApplicant|assignApplicant|contactApplicant|createTelehealthRequest')
    $practiceReviewClaimRepositorySource = Get-Content -Raw (Join-Path $solutionRoot 'backend/src/AvenChart.Api/Features/Telehealth/TelehealthApplicantPracticeReviewClaimRepository.cs')
    $practiceReviewClaimServiceSource = Get-Content -Raw (Join-Path $solutionRoot 'backend/src/AvenChart.Api/Features/Telehealth/TelehealthApplicantPracticeReviewClaimService.cs')
    $practiceReviewClaimPolicySource = Get-Content -Raw (Join-Path $solutionRoot 'backend/src/AvenChart.Api/Features/Telehealth/TelehealthApplicantPracticeReviewClaimPolicy.cs')
    $practiceReviewClaimMigrationSource = Get-Content -Raw (Join-Path $solutionRoot 'database/migrations/V0313__telehealth_practice_review_claim.sql')
    Add-Check 'Practice-review claim is a short immutable duplicate-work lease with no priority, decision, request, care, financial, integration, or external consequence' (
        $practiceReviewClaimRepositorySource -match 'for update of c' -and
        $practiceReviewClaimRepositorySource -match "now\(\)\+interval '120 seconds'" -and
        $practiceReviewClaimRepositorySource -notmatch 'HttpClient|ClientWebSocket|SmtpClient|HubConnection|WebRequest|SendAsync' -and
        $practiceReviewClaimServiceSource -match 'StaffActionTaken: true' -and
        $practiceReviewClaimServiceSource -match 'PriorityAssigned: false' -and
        $practiceReviewClaimServiceSource -match 'PracticeAccepted: false' -and
        $practiceReviewClaimServiceSource -match 'TelehealthRequestCreated: false' -and
        $practiceReviewClaimServiceSource -match 'CareAuthorized: false' -and
        $practiceReviewClaimServiceSource -match 'ExternalCallPerformed: false' -and
        $practiceReviewClaimPolicySource -match 'SYNTHETIC_ADMIN_PRACTICE_REVIEW_CLAIM' -and
        $practiceReviewClaimPolicySource -match 'LeaseSeconds = 120' -and
        $practiceReviewClaimMigrationSource -match 'enforce_telehealth_practice_review_claim' -and
        $practiceReviewClaimMigrationSource -match 'reject_telehealth_evidence_mutation' -and
        $practiceReviewClaimMigrationSource -notmatch '(?im)^\s*(drop\s+table|truncate\s+|delete\s+from)' -and
        $endpointSource -match 'applicant-practice-review/\{practiceReviewCaseId:guid\}/claim')
    $practiceReviewPacketRepositorySource = Get-Content -Raw (Join-Path $solutionRoot 'backend/src/AvenChart.Api/Features/Telehealth/TelehealthApplicantPracticeReviewPacketRepository.cs')
    $practiceReviewPacketServiceSource = Get-Content -Raw (Join-Path $solutionRoot 'backend/src/AvenChart.Api/Features/Telehealth/TelehealthApplicantPracticeReviewPacketService.cs')
    $practiceReviewPacketPolicySource = Get-Content -Raw (Join-Path $solutionRoot 'backend/src/AvenChart.Api/Features/Telehealth/TelehealthApplicantPracticeReviewPacketPolicy.cs')
    Add-Check 'Claimant-bound practice-review packet is a PHI-audited masked read with no lease, decision, chart, request, care, financial, integration, or external mutation path' (
        $practiceReviewPacketRepositorySource -match 'assigned_to_actor_id=@actorId' -and
        $practiceReviewPacketRepositorySource -match 'lease_expires_at>now\(\)' -and
        $practiceReviewPacketRepositorySource -match 'rendering_physician_network_checked' -and
        $practiceReviewPacketRepositorySource -notmatch '(?i)\b(insert\s+into|update\s+|delete\s+from|truncate\s+|for\s+update)\b' -and
        $practiceReviewPacketRepositorySource -notmatch 'HttpClient|ClientWebSocket|SmtpClient|HubConnection|WebRequest|SendAsync' -and
        $practiceReviewPacketServiceSource -match 'MaskEmail' -and
        $practiceReviewPacketServiceSource -match 'MaskPhone' -and
        $practiceReviewPacketServiceSource -match 'RenderingPhysicianNetworkChecked' -and
        $practiceReviewPacketServiceSource -match 'PriorityAssigned: false' -and
        $practiceReviewPacketServiceSource -match 'PracticeAccepted: false' -and
        $practiceReviewPacketServiceSource -match 'PatientContacted: false' -and
        $practiceReviewPacketServiceSource -match 'TelehealthRequestCreated: false' -and
        $practiceReviewPacketServiceSource -match 'CareAuthorized: false' -and
        $practiceReviewPacketServiceSource -match 'ExternalCallPerformed: false' -and
        $practiceReviewPacketPolicySource -match 'SYNTHETIC_ADMIN_PRACTICE_REVIEW_PACKET' -and
        $endpointSource -match 'MapGet\("/applicant-practice-review/\{practiceReviewCaseId:guid\}"' -and
        $endpointSource -match 'TelehealthApplicantPracticeReviewPacket')
    $practiceReviewAuthorizationRepositorySource = Get-Content -Raw (Join-Path $solutionRoot 'backend/src/AvenChart.Api/Features/Telehealth/TelehealthApplicantPracticeReviewAuthorizationRepository.cs')
    $practiceReviewAuthorizationServiceSource = Get-Content -Raw (Join-Path $solutionRoot 'backend/src/AvenChart.Api/Features/Telehealth/TelehealthApplicantPracticeReviewAuthorizationService.cs')
    $practiceReviewAuthorizationPolicySource = Get-Content -Raw (Join-Path $solutionRoot 'backend/src/AvenChart.Api/Features/Telehealth/TelehealthApplicantPracticeReviewAuthorizationPolicy.cs')
    $practiceReviewAuthorizationMigrationSource = Get-Content -Raw (Join-Path $solutionRoot 'database/migrations/V0314__telehealth_practice_review_authorization.sql')
    Add-Check 'Practice-review authorization is claimant-bound, positive-only, immutable, and creates no request, queue, contact, care, financial, integration, or external path' (
        $practiceReviewAuthorizationRepositorySource -match 'assigned_to_actor_id=@actorId' -and
        $practiceReviewAuthorizationRepositorySource -match 'lease_expires_at>now\(\)' -and
        $practiceReviewAuthorizationRepositorySource -match "status='SyntheticPracticeReviewAuthorized'" -and
        $practiceReviewAuthorizationRepositorySource -match 'telehealth_practice_review_authorizations' -and
        $practiceReviewAuthorizationRepositorySource -notmatch 'HttpClient|ClientWebSocket|SmtpClient|HubConnection|WebRequest|SendAsync' -and
        $practiceReviewAuthorizationServiceSource -match 'RequestCreationAuthorized: true' -and
        $practiceReviewAuthorizationServiceSource -match 'PracticeAccepted: false' -and
        $practiceReviewAuthorizationServiceSource -match 'PatientContacted: false' -and
        $practiceReviewAuthorizationServiceSource -match 'TelehealthRequestCreated: false' -and
        $practiceReviewAuthorizationServiceSource -match 'PatientCareQueueEntered: false' -and
        $practiceReviewAuthorizationServiceSource -match 'CareAuthorized: false' -and
        $practiceReviewAuthorizationServiceSource -match 'ExternalCallPerformed: false' -and
        $practiceReviewAuthorizationPolicySource -match 'SYNTHETIC_ADMIN_PRACTICE_REVIEW_AUTHORIZATION' -and
        $practiceReviewAuthorizationPolicySource -match 'AuthorizedForSyntheticRequestCreation' -and
        $practiceReviewAuthorizationMigrationSource -match 'enforce_telehealth_practice_review_authorization' -and
        $practiceReviewAuthorizationMigrationSource -match 'reject_telehealth_evidence_mutation' -and
        $practiceReviewAuthorizationMigrationSource -notmatch '(?im)^\s*(drop\s+table|truncate\s+|delete\s+from)' -and
        $endpointSource -match 'applicant-practice-review/\{practiceReviewCaseId:guid\}/authorization')
    $applicantRequestCreationRepositorySource = Get-Content -Raw (Join-Path $solutionRoot 'backend/src/AvenChart.Api/Features/Telehealth/TelehealthApplicantRequestCreationRepository.cs')
    $applicantRequestCreationServiceSource = Get-Content -Raw (Join-Path $solutionRoot 'backend/src/AvenChart.Api/Features/Telehealth/TelehealthApplicantRequestCreationService.cs')
    $applicantRequestCreationPolicySource = Get-Content -Raw (Join-Path $solutionRoot 'backend/src/AvenChart.Api/Features/Telehealth/TelehealthApplicantRequestCreationPolicy.cs')
    $applicantRequestCreationMigrationSource = Get-Content -Raw (Join-Path $solutionRoot 'database/migrations/V0315__telehealth_applicant_request_creation.sql')
    Add-Check 'Applicant request creation is access-key and authorization bound, creates one Draft shell, and exposes no queue, care, financial, integration, or external path' (
        $applicantRequestCreationRepositorySource -match "status='SyntheticPracticeReviewAuthorized'" -and
        $applicantRequestCreationRepositorySource -match "'Draft'" -and
        $applicantRequestCreationRepositorySource -match 'telehealth_applicant_request_creations' -and
        $applicantRequestCreationRepositorySource -match 'source_practice_review_authorization_id' -and
        $applicantRequestCreationRepositorySource -notmatch 'HttpClient|ClientWebSocket|SmtpClient|HubConnection|WebRequest|SendAsync' -and
        $applicantRequestCreationServiceSource -match 'not searching for a doctor' -and
        $applicantRequestCreationServiceSource -match 'patient or clinician care queue' -and
        $applicantRequestCreationServiceSource -match 'No contact, doctor search, queue position' -and
        $applicantRequestCreationPolicySource -match 'SYNTHETIC_APPLICANT_TELEHEALTH_REQUEST_CREATION' -and
        $applicantRequestCreationPolicySource -match 'SyntheticRequestCreated' -and
        $applicantRequestCreationMigrationSource -match 'enforce_telehealth_applicant_request_creation' -and
        $applicantRequestCreationMigrationSource -match 'protect_telehealth_request_applicant_provenance' -and
        $applicantRequestCreationMigrationSource -notmatch '(?im)^\s*(drop\s+table|truncate\s+|delete\s+from)' -and
        $endpointSource -match '/\{applicantId:guid\}/telehealth-request')
    $applicantRequestLocationRepositorySource = Get-Content -Raw (Join-Path $solutionRoot 'backend/src/AvenChart.Api/Features/Telehealth/TelehealthApplicantRequestLocationRepository.cs')
    $applicantRequestLocationServiceSource = Get-Content -Raw (Join-Path $solutionRoot 'backend/src/AvenChart.Api/Features/Telehealth/TelehealthApplicantRequestLocationService.cs')
    $applicantRequestLocationPolicySource = Get-Content -Raw (Join-Path $solutionRoot 'backend/src/AvenChart.Api/Features/Telehealth/TelehealthApplicantRequestLocationPolicy.cs')
    $applicantRequestLocationMigrationSource = Get-Content -Raw (Join-Path $solutionRoot 'database/migrations/V0316__telehealth_applicant_request_location_confirmation.sql')
    Add-Check 'Applicant request location confirmation is access-key, Draft, state, callback, and snapshot bound with no triage, queue, care, financial, integration, or external path' (
        $applicantRequestLocationRepositorySource -match "status='LocationConfirmed'" -and
        $applicantRequestLocationRepositorySource -match 'telehealth_patient_locations' -and
        $applicantRequestLocationRepositorySource -match 'telehealth_applicant_request_location_confirmations' -and
        $applicantRequestLocationRepositorySource -match 'CurrentLocationStateCode' -and
        $applicantRequestLocationRepositorySource -notmatch 'HttpClient|ClientWebSocket|SmtpClient|HubConnection|WebRequest|SendAsync' -and
        $applicantRequestLocationServiceSource -match 'TriageAssessmentCreated: false' -and
        $applicantRequestLocationServiceSource -match 'PatientCareQueueEntered: false' -and
        $applicantRequestLocationServiceSource -match 'CareAuthorized: false' -and
        $applicantRequestLocationServiceSource -match 'ExternalCallPerformed: false' -and
        $applicantRequestLocationPolicySource -match 'SYNTHETIC_APPLICANT_REQUEST_LOCATION_CONFIRMATION' -and
        $applicantRequestLocationPolicySource -match 'ResultingRequestStatus\s*=\s*"LocationConfirmed"' -and
        $applicantRequestLocationMigrationSource -match 'enforce_telehealth_applicant_request_location_confirmation' -and
        $applicantRequestLocationMigrationSource -match 'reject_telehealth_evidence_mutation' -and
        $applicantRequestLocationMigrationSource -notmatch '(?im)^\s*(drop\s+table|truncate\s+|delete\s+from)' -and
        $endpointSource -match '/\{applicantId:guid\}/telehealth-request/location')
    $applicantRequestSafetyRepositorySource = Get-Content -Raw (Join-Path $solutionRoot 'backend/src/AvenChart.Api/Features/Telehealth/TelehealthApplicantRequestUniversalSafetyRepository.cs')
    $applicantRequestSafetyServiceSource = Get-Content -Raw (Join-Path $solutionRoot 'backend/src/AvenChart.Api/Features/Telehealth/TelehealthApplicantRequestUniversalSafetyService.cs')
    $applicantRequestSafetyPolicySource = Get-Content -Raw (Join-Path $solutionRoot 'backend/src/AvenChart.Api/Features/Telehealth/TelehealthApplicantRequestUniversalSafetyPolicy.cs')
    $applicantRequestSafetyMigrationSource = Get-Content -Raw (Join-Path $solutionRoot 'database/migrations/V0317__telehealth_applicant_request_universal_safety_assessment.sql')
    Add-Check 'Applicant request universal safety is access-key, location, snapshot, fixture, and explicit-answer bound with no review work item, queue, care, financial, integration, or external path' (
        $applicantRequestSafetyRepositorySource -match "EntryRequestStatus" -and
        $applicantRequestSafetyRepositorySource -match 'telehealth_applicant_request_universal_safety_assessments' -and
        $applicantRequestSafetyRepositorySource -match 'telehealth_triage_assessments' -and
        $applicantRequestSafetyRepositorySource -notmatch 'HttpClient|ClientWebSocket|SmtpClient|HubConnection|WebRequest|SendAsync' -and
        $applicantRequestSafetyServiceSource -match 'ComplaintSpecificTriageCreated: false' -and
        $applicantRequestSafetyServiceSource -match 'ClinicalReviewCreated: false' -and
        $applicantRequestSafetyServiceSource -match 'PatientCareQueueEntered: false' -and
        $applicantRequestSafetyServiceSource -match 'CareAuthorized: false' -and
        $applicantRequestSafetyServiceSource -match 'ExternalCallPerformed: false' -and
        $applicantRequestSafetyPolicySource -match 'SYNTHETIC_APPLICANT_REQUEST_UNIVERSAL_SAFETY_ASSESSMENT' -and
        $applicantRequestSafetyPolicySource -match 'Complaint-specific triage is still required' -and
        $applicantRequestSafetyMigrationSource -match 'enforce_th_app_request_universal_safety' -and
        $applicantRequestSafetyMigrationSource -match 'reject_telehealth_evidence_mutation' -and
        $applicantRequestSafetyMigrationSource -notmatch '(?im)^\s*(drop\s+table|truncate\s+|delete\s+from)' -and
        $endpointSource -match '/\{applicantId:guid\}/telehealth-request/safety')
    $applicantRequestComplaintTriageRepositorySource = Get-Content -Raw (Join-Path $solutionRoot 'backend/src/AvenChart.Api/Features/Telehealth/TelehealthApplicantRequestComplaintTriageRepository.cs')
    $applicantRequestComplaintTriageServiceSource = Get-Content -Raw (Join-Path $solutionRoot 'backend/src/AvenChart.Api/Features/Telehealth/TelehealthApplicantRequestComplaintTriageService.cs')
    $applicantRequestComplaintTriagePolicySource = Get-Content -Raw (Join-Path $solutionRoot 'backend/src/AvenChart.Api/Features/Telehealth/TelehealthApplicantRequestComplaintTriagePolicy.cs')
    $applicantRequestComplaintTriageEvaluatorSource = Get-Content -Raw (Join-Path $solutionRoot 'backend/src/AvenChart.Api/Features/Telehealth/SyntheticTelehealthComplaintTriageEvaluator.cs')
    $applicantRequestComplaintTriageMigrationSource = Get-Content -Raw (Join-Path $solutionRoot 'database/migrations/V0318__telehealth_applicant_request_complaint_triage.sql')
    Add-Check 'Applicant request complaint triage is deterministic, access-key/source bound, unpublished, and has no review-work-item, queue, care, financial, integration, or external path' (
        $applicantRequestComplaintTriageRepositorySource -match 'telehealth_applicant_request_complaint_triage_assessments' -and
        $applicantRequestComplaintTriageRepositorySource -match 'fired_rule_codes' -and
        $applicantRequestComplaintTriageRepositorySource -notmatch 'HttpClient|ClientWebSocket|SmtpClient|HubConnection|WebRequest|SendAsync' -and
        $applicantRequestComplaintTriageServiceSource -match 'MedicalDirectorApprovalRecorded: false' -and
        $applicantRequestComplaintTriageServiceSource -match 'ClinicalGoldenCasePackApproved: false' -and
        $applicantRequestComplaintTriageServiceSource -match 'ProductionPublicationAllowed: false' -and
        $applicantRequestComplaintTriageServiceSource -match 'ClinicalReviewCreated: false' -and
        $applicantRequestComplaintTriageServiceSource -match 'PatientCareQueueEntered: false' -and
        $applicantRequestComplaintTriageServiceSource -match 'CareAuthorized: false' -and
        $applicantRequestComplaintTriageServiceSource -match 'ExternalCallPerformed: false' -and
        $applicantRequestComplaintTriagePolicySource -match 'SYNTHETIC_APPLICANT_REQUEST_COMPLAINT_TRIAGE' -and
        $applicantRequestComplaintTriageEvaluatorSource -match 'SyntheticComplaintAnswer.NotSure' -and
        $applicantRequestComplaintTriageMigrationSource -match 'enforce_th_app_request_complaint_triage' -and
        $applicantRequestComplaintTriageMigrationSource -match 'chk_th_app_req_complaint_triage_publication_gate' -and
        $applicantRequestComplaintTriageMigrationSource -match 'reject_telehealth_evidence_mutation' -and
        $applicantRequestComplaintTriageMigrationSource -notmatch '(?im)^\s*(drop\s+table|truncate\s+|delete\s+from)' -and
        $endpointSource -match '/\{applicantId:guid\}/telehealth-request/complaint-triage')
    $applicantRequestIntakeRepositorySource = Get-Content -Raw (Join-Path $solutionRoot 'backend/src/AvenChart.Api/Features/Telehealth/TelehealthApplicantRequestIntakeRepository.cs')
    $applicantRequestIntakeServiceSource = Get-Content -Raw (Join-Path $solutionRoot 'backend/src/AvenChart.Api/Features/Telehealth/TelehealthApplicantRequestIntakeService.cs')
    $applicantRequestIntakePolicySource = Get-Content -Raw (Join-Path $solutionRoot 'backend/src/AvenChart.Api/Features/Telehealth/TelehealthApplicantRequestIntakePolicy.cs')
    $applicantRequestIntakeMigrationSource = Get-Content -Raw (Join-Path $solutionRoot 'database/migrations/V0319__telehealth_applicant_request_intake_snapshot.sql')
    Add-Check 'Applicant request intake is one no-free-text snapshot, remains publication-blocked, and creates no coverage, consent, operational, queue, care, financial, integration, or external path' (
        $applicantRequestIntakeRepositorySource -match 'insert into telehealth_intake_snapshots' -and
        $applicantRequestIntakeRepositorySource -match 'insert into telehealth_applicant_request_intake_snapshots' -and
        $applicantRequestIntakeRepositorySource -notmatch '(?i)insert\s+into\s+(patients|insurance_records|telehealth_patient_confirmations|telehealth_demonstration_acknowledgments|telehealth_coverage_selections|telehealth_coverage_verifications|telehealth_queue_entries|telehealth_reservations|telehealth_video_sessions|telehealth_consultation_contexts|appointments|encounters|claims|billing|prescriptions|messages|integration_outbox)' -and
        $applicantRequestIntakeRepositorySource -notmatch 'HttpClient|ClientWebSocket|SmtpClient|HubConnection|WebRequest|SendAsync' -and
        $applicantRequestIntakeServiceSource -match 'CoverageRecordCreated: false' -and
        $applicantRequestIntakeServiceSource -match 'CoverageVerified: false' -and
        $applicantRequestIntakeServiceSource -match 'OperationalReviewCreated: false' -and
        $applicantRequestIntakeServiceSource -match 'ConsentCreated: false' -and
        $applicantRequestIntakeServiceSource -match 'PatientCareQueueEntered: false' -and
        $applicantRequestIntakeServiceSource -match 'CareAuthorized: false' -and
        $applicantRequestIntakeServiceSource -match 'ExternalCallPerformed: false' -and
        $applicantRequestIntakePolicySource -match 'SYNTHETIC_APPLICANT_REQUEST_INTAKE_SNAPSHOT_CONFIRMATION' -and
        $applicantRequestIntakePolicySource -match 'SupportedSymptomDurations' -and
        $applicantRequestIntakeMigrationSource -match 'enforce_th_app_request_intake_snapshot' -and
        $applicantRequestIntakeMigrationSource -match 'chk_th_app_req_intake_publication_gate' -and
        $applicantRequestIntakeMigrationSource -match 'reject_telehealth_evidence_mutation' -and
        $applicantRequestIntakeMigrationSource -notmatch '(?im)^\s*(drop\s+table|truncate\s+|delete\s+from)' -and
        $endpointSource -match '/\{applicantId:guid\}/telehealth-request/intake')
    $requestInsuranceSourceRepositorySource = Get-Content -Raw (Join-Path $solutionRoot 'backend/src/AvenChart.Api/Features/Telehealth/TelehealthApplicantRequestInsuranceSourceRepository.cs')
    $requestInsuranceSourceServiceSource = Get-Content -Raw (Join-Path $solutionRoot 'backend/src/AvenChart.Api/Features/Telehealth/TelehealthApplicantRequestInsuranceSourceService.cs')
    $requestInsuranceSourcePolicySource = Get-Content -Raw (Join-Path $solutionRoot 'backend/src/AvenChart.Api/Features/Telehealth/TelehealthApplicantRequestInsuranceSourcePolicy.cs')
    $requestInsuranceSourceMigrationSource = Get-Content -Raw (Join-Path $solutionRoot 'database/migrations/V0320__telehealth_applicant_request_insurance_source_confirmation.sql')
    Add-Check 'Applicant request insurance-source confirmation references protected evidence without reuse, current verification, downstream creation, or an external path' (
        $requestInsuranceSourceRepositorySource -match 'insert into telehealth_applicant_request_insurance_source_confirmations' -and
        $requestInsuranceSourceRepositorySource -notmatch '(?i)insert\s+into\s+(patients|insurance_records|telehealth_coverage_selections|telehealth_coverage_verifications|telehealth_queue_entries|telehealth_reservations|telehealth_video_sessions|telehealth_consultation_contexts|appointments|encounters|claims|billing|prescriptions|messages|integration_outbox)' -and
        $requestInsuranceSourceRepositorySource -notmatch 'Unprotect|IDataProtector|HttpClient|ClientWebSocket|SmtpClient|HubConnection|WebRequest|SendAsync' -and
        $requestInsuranceSourceServiceSource -match 'PreviousResultReusable: false' -and
        $requestInsuranceSourceServiceSource -match 'ProtectedPayloadCopied: false' -and
        $requestInsuranceSourceServiceSource -match 'ProtectedPayloadDecrypted: false' -and
        $requestInsuranceSourceServiceSource -match 'EligibilityVerificationCreated: false' -and
        $requestInsuranceSourceServiceSource -match 'NetworkVerificationCreated: false' -and
        $requestInsuranceSourceServiceSource -match 'RenderingPhysicianNetworkChecked: false' -and
        $requestInsuranceSourceServiceSource -match 'CoverageVerified: false' -and
        $requestInsuranceSourceServiceSource -match 'ExactNetworkConfirmed: false' -and
        $requestInsuranceSourceServiceSource -match 'FinancialRouteCreated: false' -and
        $requestInsuranceSourceServiceSource -match 'OperationalReviewCreated: false' -and
        $requestInsuranceSourceServiceSource -match 'PatientCareQueueEntered: false' -and
        $requestInsuranceSourceServiceSource -match 'CareAuthorized: false' -and
        $requestInsuranceSourceServiceSource -match 'ExternalCallPerformed: false' -and
        $requestInsuranceSourcePolicySource -match 'SYNTHETIC_APPLICANT_REQUEST_INSURANCE_SOURCE_CONFIRMATION' -and
        $requestInsuranceSourcePolicySource -match 'FreshVerificationRequested' -and
        $requestInsuranceSourceMigrationSource -match 'enforce_th_app_request_insurance_source' -and
        $requestInsuranceSourceMigrationSource -match 'protected_payload_referenced and not protected_payload_copied' -and
        $requestInsuranceSourceMigrationSource -match 'not prior_result_reused' -and
        $requestInsuranceSourceMigrationSource -match 'chk_th_app_req_ins_source_no_consequence' -and
        $requestInsuranceSourceMigrationSource -match 'reject_telehealth_evidence_mutation' -and
        $requestInsuranceSourceMigrationSource -notmatch '(?im)^\s*(drop\s+table|truncate\s+|delete\s+from)' -and
        $endpointSource -match '/\{applicantId:guid\}/telehealth-request/insurance-source')
    $requestEligibilityRepositorySource = Get-Content -Raw (Join-Path $solutionRoot 'backend/src/AvenChart.Api/Features/Telehealth/TelehealthApplicantRequestEligibilityRepository.cs')
    $requestEligibilityServiceSource = Get-Content -Raw (Join-Path $solutionRoot 'backend/src/AvenChart.Api/Features/Telehealth/TelehealthApplicantRequestEligibilityService.cs')
    $requestEligibilityPolicySource = Get-Content -Raw (Join-Path $solutionRoot 'backend/src/AvenChart.Api/Features/Telehealth/TelehealthApplicantRequestEligibilityPolicy.cs')
    $requestEligibilityMigrationSource = Get-Content -Raw (Join-Path $solutionRoot 'database/migrations/V0321__telehealth_applicant_request_eligibility_verification.sql')
    Add-Check 'Applicant request eligibility uses only the protected synthetic adapter and creates no network, coverage, downstream, or external path' (
        $requestEligibilityRepositorySource -match 'insert into telehealth_applicant_request_eligibility_verifications' -and
        $requestEligibilityRepositorySource -notmatch '(?i)insert\s+into\s+(patients|insurance_records|telehealth_coverage_selections|telehealth_coverage_verifications|telehealth_queue_entries|telehealth_reservations|telehealth_video_sessions|telehealth_consultation_contexts|appointments|encounters|claims|billing|prescriptions|messages|integration_outbox)' -and
        $requestEligibilityRepositorySource -notmatch 'HttpClient|ClientWebSocket|SmtpClient|HubConnection|WebRequest|SendAsync' -and
        $requestEligibilityServiceSource -match 'protector.Unprotect' -and
        $requestEligibilityServiceSource -match 'PriorEligibilityResultReused: false' -and
        $requestEligibilityServiceSource -match 'ProtectedPayloadCopied: false' -and
        $requestEligibilityServiceSource -match 'NetworkVerificationCreated: false' -and
        $requestEligibilityServiceSource -match 'RenderingPhysicianNetworkChecked: false' -and
        $requestEligibilityServiceSource -match 'CoverageVerified: false' -and
        $requestEligibilityServiceSource -match 'ExactNetworkConfirmed: false' -and
        $requestEligibilityServiceSource -match 'FinancialRouteCreated: false' -and
        $requestEligibilityServiceSource -match 'OperationalReviewCreated: false' -and
        $requestEligibilityServiceSource -match 'PatientCareQueueEntered: false' -and
        $requestEligibilityServiceSource -match 'CareAuthorized: false' -and
        $requestEligibilityServiceSource -match 'ExternalCallPerformed: false' -and
        $requestEligibilityPolicySource -match 'SYNTHETIC_APPLICANT_REQUEST_ELIGIBILITY_VERIFICATION' -and
        $requestEligibilityPolicySource -match 'EntryRequestVersion = 6' -and
        $requestEligibilityPolicySource -match 'ResultingRequestVersion = 7' -and
        $requestEligibilityMigrationSource -match 'enforce_th_app_request_eligibility' -and
        $requestEligibilityMigrationSource -match 'protected_payload_decrypted_in_server_memory' -and
        $requestEligibilityMigrationSource -match 'not prior_eligibility_result_reused' -and
        $requestEligibilityMigrationSource -match 'chk_th_app_req_eligibility_no_consequence' -and
        $requestEligibilityMigrationSource -match 'reject_telehealth_evidence_mutation' -and
        $requestEligibilityMigrationSource -notmatch '(?im)^\s*(drop\s+table|truncate\s+|delete\s+from)' -and
        $endpointSource -match '/\{applicantId:guid\}/telehealth-request/eligibility')
    $requestPracticeNetworkRepositorySource = Get-Content -Raw (Join-Path $solutionRoot 'backend/src/AvenChart.Api/Features/Telehealth/TelehealthApplicantRequestPracticeNetworkRepository.cs')
    $requestPracticeNetworkServiceSource = Get-Content -Raw (Join-Path $solutionRoot 'backend/src/AvenChart.Api/Features/Telehealth/TelehealthApplicantRequestPracticeNetworkService.cs')
    $requestPracticeNetworkPolicySource = Get-Content -Raw (Join-Path $solutionRoot 'backend/src/AvenChart.Api/Features/Telehealth/TelehealthApplicantRequestPracticeNetworkPolicy.cs')
    $requestPracticeNetworkMigrationSource = Get-Content -Raw (Join-Path $solutionRoot 'database/migrations/V0322__telehealth_applicant_request_practice_network_verification.sql')
    Add-Check 'Applicant request practice-network verification is fresh, practice-only, in-process, and creates no exact-network or downstream path' (
        $requestPracticeNetworkRepositorySource -match 'insert into telehealth_applicant_request_practice_network_verifications' -and
        $requestPracticeNetworkRepositorySource -notmatch '(?i)insert\s+into\s+(patients|insurance_records|telehealth_coverage_selections|telehealth_coverage_verifications|telehealth_queue_entries|telehealth_reservations|telehealth_video_sessions|telehealth_consultation_contexts|appointments|encounters|claims|billing|prescriptions|messages|integration_outbox)' -and
        $requestPracticeNetworkRepositorySource -notmatch 'HttpClient|ClientWebSocket|SmtpClient|HubConnection|WebRequest|SendAsync' -and
        $requestPracticeNetworkServiceSource -match 'ITelehealthProspectivePracticeNetworkGateway' -and
        $requestPracticeNetworkServiceSource -match 'RenderingPhysicianSelected: false' -and
        $requestPracticeNetworkServiceSource -match 'RenderingPhysicianNetworkChecked: false' -and
        $requestPracticeNetworkServiceSource -match 'ExactNetworkConfirmed: false' -and
        $requestPracticeNetworkServiceSource -match 'CoverageVerified: false' -and
        $requestPracticeNetworkServiceSource -match 'FinancialRouteCreated: false' -and
        $requestPracticeNetworkServiceSource -match 'OperationalReviewCreated: false' -and
        $requestPracticeNetworkServiceSource -match 'PatientCareQueueEntered: false' -and
        $requestPracticeNetworkServiceSource -match 'CareAuthorized: false' -and
        $requestPracticeNetworkServiceSource -match 'ExternalCallPerformed: false' -and
        $requestPracticeNetworkPolicySource -match 'SYNTHETIC_APPLICANT_REQUEST_PRACTICE_NETWORK_VERIFICATION' -and
        $requestPracticeNetworkPolicySource -match 'EntryRequestVersion = 7' -and
        $requestPracticeNetworkPolicySource -match 'ResultingRequestVersion = 8' -and
        $requestPracticeNetworkMigrationSource -match 'enforce_th_app_request_practice_network' -and
        $requestPracticeNetworkMigrationSource -match 'not rendering_physician_selected' -and
        $requestPracticeNetworkMigrationSource -match 'not exact_network_confirmed' -and
        $requestPracticeNetworkMigrationSource -match 'chk_th_app_req_practice_network_no_consequence' -and
        $requestPracticeNetworkMigrationSource -match 'reject_telehealth_evidence_mutation' -and
        $requestPracticeNetworkMigrationSource -notmatch '(?im)^\s*(drop\s+table|truncate\s+|delete\s+from)' -and
        $endpointSource -match '/\{applicantId:guid\}/telehealth-request/practice-network')
    $requestRenderingCandidateRepositorySource = Get-Content -Raw (Join-Path $solutionRoot 'backend/src/AvenChart.Api/Features/Telehealth/TelehealthApplicantRequestRenderingCandidateRepository.cs')
    $requestRenderingCandidateServiceSource = Get-Content -Raw (Join-Path $solutionRoot 'backend/src/AvenChart.Api/Features/Telehealth/TelehealthApplicantRequestRenderingCandidateService.cs')
    $requestRenderingCandidatePolicySource = Get-Content -Raw (Join-Path $solutionRoot 'backend/src/AvenChart.Api/Features/Telehealth/TelehealthApplicantRequestRenderingCandidatePolicy.cs')
    $requestRenderingCandidateMigrationSource = Get-Content -Raw (Join-Path $solutionRoot 'database/migrations/V0323__telehealth_applicant_request_rendering_candidate_selection.sql')
    Add-Check 'Applicant request rendering-candidate selection is roster-bound, candidate-only, and creates no assignment, exact-network, or downstream path' (
        $requestRenderingCandidateRepositorySource -match 'insert into telehealth_applicant_request_rendering_candidate_selections' -and
        $requestRenderingCandidateRepositorySource -notmatch '(?i)insert\s+into\s+(patients|insurance_records|telehealth_coverage_selections|telehealth_coverage_verifications|telehealth_queue_entries|telehealth_reservations|telehealth_video_sessions|telehealth_consultation_contexts|appointments|encounters|claims|billing|prescriptions|messages|integration_outbox)' -and
        $requestRenderingCandidateRepositorySource -notmatch 'HttpClient|ClientWebSocket|SmtpClient|HubConnection|WebRequest|SendAsync' -and
        $requestRenderingCandidateServiceSource -match 'CandidateSelectedForNetworkEvaluation: complete' -and
        $requestRenderingCandidateServiceSource -match 'RenderingPhysicianAssigned: false' -and
        $requestRenderingCandidateServiceSource -match 'RenderingPhysicianNetworkChecked: false' -and
        $requestRenderingCandidateServiceSource -match 'ExactNetworkConfirmed: false' -and
        $requestRenderingCandidateServiceSource -match 'CoverageVerified: false' -and
        $requestRenderingCandidateServiceSource -match 'FinancialRouteCreated: false' -and
        $requestRenderingCandidateServiceSource -match 'OperationalReviewCreated: false' -and
        $requestRenderingCandidateServiceSource -match 'PatientCareQueueEntered: false' -and
        $requestRenderingCandidateServiceSource -match 'CareAuthorized: false' -and
        $requestRenderingCandidateServiceSource -match 'ExternalCallPerformed: false' -and
        $requestRenderingCandidatePolicySource -match 'SYNTHETIC_APPLICANT_REQUEST_RENDERING_CANDIDATE_SELECTION' -and
        $requestRenderingCandidatePolicySource -match 'EntryRequestVersion = 8' -and
        $requestRenderingCandidatePolicySource -match 'ResultingRequestVersion = 9' -and
        $requestRenderingCandidateMigrationSource -match 'trg_th_app_request_render_candidate_guard' -and
        $requestRenderingCandidateMigrationSource -match 'chk_th_app_req_render_candidate_boundary' -and
        $requestRenderingCandidateMigrationSource -match 'chk_th_app_req_render_candidate_no_consequence' -and
        $requestRenderingCandidateMigrationSource -match 'reject_telehealth_evidence_mutation' -and
        $requestRenderingCandidateMigrationSource -notmatch '(?im)^\s*(drop\s+table|truncate\s+|delete\s+from)' -and
        $endpointSource -match '/\{applicantId:guid\}/telehealth-request/rendering-candidate')
    $requestParticipationContextRepositorySource = Get-Content -Raw (Join-Path $solutionRoot 'backend/src/AvenChart.Api/Features/Telehealth/TelehealthApplicantRequestParticipationContextRepository.cs')
    $requestParticipationContextServiceSource = Get-Content -Raw (Join-Path $solutionRoot 'backend/src/AvenChart.Api/Features/Telehealth/TelehealthApplicantRequestParticipationContextService.cs')
    $requestParticipationContextPolicySource = Get-Content -Raw (Join-Path $solutionRoot 'backend/src/AvenChart.Api/Features/Telehealth/TelehealthApplicantRequestParticipationContextPolicy.cs')
    $requestParticipationContextMigrationSource = Get-Content -Raw (Join-Path $solutionRoot 'database/migrations/V0324__telehealth_applicant_request_participation_context.sql')
    Add-Check 'Applicant request participation context is server-owned, prerequisite-only, and creates no real verification, exact-network, or downstream path' (
        $requestParticipationContextRepositorySource -match 'insert into telehealth_applicant_request_participation_contexts' -and
        $requestParticipationContextRepositorySource -notmatch '(?i)insert\s+into\s+(patients|insurance_records|telehealth_coverage_selections|telehealth_coverage_verifications|telehealth_queue_entries|telehealth_reservations|telehealth_video_sessions|telehealth_consultation_contexts|appointments|encounters|claims|billing|prescriptions|messages|integration_outbox)' -and
        $requestParticipationContextRepositorySource -notmatch 'HttpClient|ClientWebSocket|SmtpClient|HubConnection|WebRequest|SendAsync' -and
        $requestParticipationContextServiceSource -match 'ParticipationEvaluationContextConfirmed: complete' -and
        $requestParticipationContextServiceSource -match 'RealStateAuthorityVerified: false' -and
        $requestParticipationContextServiceSource -match 'RealCredentialingVerified: false' -and
        $requestParticipationContextServiceSource -match 'RenderingPhysicianAssigned: false' -and
        $requestParticipationContextServiceSource -match 'RenderingPhysicianNetworkChecked: false' -and
        $requestParticipationContextServiceSource -match 'ExactNetworkConfirmed: false' -and
        $requestParticipationContextServiceSource -match 'CoverageVerified: false' -and
        $requestParticipationContextServiceSource -match 'FinancialRouteCreated: false' -and
        $requestParticipationContextServiceSource -match 'OperationalReviewCreated: false' -and
        $requestParticipationContextServiceSource -match 'PatientCareQueueEntered: false' -and
        $requestParticipationContextServiceSource -match 'CareAuthorized: false' -and
        $requestParticipationContextServiceSource -match 'ExternalCallPerformed: false' -and
        $requestParticipationContextPolicySource -match 'SYNTHETIC_APPLICANT_REQUEST_PARTICIPATION_CONTEXT' -and
        $requestParticipationContextPolicySource -match 'EntryRequestVersion = 9' -and
        $requestParticipationContextPolicySource -match 'ResultingRequestVersion = 10' -and
        $requestParticipationContextMigrationSource -match 'trg_th_app_request_part_context_guard' -and
        $requestParticipationContextMigrationSource -match 'chk_th_app_req_part_context_boundary' -and
        $requestParticipationContextMigrationSource -match 'chk_th_app_req_part_context_no_consequence' -and
        $requestParticipationContextMigrationSource -match 'reject_telehealth_evidence_mutation' -and
        $requestParticipationContextMigrationSource -notmatch '(?im)^\s*(drop\s+table|truncate\s+|delete\s+from)' -and
        $endpointSource -match '/\{applicantId:guid\}/telehealth-request/participation-context')
    $requestParticipationEvaluationRepositorySource = Get-Content -Raw (Join-Path $solutionRoot 'backend/src/AvenChart.Api/Features/Telehealth/TelehealthApplicantRequestParticipationEvaluationRepository.cs')
    $requestParticipationEvaluationServiceSource = Get-Content -Raw (Join-Path $solutionRoot 'backend/src/AvenChart.Api/Features/Telehealth/TelehealthApplicantRequestParticipationEvaluationService.cs')
    $requestParticipationEvaluationPolicySource = Get-Content -Raw (Join-Path $solutionRoot 'backend/src/AvenChart.Api/Features/Telehealth/TelehealthApplicantRequestParticipationEvaluationPolicy.cs')
    $requestParticipationEvaluationMigrationSource = Get-Content -Raw (Join-Path $solutionRoot 'database/migrations/V0325__telehealth_applicant_request_participation_evaluation.sql')
    Add-Check 'Applicant request participation evaluation is an exact synthetic catalog match with no real verification, assignment, coverage, or downstream path' (
        $requestParticipationEvaluationRepositorySource -match 'insert into telehealth_applicant_request_participation_evaluations' -and
        $requestParticipationEvaluationRepositorySource -notmatch '(?i)insert\s+into\s+(patients|insurance_records|telehealth_coverage_selections|telehealth_coverage_verifications|telehealth_queue_entries|telehealth_reservations|telehealth_video_sessions|telehealth_consultation_contexts|appointments|encounters|claims|billing|prescriptions|messages|integration_outbox)' -and
        $requestParticipationEvaluationRepositorySource -notmatch 'HttpClient|ClientWebSocket|SmtpClient|HubConnection|WebRequest|SendAsync' -and
        $requestParticipationEvaluationServiceSource -match 'SyntheticParticipationEvaluated: complete' -and
        $requestParticipationEvaluationServiceSource -match 'SyntheticNewPatientsAccepted: complete' -and
        $requestParticipationEvaluationServiceSource -match 'SyntheticExactNetworkMatched: complete' -and
        $requestParticipationEvaluationServiceSource -match 'RealStateAuthorityVerified: false' -and
        $requestParticipationEvaluationServiceSource -match 'RealCredentialingVerified: false' -and
        $requestParticipationEvaluationServiceSource -match 'RenderingPhysicianAssigned: false' -and
        $requestParticipationEvaluationServiceSource -match 'RenderingPhysicianNetworkChecked: false' -and
        $requestParticipationEvaluationServiceSource -match 'ExactNetworkConfirmed: false' -and
        $requestParticipationEvaluationServiceSource -match 'CoverageVerified: false' -and
        $requestParticipationEvaluationServiceSource -match 'OperationalReviewCreated: false' -and
        $requestParticipationEvaluationServiceSource -match 'PatientCareQueueEntered: false' -and
        $requestParticipationEvaluationServiceSource -match 'CareAuthorized: false' -and
        $requestParticipationEvaluationServiceSource -match 'ExternalCallPerformed: false' -and
        $requestParticipationEvaluationPolicySource -match 'SYNTHETIC_APPLICANT_REQUEST_PARTICIPATION_EVALUATION' -and
        $requestParticipationEvaluationPolicySource -match 'EntryRequestVersion = 10' -and
        $requestParticipationEvaluationPolicySource -match 'ResultingRequestVersion = 11' -and
        $requestParticipationEvaluationPolicySource -match 'HL7_FHIR_R4_DAVINCI_PDEX_PLAN_NET_1_2_0' -and
        $requestParticipationEvaluationMigrationSource -match 'trg_th_app_request_part_eval_guard' -and
        $requestParticipationEvaluationMigrationSource -match 'chk_th_app_req_part_eval_result' -and
        $requestParticipationEvaluationMigrationSource -match 'chk_th_app_req_part_eval_no_consequence' -and
        $requestParticipationEvaluationMigrationSource -match 'reject_telehealth_evidence_mutation' -and
        $requestParticipationEvaluationMigrationSource -notmatch '(?im)^\s*(drop\s+table|truncate\s+|delete\s+from)' -and
        $endpointSource -match '/\{applicantId:guid\}/telehealth-request/participation-evaluation')
    $requestOperationalReviewSubmissionRepositorySource = Get-Content -Raw (Join-Path $solutionRoot 'backend/src/AvenChart.Api/Features/Telehealth/TelehealthApplicantRequestOperationalReviewSubmissionRepository.cs')
    $requestOperationalReviewSubmissionServiceSource = Get-Content -Raw (Join-Path $solutionRoot 'backend/src/AvenChart.Api/Features/Telehealth/TelehealthApplicantRequestOperationalReviewSubmissionService.cs')
    $requestOperationalReviewSubmissionPolicySource = Get-Content -Raw (Join-Path $solutionRoot 'backend/src/AvenChart.Api/Features/Telehealth/TelehealthApplicantRequestOperationalReviewSubmissionPolicy.cs')
    $requestOperationalReviewSubmissionMigrationSource = Get-Content -Raw (Join-Path $solutionRoot 'database/migrations/V0326__telehealth_applicant_request_operational_review_submission.sql')
    Add-Check 'Applicant operational-review submission advances only review state and creates no acceptance, financial, queue, care, integration, or external path' (
        $requestOperationalReviewSubmissionRepositorySource -match 'insert into telehealth_applicant_request_operational_review_submissions' -and
        $requestOperationalReviewSubmissionRepositorySource -match "status='OperationalReview',version=12" -and
        $requestOperationalReviewSubmissionRepositorySource -notmatch '(?i)insert\s+into\s+(patients|insurance_records|telehealth_coverage_selections|telehealth_coverage_verifications|telehealth_queue_entries|telehealth_reservations|telehealth_video_sessions|telehealth_consultation_contexts|appointments|encounters|claims|billing|prescriptions|messages|integration_outbox)' -and
        $requestOperationalReviewSubmissionRepositorySource -notmatch 'HttpClient|ClientWebSocket|SmtpClient|HubConnection|WebRequest|SendAsync' -and
        $requestOperationalReviewSubmissionServiceSource -match 'SyntheticAutomatedChecksComplete: complete' -and
        $requestOperationalReviewSubmissionServiceSource -match 'OperationalReviewCreated: complete' -and
        $requestOperationalReviewSubmissionServiceSource -match 'PracticeAccepted: false' -and
        $requestOperationalReviewSubmissionServiceSource -match 'CoverageVerified: false' -and
        $requestOperationalReviewSubmissionServiceSource -match 'PatientCareQueueEntered: false' -and
        $requestOperationalReviewSubmissionServiceSource -match 'CareAuthorized: false' -and
        $requestOperationalReviewSubmissionServiceSource -match 'ExternalCallPerformed: false' -and
        $requestOperationalReviewSubmissionPolicySource -match 'SYNTHETIC_APPLICANT_REQUEST_OPERATIONAL_REVIEW_SUBMISSION' -and
        $requestOperationalReviewSubmissionPolicySource -match 'EntryRequestVersion = 11' -and
        $requestOperationalReviewSubmissionPolicySource -match 'ResultingRequestVersion = 12' -and
        $requestOperationalReviewSubmissionMigrationSource -match 'trg_th_app_request_op_review_submission_guard' -and
        $requestOperationalReviewSubmissionMigrationSource -match 'chk_th_app_req_op_review_submission_no_consequence' -and
        $requestOperationalReviewSubmissionMigrationSource -match 'reject_telehealth_evidence_mutation' -and
        $requestOperationalReviewSubmissionMigrationSource -notmatch '(?im)^\s*(drop\s+table|truncate\s+|delete\s+from)' -and
        $endpointSource -match '/\{applicantId:guid\}/telehealth-request/operational-review-submission')
    $requestQueueAuthorizationRepositorySource = Get-Content -Raw (Join-Path $solutionRoot 'backend/src/AvenChart.Api/Features/Telehealth/TelehealthApplicantRequestQueueAuthorizationRepository.cs')
    $requestQueueAuthorizationServiceSource = Get-Content -Raw (Join-Path $solutionRoot 'backend/src/AvenChart.Api/Features/Telehealth/TelehealthApplicantRequestQueueAuthorizationService.cs')
    $requestQueueAuthorizationPolicySource = Get-Content -Raw (Join-Path $solutionRoot 'backend/src/AvenChart.Api/Features/Telehealth/TelehealthApplicantRequestQueueAuthorizationPolicy.cs')
    $requestQueueAuthorizationMigrationSource = Get-Content -Raw (Join-Path $solutionRoot 'database/migrations/V0327__telehealth_applicant_request_queue_authorization.sql')
    $telehealthRepositorySource = Get-Content -Raw (Join-Path $solutionRoot 'backend/src/AvenChart.Api/Features/Telehealth/TelehealthRepository.cs')
    Add-Check 'Applicant queue authorization is a dedicated staff-only atomic queue boundary with no real-coverage, assignment, care, or external path' (
        $requestQueueAuthorizationRepositorySource -match 'insert into telehealth_applicant_request_queue_authorizations' -and
        $requestQueueAuthorizationRepositorySource -match 'insert into appointments' -and
        $requestQueueAuthorizationRepositorySource -match 'insert into telehealth_queue_entries' -and
        $requestQueueAuthorizationRepositorySource -match "status='Queued',appointment_id=@appointmentId,version=13" -and
        $requestQueueAuthorizationRepositorySource -notmatch '(?i)insert\s+into\s+(insurance_records|telehealth_coverage_selections|telehealth_coverage_verifications|telehealth_reservations|telehealth_video_sessions|telehealth_consultation_contexts|encounters|claims|billing|prescriptions|messages|integration_outbox)' -and
        $requestQueueAuthorizationRepositorySource -notmatch 'HttpClient|ClientWebSocket|SmtpClient|HubConnection|WebRequest|SendAsync' -and
        $requestQueueAuthorizationServiceSource -match 'PracticeAccepted: complete' -and
        $requestQueueAuthorizationServiceSource -match 'RenderingPhysicianAssigned: false' -and
        $requestQueueAuthorizationServiceSource -match 'CoverageVerified: false' -and
        $requestQueueAuthorizationServiceSource -match 'QueuePositionAssigned: false' -and
        $requestQueueAuthorizationServiceSource -match 'EncounterCreated: false' -and
        $requestQueueAuthorizationServiceSource -match 'CareAuthorized: false' -and
        $requestQueueAuthorizationServiceSource -match 'ExternalCallPerformed: false' -and
        $requestQueueAuthorizationPolicySource -match 'SYNTHETIC_APPLICANT_REQUEST_QUEUE_AUTHORIZATION' -and
        $requestQueueAuthorizationPolicySource -match 'EntryRequestVersion = 12' -and
        $requestQueueAuthorizationPolicySource -match 'ResultingRequestVersion = 13' -and
        $requestQueueAuthorizationMigrationSource -match 'trg_th_app_request_queue_auth_guard' -and
        $requestQueueAuthorizationMigrationSource -match 'chk_th_app_req_queue_auth_no_consequence' -and
        $requestQueueAuthorizationMigrationSource -match 'reject_telehealth_evidence_mutation' -and
        $telehealthRepositorySource -match 'telehealth_applicant_request_dedicated_authorization_required' -and
        $telehealthRepositorySource -match 'source_applicant_id is not null' -and
        $endpointSource -match '/applicant-requests/\{requestId:guid\}/queue-authorization')
    $applicantQueueStatusRepositorySource = Get-Content -Raw (Join-Path $solutionRoot 'backend/src/AvenChart.Api/Features/Telehealth/TelehealthApplicantRequestQueueStatusRepository.cs')
    $applicantQueueStatusServiceSource = Get-Content -Raw (Join-Path $solutionRoot 'backend/src/AvenChart.Api/Features/Telehealth/TelehealthApplicantRequestQueueStatusService.cs')
    $applicantQueueStatusPolicySource = Get-Content -Raw (Join-Path $solutionRoot 'backend/src/AvenChart.Api/Features/Telehealth/TelehealthApplicantRequestQueueStatusPolicy.cs')
    Add-Check 'Applicant queue status is a read-only access-key projection with approximate-only position and no care or external consequence' (
        $applicantQueueStatusRepositorySource -match 'telehealth_applicant_request_queue_authorizations' -and
        $applicantQueueStatusRepositorySource -match 'source_applicant_id=a.applicant_id' -and
        $applicantQueueStatusRepositorySource -match "candidate.status='Ready'" -and
        $applicantQueueStatusRepositorySource -notmatch '(?i)\b(insert\s+into|update\s+|delete\s+from|truncate\s+)\b' -and
        $applicantQueueStatusRepositorySource -notmatch 'HttpClient|ClientWebSocket|SmtpClient|HubConnection|WebRequest|SendAsync' -and
        $applicantQueueStatusServiceSource -match 'RequireAccessKey' -and
        $applicantQueueStatusPolicySource -match 'SYNTHETIC_APPLICANT_REQUEST_QUEUE_STATUS' -and
        $applicantQueueStatusPolicySource -match 'ExactQueuePositionAssigned: false' -and
        $applicantQueueStatusPolicySource -match 'WaitEstimateAvailable: false' -and
        $applicantQueueStatusPolicySource -match 'RenderingPhysicianIdentityDisclosed: false' -and
        $applicantQueueStatusPolicySource -match 'CoverageVerified: false' -and
        $applicantQueueStatusPolicySource -match 'ConsentCreated: false' -and
        $applicantQueueStatusPolicySource -match 'CareAuthorized: false' -and
        $applicantQueueStatusPolicySource -match 'ExternalCallPerformed: false' -and
        $endpointSource -match '/\{applicantId:guid\}/telehealth-request/queue-status')
    $videoProviderSource = Get-Content -Raw (Join-Path $solutionRoot 'backend/src/AvenChart.Api/Features/Telehealth/TelehealthVideoProvider.cs')
    $videoServiceSource = Get-Content -Raw (Join-Path $solutionRoot 'backend/src/AvenChart.Api/Features/Telehealth/TelehealthVideoService.cs')
    $videoRepositorySource = Get-Content -Raw (Join-Path $solutionRoot 'backend/src/AvenChart.Api/Features/Telehealth/TelehealthVideoRepository.cs')
    $localWebRtcRelaySource = Get-Content -Raw (Join-Path $solutionRoot 'backend/src/AvenChart.Api/Features/Telehealth/TelehealthLocalWebRtcPocRelay.cs')
    $localWebRtcRepositorySource = Get-Content -Raw (Join-Path $solutionRoot 'backend/src/AvenChart.Api/Features/Telehealth/TelehealthLocalWebRtcPocRepository.cs')
    $localWebRtcServiceSource = Get-Content -Raw (Join-Path $solutionRoot 'backend/src/AvenChart.Api/Features/Telehealth/TelehealthLocalWebRtcPocService.cs')
    $applicantConnectionPolicySource = Get-Content -Raw (Join-Path $solutionRoot 'backend/src/AvenChart.Api/Features/Telehealth/TelehealthApplicantConnectionPolicy.cs')
    $videoMigrationSource = Get-Content -Raw (Join-Path $solutionRoot 'database/migrations/V0285__telehealth_connection_room_shell.sql')
    Add-Check 'Connection-room adapter excludes vendor, recording, transcription, external-call, and encounter mutation paths' (
        $videoProviderSource -notmatch 'HttpClient|ClientWebSocket|SmtpClient|SignalR|HubConnection|RTCPeerConnection|MediaStream' -and
        $videoServiceSource -notmatch 'HttpClient|recording_enabled\s*=\s*true|transcription_enabled\s*=\s*true' -and
        $videoRepositorySource -notmatch '(?i)insert\s+into\s+(encounters|clinical_notes|prescriptions|claims)' -and
        $videoMigrationSource -match 'media_transport_enabled boolean not null default false' -and
        $videoMigrationSource -match 'recording_enabled = false and transcription_enabled = false and media_transport_enabled = false')
    Add-Check 'Optional local WebRTC POC is default-off, grant-bound, transient-only, and excludes recording, external, or clinical mutation paths' (
        $settings.Telehealth.LocalWebRtcPocEnabled -eq $false -and
        $development.Telehealth.LocalWebRtcPocEnabled -eq $false -and
        $featureSource -match 'LocalWebRtcPocEnabled' -and
        $localWebRtcServiceSource -match 'if \(!_options\.LocalWebRtcPocEnabled\)' -and
        $localWebRtcServiceSource -match 'RequireAccessKey' -and
        $localWebRtcRepositorySource -match "video_grant\.status='Issued'" -and
        $localWebRtcRepositorySource -match "session\.status='WaitingRoom'" -and
        $localWebRtcRepositorySource -match "request\.status='Connecting'" -and
        $localWebRtcRepositorySource -notmatch '(?i)\b(insert\s+into|update\s+|delete\s+from|truncate\s+)\b' -and
        $localWebRtcRelaySource -match 'ConcurrentDictionary' -and
        $localWebRtcRelaySource -match 'RemoveExpiredSessions' -and
        $localWebRtcRelaySource -notmatch 'HttpClient|ClientWebSocket|SmtpClient|HubConnection|\bSignalR\b|recording|transcription|MediaStream|RTCPeerConnection' -and
        $localWebRtcServiceSource -notmatch 'HttpClient|ClientWebSocket|SmtpClient|HubConnection|\bSignalR\b|recording|transcription|MediaStream|RTCPeerConnection' -and
        $endpointSource -match '/local-webrtc/signals/write' -and
        $endpointSource -match '/local-webrtc/signals/read')
    Add-Check 'Applicant connection preparation is owner-bound to the exact reserved request and cannot start media, communication, consent, encounter, care, or external work' (
        $applicantConnectionPolicySource -match 'SYNTHETIC_APPLICANT_CONNECTION_ROOM' -and
        $applicantConnectionPolicySource -match 'NON_PRODUCTION' -and
        $applicantConnectionPolicySource -match 'CreateParticipantSubjectHash' -and
        $videoServiceSource -match 'PrepareApplicantAsync' -and
        $videoServiceSource -match 'RequireAccessKey' -and
        $videoRepositorySource -match 'PrepareApplicantContextAsync' -and
        $videoRepositorySource -match "r.status in \('Reserved','Connecting'\)" -and
        $videoRepositorySource -match 'r\.source_applicant_id=applicant\.applicant_id' -and
        $videoRepositorySource -match 'queue_authorization\.candidate_staff_id=reservation\.clinician_staff_id' -and
        $videoRepositorySource -match "'NON_PRODUCTION',@providerReference,'Prepared'" -and
        $videoMigrationSource -match 'recording_enabled boolean not null default false' -and
        $videoMigrationSource -match 'transcription_enabled boolean not null default false' -and
        $videoMigrationSource -match 'media_transport_enabled boolean not null default false' -and
        $videoRepositorySource -notmatch 'HttpClient|ClientWebSocket|SmtpClient|HubConnection|RTCPeerConnection|MediaStream' -and
        $endpointSource -match '/\{applicantId:guid\}/telehealth-request/\{requestId:guid\}/connection-grants')
    $consultationServiceSource = Get-Content -Raw (Join-Path $solutionRoot 'backend/src/AvenChart.Api/Features/Telehealth/TelehealthConsultationService.cs')
    $consultationRepositorySource = Get-Content -Raw (Join-Path $solutionRoot 'backend/src/AvenChart.Api/Features/Telehealth/TelehealthConsultationRepository.cs')
    $consultationMigrationSource = Get-Content -Raw (Join-Path $solutionRoot 'database/migrations/V0286__telehealth_consultation_start_handoff.sql')
    $wrapUpMigrationSource = Get-Content -Raw (Join-Path $solutionRoot 'database/migrations/V0287__telehealth_consultation_wrap_up_handoff.sql')
    Add-Check 'Consultation handoff and draft remain synthetic with no signing, media, payer, or pharmacy mutation path' (
        $consultationServiceSource -notmatch 'HttpClient|ClientWebSocket|HubConnection|RTCPeerConnection|MediaStream' -and
        $consultationRepositorySource -match 'EncounterRepository' -and
        $consultationRepositorySource -notmatch '(?i)insert\s+into\s+(encounter_signatures|prescriptions|claims|billing)' -and
        $consultationRepositorySource -notmatch 'HttpClient|ClientWebSocket|HubConnection|RTCPeerConnection|MediaStream' -and
        $consultationMigrationSource -match 'legal_effect boolean not null default false' -and
        $consultationMigrationSource -match "modality = 'SYNTHETIC_VIDEO'")
    Add-Check 'Applicant consultation start requires the exact current synthetic queue authorization while real coverage, care, and downstream gates remain closed' (
        $consultationRepositorySource -match 'request\.source_applicant_id is null[\s\S]{0,500}telehealth_coverage_verifications' -and
        $consultationRepositorySource -match 'request\.source_applicant_id is not null and exists\([\s\S]{0,250}telehealth_applicant_request_queue_authorizations' -and
        $consultationRepositorySource -match 'from telehealth_applicant_request_queue_authorizations queue_authorization' -and
        $consultationRepositorySource -match 'queue_authorization\.applicant_id=request\.source_applicant_id' -and
        $consultationRepositorySource -match 'queue_authorization\.canonical_patient_id=request\.patient_id' -and
        $consultationRepositorySource -match 'queue_authorization\.candidate_staff_id=@physician' -and
        $consultationRepositorySource -match 'queue_authorization\.result_valid_through>now\(\)' -and
        $consultationRepositorySource -match 'not queue_authorization\.coverage_verified' -and
        $consultationRepositorySource -match 'not queue_authorization\.consent_created' -and
        $consultationRepositorySource -match 'not queue_authorization\.care_authorized' -and
        $consultationRepositorySource -match 'not queue_authorization\.prescribing_enabled' -and
        $consultationRepositorySource -match 'not queue_authorization\.billing_enabled' -and
        $consultationRepositorySource -match 'not queue_authorization\.claim_created' -and
        $consultationRepositorySource -match 'not queue_authorization\.integration_enabled' -and
        $consultationRepositorySource -match 'not queue_authorization\.external_call_performed' -and
        $consultationRepositorySource -match 'not real coverage verification or a payment guarantee')
    Add-Check 'Consultation workspace and draft are bounded without general-chart, signing, prescribing, or financial access' (
        $consultationRepositorySource -match 'GetWorkspaceAsync' -and
        $consultationRepositorySource -match 'limit 20' -and
        $consultationRepositorySource -notmatch 'PatientChartResponse|PatientRepository|DocumentRepository|MessageRepository|LabRepository' -and
        $consultationRepositorySource -notmatch '(?i)select[\s\S]{0,250}(policy_number|group_number|insurance|street|address_line|email)' -and
        $consultationRepositorySource -match 'SaveDocumentationDraftAsync' -and
        $consultationRepositorySource -match 'EncounterSoapNoteCreateRequest' -and
        $consultationRepositorySource -notmatch '(?i)insert\s+into\s+(encounter_signatures|prescriptions|claims|billing)' -and
        $consultationServiceSource -match 'TelehealthAuthorizationPolicy\.IsPhysicianRole\(session\.Role\)' -and
        $endpointSource -match 'CacheControl = "no-store, private"' -and
        $endpointSource -match 'PhiAuditResourceContext\.Set\(context, "TelehealthConsultation"')
    Add-Check 'Consultation wrap-up is a monotonic unfinished handoff with no signing, disposition, downstream clinical, media, or availability release path' (
        $consultationServiceSource -match 'EnterWrapUpAsync' -and
        $consultationRepositorySource -match 'EnterWrapUpAsync' -and
        $consultationRepositorySource -notmatch '(?i)insert\s+into\s+(encounter_signatures|prescriptions|claims|billing|diagnoses|orders)' -and
        $consultationRepositorySource -notmatch 'HttpClient|ClientWebSocket|HubConnection|RTCPeerConnection|MediaStream' -and
        $wrapUpMigrationSource -match "new\.status <> 'MediaEnded'" -and
        $wrapUpMigrationSource -match "where status in \('Active','Busy','WrapUp'\)" -and
        $wrapUpMigrationSource -match 'does not complete an encounter' -and
        $endpointSource -match 'consultations/\{consultationId:guid\}/wrap-up')
    $pharmacyDirectorySource = Get-Content -Raw (Join-Path $solutionRoot 'backend/src/AvenChart.Api/Features/Telehealth/TelehealthPharmacyDirectory.cs')
    $pharmacyRepositorySource = Get-Content -Raw (Join-Path $solutionRoot 'backend/src/AvenChart.Api/Features/Telehealth/TelehealthPharmacyRepository.cs')
    $pharmacyMigrationSource = Get-Content -Raw (Join-Path $solutionRoot 'database/migrations/V0288__telehealth_synthetic_pharmacy_choice.sql')
    Add-Check 'Synthetic pharmacy choice has a versioned neutral directory and no prescription, claim, geocoder, vendor, or outbound delivery path' (
        $pharmacyDirectorySource -match 'public const string Mode = "NON_PRODUCTION"' -and
        $pharmacyDirectorySource -match 'ElectronicRoutingCapability: "NON_PRODUCTION_ONLY"' -and
        $pharmacyDirectorySource -notmatch 'HttpClient|ClientWebSocket|SmtpClient|HubConnection|WebRequest|Geolocation' -and
        $pharmacyRepositorySource -notmatch '(?i)insert\s+into\s+(prescriptions|medications|encounter_signatures|claims|billing|orders|diagnoses)' -and
        $pharmacyRepositorySource -notmatch 'HttpClient|ClientWebSocket|SmtpClient|HubConnection|WebRequest' -and
        $pharmacyMigrationSource -match "choice_basis='PatientConfirmedDuringConsultation'" -and
        $pharmacyMigrationSource -match "electronic_routing_capability='NON_PRODUCTION_ONLY'" -and
        $endpointSource -match 'consultations/\{consultationId:guid\}/pharmacy-choice')
    $prescriptionRepositorySource = Get-Content -Raw (Join-Path $solutionRoot 'backend/src/AvenChart.Api/Features/Telehealth/TelehealthPrescriptionRepository.cs')
    $prescriptionGatewaySource = Get-Content -Raw (Join-Path $solutionRoot 'backend/src/AvenChart.Api/Features/Telehealth/TelehealthPrescriptionGateways.cs')
    $prescriptionMigrationSource = Get-Content -Raw (Join-Path $solutionRoot 'database/migrations/V0290__telehealth_synthetic_prescription_preparation_draft.sql')
    $prescriptionSigningMigrationSource = Get-Content -Raw (Join-Path $solutionRoot 'database/migrations/V0328__telehealth_synthetic_prescription_signing.sql')
    $finalClinicalReviewRepositorySource = Get-Content -Raw (Join-Path $solutionRoot 'backend/src/AvenChart.Api/Features/Telehealth/TelehealthFinalClinicalReviewRepository.cs')
    $finalClinicalReviewMigrationSource = Get-Content -Raw (Join-Path $solutionRoot 'database/migrations/V0329__telehealth_synthetic_final_clinical_review.sql')
    $finalizationRepositorySource = Get-Content -Raw (Join-Path $solutionRoot 'backend/src/AvenChart.Api/Features/Telehealth/TelehealthEncounterFinalizationRepository.cs')
    Add-Check 'Prescription preparation remains a non-controlled synthetic draft until the bounded signing command' (
        $prescriptionRepositorySource -match 'public const string AdapterMode = "NON_PRODUCTION"' -and
        $prescriptionRepositorySource -match 'public const string IntendedStandard = "NCPDP_SCRIPT_2017071"' -and
        $prescriptionRepositorySource -match 'TransmissionEnabled: false' -and
        $prescriptionRepositorySource -notmatch '(?i)insert\s+into\s+(medications|encounter_signatures|claims|billing|orders|diagnoses|messages|integration_outbox)' -and
        $prescriptionRepositorySource -notmatch 'HttpClient|ClientWebSocket|SmtpClient|HubConnection|WebRequest|SendAsync' -and
        $prescriptionMigrationSource -match 'controlled_substance_schedule_snapshot is null' -and
        $prescriptionMigrationSource -match 'not legal_effect and not safety_checked and not signed and not transmission_queued' -and
        $prescriptionMigrationSource -notmatch '(?im)^\s*(drop\s+table|truncate\s+|delete\s+from)' -and
        $endpointSource -match 'consultations/\{consultationId:guid\}/prescription-preparation-draft')
    Add-Check 'Synthetic prescription signing is zero-list gated, immutable, prepared-only, and cannot contact an external destination' (
        $prescriptionGatewaySource -match 'ActiveMedicationCount != 0' -and
        $prescriptionGatewaySource -match 'ActiveAllergyCount != 0' -and
        $prescriptionGatewaySource -match 'public const string TargetStandard = "NCPDP_SCRIPT_2023011"' -and
        $prescriptionGatewaySource -match 'ExternalDestinationContacted: false' -and
        $prescriptionRepositorySource -match '(?i)insert\s+into\s+prescriptions' -and
        $prescriptionRepositorySource -match 'IsolationLevel\.Serializable' -and
        $prescriptionRepositorySource -match 'TransmissionEnabled: false' -and
        $prescriptionRepositorySource -notmatch 'HttpClient|ClientWebSocket|SmtpClient|HubConnection|WebRequest|SendAsync' -and
        $prescriptionSigningMigrationSource -match "message='signed_telehealth_prescription_is_immutable'" -and
        $prescriptionSigningMigrationSource -match 'not certified' -and
        $prescriptionSigningMigrationSource -match 'not external_destination_contacted' -and
        $prescriptionSigningMigrationSource -match 'not legal_effect' -and
        $prescriptionSigningMigrationSource -notmatch '(?im)^\s*(drop\s+table|truncate\s+|delete\s+from)' -and
        $endpointSource -match 'consultations/\{consultationId:guid\}/prescription')
    Add-Check 'Synthetic final clinical review is source-version-bound, immutable, and cannot sign, complete, bill, claim, deliver, or act externally' (
        $finalClinicalReviewRepositorySource -match 'IsolationLevel.Serializable' -and
        $finalClinicalReviewRepositorySource -match "context.status='MediaEnded'" -and
        $finalClinicalReviewRepositorySource -match "request.status='WrapUp'" -and
        $finalClinicalReviewRepositorySource -match 'ReadReplayAsync' -and
        $finalClinicalReviewMigrationSource -match 'not legal_effect and not encounter_signature_created' -and
        $finalClinicalReviewMigrationSource -match 'not billing_created and not claim_created' -and
        $finalClinicalReviewMigrationSource -match 'not external_destination_contacted' -and
        $endpointSource -match 'consultations/\{consultationId:guid\}/final-clinical-review')
    Add-Check 'Synthetic encounter finalization validates current owner and source versions inside the governed lock transaction and creates no completion, financial, delivery, or external consequence' (
        $finalizationRepositorySource -match 'encounters\.SignAsync' -and
        $finalizationRepositorySource -match 'ReadAndLockSourceAsync' -and
        $finalizationRepositorySource -match 'for update of context,request,reservation,shift,session,appointment,encounter' -and
        $finalizationRepositorySource -match 'review\.prescription_order_id is not distinct from prescription\.order_id' -and
        $finalizationRepositorySource -match 'ExpectedFinalClinicalReviewVersion' -and
        $finalizationRepositorySource -match 'LegalEffect: false' -and
        $finalizationRepositorySource -match 'CompletionCreated: false' -and
        $finalizationRepositorySource -match 'BillingCreated: false' -and
        $finalizationRepositorySource -match 'ClaimCreated: false' -and
        $finalizationRepositorySource -match 'ExternalDestinationContacted: false' -and
        $finalizationRepositorySource -notmatch 'HttpClient|ClientWebSocket|SmtpClient|HubConnection|WebRequest|SendAsync' -and
        $endpointSource -match 'consultations/\{consultationId:guid\}/finalize')
    $syntheticVisitClosureRepositorySource = Get-Content -Raw (Join-Path $solutionRoot 'backend/src/AvenChart.Api/Features/Telehealth/TelehealthSyntheticVisitClosureRepository.cs')
    $syntheticVisitClosureMigrationSource = Get-Content -Raw (Join-Path $solutionRoot 'database/migrations/V0330__telehealth_synthetic_visit_closure.sql')
    Add-Check 'Synthetic visit closure requires the governed encounter lock, atomically closes only the telehealth lifecycle, returns the physician shift to availability, and does not complete the appointment or act externally' (
        $syntheticVisitClosureRepositorySource -match "context.status='MediaEnded'" -and
        $syntheticVisitClosureRepositorySource -match "request.status='WrapUp'" -and
        $syntheticVisitClosureRepositorySource -match 'encounter_signatures signature' -and
        $syntheticVisitClosureRepositorySource -match "set status='Closed'" -and
        $syntheticVisitClosureRepositorySource -match "set status='Active'" -and
        $syntheticVisitClosureRepositorySource -notmatch 'update appointments' -and
        $syntheticVisitClosureRepositorySource -notmatch 'HttpClient|ClientWebSocket|SmtpClient|HubConnection|WebRequest|SendAsync' -and
        $syntheticVisitClosureMigrationSource -match "status='Closed'" -and
        $syntheticVisitClosureMigrationSource -match 'closed_at' -and
        $endpointSource -match 'consultations/\{consultationId:guid\}/close')
    $dispositionRepositorySource = Get-Content -Raw (Join-Path $solutionRoot 'backend/src/AvenChart.Api/Features/Telehealth/TelehealthDispositionRepository.cs')
    $dispositionMigrationSource = Get-Content -Raw (Join-Path $solutionRoot 'database/migrations/V0289__telehealth_synthetic_safety_disposition_draft.sql')
    Add-Check 'Safety disposition remains an unsigned, undelivered, versioned physician draft with no lifecycle, downstream, advice, or outbound path' (
        $dispositionRepositorySource -match 'LegalEffect: false' -and
        $dispositionRepositorySource -match 'Signed: false' -and
        $dispositionRepositorySource -match 'PatientDelivered: false' -and
        $dispositionRepositorySource -notmatch '(?i)insert\s+into\s+(encounter_signatures|prescriptions|medications|claims|billing|orders|diagnoses|messages|integration_outbox)' -and
        $dispositionRepositorySource -notmatch 'HttpClient|ClientWebSocket|SmtpClient|HubConnection|WebRequest|SendAsync' -and
        $dispositionMigrationSource -match 'legal_effect boolean not null default false' -and
        $dispositionMigrationSource -notmatch '(?im)^\s*(drop\s+table|truncate\s+|delete\s+from)' -and
        $endpointSource -match 'consultations/\{consultationId:guid\}/safety-disposition-draft')
    $completionReviewSource = Get-Content -Raw (Join-Path $solutionRoot 'backend/src/AvenChart.Api/Features/Telehealth/TelehealthCompletionReviewRepository.cs')
    Add-Check 'Completion-prerequisites review is a minimized read-only projection with no signing, lifecycle, downstream, or outbound path' (
        $completionReviewSource -match 'IsolationLevel\.RepeatableRead' -and
        $completionReviewSource -match 'DOCUMENTATION_DRAFT_MISSING' -and
        $completionReviewSource -match 'SIGNATURE_FINALIZATION_NOT_IMPLEMENTED' -and
        $completionReviewSource -match 'SigningEnabled: false' -and
        $completionReviewSource -match 'CompletionEnabled: false' -and
        $completionReviewSource -match 'PatientDeliveryEnabled: false' -and
        $completionReviewSource -match 'DownstreamCreationEnabled: false' -and
        $completionReviewSource -notmatch '(?i)insert\s+into|update\s+(telehealth|encounters|appointments|clinical_notes)|delete\s+from|HttpClient|ClientWebSocket|SmtpClient|HubConnection|WebRequest|SendAsync' -and
        $endpointSource -match 'consultations/\{consultationId:guid\}/completion-prerequisites')

    if (-not [string]::IsNullOrWhiteSpace($ApiBaseUrl)) {
        if (([Uri]$ApiBaseUrl).Host -notin @('localhost', '127.0.0.1', '::1')) {
            throw 'Telehealth runtime evidence is local-only.'
        }
        $health = Invoke-RestMethod "$ApiBaseUrl/health/ready" -TimeoutSec 20
        $telehealth = $health.details.telehealth
        Add-Check 'Ready health reports only non-PHI synthetic capability state' (
            $health.status -eq 'healthy' -and
            $telehealth.data.enabled -eq $true -and
            $telehealth.data.mode -eq 'Synthetic' -and
            [int]$telehealth.data.requiredTableCount -eq 71 -and
            [int]$telehealth.data.presentTableCount -eq 71) $telehealth
    }
}
catch {
    Add-Check 'Runtime-safety proof execution' $false $_.Exception.Message
}
finally {
    $result = [ordered]@{
        status=$(if ($passed) { 'passed' } else { 'failed' })
        generatedAtUtc=(Get-Date).ToUniversalTime().ToString('O')
        decisions=@('TH-DEC-0003','TH-DEC-0005','TH-DEC-0006','TH-DEC-0007','TH-DEC-0008','TH-DEC-0009','TH-DEC-0010','TH-DEC-0011','TH-DEC-0012','TH-DEC-0013','TH-DEC-0014','TH-DEC-0015','TH-DEC-0016','TH-DEC-0017','TH-DEC-0018','TH-DEC-0019','TH-DEC-0020','TH-DEC-0021','TH-DEC-0022','TH-DEC-0023','TH-DEC-0024','TH-DEC-0025','TH-DEC-0026','TH-DEC-0027','TH-DEC-0028','TH-DEC-0029','TH-DEC-0030','TH-DEC-0031','TH-DEC-0032','TH-DEC-0033','TH-DEC-0034','TH-DEC-0035','TH-DEC-0036','TH-DEC-0037','TH-DEC-0038','TH-DEC-0039','TH-DEC-0040','TH-DEC-0041','TH-DEC-0042','TH-DEC-0043','TH-DEC-0044','TH-DEC-0045','TH-DEC-0046','TH-DEC-0047','TH-DEC-0048','TH-DEC-0049','TH-DEC-0050','TH-DEC-0051','TH-DEC-0052','TH-DEC-0053','TH-DEC-0054','TH-DEC-0055','TH-DEC-0056','TH-DEC-0057','TH-DEC-0058','TH-DEC-0059','TH-DEC-0060','TH-DEC-0061','TH-DEC-0062','TH-DEC-0063','TH-DEC-0064','TH-DEC-0065','TH-DEC-0066')
        checks=$checks
    }
    $result | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $resultPath -Encoding utf8
    $result | ConvertTo-Json -Depth 8
}

if (-not $passed) { exit 1 }
