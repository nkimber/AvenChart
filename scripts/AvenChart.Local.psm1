# SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
# SPDX-License-Identifier: GPL-3.0-or-later

Set-StrictMode -Version Latest

$script:RepositoryRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$script:CoreComposeDirectory = Join-Path $script:RepositoryRoot 'avenchart'
$script:ModernUiComposeDirectory = Join-Path $script:RepositoryRoot 'avenchart-ui'

function Get-AvenChartRepositoryRoot {
    return $script:RepositoryRoot
}

function Assert-AvenChartDocker {
    if ($null -eq (Get-Command docker -ErrorAction SilentlyContinue)) {
        throw 'Docker was not found on PATH. Install and start Docker Desktop, then open a new terminal.'
    }

    docker version --format '{{.Server.Version}}' 2>$null | Out-Null
    if ($LASTEXITCODE -ne 0) {
        throw 'The Docker engine is not available. Start Docker Desktop and wait until it reports that the engine is running.'
    }

    docker compose version 2>$null | Out-Null
    if ($LASTEXITCODE -ne 0) {
        throw 'Docker Compose is not available. Install a current Docker Desktop release with the Compose plugin.'
    }
}

function Invoke-AvenChartCompose {
    param(
        [Parameter(Mandatory = $true)]
        [ValidateSet('Core', 'ModernUi')]
        [string]$Project,

        [Parameter(Mandatory = $true)]
        [string[]]$ComposeArguments
    )

    $composeDirectory = if ($Project -eq 'Core') {
        $script:CoreComposeDirectory
    }
    else {
        $script:ModernUiComposeDirectory
    }

    Push-Location $composeDirectory
    try {
        docker compose @ComposeArguments
        $exitCode = $LASTEXITCODE
    }
    finally {
        Pop-Location
    }

    if ($exitCode -ne 0) {
        throw "Docker Compose failed for the $Project project with exit code $exitCode."
    }
}

function Get-AvenChartEndpoints {
    @(
        [pscustomobject]@{ Name = 'Modern UI'; Port = 3100; Url = 'http://localhost:3100/' }
        [pscustomobject]@{ Name = 'Professional sign-in'; Port = 3100; Url = 'http://localhost:3100/login' }
        [pscustomobject]@{ Name = 'Patient portal'; Port = 3100; Url = 'http://localhost:3100/portal/login' }
        [pscustomobject]@{ Name = 'Reference UI'; Port = 3000; Url = 'http://localhost:3000/?entry=chooser' }
        [pscustomobject]@{ Name = 'API readiness'; Port = 5001; Url = 'http://localhost:5001/health/ready' }
        [pscustomobject]@{ Name = 'PostgreSQL'; Port = 5433; Url = 'postgresql://localhost:5433/avenchart' }
    )
}

function Write-AvenChartEndpoints {
    Write-Output ''
    Write-Output 'Local AvenChart endpoints:'
    $table = Get-AvenChartEndpoints | Format-Table Name, Port, Url -AutoSize | Out-String
    Write-Output $table.TrimEnd()
}

function Test-AvenChartHttpEndpoint {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Url
    )

    try {
        $response = Invoke-WebRequest -Uri $Url -UseBasicParsing -TimeoutSec 5
        return $response.StatusCode -ge 200 -and $response.StatusCode -lt 400
    }
    catch {
        return $false
    }
}

function Wait-AvenChartHttpEndpoint {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Name,

        [Parameter(Mandatory = $true)]
        [string]$Url,

        [ValidateRange(1, 900)]
        [int]$WaitSeconds = 120
    )

    Write-Output "Waiting for $Name at $Url ..."
    $deadline = (Get-Date).AddSeconds($WaitSeconds)
    do {
        if (Test-AvenChartHttpEndpoint -Url $Url) {
            Write-Output "Ready: $Name"
            return
        }

        Start-Sleep -Seconds 2
    } while ((Get-Date) -lt $deadline)

    throw "$Name did not become ready at $Url within $WaitSeconds seconds."
}

function Get-AvenChartHttpStatus {
    foreach ($endpoint in (Get-AvenChartEndpoints | Where-Object { $_.Url -like 'http://*' })) {
        [pscustomobject]@{
            Name = $endpoint.Name
            Port = $endpoint.Port
            Status = if (Test-AvenChartHttpEndpoint -Url $endpoint.Url) { 'ready' } else { 'unavailable' }
            Url = $endpoint.Url
        }
    }
}

Export-ModuleMember -Function @(
    'Assert-AvenChartDocker',
    'Get-AvenChartEndpoints',
    'Get-AvenChartHttpStatus',
    'Get-AvenChartRepositoryRoot',
    'Invoke-AvenChartCompose',
    'Test-AvenChartHttpEndpoint',
    'Wait-AvenChartHttpEndpoint',
    'Write-AvenChartEndpoints'
)
