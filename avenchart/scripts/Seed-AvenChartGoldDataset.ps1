param(
    [int]$PostgresWaitSeconds = 90
)

$ErrorActionPreference = "Stop"

$SolutionRoot = Resolve-Path (Join-Path $PSScriptRoot "..")
$ArtifactsRoot = Join-Path $SolutionRoot "artifacts"
$SqlPath = Join-Path $ArtifactsRoot "postgres\seed-gold.sql"
$ResultPath = Join-Path $ArtifactsRoot "latest-modernized-seed-result.json"

Push-Location $SolutionRoot
try {
    New-Item -ItemType Directory -Force $ArtifactsRoot | Out-Null

    node .\scripts\generate-postgres-seed.mjs
    if ($LASTEXITCODE -ne 0) {
        throw "Gold dataset SQL generation failed with exit code $LASTEXITCODE."
    }

    docker compose up -d postgres
    if ($LASTEXITCODE -ne 0) {
        throw "Failed to start the modernized PostgreSQL service."
    }

    $deadline = (Get-Date).AddSeconds($PostgresWaitSeconds)
    $ready = $false
    while ((Get-Date) -lt $deadline) {
        docker compose exec -T postgres pg_isready -U legacy-ehr -d legacy-ehr_modernized *> $null
        if ($LASTEXITCODE -eq 0) {
            $ready = $true
            break
        }

        Start-Sleep -Seconds 2
    }

    if (-not $ready) {
        throw "PostgreSQL was not ready within $PostgresWaitSeconds seconds."
    }

    $sql = Get-Content -LiteralPath $SqlPath -Raw
    $sql | docker compose exec -T postgres psql -U legacy-ehr -d legacy-ehr_modernized -v ON_ERROR_STOP=1
    if ($LASTEXITCODE -ne 0) {
        throw "Gold dataset import failed with exit code $LASTEXITCODE."
    }

    $countsJson = docker compose exec -T postgres psql -U legacy-ehr -d legacy-ehr_modernized -t -A -c "select json_build_object('patients',(select count(*) from patients),'insuranceRecords',(select count(*) from insurance_records),'patientHistories',(select count(*) from patient_histories),'portalAccounts',(select count(*) from patient_portal_accounts),'portalReportAuditEvents',(select count(*) from patient_portal_report_audit_events),'portalMessageAuditEvents',(select count(*) from patient_portal_message_audit_events),'appointments',(select count(*) from appointments),'encounters',(select count(*) from encounters),'encounterSignatures',(select count(*) from encounter_signatures),'vitals',(select count(*) from vitals),'clinicalNotes',(select count(*) from clinical_notes),'prescriptions',(select count(*) from prescriptions),'billing',(select count(*) from billing),'labProviders',(select count(*) from lab_providers),'labOrders',(select count(*) from lab_orders),'procedureOrderCatalogItems',(select count(*) from lab_order_catalog),'labReports',(select count(*) from lab_reports),'labResults',(select count(*) from lab_results),'messages',(select count(*) from messages),'portalMailboxMessages',(select count(*) from portal_mailbox_messages),'patientDocuments',(select count(*) from patient_documents),'problems',(select count(*) from problems),'allergies',(select count(*) from allergies),'medications',(select count(*) from medications));"
    if ($LASTEXITCODE -ne 0) {
        throw "Could not read modernized seed counts."
    }

    $result = [ordered]@{
        status = "passed"
        generatedAt = (Get-Date).ToUniversalTime().ToString("o")
        datasetId = "legacy-ehr-shared-synthetic-v1"
        database = "legacy-ehr_modernized"
        sqlPath = $SqlPath
        counts = $countsJson | ConvertFrom-Json
    }

    $result | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $ResultPath -Encoding UTF8
    Write-Host "Modernized gold dataset seed complete: $ResultPath"
}
finally {
    Pop-Location
}
