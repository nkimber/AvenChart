# SPDX-FileCopyrightText: 2026 Neil Kimber and Legacy EHR Modernization Project contributors
# SPDX-License-Identifier: GPL-3.0-or-later

param(
    [int]$PostgresWaitSeconds = 90
)

$ErrorActionPreference = "Stop"

$SolutionRoot = Resolve-Path (Join-Path $PSScriptRoot "..")
$ArtifactsRoot = Join-Path $SolutionRoot "artifacts"
$SqlPath = Join-Path $ArtifactsRoot "postgres\seed-gold.sql"
$ResultPath = Join-Path $ArtifactsRoot "latest-modernized-seed-result.json"
$SeedLock = [System.Threading.Mutex]::new($false, "Global\AvenChartSchemaMaintenance")
$SeedLockHeld = $false
$LocationPushed = $false
$SeedCompleted = $false
$ServicesToRestart = @()

try {
    $SeedLockHeld = $SeedLock.WaitOne([TimeSpan]::FromMinutes(15))
    if (-not $SeedLockHeld) {
        throw "Timed out waiting for the modernized gold-seed/reset lock."
    }

    Push-Location $SolutionRoot
    $LocationPushed = $true
    New-Item -ItemType Directory -Force $ArtifactsRoot | Out-Null

    $runningServices = @(docker compose ps --services --filter status=running)
    if ($LASTEXITCODE -ne 0) {
        throw "Could not inspect the modernized service state before reset."
    }
    $ServicesToRestart = @(@('api', 'frontend') | Where-Object { $_ -in $runningServices })
    if ($ServicesToRestart.Count -gt 0) {
        docker compose stop frontend api
        if ($LASTEXITCODE -ne 0) {
            throw "Could not stop the API and frontend before the database reset."
        }
    }

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

    # Recreate the application schema as one deterministic boundary. Versioned migrations can add
    # foreign keys from migration-owned tables into seed-owned tables, so dropping seed tables one
    # at a time is not a reliable clean reset once those relationships exist.
    $schemaResetSql = @'
drop schema if exists public cascade;
create schema public authorization legacy-ehr;
grant all on schema public to legacy-ehr;
grant all on schema public to public;
'@
    $schemaResetSql | docker compose exec -T postgres psql -U legacy-ehr -d legacy-ehr_modernized -v ON_ERROR_STOP=1
    if ($LASTEXITCODE -ne 0) {
        throw "Modernized application schema reset failed with exit code $LASTEXITCODE."
    }

    $sql = Get-Content -LiteralPath $SqlPath -Raw
    $sql | docker compose exec -T postgres psql -U legacy-ehr -d legacy-ehr_modernized -v ON_ERROR_STOP=1
    if ($LASTEXITCODE -ne 0) {
        throw "Gold dataset import failed with exit code $LASTEXITCODE."
    }

    # The generated dataset rebuilds the base schema. Reapply all versioned migrations afterward so
    # migration-owned tables and schema extensions are present in every deterministic reset.
    $migrationLedgerSql = @'
create table if not exists schema_migrations (
  migration_id text primary key,
  checksum_sha256 text not null,
  description text not null,
  applied_at timestamptz not null,
  applied_by text not null
);
delete from schema_migrations;
'@
    $migrationLedgerSql | docker compose exec -T postgres psql -U legacy-ehr -d legacy-ehr_modernized -v ON_ERROR_STOP=1
    if ($LASTEXITCODE -ne 0) {
        throw "Modernized migration ledger reset failed with exit code $LASTEXITCODE."
    }

    & .\scripts\Invoke-ModernizedMigrations.ps1 -SkipPostgresStartup
    if ($LASTEXITCODE -ne 0) {
        throw "Modernized schema migrations failed with exit code $LASTEXITCODE."
    }

    $countsJson = docker compose exec -T postgres psql -U legacy-ehr -d legacy-ehr_modernized -t -A -c "select json_build_object('patients',(select count(*) from patients),'insuranceRecords',(select count(*) from insurance_records),'patientHistories',(select count(*) from patient_histories),'portalAccounts',(select count(*) from patient_portal_accounts),'portalProfileChangeRequests',(select count(*) from patient_portal_profile_change_requests),'portalReportAuditEvents',(select count(*) from patient_portal_report_audit_events),'portalMessageAuditEvents',(select count(*) from patient_portal_message_audit_events),'appointments',(select count(*) from appointments),'encounters',(select count(*) from encounters),'encounterSignatures',(select count(*) from encounter_signatures),'vitals',(select count(*) from vitals),'clinicalNotes',(select count(*) from clinical_notes),'prescriptions',(select count(*) from prescriptions),'billing',(select count(*) from billing),'labProviders',(select count(*) from lab_providers),'labOrders',(select count(*) from lab_orders),'procedureOrderCatalogItems',(select count(*) from lab_order_catalog),'labReports',(select count(*) from lab_reports),'labResults',(select count(*) from lab_results),'messages',(select count(*) from messages),'portalMailboxMessages',(select count(*) from portal_mailbox_messages),'patientReminders',(select count(*) from patient_reminders),'patientDocuments',(select count(*) from patient_documents),'problems',(select count(*) from problems),'allergies',(select count(*) from allergies),'medications',(select count(*) from medications));"
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
    $SeedCompleted = $true

    if ($ServicesToRestart.Count -gt 0) {
        docker compose up -d @ServicesToRestart
        if ($LASTEXITCODE -ne 0) {
            throw "The gold dataset was seeded, but the previously running application services could not be restarted."
        }
    }

    Write-Host "Modernized gold dataset seed complete: $ResultPath"
}
finally {
    if ($LocationPushed) {
        Pop-Location
    }
    if (-not $SeedCompleted -and $ServicesToRestart.Count -gt 0) {
        Write-Warning "The database reset did not complete. API and frontend services remain stopped until migrations and readiness checks pass."
    }
    if ($SeedLockHeld) {
        $SeedLock.ReleaseMutex()
    }
    $SeedLock.Dispose()
}
