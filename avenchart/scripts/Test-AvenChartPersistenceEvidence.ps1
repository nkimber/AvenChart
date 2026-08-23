# SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
# SPDX-License-Identifier: GPL-3.0-or-later

param(
    [int]$PostgresWaitSeconds = 90,
    [int]$MaxQueryMilliseconds = 250
)

$ErrorActionPreference = 'Stop'

if ($PostgresWaitSeconds -lt 1 -or $PostgresWaitSeconds -gt 300) {
    throw 'PostgresWaitSeconds must be between 1 and 300.'
}
if ($MaxQueryMilliseconds -lt 1 -or $MaxQueryMilliseconds -gt 5000) {
    throw 'MaxQueryMilliseconds must be between 1 and 5000.'
}

$SolutionRoot = Resolve-Path (Join-Path $PSScriptRoot '..')
$ArtifactsRoot = Join-Path $SolutionRoot 'artifacts\persistence-evidence'
$ResultPath = Join-Path $ArtifactsRoot 'latest-avenchart-persistence-evidence.json'
$checks = [System.Collections.Generic.List[object]]::new()
$status = 'passed'

function Add-Check {
    param([string]$Name, [bool]$Passed, [object]$Details)

    $script:checks.Add([ordered]@{
        name = $Name
        status = if ($Passed) { 'passed' } else { 'failed' }
        details = $Details
    })
    if (-not $Passed) {
        $script:status = 'failed'
    }
}

function Invoke-PostgresScalar {
    param([string]$Sql)

    $value = docker compose exec -T postgres psql -X -U avenchart -d avenchart -t -A -v ON_ERROR_STOP=1 -c $Sql
    if ($LASTEXITCODE -ne 0) {
        throw 'PostgreSQL scalar query failed while collecting persistence evidence.'
    }
    return ($value | Select-Object -Last 1).Trim()
}

function Wait-Postgres {
    docker compose up -d postgres
    if ($LASTEXITCODE -ne 0) {
        throw 'Could not start PostgreSQL for persistence evidence collection.'
    }

    $deadline = (Get-Date).AddSeconds($PostgresWaitSeconds)
    while ((Get-Date) -lt $deadline) {
        docker compose exec -T postgres pg_isready -U avenchart -d avenchart *> $null
        if ($LASTEXITCODE -eq 0) {
            return
        }
        Start-Sleep -Seconds 2
    }

    throw "PostgreSQL was not ready within $PostgresWaitSeconds seconds."
}

function Find-PlanNodes {
    param($Node)

    $nodes = [System.Collections.Generic.List[object]]::new()
    $nodes.Add($Node)
    if ($null -ne $Node.Plans) {
        foreach ($child in $Node.Plans) {
            foreach ($descendant in Find-PlanNodes -Node $child) {
                $nodes.Add($descendant)
            }
        }
    }
    return $nodes
}

