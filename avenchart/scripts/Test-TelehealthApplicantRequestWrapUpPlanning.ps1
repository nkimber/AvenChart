# SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
# SPDX-License-Identifier: GPL-3.0-or-later

[CmdletBinding()]
param(
    [string]$ApiBaseUrl = 'http://127.0.0.1:5001',
    [ValidatePattern('^[a-z][a-z0-9_]{2,62}$')]
    [string]$DatabaseName = 'avenchart',
    [ValidateRange(20,100)]
    [int]$CallerCount = 20
)

$ErrorActionPreference = 'Stop'
if (([Uri]$ApiBaseUrl).Host -notin @('localhost','127.0.0.1','::1')) {
    throw 'Telehealth applicant wrap-up planning proof is local-only.'
}

. (Join-Path $PSScriptRoot 'Test-TelehealthApplicantRequestConsultationStart.ps1') `
    -ApiBaseUrl $ApiBaseUrl -DatabaseName $DatabaseName -CallerCount $CallerCount

$solutionRoot = Resolve-Path (Join-Path $PSScriptRoot '..')
$artifactsRoot = Join-Path $solutionRoot 'artifacts/telehealth'
$resultPath = Join-Path $artifactsRoot 'latest-telehealth-applicant-request-wrap-up-planning.json'
$planningChecks = [System.Collections.Generic.List[object]]::new()
$planningPassed = $true

function Add-PlanningCheck([string]$Name,[bool]$Result,[object]$Details=$null) {
    $script:planningChecks.Add([ordered]@{name=$Name;status=$(if($Result){'passed'}else{'failed'});details=$Details})
    if(-not$Result){$script:planningPassed=$false}
}
function Invoke-PlanningPut([string]$Path,[hashtable]$Headers,[object]$Body) {
    Invoke-RestMethod "$ApiBaseUrl$Path" -Method Put -Headers $Headers -ContentType 'application/json' `
        -Body ($Body|ConvertTo-Json -Compress) -TimeoutSec 40
}
function Invoke-PlanningPost([string]$Path,[hashtable]$Headers,[object]$Body) {
    Invoke-RestMethod "$ApiBaseUrl$Path" -Method Post -Headers $Headers -ContentType 'application/json' `
        -Body ($Body|ConvertTo-Json -Compress) -TimeoutSec 40
}

try {
    $consultationId=[string]$consultation.consultationId
    $ownerHeaders=$providerHeaders.Clone()
    $consequencesBefore=(Invoke-Scalar "select json_build_object(
      'prescriptions',(select count(*) from prescriptions),
      'medications',(select count(*) from medications),
      'signatures',(select count(*) from encounter_signatures),
      'billing',(select count(*) from billing),
      'claims',(select count(*) from claims),
      'messages',(select count(*) from messages),
      'portalMailbox',(select count(*) from portal_mailbox_messages),
      'integrationOutbox',(select count(*) from integration_outbox),
      'integrationInbox',(select count(*) from integration_inbox)
    )::text;")|ConvertFrom-Json

    $draftPath="/api/telehealth/v1/clinician/consultations/$consultationId/documentation/draft"
    $documentation=Invoke-PlanningPut $draftPath $ownerHeaders @{
        expectedVersion=0
        subjective='Synthetic applicant-originated visit history authored by the physician.'
        objective='Synthetic remote observation limitations recorded.'
        assessment=$null
        plan='Synthetic unsigned wrap-up planning only.'
    }
    Add-PlanningCheck 'Applicant-originated consultation accepts only the existing explicit unsigned documentation draft' (
        [int]$documentation.version-eq 1 -and -not$documentation.isSigned -and
        -not$documentation.isFinal -and -not$documentation.isLocked) $documentation

    $wrapPath="/api/telehealth/v1/clinician/consultations/$consultationId/wrap-up"
    $wrapHeaders=$ownerHeaders.Clone()
    $wrapHeaders['X-Idempotency-Key']="sp57-wrap-$([Guid]::NewGuid().ToString('N'))"
    $wrapBody=@{
        expectedVersion=1
        syntheticSessionEndedConfirmed=$true
        documentationStillIncompleteAcknowledged=$true
        wrapUpResponsibilityAcknowledged=$true
    }
    $wrap=Invoke-PlanningPost $wrapPath $wrapHeaders $wrapBody
    $wrapReplay=Invoke-PlanningPost $wrapPath $wrapHeaders $wrapBody
    Add-PlanningCheck 'Exact owning physician enters one unfinished wrap-up transition with stable replay' (
        $wrap.consultationStatus-eq'MediaEnded' -and [int]$wrap.version-eq 2 -and
        $wrap.requestStatus-eq'WrapUp' -and $wrap.shiftStatus-eq'WrapUp' -and
        -not$wrap.completionEnabled -and -not$wrap.clinicianAvailableForNewWork -and
        $wrapReplay.mediaEndedAt-eq$wrap.mediaEndedAt -and
        [int](Invoke-Scalar "select count(*) from telehealth_consultation_events where consultation_id='$consultationId'::uuid and action='consultation-wrap-up-entered';")-eq 1) $wrap

    $applicantStatus=Get-Applicant-Queue-Status $reservedApplicant
    $applicantStatusJson=$applicantStatus|ConvertTo-Json -Depth 10 -Compress
    Add-PlanningCheck 'Applicant polling reaches minimized unfinished wrap-up without protected or consequential facts' (
        $applicantStatus.requestStatus-eq'WrapUp' -and $applicantStatus.phase-eq'WrapUp' -and
        -not$applicantStatus.coverageVerified -and -not$applicantStatus.consentCreated -and
        -not$applicantStatus.careAuthorized -and -not$applicantStatus.integrationEnabled -and
        $applicantStatusJson-notmatch'gold-provider|physicianStaffId|providerId|patientId|encounterId|appointmentId|memberId|policyNumber|documentation|prescriptionId') $applicantStatus

    $pharmacyPath="/api/telehealth/v1/clinician/consultations/$consultationId/pharmacy-choices"
    $pharmacyWorkspace=Invoke-RestMethod "$ApiBaseUrl$pharmacyPath`?state=$state&limit=25" -Headers $ownerHeaders -TimeoutSec 40
    $directoryEntry=@($pharmacyWorkspace.pharmacies)|Select-Object -First 1
    if($null-eq$directoryEntry){throw "No synthetic pharmacy fixture is available for $state."}
    $choicePath="/api/telehealth/v1/clinician/consultations/$consultationId/pharmacy-choice"
    $choiceHeaders=$ownerHeaders.Clone()
    $choiceHeaders['X-Idempotency-Key']="sp57-pharmacy-$([Guid]::NewGuid().ToString('N'))"
    $choice=Invoke-PlanningPut $choicePath $choiceHeaders @{
        expectedVersion=0
        directoryEntryId=$directoryEntry.directoryEntryId
        patientChoiceConfirmed=$true
        syntheticDataConfirmed=$true
    }
    Add-PlanningCheck 'Owner records one patient-confirmed neutral synthetic pharmacy destination without transmission' (
        [int]$choice.version-eq 1 -and $choice.patientChoiceConfirmed -and
        -not$choice.prescriptionCreated -and -not$choice.transmitted -and
        -not$pharmacyWorkspace.prescriptionEnabled -and -not$pharmacyWorkspace.transmissionEnabled) @{
            directoryEntryId=$choice.directoryEntryId
            adapterMode=$pharmacyWorkspace.adapterMode
            datasetVersion=$pharmacyWorkspace.datasetVersion
        }

    $prescriptionPath="/api/telehealth/v1/clinician/consultations/$consultationId/prescription-preparation-draft"
    $prescriptionWorkspace=Invoke-RestMethod "$ApiBaseUrl$prescriptionPath`?query=metformin" -Headers $ownerHeaders -TimeoutSec 40
    $catalogItem=@($prescriptionWorkspace.catalogResults|Where-Object{$_.rxNormCode-eq'860975'})|Select-Object -First 1
    if($null-eq$catalogItem){throw 'The bounded non-controlled synthetic medication fixture was not returned.'}
    $prescriptionHeaders=$ownerHeaders.Clone()
    $prescriptionHeaders['X-Idempotency-Key']="sp57-prescription-$([Guid]::NewGuid().ToString('N'))"
    $prescriptionDraft=Invoke-PlanningPut $prescriptionPath $prescriptionHeaders @{
        expectedVersion=0
        rxNormCode='860975'
        doseAmount=1
        doseUnit='tablet'
        frequency='once daily (synthetic)'
        quantityValue=7
        quantityUnit='tablet'
        durationDays=7
        refills=0
        indication='Synthetic physician-authored indication.'
        directions='Take one synthetic tablet once daily for seven days.'
        medicationListReviewed=$true
        allergyListReviewed=$true
        adequateEvaluationCompleted=$true
        syntheticDataConfirmed=$true
    }
    Add-PlanningCheck 'Owner records one catalog-bound preparation draft with no legal, safety, signature, transmission, or delivery effect' (
        [int]$prescriptionDraft.version-eq 1 -and -not$prescriptionDraft.legalEffect -and
        -not$prescriptionDraft.safetyChecked -and -not$prescriptionDraft.signed -and
        -not$prescriptionDraft.transmissionQueued -and -not$prescriptionDraft.transmitted -and
        -not$prescriptionDraft.patientDelivered -and
        -not$prescriptionWorkspace.prescriptionCreationEnabled -and -not$prescriptionWorkspace.transmissionEnabled) @{
            rxNormCode=$prescriptionDraft.rxNormCode
            intendedStandard=$prescriptionWorkspace.intendedStandard
            adapterMode=$prescriptionWorkspace.adapterMode
        }

    $dispositionPath="/api/telehealth/v1/clinician/consultations/$consultationId/safety-disposition-draft"
    $dispositionWorkspace=Invoke-RestMethod "$ApiBaseUrl$dispositionPath" -Headers $ownerHeaders -TimeoutSec 40
    $dispositionHeaders=$ownerHeaders.Clone()
    $dispositionHeaders['X-Idempotency-Key']="sp57-disposition-$([Guid]::NewGuid().ToString('N'))"
    $disposition=Invoke-PlanningPut $dispositionPath $dispositionHeaders @{
        expectedVersion=0
        dispositionCode='TreatedTelehealth'
        adequateEvaluationCompleted=$true
        followUpOwner='Patient'
        followUpTimeframe='within two synthetic days'
        nextStepInstructions='Physician-authored synthetic next step.'
        warningEscalationInstructions='Physician-authored synthetic warning and escalation instruction.'
        communicationMethod='DiscussedDuringSyntheticConsultation'
        communicationCompleted=$true
        locationCallbackReconfirmed=$false
        emergencyInstructionProvided=$false
        emergencyHandoffStatus=$null
        contactAttemptSummary=$null
        syntheticDataConfirmed=$true
    }
    Add-PlanningCheck 'Owner records one unsigned safety-disposition draft without delivery or finalization' (
        [int]$disposition.version-eq 1 -and -not$disposition.legalEffect -and
        -not$disposition.signed -and -not$disposition.finalized -and -not$disposition.patientDelivered -and
        -not$dispositionWorkspace.signingEnabled -and -not$dispositionWorkspace.patientDeliveryEnabled -and
        -not$dispositionWorkspace.completionEnabled) $disposition

    $completionPath="/api/telehealth/v1/clinician/consultations/$consultationId/completion-prerequisites"
    $completion=Invoke-RestMethod "$ApiBaseUrl$completionPath" -Headers $ownerHeaders -TimeoutSec 40
    $completionJson=$completion|ConvertTo-Json -Depth 12 -Compress
    Add-PlanningCheck 'Completion review reports structural presence while permanent consequential blockers remain' (
        $completion.structuralEvidencePresent -and [int]$completion.documentation.version-eq 1 -and
        [int]$completion.safetyDisposition.version-eq 1 -and [int]$completion.pharmacyChoice.version-eq 1 -and
        @($completion.productBlockers)-contains'FINAL_CLINICAL_REVIEW_NOT_RECORDED' -and
        @($completion.productBlockers)-contains'SIGNATURE_FINALIZATION_NOT_IMPLEMENTED' -and
        @($completion.productBlockers)-contains'ATOMIC_DOWNSTREAM_OWNERSHIP_NOT_IMPLEMENTED' -and
        -not$completion.signingEnabled -and -not$completion.completionEnabled -and
        -not$completion.patientDeliveryEnabled -and -not$completion.downstreamCreationEnabled -and
        $completionJson-notmatch'Synthetic applicant-originated|Physician-authored|patientId|encounterId|appointmentId|requestId|ncpdp|npi') @{
            blockers=@($completion.productBlockers)
            structuralEvidencePresent=$completion.structuralEvidencePresent
        }

    $consequencesAfter=(Invoke-Scalar "select json_build_object(
      'prescriptions',(select count(*) from prescriptions),
      'medications',(select count(*) from medications),
      'signatures',(select count(*) from encounter_signatures),
      'billing',(select count(*) from billing),
      'claims',(select count(*) from claims),
      'messages',(select count(*) from messages),
      'portalMailbox',(select count(*) from portal_mailbox_messages),
      'integrationOutbox',(select count(*) from integration_outbox),
      'integrationInbox',(select count(*) from integration_inbox)
    )::text;")|ConvertFrom-Json
    $planningFacts=(Invoke-Scalar "select json_build_object(
      'noteVersions',(select count(*) from clinical_notes note join telehealth_consultation_contexts context on context.encounter_id=note.encounter where context.consultation_id='$consultationId'::uuid),
      'pharmacyVersions',(select count(*) from telehealth_consultation_pharmacy_choice_versions where consultation_id='$consultationId'::uuid),
      'prescriptionDraftVersions',(select count(*) from telehealth_consultation_prescription_draft_versions where consultation_id='$consultationId'::uuid),
      'dispositionVersions',(select count(*) from telehealth_consultation_disposition_draft_versions where consultation_id='$consultationId'::uuid),
      'requestStatus',(select request.status from telehealth_requests request join telehealth_consultation_contexts context on context.request_id=request.request_id where context.consultation_id='$consultationId'::uuid),
      'shiftStatus',(select shift.status from telehealth_clinician_shifts shift join telehealth_consultation_contexts context on context.shift_id=shift.shift_id where context.consultation_id='$consultationId'::uuid),
      'appointmentStatus',(select appointment.status from appointments appointment join telehealth_consultation_contexts context on context.appointment_id=appointment.id where context.consultation_id='$consultationId'::uuid)
    )::text;")|ConvertFrom-Json
    Add-PlanningCheck 'Applicant planning drafts remain isolated from canonical prescribing, completion, financial, communication, and integration state' (
        ($consequencesBefore|ConvertTo-Json -Compress)-eq($consequencesAfter|ConvertTo-Json -Compress) -and
        [int]$planningFacts.noteVersions-eq 1 -and [int]$planningFacts.pharmacyVersions-eq 1 -and
        [int]$planningFacts.prescriptionDraftVersions-eq 1 -and [int]$planningFacts.dispositionVersions-eq 1 -and
        $planningFacts.requestStatus-eq'WrapUp' -and $planningFacts.shiftStatus-eq'WrapUp' -and
        $planningFacts.appointmentStatus-eq'>') @{planning=$planningFacts;before=$consequencesBefore;after=$consequencesAfter}
}
catch {
    Add-PlanningCheck 'Applicant wrap-up planning proof execution' $false @{message=$_.Exception.Message;stack=$_.ScriptStackTrace}
}
finally {
    New-Item -ItemType Directory -Force -Path $artifactsRoot | Out-Null
    $report=[ordered]@{
        generatedAt=(Get-Date).ToUniversalTime().ToString('o')
        decision='TH-DEC-0060'
        apiBaseUrl=$ApiBaseUrl
        database=$DatabaseName
        passed=$planningPassed
        checkCount=$planningChecks.Count
        checks=$planningChecks
    }
    $report|ConvertTo-Json -Depth 12|Set-Content -LiteralPath $resultPath -Encoding utf8
}

if(-not$planningPassed){throw "Telehealth applicant wrap-up planning proof failed. See $resultPath"}
Write-Host "Telehealth applicant wrap-up planning proof passed $($planningChecks.Count) checks."
