# SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
# SPDX-License-Identifier: GPL-3.0-or-later

function New-AvenChartStaffAccessContextHeaders {
    param(
        [Parameter(Mandatory = $true)]
        [object]$Login,
        [int]$FacilityId,
        [ValidateSet("treatment", "payment", "healthcare-operations")]
        [string]$PurposeOfUse
    )

    if ($Login.authenticated -ne $true -or [string]::IsNullOrWhiteSpace($Login.sessionId)) {
        throw "An authenticated login with an active session is required."
    }

    $accessContext = $Login.accessContext
    if ($null -eq $accessContext) {
        throw "The login response did not contain the required staff access context."
    }

    $facilities = @($accessContext.facilities)
    # Wrap the whole conditional so Windows PowerShell 5.1 cannot unwrap the
    # selected facility to a scalar while emitting the branch result.
    $facility = @(if ($FacilityId -gt 0) {
        $facilities | Where-Object { $_.facilityId -eq $FacilityId } | Select-Object -First 1
    }
    else {
        $facilities | Where-Object { $_.isDefault -eq $true } | Select-Object -First 1
    })
    if ($facility.Count -eq 0) {
        if ($FacilityId -gt 0) {
            throw "The login response does not grant access to facility '$FacilityId'."
        }

        $facility = @($facilities | Select-Object -First 1)
    }
    if ($facility.Count -ne 1 -or $facility[0].facilityId -le 0) {
        throw "The login response did not contain an active facility grant."
    }

    $purposes = @($accessContext.purposes)
    if ([string]::IsNullOrWhiteSpace($PurposeOfUse)) {
        $PurposeOfUse = @("healthcare-operations", "payment", "treatment") |
            Where-Object { $purposes -contains $_ } |
            Select-Object -First 1
    }
    if ([string]::IsNullOrWhiteSpace($PurposeOfUse) -or $purposes -notcontains $PurposeOfUse) {
        throw "The login response does not grant purpose of use '$PurposeOfUse'."
    }

    return @{
        "X-AvenChart-Session" = $Login.sessionId
        "X-AvenChart-Facility-Id" = [string]$facility[0].facilityId
        "X-AvenChart-Purpose-Of-Use" = $PurposeOfUse
    }
}
