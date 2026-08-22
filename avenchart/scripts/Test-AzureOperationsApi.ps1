# SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
# SPDX-License-Identifier: GPL-3.0-or-later

param(
    [int]$ApiPort = 5019,
    [int]$ApiWaitSeconds = 90
)

$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'AvenChartStaffAccessContext.ps1')

if ($ApiPort -lt 1024 -or $ApiPort -gt 65535) {
    throw 'ApiPort must be between 1024 and 65535.'
}

$SolutionRoot = Resolve-Path (Join-Path $PSScriptRoot '..')
$RepositoryRoot = Resolve-Path (Join-Path $SolutionRoot '..')
$DatabaseName = "avenchart_test_$([Guid]::NewGuid().ToString('N'))"
$ApiProcess = $null
$StdoutPath = Join-Path ([IO.Path]::GetTempPath()) "$DatabaseName-api.out.log"
$StderrPath = Join-Path ([IO.Path]::GetTempPath()) "$DatabaseName-api.err.log"
$PreviousEnvironment = @{}

function Assert-TestDatabaseName {
    if ($DatabaseName -notmatch '^avenchart_test_[a-f0-9]{32}$') {
        throw "Refusing database operation for unexpected test database name '$DatabaseName'."
    }
}

function Invoke-Api {
    param(
        [Parameter(Mandatory)] [string]$Method,
        [Parameter(Mandatory)] [string]$Path,
        [hashtable]$Headers = @{},
        [string]$Body = ''
    )

    $request = @{
        Uri = "http://127.0.0.1:$ApiPort$Path"
        Method = $Method
        Headers = $Headers
        UseBasicParsing = $true
    }
    if ($Body) {
        $request.ContentType = 'application/json'
        $request.Body = $Body
    }
    try {
        $response = Invoke-WebRequest @request
        return [pscustomobject]@{ Status = [int]$response.StatusCode; Content = [string]$response.Content }
    }
    catch {
        if (-not $_.Exception.Response) { throw }
        $status = [int]$_.Exception.Response.StatusCode
        $content = [string]$_.ErrorDetails.Message
        if (-not $content -and $_.Exception.Response.GetResponseStream) {
            $reader = [IO.StreamReader]::new($_.Exception.Response.GetResponseStream())
            try { $content = $reader.ReadToEnd() } finally { $reader.Dispose() }
        }
        return [pscustomobject]@{ Status = $status; Content = $content }
    }
}

function Start-TestApi {
    param([bool]$OperationsEnabled)

    $env:ConnectionStrings__AvenChart = "Host=127.0.0.1;Port=5433;Database=$DatabaseName;Username=avenchart;Password=avenchart_demo"
    $env:DatabaseSchema__MigrationsPath = (Resolve-Path (Join-Path $SolutionRoot 'database/migrations')).Path
    $env:ASPNETCORE_URLS = "http://127.0.0.1:$ApiPort"
    $env:AzureOperations__Enabled = $OperationsEnabled.ToString().ToLowerInvariant()
    $env:AzureOperations__AllowPlanExecution = 'false'

    $start = @{
        FilePath = 'dotnet'
        ArgumentList = @('run', '--project', 'avenchart/backend/src/AvenChart.Api/AvenChart.Api.csproj', '--configuration', 'Release', '--no-launch-profile')
        WorkingDirectory = $RepositoryRoot
        PassThru = $true
        RedirectStandardOutput = $StdoutPath
        RedirectStandardError = $StderrPath
    }
    if ($IsWindows -or $PSVersionTable.PSVersion.Major -lt 6) {
        $start.WindowStyle = 'Hidden'
    }
    $script:ApiProcess = Start-Process @start

    $deadline = (Get-Date).AddSeconds($ApiWaitSeconds)
    do {
        Start-Sleep -Seconds 2
        if ($ApiProcess.HasExited) {
            throw "The isolated API exited early. Output: $(Get-Content -Raw $StdoutPath) Error: $(Get-Content -Raw $StderrPath)"
        }
        try {
            $ready = Invoke-Api -Method GET -Path '/health/ready'
        }
        catch {
            $ready = [pscustomobject]@{ Status = 0; Content = $_.Exception.Message }
        }
    } while ($ready.Status -ne 200 -and (Get-Date) -lt $deadline)
    if ($ready.Status -ne 200) {
        throw "The isolated API did not become ready within $ApiWaitSeconds seconds."
    }
}

function Stop-TestApi {
    if ($ApiProcess -and -not $ApiProcess.HasExited) {
        Stop-Process -Id $ApiProcess.Id -Force
        $ApiProcess.WaitForExit()
    }
    $script:ApiProcess = $null
}

function Assert-Status {
    param(
        [Parameter(Mandatory)] $Response,
        [Parameter(Mandatory)] [int]$Expected,
        [Parameter(Mandatory)] [string]$Action
    )
    if ($Response.Status -ne $Expected) {
        throw "$Action returned HTTP $($Response.Status): $($Response.Content)"
    }
}