Push-Location $SolutionRoot
try {
    Wait-Postgres
    New-Item -ItemType Directory -Force $ArtifactsRoot | Out-Null

    $facilityId = [int](Invoke-PostgresScalar -Sql @"
select facility_id
from patients
where merged_into_patient_id is null
group by facility_id
order by count(*) desc, facility_id
limit 1;
"@)
    $activePatientCount = [int](Invoke-PostgresScalar -Sql @"
select count(*)
from patients
where facility_id = $facilityId
  and merged_into_patient_id is null;
"@)
    Add-Check 'Synthetic facility fixture has a representative active chart set' (
        $activePatientCount -ge 100
    ) @{ facilityId = $facilityId; activePatientCount = $activePatientCount }

    $planJson = docker compose exec -T postgres psql -X -U avenchart -d avenchart -t -A -v ON_ERROR_STOP=1 -c @"
explain (analyze, buffers, format json)
select p.canonical_id, p.last_name, p.first_name
from patients p
where p.facility_id = $facilityId
  and p.merged_into_patient_id is null
order by p.last_name, p.first_name, p.canonical_id
limit 100;
"@
    if ($LASTEXITCODE -ne 0) {
        throw 'PostgreSQL plan capture failed for facility-scoped patient search.'
    }

    $planDocument = ($planJson -join "`n") | ConvertFrom-Json
    $rootPlan = $planDocument[0].Plan
    $nodes = @(Find-PlanNodes -Node $rootPlan)
    $indexNode = @($nodes | Where-Object {
        $_.'Index Name' -eq 'idx_patients_facility_active_display'
    } | Select-Object -First 1)
    $sortNode = @($nodes | Where-Object { $_.'Node Type' -eq 'Sort' } | Select-Object -First 1)
    $executionMilliseconds = [double]$planDocument[0].'Execution Time'

    Add-Check 'Facility-scoped patient search uses its dedicated active-chart index' (
        $indexNode.Count -eq 1
    ) @{ nodeType = $indexNode[0].'Node Type'; indexName = $indexNode[0].'Index Name' }
    Add-Check 'Facility-scoped patient search avoids a separate display sort' (
        $sortNode.Count -eq 0
    ) @{ sortNode = $sortNode[0].'Node Type' }
    Add-Check 'Facility-scoped patient search remains within the synthetic query guardrail' (
        $executionMilliseconds -le $MaxQueryMilliseconds
    ) @{ executionMilliseconds = $executionMilliseconds; maxQueryMilliseconds = $MaxQueryMilliseconds }

    $baseDate = Invoke-PostgresScalar -Sql 'select base_date from dataset_metadata limit 1;'
    $parsedBaseDate = [DateOnly]::MinValue
    if (-not [DateOnly]::TryParse($baseDate, [ref]$parsedBaseDate)) {
        throw "Synthetic dataset base date '$baseDate' is invalid."
    }
    $flowBoardFacilityId = [int](Invoke-PostgresScalar -Sql @"
select p.facility_id
from appointments a
join patients p on p.legacy_pid = a.pid
where a.appointment_date = '$baseDate'
group by p.facility_id
order by count(*) desc, p.facility_id
limit 1;
"@)
    $flowBoardAppointmentCount = [int](Invoke-PostgresScalar -Sql @"
select count(*)
from appointments a
join patients p on p.legacy_pid = a.pid
where a.appointment_date = '$baseDate'
  and p.facility_id = $flowBoardFacilityId;
"@)
    Add-Check 'Synthetic flow-board fixture has representative appointments' (
        $flowBoardAppointmentCount -ge 10
    ) @{ facilityId = $flowBoardFacilityId; baseDate = $baseDate; appointments = $flowBoardAppointmentCount }

    $flowBoardPlanJson = docker compose exec -T postgres psql -X -U avenchart -d avenchart -t -A -v ON_ERROR_STOP=1 -c @"
explain (analyze, buffers, format json)
select a.id, a.row_version, p.canonical_id, p.first_name || ' ' || p.last_name,
  a.start_time, a.title, a.room, s.first_name || ' ' || s.last_name, f.name, a.status
from appointments a
join patients p on p.legacy_pid = a.pid
left join staff s on s.id = a.provider_id
left join facilities f on f.id = a.facility_id
where a.appointment_date = '$baseDate'
  and p.facility_id = $flowBoardFacilityId
order by a.start_time, a.id;
"@
    if ($LASTEXITCODE -ne 0) {
        throw 'PostgreSQL plan capture failed for the facility-scoped flow board.'
    }

    $flowBoardPlanDocument = ($flowBoardPlanJson -join "`n") | ConvertFrom-Json
    $flowBoardNodes = @(Find-PlanNodes -Node $flowBoardPlanDocument[0].Plan)
    $appointmentIndexNode = @($flowBoardNodes | Where-Object {
        $_.'Index Name' -eq 'idx_appointments_date_start_id'
    } | Select-Object -First 1)
    $flowBoardExecutionMilliseconds = [double]$flowBoardPlanDocument[0].'Execution Time'
    Add-Check 'Facility-scoped flow board uses the appointment date access path' (
        $appointmentIndexNode.Count -eq 1
    ) @{ nodeType = $appointmentIndexNode[0].'Node Type'; indexName = $appointmentIndexNode[0].'Index Name' }
    Add-Check 'Facility-scoped flow board remains within the synthetic query guardrail' (
        $flowBoardExecutionMilliseconds -le $MaxQueryMilliseconds
    ) @{ executionMilliseconds = $flowBoardExecutionMilliseconds; maxQueryMilliseconds = $MaxQueryMilliseconds }

    [ordered]@{
        status = $status
        completedAt = (Get-Date).ToUniversalTime().ToString('o')
        fixture = @{
            patientSearch = @{ facilityId = $facilityId; activePatientCount = $activePatientCount }
            flowBoard = @{ facilityId = $flowBoardFacilityId; baseDate = $baseDate; appointments = $flowBoardAppointmentCount }
        }
        queryGuardrailMilliseconds = $MaxQueryMilliseconds
        checks = $checks
        plans = @{ patientSearch = $planDocument[0]; flowBoard = $flowBoardPlanDocument[0] }
    } | ConvertTo-Json -Depth 100 | Set-Content -LiteralPath $ResultPath -Encoding utf8
    Write-Host "Persistence evidence result: $ResultPath"
}
catch {
    Add-Check 'Persistence evidence execution' $false $_.Exception.Message
    [ordered]@{
        status = 'failed'
        completedAt = (Get-Date).ToUniversalTime().ToString('o')
        checks = $checks
    } | ConvertTo-Json -Depth 20 | Set-Content -LiteralPath $ResultPath -Encoding utf8
    throw
}
finally {
    Pop-Location
}

if ($status -ne 'passed') {
    exit 1
}
