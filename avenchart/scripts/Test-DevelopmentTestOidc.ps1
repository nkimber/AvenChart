# SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
# SPDX-License-Identifier: GPL-3.0-or-later

param(
    [string]$ApiBaseUrl = "http://localhost:5001"
)

$ErrorActionPreference = "Stop"
$solutionRoot = Resolve-Path (Join-Path $PSScriptRoot "..")
$artifactsRoot = Join-Path $solutionRoot "artifacts"
$resultPath = Join-Path $artifactsRoot "latest-development-test-oidc-test.json"
New-Item -ItemType Directory -Force $artifactsRoot | Out-Null
$checks = [System.Collections.Generic.List[object]]::new()
$status = "passed"

function Add-Check {
    param([string]$Name, [bool]$Passed, [object]$Details = $null)
    $script:checks.Add([ordered]@{ name = $Name; status = if ($Passed) { "passed" } else { "failed" }; details = $Details })
    if (-not $Passed) { $script:status = "failed" }
}

function Get-HttpStatus {
    param([string]$Uri, [string]$Method, [hashtable]$Headers = @{}, [object]$Body = $null)
    try {
        $parameters = @{ Uri = $Uri; Method = $Method; Headers = $Headers; TimeoutSec = 20; UseBasicParsing = $true }
        if ($null -ne $Body) { $parameters.ContentType = 'application/json'; $parameters.Body = ($Body | ConvertTo-Json) }
        $response = Invoke-WebRequest @parameters
        return [int]$response.StatusCode
    }
    catch {
        return if ($null -eq $_.Exception.Response) { 0 } else { [int]$_.Exception.Response.StatusCode }
    }
}

try {
    $metadata = Invoke-RestMethod -Uri "$ApiBaseUrl/api/test-idp/.well-known/openid-configuration" -TimeoutSec 20
    $jwks = Invoke-RestMethod -Uri "$ApiBaseUrl/api/test-idp/jwks" -TimeoutSec 20
    Add-Check "Development test IdP publishes OIDC metadata and an RS256 JWK" (
        -not [string]::IsNullOrWhiteSpace($metadata.issuer) -and
        $metadata.jwks_uri -match '/api/test-idp/jwks$' -and
        @($jwks.keys).Count -eq 1 -and $jwks.keys[0].kty -eq 'RSA' -and $jwks.keys[0].alg -eq 'RS256'
    ) @{ issuer = $metadata.issuer; keyId = $jwks.keys[0].kid }

    $token = Invoke-RestMethod -Uri "$ApiBaseUrl/api/test-idp/token" -Method Post -ContentType "application/json" `
        -Body (@{ username = 'admin'; password = 'pass' } | ConvertTo-Json) -TimeoutSec 20
    if ([string]::IsNullOrWhiteSpace($token.accessToken)) { throw 'The development test IdP did not issue a bearer token.' }
    $bearerHeaders = @{ Authorization = "Bearer $($token.accessToken)" }
    $session = Invoke-RestMethod -Uri "$ApiBaseUrl/api/auth/session" -Headers $bearerHeaders -TimeoutSec 20
    if (-not $session.authenticated) {
        throw 'The API did not accept the test OIDC bearer token. Start it with IdentityProvider__Mode=test-oidc in Development.'
    }
    $access = Invoke-RestMethod -Uri "$ApiBaseUrl/api/auth/access-context" -Headers $bearerHeaders -TimeoutSec 20
    $facility = @($access.facilities | Where-Object { $_.isDefault } | Select-Object -First 1)
    if ($facility.Count -ne 1 -or @($access.purposes).Count -eq 0) { throw 'The mapped test OIDC principal has no usable facility/purpose context.' }
    $requestHeaders = @{
        Authorization = $bearerHeaders.Authorization
        'X-AvenChart-Facility-Id' = [string]$facility[0].facilityId
        'X-AvenChart-Purpose-Of-Use' = [string]$access.purposes[0]
    }
    $protectedStatus = Get-HttpStatus "$ApiBaseUrl/api/patients/?limit=1" 'Get' $requestHeaders
    Add-Check "Mapped test OIDC identity reaches a facility/purpose-protected endpoint" ($protectedStatus -eq 200) @{ status = $protectedStatus; username = $session.username; source = $session.sessionSource }

    $invalidStatus = Get-HttpStatus "$ApiBaseUrl/api/auth/session" 'Get' @{ Authorization = 'Bearer not-a-jwt' }
    $localLoginStatus = Get-HttpStatus "$ApiBaseUrl/api/auth/login" 'Post' @{} @{ username = 'admin'; password = 'pass' }
    Add-Check "Invalid bearer and disabled local-login boundary fail closed" ($invalidStatus -eq 401 -and $localLoginStatus -eq 404) @{ invalidBearer = $invalidStatus; localLogin = $localLoginStatus }
}
catch {
    Add-Check "Unhandled development test OIDC verification error" $false $_.Exception.Message
}
finally {
    $result = [ordered]@{ status = $status; generatedAtUtc = (Get-Date).ToUniversalTime().ToString('O'); checks = $checks }
    $result | ConvertTo-Json -Depth 10 | Set-Content -LiteralPath $resultPath -Encoding utf8
    $result | ConvertTo-Json -Depth 10
}

if ($status -ne 'passed') { exit 1 }
