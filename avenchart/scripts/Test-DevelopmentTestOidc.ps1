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

function Get-QueryValues {
    param([string]$Uri)
    $values = @{}
    $query = ([uri]$Uri).Query.TrimStart('?')
    foreach ($pair in $query.Split('&', [System.StringSplitOptions]::RemoveEmptyEntries)) {
        $parts = $pair.Split('=', 2)
        $name = [uri]::UnescapeDataString($parts[0])
        $value = if ($parts.Count -gt 1) { [uri]::UnescapeDataString($parts[1]) } else { '' }
        $values[$name] = $value
    }
    return $values
}

function Invoke-NoRedirect {
    param(
        [string]$Uri,
        [string]$Method = 'Get',
        [Microsoft.PowerShell.Commands.WebRequestSession]$WebSession,
        [hashtable]$Headers = @{},
        [object]$Body = $null,
        [string]$ContentType = $null
    )
    $parameters = @{
        Uri = $Uri
        Method = $Method
        WebSession = $WebSession
        Headers = $Headers
        MaximumRedirection = 0
        SkipHttpErrorCheck = $true
        TimeoutSec = 20
    }
    if ($null -ne $Body) { $parameters.Body = $Body }
    if (-not [string]::IsNullOrWhiteSpace($ContentType)) { $parameters.ContentType = $ContentType }
    return Invoke-WebRequest @parameters
}

