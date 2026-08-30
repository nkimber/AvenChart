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
    throw 'Telehealth synthetic prescription-signing proof is local-only.'
}

. (Join-Path $PSScriptRoot 'Test-TelehealthApplicantRequestWrapUpPlanning.ps1') `
    -ApiBaseUrl $ApiBaseUrl -DatabaseName $DatabaseName -CallerCount $CallerCount

$solutionRoot = Resolve-Path (Join-Path $PSScriptRoot '..')
$artifactsRoot = Join-Path $solutionRoot 'artifacts/telehealth'
$resultPath = Join-Path $artifactsRoot 'latest-telehealth-synthetic-prescription-signing.json'
$signingChecks = [System.Collections.Generic.List[object]]::new()
$signingPassed = $true

function Add-SigningCheck([string]$Name,[bool]$Result,[object]$Details=$null) {
    $script:signingChecks.Add([ordered]@{name=$Name;status=$(if($Result){'passed'}else{'failed'});details=$Details})
    if(-not$Result){$script:signingPassed=$false}
}

try {
    $signPath="/api/telehealth/v1/clinician/consultations/$consultationId/prescription"
    $signHeaders=$ownerHeaders.Clone()
    $signHeaders['X-Idempotency-Key']="sp58-sign-$([Guid]::NewGuid().ToString('N'))"
    $signBody=@{
        expectedDraftVersion=[int]$prescriptionDraft.version
        noCurrentMedicationsConfirmed=$true
        noKnownAllergiesConfirmed=$true
        adequateEvaluationConfirmed=$true
        syntheticDataConfirmed=$true
    }
    $signed=Invoke-PlanningPost $signPath $signHeaders $signBody
    $replay=Invoke-PlanningPost $signPath $signHeaders $signBody
    Add-SigningCheck 'Exact owner creates one safety-gated immutable synthetic prescription with stable replay' (
        $signed.prescriptionId-eq$replay.prescriptionId -and $signed.orderId-eq$replay.orderId -and
        $signed.safetyOutcome-eq'SYNTHETIC_ZERO_LIST_GATE_PASSED' -and
        [int]$signed.activeMedicationCount-eq 0 -and [int]$signed.activeAllergyCount-eq 0 -and
        $signed.safetyChecked -and $signed.signed -and $signed.canonicalPrescriptionCreated -and
        -not$signed.legalEffect -and -not$signed.patientDelivered) $signed

    Add-SigningCheck 'Prepared NewRx targets SCRIPT 2023011 without certification, transmission, or external destination' (
        $signed.adapterMode-eq'NON_PRODUCTION' -and
        $signed.canonicalModelVersion-eq'AVENCHART_ERX_CANONICAL_V1' -and
        $signed.targetStandard-eq'NCPDP_SCRIPT_2023011' -and
        $signed.transitionStandard-eq'NCPDP_SCRIPT_2017071_THROUGH_2027_12_31' -and
        $signed.transactionType-eq'NewRx' -and $signed.transmissionState-eq'PreparedOnly' -and
        -not$signed.certified -and -not$signed.externalDestinationContacted) $signed

    $workspaceAfter=Invoke-RestMethod "$ApiBaseUrl$prescriptionPath" -Headers $ownerHeaders -TimeoutSec 40
    Add-SigningCheck 'Workspace exposes the immutable result and closes repeat signing capability' (
        $workspaceAfter.currentSignedPrescription.prescriptionId-eq$signed.prescriptionId -and
        -not$workspaceAfter.safetyCheckEnabled -and -not$workspaceAfter.signingEnabled -and
        -not$workspaceAfter.prescriptionCreationEnabled -and -not$workspaceAfter.transmissionEnabled -and
        -not$workspaceAfter.patientDeliveryEnabled -and -not$workspaceAfter.completionEnabled) $workspaceAfter

    $facts=(Invoke-Scalar "select json_build_object(
      'orders',(select count(*) from telehealth_consultation_prescription_orders where consultation_id='$consultationId'::uuid),
      'prescriptions',(select count(*) from prescriptions where id='$($signed.prescriptionId)'),
      'audits',(select count(*) from prescription_audit_events where prescription_id='$($signed.prescriptionId)'),
      'uploaded',(select erx_uploaded from prescriptions where id='$($signed.prescriptionId)'),
      'requestStatus',(select request.status from telehealth_requests request join telehealth_consultation_contexts context on context.request_id=request.request_id where context.consultation_id='$consultationId'::uuid),
      'shiftStatus',(select shift.status from telehealth_clinician_shifts shift join telehealth_consultation_contexts context on context.shift_id=shift.shift_id where context.consultation_id='$consultationId'::uuid),
      'appointmentStatus',(select appointment.status from appointments appointment join telehealth_consultation_contexts context on context.appointment_id=appointment.id where context.consultation_id='$consultationId'::uuid),
      'outbox',(select count(*) from integration_outbox)
    )::text;")|ConvertFrom-Json
    Add-SigningCheck 'Atomic persistence creates only the prescription and audit while lifecycle and integrations remain unfinished' (
        [int]$facts.orders-eq 1 -and [int]$facts.prescriptions-eq 1 -and [int]$facts.audits-eq 1 -and
        [int]$facts.uploaded-eq 0 -and $facts.requestStatus-eq'WrapUp' -and
        $facts.shiftStatus-eq'WrapUp' -and $facts.appointmentStatus-eq'>' -and [int]$facts.outbox-eq 0) $facts

    $mutationRejected=$false
    $mutationError=$null
    try {
        Invoke-Scalar "update prescriptions set note='forbidden mutation' where id='$($signed.prescriptionId)' returning id;" | Out-Null
    } catch {
        $mutationRejected=$true
        $mutationError=$_.Exception.Message
    }
    $storedDirections=Invoke-Scalar "select note from prescriptions where id='$($signed.prescriptionId)';"
    Add-SigningCheck 'Database rejects mutation of the signed canonical prescription' (
        $mutationRejected -and $storedDirections-eq$signed.directions) @{
        error=$mutationError
        contentUnchanged=($storedDirections-eq$signed.directions)
    }
}
catch {
    Add-SigningCheck 'Synthetic prescription-signing proof execution' $false @{message=$_.Exception.Message;stack=$_.ScriptStackTrace}
}
finally {
    New-Item -ItemType Directory -Force -Path $artifactsRoot | Out-Null
    $report=[ordered]@{
        generatedAt=(Get-Date).ToUniversalTime().ToString('o')
        decision='TH-DEC-0061'
        apiBaseUrl=$ApiBaseUrl
        database=$DatabaseName
        passed=$signingPassed
        checkCount=$signingChecks.Count
        checks=$signingChecks
    }
    $report|ConvertTo-Json -Depth 12|Set-Content -LiteralPath $resultPath -Encoding utf8
}

if(-not$signingPassed){throw "Telehealth synthetic prescription-signing proof failed. See $resultPath"}
Write-Host "Telehealth synthetic prescription-signing proof passed $($signingChecks.Count) checks."