try {
    Push-Location $RepositoryRoot
    foreach ($name in @(
        'ConnectionStrings__AvenChart',
        'DatabaseSchema__MigrationsPath',
        'ASPNETCORE_URLS',
        'AzureOperations__Enabled',
        'AzureOperations__AllowPlanExecution')) {
        $PreviousEnvironment[$name] = [Environment]::GetEnvironmentVariable($name, 'Process')
    }

    docker compose -f avenchart/docker-compose.yml up -d postgres
    if ($LASTEXITCODE -ne 0) { throw 'PostgreSQL startup failed.' }
    Assert-TestDatabaseName
    docker compose -f avenchart/docker-compose.yml exec -T postgres `
        psql -X -U avenchart -d postgres -v ON_ERROR_STOP=1 `
        -c "create database $DatabaseName owner avenchart;"
    if ($LASTEXITCODE -ne 0) { throw 'Test database creation failed.' }

    & (Join-Path $SolutionRoot 'scripts/Seed-AvenChartGoldDataset.ps1') `
        -DatabaseName $DatabaseName `
        -SkipMigrationImageBuild `
        -SkipArtifact

    Start-TestApi -OperationsEnabled $true
    $login = Invoke-Api -Method POST -Path '/api/auth/login' `
        -Body '{"username":"admin","password":"pass"}'
    Assert-Status $login 200 'Administrator login'
    $adminLogin = $login.Content | ConvertFrom-Json
    $sessionId = $adminLogin.sessionId
    $sessionHeaders = New-AvenChartStaffAccessContextHeaders -Login $adminLogin

    $unlock = Invoke-Api -Method POST `
        -Path '/api/administration/azure-operations/access/unlock' `
        -Headers $sessionHeaders `
        -Body '{"code":"AvenChartAdmin"}'
    Assert-Status $unlock 200 'Bootstrap Operations unlock'
    $grant = $unlock.Content | ConvertFrom-Json
    if (-not $grant.requiresCodeChange) {
        throw 'The bootstrap Operations grant did not require an access-code change.'
    }
    $protectedHeaders = @{} + $sessionHeaders
    $protectedHeaders['X-AvenChart-Operations-Access'] = $grant.accessToken

    $blocked = Invoke-Api -Method GET `
        -Path '/api/administration/azure-operations/capabilities' `
        -Headers $protectedHeaders
    Assert-Status $blocked 403 'Bootstrap information-disclosure gate'
    if (($blocked.Content | ConvertFrom-Json).error -ne 'operations_code_change_required') {
        throw 'The bootstrap information-disclosure gate returned the wrong error contract.'
    }

    $replacementCode = 'Test-Operations-Code-2026!'
    $change = Invoke-Api -Method POST `
        -Path '/api/administration/azure-operations/access/change-code' `
        -Headers $protectedHeaders `
        -Body (@{ currentCode = 'AvenChartAdmin'; newCode = $replacementCode } | ConvertTo-Json -Compress)
    Assert-Status $change 200 'Operations access-code rotation'

    $unlock = Invoke-Api -Method POST `
        -Path '/api/administration/azure-operations/access/unlock' `
        -Headers $sessionHeaders `
        -Body (@{ code = $replacementCode } | ConvertTo-Json -Compress)
    Assert-Status $unlock 200 'Replacement Operations unlock'
    $protectedHeaders['X-AvenChart-Operations-Access'] = ($unlock.Content | ConvertFrom-Json).accessToken

    $capabilities = Invoke-Api -Method GET `
        -Path '/api/administration/azure-operations/capabilities' `
        -Headers $protectedHeaders
    Assert-Status $capabilities 200 'Protected capability query'

    $incomplete = Invoke-Api -Method POST `
        -Path '/api/administration/azure-operations/assess' `
        -Headers $protectedHeaders `
        -Body '{"environmentKind":"demo"}'
    Assert-Status $incomplete 200 'Incomplete profile assessment'
    if (($incomplete.Content | ConvertFrom-Json).valid) {
        throw 'A structurally incomplete deployment profile was accepted.'
    }

    $profile = @{
        environmentKind = 'demo'; workloadMode = 'synthetic-interactive'
        tenantId = '00000000-0000-0000-0000-000000000001'
        subscriptionId = '00000000-0000-0000-0000-000000000002'
        location = 'eastus2'; resourceGroupName = 'rg-avenchart-demo-test'; resourceNamePrefix = 'avc-test'
        containerRegistryName = 'avctestacr'; keyVaultName = 'avctest-kv'; postgresServerName = 'avctest-pg'
        containerAppsEnvironmentName = 'avctest-cae'; managedIdentityName = 'avctest-identity'
        logAnalyticsWorkspaceName = 'avctest-logs'; containerAppName = 'avctest-app'; migrationJobName = 'avctest-migrate'
        databaseName = 'avenchart'; databaseAdministratorLogin = 'avenchartadmin'
        databasePasswordSecretName = 'avenchart-database-administrator-password'
        expectedNamedUsers = 20; expectedConcurrentUsers = 10; apiCpu = 0.5; apiMemoryGiB = 1
        uiCpu = 0.25; uiMemoryGiB = 0.5; minimumReplicas = 1; maximumReplicas = 2; httpConcurrency = 20
        postgresSkuName = 'Standard_B1ms'; postgresTier = 'Burstable'; postgresStorageGiB = 32
        connectionPoolMaximum = 15; backupRetentionDays = 7; enableGeoRedundantBackup = $false
        enableHighAvailability = $false; vnetAddressPrefix = '10.42.0.0/16'
        infrastructureSubnetPrefix = '10.42.0.0/29'; databaseSubnetPrefix = '10.42.2.0/28'
        customDomain = ''; dnsZoneResourceId = ''; allowedIpRanges = @()
        apiImage = 'avenchart-api:demo'; uiImage = 'avenchart-ui:demo'; sourceRevision = 'test'
        rateLimitPermitLimit = 300; logRetentionDays = 30; monthlyBudgetUsd = 150; alertEmails = @()
        owner = 'test-operator'; costCenter = 'test'; tags = @{ application = 'AvenChart'; environment = 'demo' }
        enableDemoSeed = $true; enableDemoReset = $false; acknowledgedSyntheticOnly = $true
    }
    $network = Invoke-Api -Method POST `
        -Path '/api/administration/azure-operations/assess' `
        -Headers $protectedHeaders `
        -Body ($profile | ConvertTo-Json -Depth 10 -Compress)
    Assert-Status $network 200 'Invalid subnet assessment'
    $networkAssessment = $network.Content | ConvertFrom-Json
    if ($networkAssessment.valid -or -not ($networkAssessment.issues.code -contains 'invalid-cidr')) {
        throw 'An undersized Container Apps infrastructure subnet was accepted.'
    }

    $profile.infrastructureSubnetPrefix = '10.42.0.0/23'
    $profile.databaseSubnetPrefix = '10.43.0.0/28'
    $network = Invoke-Api -Method POST `
        -Path '/api/administration/azure-operations/assess' `
        -Headers $protectedHeaders `
        -Body ($profile | ConvertTo-Json -Depth 10 -Compress)
    Assert-Status $network 200 'Out-of-range subnet assessment'
    if (-not (($network.Content | ConvertFrom-Json).issues.code -contains 'subnet-outside-vnet')) {
        throw 'A PostgreSQL subnet outside the deployment virtual network was accepted.'
    }

    $profile.databaseSubnetPrefix = '10.42.0.0/28'
    $network = Invoke-Api -Method POST `
        -Path '/api/administration/azure-operations/assess' `
        -Headers $protectedHeaders `
        -Body ($profile | ConvertTo-Json -Depth 10 -Compress)
    Assert-Status $network 200 'Overlapping subnet assessment'
    if (-not (($network.Content | ConvertFrom-Json).issues.code -contains 'subnets-overlap')) {
        throw 'Overlapping Container Apps and PostgreSQL delegated subnets were accepted.'
    }

    Stop-TestApi
    Start-TestApi -OperationsEnabled $false
    $disabled = Invoke-Api -Method POST `
        -Path '/api/administration/azure-operations/access/unlock' `
        -Headers $sessionHeaders `
        -Body (@{ code = $replacementCode } | ConvertTo-Json -Compress)
    Assert-Status $disabled 503 'Azure Operations host kill switch'
    if (($disabled.Content | ConvertFrom-Json).error -ne 'azure_operations_disabled') {
        throw 'The Azure Operations host kill switch returned the wrong error contract.'
    }

    Write-Output 'Azure Operations API integration verification passed.'
}
finally {
    Stop-TestApi
    foreach ($entry in $PreviousEnvironment.GetEnumerator()) {
        [Environment]::SetEnvironmentVariable($entry.Key, $entry.Value, 'Process')
    }
    Assert-TestDatabaseName
    docker compose -f (Join-Path $RepositoryRoot 'avenchart/docker-compose.yml') exec -T postgres `
        psql -X -U avenchart -d postgres -v ON_ERROR_STOP=1 `
        -c "select pg_terminate_backend(pid) from pg_stat_activity where datname = '$DatabaseName' and pid <> pg_backend_pid();" *> $null
    docker compose -f (Join-Path $RepositoryRoot 'avenchart/docker-compose.yml') exec -T postgres `
        psql -X -U avenchart -d postgres -v ON_ERROR_STOP=1 `
        -c "drop database if exists $DatabaseName;" *> $null
    Remove-Item -LiteralPath $StdoutPath, $StderrPath -Force -ErrorAction SilentlyContinue
    Pop-Location
}
