# SPDX-FileCopyrightText: 2026 Neil Kimber and Legacy EHR Modernization Project contributors
# SPDX-License-Identifier: GPL-3.0-or-later

param(
    [int]$PostgresWaitSeconds = 90,
    [switch]$SkipPostgresStartup,
    [string]$DatabaseName = "legacy-ehr_modernized",
    [int]$TestFaultAfterAppliedMigrationCount = 0,
    [switch]$SkipHostLock,
    [switch]$SkipImageBuild,
    [switch]$SkipArtifact
)

$ErrorActionPreference = "Stop"

if ($DatabaseName -ne "legacy-ehr_modernized" -and $DatabaseName -notmatch '^legacy-ehr_modernized_test_[a-z0-9_]+$') {
    throw "DatabaseName must be 'legacy-ehr_modernized' or an isolated legacy-ehr_modernized_test_* database."
}
if ($TestFaultAfterAppliedMigrationCount -lt 0) {
    throw "TestFaultAfterAppliedMigrationCount cannot be negative."
}
if ($TestFaultAfterAppliedMigrationCount -gt 0 -and $DatabaseName -eq "legacy-ehr_modernized") {
    throw "Migration fault injection is only allowed against an isolated legacy-ehr_modernized_test_* database."
}

$SolutionRoot = Resolve-Path (Join-Path $PSScriptRoot "..")
$ArtifactsRoot = Join-Path $SolutionRoot "artifacts\migrations"
$ResultPath = Join-Path $ArtifactsRoot "latest-modernized-migration-result.json"
$mutexPrefix = if ($IsWindows) { "Global\" } else { "" }
$MigrationLock = [System.Threading.Mutex]::new($false, "$($mutexPrefix)AvenChartSchemaMaintenance")
$MigrationLockHeld = $false
$LocationPushed = $false

try {
    if (-not $SkipHostLock) {
        $MigrationLockHeld = $MigrationLock.WaitOne([TimeSpan]::FromMinutes(15))
        if (-not $MigrationLockHeld) {
            throw "Timed out waiting for the modernized schema-maintenance lock."
        }
    }

    Push-Location $SolutionRoot
    $LocationPushed = $true

    if (-not $SkipPostgresStartup) {
        docker compose up -d postgres
        if ($LASTEXITCODE -ne 0) {
            throw "Failed to start the modernized PostgreSQL service."
        }

        $deadline = (Get-Date).AddSeconds($PostgresWaitSeconds)
        $ready = $false
        while ((Get-Date) -lt $deadline) {
            docker compose exec -T postgres pg_isready -U legacy-ehr -d $DatabaseName *> $null
            if ($LASTEXITCODE -eq 0) {
                $ready = $true
                break
            }

            Start-Sleep -Seconds 2
        }

        if (-not $ready) {
            throw "PostgreSQL database '$DatabaseName' was not ready within $PostgresWaitSeconds seconds."
        }
    }

    $connectionString = "Host=postgres;Port=5432;Database=$DatabaseName;Username=legacy-ehr;Password=legacy-ehr_demo"
    $composeArguments = @(
        "compose", "run", "--rm", "--no-deps"
    )
    if (-not $SkipImageBuild) {
        $composeArguments += @("--build", "--quiet-build")
    }
    $composeArguments += @(
        "-e", "ConnectionStrings__AvenChart=$connectionString",
        "-e", "DatabaseSchema__MigrationsPath=/app/database/migrations"
    )
    if ($TestFaultAfterAppliedMigrationCount -gt 0) {
        $composeArguments += @(
            "-e", "DatabaseSchema__AllowTestFaultInjection=true",
            "-e", "DatabaseSchema__FaultAfterAppliedMigrationCount=$TestFaultAfterAppliedMigrationCount"
        )
    }
    $composeArguments += "migrator"

    $priorErrorActionPreference = $ErrorActionPreference
    try {
        # Windows PowerShell promotes Docker build progress written to stderr into terminating
        # NativeCommandError records when Stop is active. Capture it, then judge the native exit code.
        $ErrorActionPreference = "Continue"
        $output = @(& docker @composeArguments 2>&1)
        $migrationExitCode = $LASTEXITCODE
    }
    finally {
        $ErrorActionPreference = $priorErrorActionPreference
    }
    if ($migrationExitCode -ne 0) {
        $output | ForEach-Object { Write-Host $_ }
        throw "The packaged schema migrator failed with exit code $migrationExitCode."
    }

    $resultLine = $output |
        ForEach-Object { $_.ToString().Trim() } |
        Where-Object { $_.StartsWith('{') -and $_.EndsWith('}') } |
        Select-Object -Last 1
    if (-not $resultLine) {
        $output | ForEach-Object { Write-Host $_ }
        throw "The packaged schema migrator did not emit its JSON completion result."
    }
    $migrationResult = $resultLine | ConvertFrom-Json
    $output |
        ForEach-Object { $_.ToString().Trim() } |
        Where-Object { $_ -ne $resultLine } |
        ForEach-Object { Write-Host $_ }
    Write-Host "Packaged schema migrator verified $($migrationResult.expected) migrations; applied $(@($migrationResult.applied).Count), already present $(@($migrationResult.alreadyApplied).Count)."

    if (-not $SkipArtifact) {
        New-Item -ItemType Directory -Force $ArtifactsRoot | Out-Null
        [ordered]@{
            status = "passed"
            generatedAt = (Get-Date).ToUniversalTime().ToString("o")
            database = $DatabaseName
            expected = $migrationResult.expected
            applied = @($migrationResult.applied)
            alreadyApplied = @($migrationResult.alreadyApplied)
        } | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath $ResultPath -Encoding UTF8
        Write-Host "Modernized migrations complete: $ResultPath"
    }
    else {
        Write-Host "Modernized migrations complete for '$DatabaseName'."
    }
}
finally {
    if ($LocationPushed) {
        Pop-Location
    }
    if ($MigrationLockHeld) {
        $MigrationLock.ReleaseMutex()
    }
    $MigrationLock.Dispose()
}