function Start-BrowserOidcFlow {
    param(
        [string]$Audience,
        [string]$SessionPath
    )
    $webSession = [Microsoft.PowerShell.Commands.WebRequestSession]::new()
    $returnUrl = "http://localhost:3100/auth/oidc/callback?audience=$Audience"
    $startUri = "$ApiBaseUrl/api/auth/oidc/start?audience=$Audience&returnUrl=$([uri]::EscapeDataString($returnUrl))"
    $start = Invoke-NoRedirect -Uri $startUri -WebSession $webSession
    if ($start.StatusCode -ne 302 -or [string]::IsNullOrWhiteSpace($start.Headers.Location)) {
        throw "Browser OIDC start did not redirect to the test identity provider for $Audience."
    }
    $authorization = Get-QueryValues ([string]$start.Headers.Location)
    $providerCompletion = Invoke-NoRedirect -Uri "$ApiBaseUrl/api/test-idp/authorize" -Method Post -WebSession $webSession `
        -Body @{
            client_id = $authorization.client_id
            redirect_uri = $authorization.redirect_uri
            state = $authorization.state
            code_challenge = $authorization.code_challenge
            code_challenge_method = $authorization.code_challenge_method
            scope = $authorization.scope
            username = 'admin'
            password = 'pass'
        } -ContentType 'application/x-www-form-urlencoded'
    if ($providerCompletion.StatusCode -ne 302 -or [string]::IsNullOrWhiteSpace($providerCompletion.Headers.Location)) {
        throw "The development test IdP did not issue an authorization code for $Audience."
    }
    $callback = Invoke-NoRedirect -Uri ([string]$providerCompletion.Headers.Location) -WebSession $webSession
    if ($callback.StatusCode -ne 302) {
        throw "AvenChart did not complete the browser OIDC callback for $Audience."
    }
    $sessionResponse = Invoke-NoRedirect -Uri "$ApiBaseUrl$SessionPath" -WebSession $webSession
    if ($sessionResponse.StatusCode -ne 200) {
        throw "AvenChart did not expose the browser OIDC $Audience session."
    }
    $csrf = [string]$sessionResponse.Headers['X-AvenChart-CSRF']
    if ([string]::IsNullOrWhiteSpace($csrf)) {
        throw "AvenChart did not issue a browser OIDC CSRF proof for $Audience."
    }
    return [pscustomobject]@{
        webSession = $webSession
        session = $sessionResponse.Content | ConvertFrom-Json
        csrfToken = $csrf
        returnOrigin = 'http://localhost:3100'
    }
}

try {
    $metadata = Invoke-RestMethod -Uri "$ApiBaseUrl/api/test-idp/.well-known/openid-configuration" -TimeoutSec 20
    $jwks = Invoke-RestMethod -Uri "$ApiBaseUrl/api/test-idp/jwks" -TimeoutSec 20
    Add-Check "Development test IdP publishes OIDC metadata and an RS256 JWK" (
        -not [string]::IsNullOrWhiteSpace($metadata.issuer) -and
        $metadata.authorization_endpoint -match '/api/test-idp/authorize$' -and
        $metadata.jwks_uri -match '/api/test-idp/jwks$' -and
        @($metadata.response_types_supported) -contains 'code' -and
        @($metadata.grant_types_supported) -contains 'authorization_code' -and
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
    $mappings = Invoke-RestMethod -Uri "$ApiBaseUrl/api/administration/external-identity-mappings?providerId=test-oidc" -Headers $bearerHeaders -TimeoutSec 20
    $mapping = @($mappings | Where-Object { $_.providerId -eq 'test-oidc' -and $_.externalSubject -eq 'admin' -and $_.username -eq 'admin' -and $_.active } | Select-Object -First 1)
    Add-Check "Test OIDC token resolves through an explicit active provider-subject mapping" ($mapping.Count -eq 1) @{ mappingId = if ($mapping.Count -eq 1) { $mapping[0].mappingId } else { $null } }
    $browserStaff = Start-BrowserOidcFlow -Audience 'staff' -SessionPath '/api/auth/session'
    $browserStaffMissingCsrf = Invoke-NoRedirect -Uri "$ApiBaseUrl/api/auth/logout" -Method Post -WebSession $browserStaff.webSession `
        -Headers @{ Origin = $browserStaff.returnOrigin } -Body (@{ sessionId = $browserStaff.session.sessionId } | ConvertTo-Json) -ContentType 'application/json'
    $browserStaffLogout = Invoke-NoRedirect -Uri "$ApiBaseUrl/api/auth/logout" -Method Post -WebSession $browserStaff.webSession `
        -Headers @{ Origin = $browserStaff.returnOrigin; 'X-AvenChart-CSRF' = $browserStaff.csrfToken } `
        -Body (@{ sessionId = $browserStaff.session.sessionId } | ConvertTo-Json) -ContentType 'application/json'
    $browserStaffAfterLogout = Invoke-NoRedirect -Uri "$ApiBaseUrl/api/auth/session" -WebSession $browserStaff.webSession
    $browserStaffAfterLogoutJson = $browserStaffAfterLogout.Content | ConvertFrom-Json
    Add-Check "Browser authorization-code PKCE staff sign-on is token-free, CSRF-protected, and logout-revocable" (
        $browserStaff.session.authenticated -and
        $browserStaff.session.sessionSource -eq 'oidc-browser:test-oidc' -and
        $browserStaffMissingCsrf.StatusCode -eq 403 -and
        $browserStaffLogout.StatusCode -eq 200 -and
        -not $browserStaffAfterLogoutJson.authenticated
    ) @{ source = $browserStaff.session.sessionSource; missingCsrf = $browserStaffMissingCsrf.StatusCode; logout = $browserStaffLogout.StatusCode }
    $portalMappings = @(Invoke-RestMethod -Uri "$ApiBaseUrl/api/administration/patient-portal-external-identity-mappings?providerId=test-oidc" -Headers $bearerHeaders -TimeoutSec 20)
    $portalMapping = @($portalMappings | Where-Object { $_.providerId -eq 'test-oidc' -and $_.externalSubject -eq 'admin' -and $_.active } | Select-Object -First 1)
    if ($portalMapping.Count -eq 0) {
        $portalMapping = @(
            Invoke-RestMethod -Uri "$ApiBaseUrl/api/administration/patient-portal-external-identity-mappings" -Method Post -Headers $bearerHeaders -ContentType 'application/json' `
                -Body (@{ providerId = 'test-oidc'; externalSubject = 'admin'; patientId = 'MOD-PAT-0001' } | ConvertTo-Json) -TimeoutSec 20)
    }
    $browserPortal = Start-BrowserOidcFlow -Audience 'portal' -SessionPath '/api/patient-portal/session'
    $browserPortalMissingCsrf = Invoke-NoRedirect -Uri "$ApiBaseUrl/api/patient-portal/session" -Method Delete -WebSession $browserPortal.webSession `
        -Headers @{ Origin = $browserPortal.returnOrigin }
    $browserPortalLogout = Invoke-NoRedirect -Uri "$ApiBaseUrl/api/patient-portal/session" -Method Delete -WebSession $browserPortal.webSession `
        -Headers @{ Origin = $browserPortal.returnOrigin; 'X-AvenChart-CSRF' = $browserPortal.csrfToken }
    $browserPortalAfterLogout = Invoke-NoRedirect -Uri "$ApiBaseUrl/api/patient-portal/session" -WebSession $browserPortal.webSession
    $browserPortalAfterLogoutJson = $browserPortalAfterLogout.Content | ConvertFrom-Json
    Add-Check "Browser authorization-code PKCE portal sign-on is mapping-bound, CSRF-protected, and logout-revocable" (
        $browserPortal.session.authenticated -and
        $browserPortal.session.sessionSource -eq 'oidc:test-oidc' -and
        $browserPortalMissingCsrf.StatusCode -eq 403 -and
        $browserPortalLogout.StatusCode -eq 200 -and
        -not $browserPortalAfterLogoutJson.authenticated
    ) @{ source = $browserPortal.session.sessionSource; missingCsrf = $browserPortalMissingCsrf.StatusCode; logout = $browserPortalLogout.StatusCode }
    $portalSession = Invoke-RestMethod -Uri "$ApiBaseUrl/api/patient-portal/session" -Headers $bearerHeaders -TimeoutSec 20
    Add-Check "Test OIDC token resolves to the governed patient portal mapping" (
        $portalMapping.Count -eq 1 -and
        $portalSession.authenticated -and
        $portalSession.canonicalId -eq $portalMapping[0].patientId -and
        $portalSession.sessionSource -eq 'oidc:test-oidc'
    ) @{ mappingId = $portalMapping[0].mappingId; patientId = $portalSession.canonicalId; source = $portalSession.sessionSource }
    Invoke-RestMethod -Uri "$ApiBaseUrl/api/patient-portal/session" -Method Delete -Headers $bearerHeaders -TimeoutSec 20 | Out-Null
    $loggedOutPortalSession = Invoke-RestMethod -Uri "$ApiBaseUrl/api/patient-portal/session" -Headers $bearerHeaders -TimeoutSec 20
    Add-Check "Portal logout prevents the same bearer from recreating its derived session" (-not $loggedOutPortalSession.authenticated) @{ authenticatedAfterLogout = $loggedOutPortalSession.authenticated }
    Invoke-RestMethod -Uri "$ApiBaseUrl/api/administration/patient-portal-external-identity-mappings/$($portalMapping[0].mappingId)/deactivate" -Method Post -Headers $bearerHeaders -ContentType 'application/json' `
        -Body (@{ reason = 'Synthetic development test OIDC portal mapping cleanup.' } | ConvertTo-Json) -TimeoutSec 20 | Out-Null
    $revokedPortalSession = Invoke-RestMethod -Uri "$ApiBaseUrl/api/patient-portal/session" -Headers $bearerHeaders -TimeoutSec 20
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
    $localPortalLoginStatus = Get-HttpStatus "$ApiBaseUrl/api/patient-portal/login" 'Post' @{} @{ username = 'mod-pat-0001@example.test'; password = 'pass' }
    Add-Check "Revoked portal mapping, invalid bearer, and disabled local-login boundaries fail closed" (
        -not $revokedPortalSession.authenticated -and
        $invalidStatus -eq 401 -and
        $localLoginStatus -eq 404 -and
        $localPortalLoginStatus -eq 404
    ) @{ revokedPortalAuthenticated = $revokedPortalSession.authenticated; invalidBearer = $invalidStatus; localLogin = $localLoginStatus; localPortalLogin = $localPortalLoginStatus }
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
