param(
    [string]$ApiBaseUrl = "http://localhost:5001"
)

$ErrorActionPreference = "Stop"
Add-Type -AssemblyName System.Net.Http

$solutionRoot = Resolve-Path (Join-Path $PSScriptRoot "..")
$artifactsRoot = Join-Path $solutionRoot "artifacts"
$resultPath = Join-Path $artifactsRoot "latest-clinical-form-engine-test.json"
New-Item -ItemType Directory -Force $artifactsRoot | Out-Null

$checks = [System.Collections.Generic.List[object]]::new()
$status = "passed"
$adminHeaders = $null
$providerHeaders = $null
$definitionId = $null
$stableKey = $null
$providerPhysicianMembershipAdded = $false
$instanceIds = [System.Collections.Generic.List[string]]::new()

function Add-Check {
    param(
        [string]$Name,
        [bool]$Passed,
        [object]$Details = $null
    )

    $script:checks.Add([ordered]@{
        name = $Name
        status = if ($Passed) { "passed" } else { "failed" }
        details = $Details
    })
    if (-not $Passed) {
        $script:status = "failed"
    }
}

function ConvertTo-RequestJson {
    param([object]$Value)
    return $Value | ConvertTo-Json -Depth 30 -Compress
}

function Invoke-Api {
    param(
        [string]$Uri,
        [string]$Method = "GET",
        [hashtable]$RequestHeaders = @{},
        [object]$Body = $null,
        [string]$RawBody = $null
    )

    $handler = [System.Net.Http.HttpClientHandler]::new()
    $client = [System.Net.Http.HttpClient]::new($handler)
    try {
        $client.Timeout = [TimeSpan]::FromSeconds(30)
        $request = [System.Net.Http.HttpRequestMessage]::new(
            [System.Net.Http.HttpMethod]::new($Method),
            $Uri
        )
        foreach ($entry in $RequestHeaders.GetEnumerator()) {
            $request.Headers.TryAddWithoutValidation(
                [string]$entry.Key,
                [string]$entry.Value
            ) | Out-Null
        }

        if ($null -ne $Body -or -not [string]::IsNullOrEmpty($RawBody)) {
            $payload = if (-not [string]::IsNullOrEmpty($RawBody)) {
                $RawBody
            }
            else {
                ConvertTo-RequestJson $Body
            }
            $request.Content = [System.Net.Http.StringContent]::new(
                $payload,
                [Text.Encoding]::UTF8,
                "application/json"
            )
        }

        $response = $client.SendAsync($request).GetAwaiter().GetResult()
        try {
            $content = $response.Content.ReadAsStringAsync().GetAwaiter().GetResult()
            $json = $null
            if (-not [string]::IsNullOrWhiteSpace($content)) {
                try {
                    $json = $content | ConvertFrom-Json
                }
                catch {
                    $json = $null
                }
            }

            return [pscustomobject]@{
                Status = [int]$response.StatusCode
                Content = $content
                Json = $json
                ContentType = $response.Content.Headers.ContentType.MediaType
            }
        }
        finally {
            $response.Dispose()
            $request.Dispose()
        }
    }
    finally {
        $client.Dispose()
        $handler.Dispose()
    }
}

function Invoke-Json {
    param(
        [string]$Uri,
        [string]$Method = "GET",
        [hashtable]$RequestHeaders = @{},
        [object]$Body = $null
    )

    $response = Invoke-Api `
        -Uri $Uri `
        -Method $Method `
        -RequestHeaders $RequestHeaders `
        -Body $Body
    if ($response.Status -lt 200 -or $response.Status -ge 300) {
        throw "$Method $Uri returned $($response.Status): $($response.Content)"
    }
    return $response.Json
}

function New-Field {
    param(
        [string]$Key,
        [string]$Label,
        [string]$Type,
        [int]$Sequence,
        [bool]$Required = $false,
        [int]$MaxLength = 0,
        [Nullable[decimal]]$Minimum = $null,
        [Nullable[decimal]]$Maximum = $null,
        [Nullable[int]]$Precision = $null,
        [string]$Unit = $null,
        [string]$CodeSystem = $null,
        [object[]]$Options = @(),
        [Nullable[int]]$RepeatMinimum = $null,
        [Nullable[int]]$RepeatMaximum = $null,
        [object[]]$Children = @(),
        [bool]$ReadOnly = $false,
        [object[]]$RowRules = @()
    )

    return [ordered]@{
        key = $Key
        sectionKey = "main"
        label = $Label
        type = $Type
        sequence = $Sequence
        required = $Required
        accessibilityLabel = $Label
        helpText = $null
        maxLength = if ($MaxLength -gt 0) { $MaxLength } else { $null }
        minimum = $Minimum
        maximum = $Maximum
        precision = $Precision
        unit = $Unit
        codeSystem = $CodeSystem
        options = $Options
        repeatMinimum = $RepeatMinimum
        repeatMaximum = $RepeatMaximum
        children = $Children
        readOnly = $ReadOnly
        rowRules = if ($RowRules.Count -gt 0) { $RowRules } else { $null }
    }
}

function New-TestSchema {
    param(
        [string]$Key,
        [string]$Name,
        [bool]$IncludeFollowUp = $false,
        [switch]$IncludeLocalization
    )

    $fields = [System.Collections.Generic.List[object]]::new()
    $fields.Add((New-Field `
        -Key "chief_concern" `
        -Label "Chief concern" `
        -Type "multiline" `
        -Sequence 10 `
        -Required $true `
        -MaxLength 500))
    $fields.Add((New-Field `
        -Key "pain_score" `
        -Label "Pain score" `
        -Type "integer" `
        -Sequence 20 `
        -Minimum 0 `
        -Maximum 10 `
        -Precision 0 `
        -Unit "score"))
    $fields.Add((New-Field `
        -Key "disposition" `
        -Label "Disposition" `
        -Type "select" `
        -Sequence 30 `
        -CodeSystem "local-disposition-v1" `
        -Options @(
            [ordered]@{ code = "routine"; display = "Routine follow-up" },
            [ordered]@{ code = "urgent"; display = "Urgent follow-up" }
        )))
    if ($IncludeFollowUp) {
        $fields.Add((New-Field `
            -Key "follow_up" `
            -Label "Follow up" `
            -Type "boolean" `
            -Sequence 40))
    }

    $rules = @(
        [ordered]@{
            key = "warn_high_pain"
            condition = [ordered]@{
                fieldKey = "pain_score"
                operator = "greater-than-or-equal"
                value = 8
            }
            action = "warning"
            targetFieldKey = "disposition"
            message = "High pain score requires clinical attention."
            calculation = $null
        }
    )
    $schema = [ordered]@{
        stableKey = $Key
        name = $Name
        purpose = "Verify the governed typed clinical form lifecycle."
        contextScope = "encounter"
        owningService = "clinical_operations"
        capability = "encounters.auth_a"
        signaturePolicy = "author-and-cosigner"
        sections = @(
            [ordered]@{
                key = "main"
                title = "Main"
                sequence = 10
                description = "Focused synthetic verification."
            }
        )
        fields = $fields.ToArray()
        rules = $rules
    }
    if ($IncludeLocalization) {
        $spanishLabels = @{
            chief_concern = "Motivo principal"
            pain_score = "Puntuación de dolor"
            disposition = "Disposición"
            follow_up = "Seguimiento"
        }
        $localizedFields = @($fields | ForEach-Object {
            $field = $_
            [ordered]@{
                fieldKey = $field.key
                label = $spanishLabels[$field.key]
                accessibilityLabel = $spanishLabels[$field.key]
                helpText = $null
                options = @($field.options | ForEach-Object {
                    [ordered]@{
                        code = $_.code
                        display = if ($_.code -eq "routine") {
                            "Seguimiento de rutina"
                        }
                        elseif ($_.code -eq "urgent") {
                            "Seguimiento urgente"
                        }
                        else {
                            $_.display
                        }
                    }
                })
            }
        })
        $schema["localizations"] = @(
            [ordered]@{
                locale = "es-US"
                name = "Formulario focalizado $Name"
                purpose = "Verificar el ciclo de vida del formulario clínico gobernado."
                sections = @(
                    [ordered]@{
                        sectionKey = "main"
                        title = "Principal"
                        description = "Verificación sintética focalizada."
                    }
                )
                fields = $localizedFields
                rules = @(
                    [ordered]@{
                        ruleKey = "warn_high_pain"
                        message = "Una puntuación alta de dolor requiere atención clínica."
                    }
                )
            }
        )
    }

    return $schema
}

function Move-Definition {
    param(
        [string]$Action,
        [int]$Revision,
        [int]$ExpectedVersion
    )

    return Invoke-Json `
        -Uri "$ApiBaseUrl/api/form-engine/definitions/$definitionId/$Action" `
        -Method "POST" `
        -RequestHeaders $adminHeaders `
        -Body ([ordered]@{
            revision = $Revision
            expectedVersion = $ExpectedVersion
            reason = "$Action focused synthetic definition."
            effectiveFrom = $null
            effectiveTo = $null
        })
}

function Move-Instance {
    param(
        [string]$InstanceId,
        [string]$Action,
        [int]$ExpectedVersion,
        [hashtable]$Headers
    )

    return Invoke-Json `
        -Uri "$ApiBaseUrl/api/form-engine/instances/$InstanceId/$Action" `
        -Method "POST" `
        -RequestHeaders $Headers `
        -Body ([ordered]@{
            expectedVersion = $ExpectedVersion
            reason = "$Action focused synthetic instance."
        })
}

try {
    $health = Invoke-Json -Uri "$ApiBaseUrl/health"
    Add-Check "API health" ($health.status -eq "healthy") $health

    $unauthenticated = Invoke-Api -Uri "$ApiBaseUrl/api/form-engine/policy"
    Add-Check `
        "Protected form policy" `
        ($unauthenticated.Status -eq 401) `
        @{ status = $unauthenticated.Status }

    $adminLogin = Invoke-Json `
        -Uri "$ApiBaseUrl/api/auth/login" `
        -Method "POST" `
        -Body @{ username = "admin"; password = "pass" }
    $providerLogin = Invoke-Json `
        -Uri "$ApiBaseUrl/api/auth/login" `
        -Method "POST" `
        -Body @{ username = "gold-provider-01"; password = "pass" }
    if (-not $adminLogin.authenticated -or -not $providerLogin.authenticated) {
        throw "Required synthetic staff sessions were not issued."
    }
    $adminHeaders = @{ "X-Legacy EHR-Session" = $adminLogin.sessionId }
    $providerHeaders = @{ "X-Legacy EHR-Session" = $providerLogin.sessionId }

    $policy = Invoke-Json `
        -Uri "$ApiBaseUrl/api/form-engine/policy" `
        -RequestHeaders $adminHeaders
    $expectedFieldTypes = @(
        "boolean",
        "coded",
        "computed",
        "date",
        "datetime",
        "decimal",
        "integer",
        "measurement",
        "multiline",
        "multiselect",
        "repeat",
        "select",
        "text"
    )
    $actualFieldTypes = @($policy.supportedFieldTypes | Sort-Object)
    $policyPassed = `
        $policy.revision -eq "local-clinical-form-v7" `
        -and $policy.rendererVersion -eq "local-clinical-form-renderer-v1" `
        -and $policy.signaturePolicyRevision -eq "local-clinical-signature-v1" `
        -and (($actualFieldTypes -join "|") -eq ($expectedFieldTypes -join "|")) `
        -and @($policy.supportedRuleActions).Count -eq 5 `
        -and (($policy.supportedCalculationOperators -join "|") `
            -eq "sum|add|subtract|multiply|divide") `
        -and @($policy.forbiddenCapabilities).Count -ge 7 `
        -and -not $policy.arbitraryScriptsAllowed `
        -and -not $policy.rawHtmlAllowed `
        -and -not $policy.externalFetchAllowed `
        -and -not $policy.previewPersistsClinicalData `
        -and -not $policy.productionSignatureStandardApproved
    Add-Check "Constrained form runtime policy" $policyPassed $policy

    $locales = @($policy.supportedLocales)
    Add-Check `
        "Policy publishes bounded base and translation locales" `
        ($locales.Count -eq 3 `
            -and (($locales.code -join "|") -eq "en-US|es-US|fr-CA") `
            -and @($locales | Where-Object { $_.isBase }).Count -eq 1 `
            -and @($locales | Where-Object {
                $_.code -eq "en-US" -and $_.isBase
            }).Count -eq 1) `
        $locales

    $calculationTemplates = @($policy.supportedCalculationTemplates)
    Add-Check `
        "Policy publishes reusable bounded calculation starters" `
        ($calculationTemplates.Count -eq 4 `
            -and (($calculationTemplates.key -join "|") `
                -eq "bounded-sum|difference|product|ratio") `
            -and @($calculationTemplates | Where-Object {
                $_.operator -notin $policy.supportedCalculationOperators `
                    -or $_.operandCount -lt 1 `
                    -or $_.operandCount -gt 20 `
                    -or $_.defaultPrecision -lt 0 `
                    -or $_.defaultPrecision -gt 8
            }).Count -eq 0) `
        $calculationTemplates

    $optionListCatalog = Invoke-Json `
        -Uri "$ApiBaseUrl/api/form-engine/option-lists" `
        -RequestHeaders $adminHeaders
    $yesNoOptionList = @(
        $optionListCatalog.optionLists |
            Where-Object { $_.listKey -eq "yesno" }
    ) | Select-Object -First 1
    Add-Check `
        "Clinical authoring publishes exact compatible option-list revisions" `
        ($null -ne $yesNoOptionList `
            -and $yesNoOptionList.revisionId -eq 2 `
            -and $yesNoOptionList.eligible `
            -and (($yesNoOptionList.options.code -join "|") -eq "yes|no") `
            -and (($yesNoOptionList.options.display -join "|") -eq "Yes|No")) `
        $yesNoOptionList

    $providerOptionListCatalog = Invoke-Api `
        -Uri "$ApiBaseUrl/api/form-engine/option-lists" `
        -RequestHeaders $providerHeaders
    Add-Check `
        "Clinical option-list authoring remains administrator restricted" `
        ($providerOptionListCatalog.Status -eq 403) `
        @{ status = $providerOptionListCatalog.Status }

    $catalog = Invoke-Json `
        -Uri "$ApiBaseUrl/api/form-engine/catalog?page=1&pageSize=100" `
        -RequestHeaders $adminHeaders
    $seededDefinition = @(
        $catalog.definitions |
            Where-Object { $_.stableKey -eq "clinical.observation" }
    ) | Select-Object -First 1
    Add-Check `
        "Seeded effective bounded form catalog" `
        ($null -ne $seededDefinition `
            -and $seededDefinition.contextScope -eq "encounter" `
            -and $seededDefinition.effectiveRevision -eq 1) `
        $seededDefinition

    $clinicNoteDefinition = @(
        $catalog.definitions |
            Where-Object { $_.stableKey -eq "legacy.clinicnote" }
    ) | Select-Object -First 1
    $clinicNoteDetail = if ($null -ne $clinicNoteDefinition) {
        Invoke-Json `
            -Uri "$ApiBaseUrl/api/form-engine/definitions/$($clinicNoteDefinition.definitionId)" `
            -RequestHeaders $adminHeaders
    }
    else {
        $null
    }
    $clinicNoteFields = @($clinicNoteDetail.currentRevision.definition.fields)
    $clinicNoteFollowUp = @(
        $clinicNoteFields |
            Where-Object { $_.key -eq "follow_up_status" }
    ) | Select-Object -First 1
    Add-Check `
        "Legacy Clinic Note adoption maps its encounter fields without PHP execution" `
        ($null -ne $clinicNoteDefinition `
            -and $clinicNoteDefinition.contextScope -eq "encounter" `
            -and $clinicNoteDefinition.signaturePolicy -eq "author-only" `
            -and $clinicNoteDetail.currentRevision.status -eq "effective" `
            -and $clinicNoteDetail.currentRevision.schemaHash.Length -eq 64 `
            -and (($clinicNoteFields.key -join "|") -eq "history|examination|plan|follow_up_status|follow_up_timing") `
            -and $clinicNoteFollowUp.codeSystem -eq "legacy_clinic_note_followup_v1" `
            -and (($clinicNoteFollowUp.options.code -join "|") -eq "required_in|pending_investigation|none_required")) `
        @{
            definitionId = $clinicNoteDefinition.definitionId
            revision = $clinicNoteDetail.currentRevision.revision
            schemaHash = $clinicNoteDetail.currentRevision.schemaHash
            fields = $clinicNoteFields.key
            followUpCodes = $clinicNoteFollowUp.options.code
        }

    $clinicalInstructionsDefinition = @(
        $catalog.definitions |
            Where-Object { $_.stableKey -eq "legacy.clinicalinstructions" }
    ) | Select-Object -First 1
    $clinicalInstructionsDetail = if ($null -ne $clinicalInstructionsDefinition) {
        Invoke-Json `
            -Uri "$ApiBaseUrl/api/form-engine/definitions/$($clinicalInstructionsDefinition.definitionId)" `
            -RequestHeaders $adminHeaders
    }
    else {
        $null
    }
    $clinicalInstructionsFields = @($clinicalInstructionsDetail.currentRevision.definition.fields)
    Add-Check `
        "Legacy Clinical Instructions adoption maps its single encounter instruction without PHP execution" `
        ($null -ne $clinicalInstructionsDefinition `
            -and $clinicalInstructionsDefinition.contextScope -eq "encounter" `
            -and $clinicalInstructionsDefinition.signaturePolicy -eq "author-only" `
            -and $clinicalInstructionsDetail.currentRevision.status -eq "effective" `
            -and $clinicalInstructionsDetail.currentRevision.schemaHash.Length -eq 64 `
            -and $clinicalInstructionsFields.Count -eq 1 `
            -and $clinicalInstructionsFields[0].key -eq "instruction" `
            -and $clinicalInstructionsFields[0].type -eq "multiline" `
            -and $clinicalInstructionsFields[0].maxLength -eq 4000) `
        @{
            definitionId = $clinicalInstructionsDefinition.definitionId
            revision = $clinicalInstructionsDetail.currentRevision.revision
            schemaHash = $clinicalInstructionsDetail.currentRevision.schemaHash
            fields = $clinicalInstructionsFields.key
            maxLength = $clinicalInstructionsFields[0].maxLength
        }

    $soapDefinition = @(
        $catalog.definitions |
            Where-Object { $_.stableKey -eq "legacy.soap" }
    ) | Select-Object -First 1
    $soapDetail = if ($null -ne $soapDefinition) {
        Invoke-Json `
            -Uri "$ApiBaseUrl/api/form-engine/definitions/$($soapDefinition.definitionId)" `
            -RequestHeaders $adminHeaders
    }
    else {
        $null
    }
    $soapFields = @($soapDetail.currentRevision.definition.fields)
    Add-Check `
        "Legacy SOAP adoption maps its four encounter narratives without PHP execution" `
        ($null -ne $soapDefinition `
            -and $soapDefinition.contextScope -eq "encounter" `
            -and $soapDefinition.signaturePolicy -eq "author-only" `
            -and $soapDetail.currentRevision.status -eq "effective" `
            -and $soapDetail.currentRevision.schemaHash -eq "dead7d95ea9efc8a9a4800aac321b53143a34f8d5663958935290184924a90a0" `
            -and (($soapFields.key -join "|") -eq "subjective|objective|assessment|plan") `
            -and @($soapFields | Where-Object { $_.type -ne "multiline" -or $_.maxLength -ne 4000 }).Count -eq 0) `
        @{
            definitionId = $soapDefinition.definitionId
            revision = $soapDetail.currentRevision.revision
            schemaHash = $soapDetail.currentRevision.schemaHash
            fields = $soapFields.key
            maxLengths = $soapFields.maxLength
        }

    $carePlanDefinition = @(
        $catalog.definitions |
            Where-Object { $_.stableKey -eq "legacy.careplan" }
    ) | Select-Object -First 1
    $carePlanDetail = if ($null -ne $carePlanDefinition) {
        Invoke-Json `
            -Uri "$ApiBaseUrl/api/form-engine/definitions/$($carePlanDefinition.definitionId)" `
            -RequestHeaders $adminHeaders
    }
    else {
        $null
    }
    $carePlanItems = @($carePlanDetail.currentRevision.definition.fields | Where-Object { $_.key -eq "items" }) | Select-Object -First 1
    $carePlanPreview = if ($null -ne $carePlanItems) {
        Invoke-Json `
            -Uri "$ApiBaseUrl/api/form-engine/preview" `
            -Method "POST" `
            -RequestHeaders $adminHeaders `
            -Body @{
                definition = $carePlanDetail.currentRevision.definition
                values = @{
                    items = @(@{
                        code = "SNOMED-CT:123456"
                        code_text = "Focused care plan item"
                        plan_type = "goal"
                        service_date = "2026-07-28T09:00:00Z"
                        target_date = "2026-08-28T09:00:00Z"
                        plan_status = "active"
                        description = "Synthetic bounded Care Plan verification."
                        reason_code = "SNOMED-CT:654321"
                        reason_status = "active"
                    })
                }
            }
    }
    else {
        $null
    }
    Add-Check `
        "Legacy Care Plan adoption maps bounded repeating encounter items without PHP execution" `
        ($null -ne $carePlanDefinition `
            -and $carePlanDefinition.contextScope -eq "encounter" `
            -and $carePlanDefinition.signaturePolicy -eq "author-only" `
            -and $carePlanDetail.currentRevision.status -eq "effective" `
            -and $carePlanDetail.currentRevision.schemaHash -eq "fe5d0b72861330ec2da403f62910467dd035112858ee34f544b13768f6e8c535" `
            -and $carePlanItems.type -eq "repeat" `
            -and $carePlanItems.repeatMinimum -eq 0 `
            -and $carePlanItems.repeatMaximum -eq 20 `
            -and (($carePlanItems.children.key -join "|") -eq "code|code_text|plan_type|service_date|target_date|end_date|plan_status|description|reason_code|reason_description|reason_status|reason_start_date|reason_end_date") `
            -and (($carePlanItems.children | Where-Object { $_.key -eq "plan_type" }).options.code -join "|") -eq "plan_of_care|test_or_order|procedure|appointments|instructions|goal|health_concern|medication|intervention|planned_medication_activity|supply_order|device_order" `
            -and (($carePlanItems.children | Where-Object { $_.key -eq "plan_status" }).options.code -join "|") -eq "draft|active|on_hold|revoked|completed|entered_in_error|unknown" `
            -and $carePlanPreview.valid) `
        @{
            definitionId = $carePlanDefinition.definitionId
            revision = $carePlanDetail.currentRevision.revision
            schemaHash = $carePlanDetail.currentRevision.schemaHash
            childFields = $carePlanItems.children.key
            planTypeCodes = ($carePlanItems.children | Where-Object { $_.key -eq "plan_type" }).options.code
            planStatusCodes = ($carePlanItems.children | Where-Object { $_.key -eq "plan_status" }).options.code
            previewValid = $carePlanPreview.valid
        }

    $clinicalNotesDefinition = @($catalog.definitions | Where-Object { $_.stableKey -eq "legacy.clinicalnotes" }) | Select-Object -First 1
    $clinicalNotesDetail = if ($null -ne $clinicalNotesDefinition) { Invoke-Json -Uri "$ApiBaseUrl/api/form-engine/definitions/$($clinicalNotesDefinition.definitionId)" -RequestHeaders $adminHeaders } else { $null }
    $clinicalNotesItems = @($clinicalNotesDetail.currentRevision.definition.fields | Where-Object { $_.key -eq "items" }) | Select-Object -First 1
    $clinicalNotesPreview = if ($null -ne $clinicalNotesItems) { Invoke-Json -Uri "$ApiBaseUrl/api/form-engine/preview" -Method "POST" -RequestHeaders $adminHeaders -Body @{ definition=$clinicalNotesDetail.currentRevision.definition; values=@{ items=@(@{ note_date="2026-07-28"; code="SNOMED-CT:123456"; note_type="Progress"; note_category="Clinical"; narrative="Synthetic bounded Clinical Notes verification." }) } } } else { $null }
    Add-Check `
        "Legacy Clinical Notes adoption maps bounded repeating encounter entries without PHP execution" `
        ($null -ne $clinicalNotesDefinition -and $clinicalNotesDefinition.contextScope -eq "encounter" -and $clinicalNotesDefinition.signaturePolicy -eq "author-only" -and $clinicalNotesDetail.currentRevision.status -eq "effective" -and $clinicalNotesDetail.currentRevision.schemaHash -eq "3dee3d5d24d1e564b14e3e3e0f6c1a618895ffe2cff8cfef8431eec32066299f" -and $clinicalNotesItems.type -eq "repeat" -and $clinicalNotesItems.repeatMaximum -eq 20 -and (($clinicalNotesItems.children.key -join "|") -eq "note_date|code|code_text|note_type|note_category|narrative") -and $clinicalNotesPreview.valid) `
        @{ definitionId=$clinicalNotesDefinition.definitionId; schemaHash=$clinicalNotesDetail.currentRevision.schemaHash; childFields=$clinicalNotesItems.children.key; previewValid=$clinicalNotesPreview.valid }

    $functionalDefinition = @($catalog.definitions | Where-Object { $_.stableKey -eq "legacy.functionalcognitivestatus" }) | Select-Object -First 1
    $functionalDetail = if ($null -ne $functionalDefinition) { Invoke-Json -Uri "$ApiBaseUrl/api/form-engine/definitions/$($functionalDefinition.definitionId)" -RequestHeaders $adminHeaders } else { $null }
    $functionalItems = @($functionalDetail.currentRevision.definition.fields | Where-Object { $_.key -eq "items" }) | Select-Object -First 1
    Add-Check `
        "Legacy Functional and Cognitive Status adoption maps bounded encounter entries without PHP execution" `
        ($null -ne $functionalDefinition -and $functionalDefinition.contextScope -eq "encounter" -and $functionalDefinition.signaturePolicy -eq "author-only" -and $functionalDetail.currentRevision.schemaHash -eq "088c747c126a4b4520992468caacb9f4c704acb604315d06760b2d34c4069b71" -and $functionalItems.repeatMaximum -eq 20 -and (($functionalItems.children.key -join "|") -eq "code|code_text|status_date|is_mental_status|description")) `
        @{ definitionId=$functionalDefinition.definitionId; schemaHash=$functionalDetail.currentRevision.schemaHash; childFields=$functionalItems.children.key }

    $afterCareDefinition = @($catalog.definitions | Where-Object { $_.stableKey -eq "legacy.aftercareplan" }) | Select-Object -First 1
    $afterCareDetail = if ($null -ne $afterCareDefinition) { Invoke-Json -Uri "$ApiBaseUrl/api/form-engine/definitions/$($afterCareDefinition.definitionId)" -RequestHeaders $adminHeaders } else { $null }
    $afterCareFields = @($afterCareDetail.currentRevision.definition.fields)
    $afterCareGoalFields = @($afterCareFields | Where-Object { $_.key -like "goal_*" })
    Add-Check `
        "Legacy AfterCare Plan adoption maps fixed encounter discharge goals without PHP execution" `
        ($null -ne $afterCareDefinition -and $afterCareDefinition.contextScope -eq "encounter" -and $afterCareDefinition.signaturePolicy -eq "author-only" -and $afterCareDetail.currentRevision.schemaHash -eq "3e8e5d6d3b865136d321e250653a31aa628957b1ad57ed820f2dc65ad1015bb5" -and (($afterCareFields.key -join "|") -eq "admit_date|discharged_date|goal_a_1|goal_a_2|goal_a_3|goal_b_1|goal_b_2|goal_c_1|goal_c_2") -and @($afterCareFields | Where-Object { $_.type -eq "date" }).Count -eq 2 -and $afterCareGoalFields.Count -eq 7 -and @($afterCareGoalFields | Where-Object { $_.type -ne "multiline" -or $_.maxLength -ne 4000 }).Count -eq 0) `
        @{ definitionId=$afterCareDefinition.definitionId; schemaHash=$afterCareDetail.currentRevision.schemaHash; fields=$afterCareFields.key }

    $phqDefinition = @($catalog.definitions | Where-Object { $_.stableKey -eq "legacy.phq9" }) | Select-Object -First 1
    $phqDetail = if ($null -ne $phqDefinition) { Invoke-Json -Uri "$ApiBaseUrl/api/form-engine/definitions/$($phqDefinition.definitionId)" -RequestHeaders $adminHeaders } else { $null }
    $phqScoreFields = @($phqDetail.currentRevision.definition.fields | Where-Object { $_.key -match "_score$" -and $_.key -ne "total_score" })
    $phqValues = [ordered]@{
        interest_score = "0"; hopeless_score = "1"; sleep_score = "2"; fatigue_score = "3"; appetite_score = "0"; failure_score = "1"; focus_score = "2"; psychomotor_score = "3"; suicide_score = "0"; difficulty = "1"
    }
    $phqPreview = if ($null -ne $phqDetail) { Invoke-Json -Uri "$ApiBaseUrl/api/form-engine/preview" -Method "POST" -RequestHeaders $adminHeaders -Body @{ definition = $phqDetail.currentRevision.definition; values = $phqValues } } else { $null }
    $phqPositivePreview = if ($null -ne $phqDetail) { Invoke-Json -Uri "$ApiBaseUrl/api/form-engine/preview" -Method "POST" -RequestHeaders $adminHeaders -Body @{ definition = $phqDetail.currentRevision.definition; values = @{ interest_score="0"; hopeless_score="0"; sleep_score="0"; fatigue_score="0"; appetite_score="0"; failure_score="0"; focus_score="0"; psychomotor_score="0"; suicide_score="1"; difficulty="1" } } } else { $null }
    Add-Check `
        "Legacy PHQ-9 adoption calculates the bounded total and conditional impact question without PHP execution" `
        ($null -ne $phqDefinition -and $phqDefinition.contextScope -eq "encounter" -and $phqDefinition.signaturePolicy -eq "author-only" -and $phqDetail.currentRevision.schemaHash -eq "554327a15216462cf1b2e5edfbbc444f51c9e79da984a4408153ce3621b2c900" -and $phqScoreFields.Count -eq 9 -and @($phqScoreFields | Where-Object { $_.type -ne "select" -or $_.required -ne $true -or $_.options.Count -ne 4 }).Count -eq 0 -and $phqPreview.valid -and $phqPreview.values.total_score -eq 12 -and $phqPreview.visibleFields.difficulty -and $phqPreview.requiredFields.difficulty -and $phqPositivePreview.valid -and $phqPositivePreview.values.total_score -eq 1 -and @($phqPositivePreview.issues | Where-Object { $_.ruleKey -eq "warn_positive_self_harm_response" -and $_.severity -eq "warning" }).Count -eq 1) `
        @{ definitionId=$phqDefinition.definitionId; schemaHash=$phqDetail.currentRevision.schemaHash; calculatedTotal=$phqPreview.values.total_score; selfHarmWarningCount=@($phqPositivePreview.issues | Where-Object { $_.ruleKey -eq "warn_positive_self_harm_response" }).Count }

    $gadDefinition = @($catalog.definitions | Where-Object { $_.stableKey -eq "legacy.gad7" }) | Select-Object -First 1
    $gadDetail = if ($null -ne $gadDefinition) { Invoke-Json -Uri "$ApiBaseUrl/api/form-engine/definitions/$($gadDefinition.definitionId)" -RequestHeaders $adminHeaders } else { $null }
    $gadScores = @($gadDetail.currentRevision.definition.fields | Where-Object { $_.key -match "_score$" -and $_.key -ne "total_score" })
    $gadPreview = if ($null -ne $gadDetail) { Invoke-Json -Uri "$ApiBaseUrl/api/form-engine/preview" -Method "POST" -RequestHeaders $adminHeaders -Body @{ definition=$gadDetail.currentRevision.definition; values=@{ nervous_score="0"; control_worry_score="1"; worry_score="2"; relax_score="3"; restless_score="0"; irritable_score="1"; fear_score="2"; difficulty="1" } } } else { $null }
    Add-Check `
        "Legacy GAD-7 adoption calculates its bounded total and conditional impact question without PHP execution" `
        ($null -ne $gadDefinition -and $gadDefinition.contextScope -eq "encounter" -and $gadDefinition.signaturePolicy -eq "author-only" -and $gadScores.Count -eq 7 -and (($gadDetail.currentRevision.definition.fields.key -join "|") -eq "nervous_score|control_worry_score|worry_score|relax_score|restless_score|irritable_score|fear_score|difficulty|total_score") -and $gadPreview.valid -and $gadPreview.values.total_score -eq 9 -and $gadPreview.visibleFields.difficulty -and $gadPreview.requiredFields.difficulty) `
        @{ definitionId=$gadDefinition.definitionId; schemaHash=$gadDetail.currentRevision.schemaHash; calculatedTotal=$gadPreview.values.total_score }

    $transferSummaryDefinition = @($catalog.definitions | Where-Object { $_.stableKey -eq "legacy.transfersummary" }) | Select-Object -First 1
    $transferSummaryDetail = if ($null -ne $transferSummaryDefinition) { Invoke-Json -Uri "$ApiBaseUrl/api/form-engine/definitions/$($transferSummaryDefinition.definitionId)" -RequestHeaders $adminHeaders } else { $null }
    $transferSummaryFields = @($transferSummaryDetail.currentRevision.definition.fields)
    $transferSummaryNarratives = @($transferSummaryFields | Where-Object { $_.key -in @("status_of_admission", "diagnosis", "intervention_provided", "overall_status_of_discharge") })
    $transferSummaryPreview = if ($null -ne $transferSummaryDetail) { Invoke-Json -Uri "$ApiBaseUrl/api/form-engine/preview" -Method "POST" -RequestHeaders $adminHeaders -Body @{ definition=$transferSummaryDetail.currentRevision.definition; values=@{ transfer_to="Example specialty clinic"; transfer_date="2026-07-29"; status_of_admission="Stable admission status."; diagnosis="Transfer diagnosis."; intervention_provided="Care coordination completed."; overall_status_of_discharge="Transferred in stable condition." } } } else { $null }
    Add-Check `
        "Legacy Transfer Summary adoption maps fixed encounter transfer fields without PHP execution" `
        ($null -ne $transferSummaryDefinition -and $transferSummaryDefinition.contextScope -eq "encounter" -and $transferSummaryDefinition.signaturePolicy -eq "author-only" -and $transferSummaryDetail.currentRevision.status -eq "effective" -and (($transferSummaryFields.key -join "|") -eq "transfer_to|transfer_date|status_of_admission|diagnosis|intervention_provided|overall_status_of_discharge") -and ($transferSummaryFields | Where-Object { $_.key -eq "transfer_to" }).type -eq "text" -and ($transferSummaryFields | Where-Object { $_.key -eq "transfer_to" }).maxLength -eq 255 -and ($transferSummaryFields | Where-Object { $_.key -eq "transfer_date" }).type -eq "date" -and $transferSummaryNarratives.Count -eq 4 -and @($transferSummaryNarratives | Where-Object { $_.type -ne "multiline" -or $_.maxLength -ne 4000 }).Count -eq 0 -and $transferSummaryPreview.valid) `
        @{ definitionId=$transferSummaryDefinition.definitionId; schemaHash=$transferSummaryDetail.currentRevision.schemaHash; fields=$transferSummaryFields.key; previewValid=$transferSummaryPreview.valid }

    $dictationDefinition = @($catalog.definitions | Where-Object { $_.stableKey -eq "legacy.dictation" }) | Select-Object -First 1
    $dictationDetail = if ($null -ne $dictationDefinition) { Invoke-Json -Uri "$ApiBaseUrl/api/form-engine/definitions/$($dictationDefinition.definitionId)" -RequestHeaders $adminHeaders } else { $null }
    $dictationFields = @($dictationDetail.currentRevision.definition.fields)
    $dictationPreview = if ($null -ne $dictationDetail) { Invoke-Json -Uri "$ApiBaseUrl/api/form-engine/preview" -Method "POST" -RequestHeaders $adminHeaders -Body @{ definition=$dictationDetail.currentRevision.definition; values=@{ dictation="Synthetic dictation transcript."; additional_notes="Synthetic additional notes." } } } else { $null }
    Add-Check `
        "Legacy Speech Dictation adoption maps fixed encounter narratives without PHP execution" `
        ($null -ne $dictationDefinition -and $dictationDefinition.contextScope -eq "encounter" -and $dictationDefinition.signaturePolicy -eq "author-only" -and $dictationDetail.currentRevision.status -eq "effective" -and (($dictationFields.key -join "|") -eq "dictation|additional_notes") -and @($dictationFields | Where-Object { $_.type -ne "multiline" -or $_.maxLength -ne 4000 }).Count -eq 0 -and $dictationPreview.valid) `
        @{ definitionId=$dictationDefinition.definitionId; schemaHash=$dictationDetail.currentRevision.schemaHash; fields=$dictationFields.key; previewValid=$dictationPreview.valid }

    $workSchoolNoteDefinition = @($catalog.definitions | Where-Object { $_.stableKey -eq "legacy.workschoolnote" }) | Select-Object -First 1
    $workSchoolNoteDetail = if ($null -ne $workSchoolNoteDefinition) { Invoke-Json -Uri "$ApiBaseUrl/api/form-engine/definitions/$($workSchoolNoteDefinition.definitionId)" -RequestHeaders $adminHeaders } else { $null }
    $workSchoolNoteFields = @($workSchoolNoteDetail.currentRevision.definition.fields)
    $workSchoolNotePreview = if ($null -ne $workSchoolNoteDetail) { Invoke-Json -Uri "$ApiBaseUrl/api/form-engine/preview" -Method "POST" -RequestHeaders $adminHeaders -Body @{ definition=$workSchoolNoteDetail.currentRevision.definition; values=@{ note_type="work_note"; message="Synthetic work note message."; doctor="Example clinician"; date_of_signature="2026-07-29" } } } else { $null }
    Add-Check `
        "Legacy Work School Note adoption maps fixed encounter certificate fields without PHP execution" `
        ($null -ne $workSchoolNoteDefinition -and $workSchoolNoteDefinition.contextScope -eq "encounter" -and $workSchoolNoteDefinition.signaturePolicy -eq "author-only" -and $workSchoolNoteDetail.currentRevision.status -eq "effective" -and (($workSchoolNoteFields.key -join "|") -eq "note_type|message|doctor|date_of_signature") -and (($workSchoolNoteFields | Where-Object { $_.key -eq "note_type" }).options.code -join "|") -eq "work_note|school_note" -and ($workSchoolNoteFields | Where-Object { $_.key -eq "message" }).type -eq "multiline" -and ($workSchoolNoteFields | Where-Object { $_.key -eq "message" }).maxLength -eq 4000 -and ($workSchoolNoteFields | Where-Object { $_.key -eq "doctor" }).maxLength -eq 255 -and ($workSchoolNoteFields | Where-Object { $_.key -eq "date_of_signature" }).type -eq "date" -and $workSchoolNotePreview.valid) `
        @{ definitionId=$workSchoolNoteDefinition.definitionId; schemaHash=$workSchoolNoteDetail.currentRevision.schemaHash; fields=$workSchoolNoteFields.key; previewValid=$workSchoolNotePreview.valid }

    $priorAuthorizationDefinition = @($catalog.definitions | Where-Object { $_.stableKey -eq "legacy.priorauthorization" }) | Select-Object -First 1
    $priorAuthorizationDetail = if ($null -ne $priorAuthorizationDefinition) { Invoke-Json -Uri "$ApiBaseUrl/api/form-engine/definitions/$($priorAuthorizationDefinition.definitionId)" -RequestHeaders $adminHeaders } else { $null }
    $priorAuthorizationFields = @($priorAuthorizationDetail.currentRevision.definition.fields)
    $priorAuthorizationPreview = if ($null -ne $priorAuthorizationDetail) { Invoke-Json -Uri "$ApiBaseUrl/api/form-engine/preview" -Method "POST" -RequestHeaders $adminHeaders -Body @{ definition=$priorAuthorizationDetail.currentRevision.definition; values=@{ prior_auth_number="AUTH-12345"; date_from="2026-07-29"; date_to="2026-08-29"; comments="Synthetic prior authorization comments." } } } else { $null }
    Add-Check `
        "Legacy Prior Authorization adoption maps fixed encounter authorization fields without PHP execution" `
        ($null -ne $priorAuthorizationDefinition -and $priorAuthorizationDefinition.contextScope -eq "encounter" -and $priorAuthorizationDefinition.signaturePolicy -eq "author-only" -and $priorAuthorizationDetail.currentRevision.status -eq "effective" -and (($priorAuthorizationFields.key -join "|") -eq "prior_auth_number|date_from|date_to|comments") -and ($priorAuthorizationFields | Where-Object { $_.key -eq "prior_auth_number" }).maxLength -eq 35 -and @($priorAuthorizationFields | Where-Object { $_.key -in @("date_from", "date_to") -and $_.type -ne "date" }).Count -eq 0 -and ($priorAuthorizationFields | Where-Object { $_.key -eq "comments" }).maxLength -eq 4000 -and $priorAuthorizationPreview.valid) `
        @{ definitionId=$priorAuthorizationDefinition.definitionId; schemaHash=$priorAuthorizationDetail.currentRevision.schemaHash; fields=$priorAuthorizationFields.key; previewValid=$priorAuthorizationPreview.valid }

    $physicalExamDefinition = @($catalog.definitions | Where-Object { $_.stableKey -eq "legacy.physicalexam" }) | Select-Object -First 1
    $physicalExamDetail = if ($null -ne $physicalExamDefinition) { Invoke-Json -Uri "$ApiBaseUrl/api/form-engine/definitions/$($physicalExamDefinition.definitionId)" -RequestHeaders $adminHeaders } else { $null }
    $physicalExamItems = @($physicalExamDetail.currentRevision.definition.fields | Where-Object { $_.key -eq "items" }) | Select-Object -First 1
    $physicalExamPreview = if ($null -ne $physicalExamDetail) { Invoke-Json -Uri "$ApiBaseUrl/api/form-engine/preview" -Method "POST" -RequestHeaders $adminHeaders -Body @{ definition=$physicalExamDetail.currentRevision.definition; values=@{ items=@(@{ line_id="genwell"; wnl=$true; abn=$false; diagnosis=""; comments="Normal appearance." }, @{ line_id="trtlabs"; wnl=$true; abn=$false; diagnosis=""; comments="Laboratory evaluation ordered." }) } } } else { $null }
    Add-Check `
        "Legacy Physical Exam adoption maps bounded catalog-backed observation and treatment lines without PHP execution" `
        ($null -ne $physicalExamDefinition -and $physicalExamDefinition.contextScope -eq "encounter" -and $physicalExamDefinition.signaturePolicy -eq "author-only" -and $physicalExamDetail.currentRevision.status -eq "effective" -and $physicalExamItems.type -eq "repeat" -and $physicalExamItems.repeatMinimum -eq 0 -and $physicalExamItems.repeatMaximum -eq 20 -and (($physicalExamItems.children.key -join "|") -eq "line_id|wnl|abn|diagnosis|comments") -and $physicalExamItems.children[0].options.Count -eq 37 -and ($physicalExamItems.children | Where-Object { $_.key -eq "diagnosis" }).maxLength -eq 255 -and ($physicalExamItems.children | Where-Object { $_.key -eq "comments" }).maxLength -eq 250 -and $physicalExamPreview.valid) `
        @{ definitionId=$physicalExamDefinition.definitionId; schemaHash=$physicalExamDetail.currentRevision.schemaHash; lineOptionCount=$physicalExamItems.children[0].options.Count; childFields=$physicalExamItems.children.key; previewValid=$physicalExamPreview.valid; previewIssues=$physicalExamPreview.issues }

    $treatmentPlanDefinition = @($catalog.definitions | Where-Object { $_.stableKey -eq "legacy.treatmentplan" }) | Select-Object -First 1
    $treatmentPlanDetail = if ($null -ne $treatmentPlanDefinition) { Invoke-Json -Uri "$ApiBaseUrl/api/form-engine/definitions/$($treatmentPlanDefinition.definitionId)" -RequestHeaders $adminHeaders } else { $null }
    $treatmentPlanFields = @($treatmentPlanDetail.currentRevision.definition.fields)
    $treatmentPlanNarratives = @($treatmentPlanFields | Where-Object { $_.key -in @("presenting_issues", "patient_history", "medications", "anyother_relevant_information", "diagnosis", "treatment_received", "recommendation_for_follow_up") })
    $treatmentPlanPreview = if ($null -ne $treatmentPlanDetail) { Invoke-Json -Uri "$ApiBaseUrl/api/form-engine/preview" -Method "POST" -RequestHeaders $adminHeaders -Body @{ definition=$treatmentPlanDetail.currentRevision.definition; values=@{ provider="Example clinician"; admit_date="2026-07-29"; presenting_issues="Synthetic presenting issue."; patient_history="Synthetic history."; medications="Synthetic medication summary."; anyother_relevant_information="Synthetic relevant information."; diagnosis="Synthetic diagnosis."; treatment_received="Synthetic treatment."; recommendation_for_follow_up="Synthetic follow-up." } } } else { $null }
    Add-Check `
        "Legacy Treatment Plan adoption maps fixed encounter planning fields without PHP execution" `
        ($null -ne $treatmentPlanDefinition -and $treatmentPlanDefinition.contextScope -eq "encounter" -and $treatmentPlanDefinition.signaturePolicy -eq "author-only" -and $treatmentPlanDetail.currentRevision.status -eq "effective" -and (($treatmentPlanFields.key -join "|") -eq "provider|admit_date|presenting_issues|patient_history|medications|anyother_relevant_information|diagnosis|treatment_received|recommendation_for_follow_up") -and ($treatmentPlanFields | Where-Object { $_.key -eq "provider" }).maxLength -eq 255 -and ($treatmentPlanFields | Where-Object { $_.key -eq "admit_date" }).type -eq "date" -and $treatmentPlanNarratives.Count -eq 7 -and @($treatmentPlanNarratives | Where-Object { $_.type -ne "multiline" -or $_.maxLength -ne 4000 }).Count -eq 0 -and $treatmentPlanPreview.valid) `
        @{ definitionId=$treatmentPlanDefinition.definitionId; schemaHash=$treatmentPlanDetail.currentRevision.schemaHash; fields=$treatmentPlanFields.key; previewValid=$treatmentPlanPreview.valid }

    $ankleDefinition = @($catalog.definitions | Where-Object { $_.stableKey -eq "legacy.ankleassessment" }) | Select-Object -First 1
    $ankleDetail = if ($null -ne $ankleDefinition) { Invoke-Json -Uri "$ApiBaseUrl/api/form-engine/definitions/$($ankleDefinition.definitionId)" -RequestHeaders $adminHeaders } else { $null }
    $ankleFields = @($ankleDetail.currentRevision.definition.fields)
    $anklePreview = if ($null -ne $ankleDetail) { Invoke-Json -Uri "$ApiBaseUrl/api/form-engine/preview" -Method "POST" -RequestHeaders $adminHeaders -Body @{ definition=$ankleDetail.currentRevision.definition; values=@{ ankle_date_of_injuary="2026-07-29"; ankle_work_related=$true; ankle_foot="left"; ankle_severity_of_pain="2"; ankle_significant_swelling=$true; ankle_onset_of_swelling="within_hours"; ankle_how_did_injury_occur="Synthetic injury mechanism."; ankle_ottawa_bone_tenderness="lateral_malleolus"; ankle_able_to_bear_weight_steps="no"; ankle_x_ray_interpretation="normal"; ankle_additional_x_ray_notes="Synthetic x-ray note." } } } else { $null }
    Add-Check `
        "Legacy Ankle Assessment adoption maps fixed injury, Ottawa-rule, and x-ray fields without PHP execution" `
        ($null -ne $ankleDefinition -and $ankleDefinition.contextScope -eq "encounter" -and $ankleDefinition.signaturePolicy -eq "author-only" -and $ankleDetail.currentRevision.status -eq "effective" -and $ankleFields.Count -eq 11 -and (($ankleFields.key -join "|") -eq "ankle_date_of_injuary|ankle_work_related|ankle_foot|ankle_severity_of_pain|ankle_significant_swelling|ankle_onset_of_swelling|ankle_how_did_injury_occur|ankle_ottawa_bone_tenderness|ankle_able_to_bear_weight_steps|ankle_x_ray_interpretation|ankle_additional_x_ray_notes") -and (($ankleFields | Where-Object { $_.key -eq "ankle_foot" }).options.code -join "|") -eq "left|right" -and ($ankleFields | Where-Object { $_.key -eq "ankle_x_ray_interpretation" }).options.Count -eq 9 -and $anklePreview.valid) `
        @{ definitionId=$ankleDefinition.definitionId; schemaHash=$ankleDetail.currentRevision.schemaHash; fields=$ankleFields.key; previewValid=$anklePreview.valid }

    $anklePlanDefinition = @($catalog.definitions | Where-Object { $_.stableKey -eq "legacy.anklediagnosisplan" }) | Select-Object -First 1
    $anklePlanDetail = if ($null -ne $anklePlanDefinition) { Invoke-Json -Uri "$ApiBaseUrl/api/form-engine/definitions/$($anklePlanDefinition.definitionId)" -RequestHeaders $adminHeaders } else { $null }
    $anklePlanFields = @($anklePlanDetail.currentRevision.definition.fields)
    $anklePlanPreview = if ($null -ne $anklePlanDetail) { Invoke-Json -Uri "$ApiBaseUrl/api/form-engine/preview" -Method "POST" -RequestHeaders $adminHeaders -Body @{ definition=$anklePlanDetail.currentRevision.definition; values=@{ ankle_diagnosis1="icd9_845_00"; ankle_diagnosis2="icd9_824_2"; ankle_additional_diagnisis="Synthetic additional diagnosis."; ankle_plan="Synthetic treatment plan." } } } else { $null }
    Add-Check `
        "Legacy Ankle Diagnosis Plan adoption maps fixed diagnosis and treatment fields without PHP execution" `
        ($null -ne $anklePlanDefinition -and $anklePlanDefinition.contextScope -eq "encounter" -and $anklePlanDefinition.signaturePolicy -eq "author-only" -and $anklePlanDetail.currentRevision.status -eq "effective" -and (($anklePlanFields.key -join "|") -eq "ankle_diagnosis1|ankle_diagnosis2|ankle_diagnosis3|ankle_diagnosis4|ankle_additional_diagnisis|ankle_plan") -and @($anklePlanFields | Where-Object { $_.key -like "ankle_diagnosis[1-4]" -and $_.options.Count -ne 9 }).Count -eq 0 -and @($anklePlanFields | Where-Object { $_.key -in @("ankle_additional_diagnisis", "ankle_plan") -and ($_.type -ne "multiline" -or $_.maxLength -ne 4000) }).Count -eq 0 -and $anklePlanPreview.valid) `
        @{ definitionId=$anklePlanDefinition.definitionId; schemaHash=$anklePlanDetail.currentRevision.schemaHash; fields=$anklePlanFields.key; previewValid=$anklePlanPreview.valid }

    $bronchitisDefinition = @($catalog.definitions | Where-Object { $_.stableKey -eq "legacy.bronchitishistory" }) | Select-Object -First 1
    $bronchitisDetail = if ($null -ne $bronchitisDefinition) { Invoke-Json -Uri "$ApiBaseUrl/api/form-engine/definitions/$($bronchitisDefinition.definitionId)" -RequestHeaders $adminHeaders } else { $null }
    $bronchitisFields = @($bronchitisDetail.currentRevision.definition.fields)
    $bronchitisBooleanFields = @($bronchitisFields | Where-Object { $_.type -eq "boolean" })
    $bronchitisPreview = if ($null -ne $bronchitisDetail) { Invoke-Json -Uri "$ApiBaseUrl/api/form-engine/preview" -Method "POST" -RequestHeaders $adminHeaders -Body @{ definition=$bronchitisDetail.currentRevision.definition; values=@{ bronchitis_date_of_illness="2026-07-29"; bronchitis_hpi="Synthetic acute cough history."; bronchitis_ops_fever=$true; bronchitis_ops_cough=$true; bronchitis_ops_dizziness=$false; bronchitis_ops_chest_pain=$false; bronchitis_ops_dyspnea=$true; bronchitis_ops_sweating=$false; bronchitis_ops_wheezing=$true; bronchitis_ops_malaise=$true; bronchitis_ops_sputum=$true; bronchitis_ops_appearance="yellow"; bronchitis_ops_all_reviewed=$false; bronchitis_review_of_pmh=$true; bronchitis_review_of_medications=$true; bronchitis_review_of_allergies=$true; bronchitis_review_of_sh=$true; bronchitis_review_of_fh=$true } } } else { $null }
    Add-Check `
        "Legacy Bronchitis history adoption maps onset, HPI, symptoms, and history review without PHP execution" `
        ($null -ne $bronchitisDefinition -and $bronchitisDefinition.contextScope -eq "encounter" -and $bronchitisDefinition.signaturePolicy -eq "author-only" -and $bronchitisDetail.currentRevision.status -eq "effective" -and (($bronchitisFields.key -join "|") -eq "bronchitis_date_of_illness|bronchitis_hpi|bronchitis_ops_fever|bronchitis_ops_cough|bronchitis_ops_dizziness|bronchitis_ops_chest_pain|bronchitis_ops_dyspnea|bronchitis_ops_sweating|bronchitis_ops_wheezing|bronchitis_ops_malaise|bronchitis_ops_sputum|bronchitis_ops_appearance|bronchitis_ops_all_reviewed|bronchitis_review_of_pmh|bronchitis_review_of_medications|bronchitis_review_of_allergies|bronchitis_review_of_sh|bronchitis_review_of_fh") -and ($bronchitisFields | Where-Object { $_.key -eq "bronchitis_date_of_illness" }).type -eq "date" -and ($bronchitisFields | Where-Object { $_.key -eq "bronchitis_hpi" }).maxLength -eq 4000 -and $bronchitisBooleanFields.Count -eq 15 -and ($bronchitisFields | Where-Object { $_.key -eq "bronchitis_ops_appearance" }).maxLength -eq 255 -and $bronchitisPreview.valid) `
        @{ definitionId=$bronchitisDefinition.definitionId; schemaHash=$bronchitisDetail.currentRevision.schemaHash; fields=$bronchitisFields.key; booleanFieldCount=$bronchitisBooleanFields.Count; previewValid=$bronchitisPreview.valid }

    $bronchitisEarNoseDefinition = @($catalog.definitions | Where-Object { $_.stableKey -eq "legacy.bronchitisearnoseexam" }) | Select-Object -First 1
    $bronchitisEarNoseDetail = if ($null -ne $bronchitisEarNoseDefinition) { Invoke-Json -Uri "$ApiBaseUrl/api/form-engine/definitions/$($bronchitisEarNoseDefinition.definitionId)" -RequestHeaders $adminHeaders } else { $null }
    $bronchitisEarNoseFields = @($bronchitisEarNoseDetail.currentRevision.definition.fields)
    $bronchitisEarNosePreview = if ($null -ne $bronchitisEarNoseDetail) { Invoke-Json -Uri "$ApiBaseUrl/api/form-engine/preview" -Method "POST" -RequestHeaders $adminHeaders -Body @{ definition=$bronchitisEarNoseDetail.currentRevision.definition; values=@{ bronchitis_tms_normal_right=$true; bronchitis_tms_normal_left=$true; bronchitis_nares_normal_right=$true; bronchitis_nares_normal_left=$true; bronchitis_tms_thickened_right=$false; bronchitis_tms_thickened_left=$false; bronchitis_nares_swelling_right=$true; bronchitis_nares_swelling_left=$false; bronchitis_tms_af_level_right=$false; bronchitis_tms_af_level_left=$false; bronchitis_nares_discharge_right=$true; bronchitis_nares_discharge_left=$false; bronchitis_tms_retracted_right=$false; bronchitis_tms_retracted_left=$false; bronchitis_tms_bulging_right=$false; bronchitis_tms_bulging_left=$false; bronchitis_tms_perforated_right=$false; bronchitis_tms_perforated_left=$false; bronchitis_tms_nares_not_examined=$false } } } else { $null }
    Add-Check `
        "Legacy Bronchitis ear and nares exam adoption maps bilateral checklist fields without PHP execution" `
        ($null -ne $bronchitisEarNoseDefinition -and $bronchitisEarNoseDefinition.contextScope -eq "encounter" -and $bronchitisEarNoseDefinition.signaturePolicy -eq "author-only" -and $bronchitisEarNoseDetail.currentRevision.status -eq "effective" -and $bronchitisEarNoseFields.Count -eq 19 -and (($bronchitisEarNoseFields.key -join "|") -eq "bronchitis_tms_normal_right|bronchitis_tms_normal_left|bronchitis_nares_normal_right|bronchitis_nares_normal_left|bronchitis_tms_thickened_right|bronchitis_tms_thickened_left|bronchitis_nares_swelling_right|bronchitis_nares_swelling_left|bronchitis_tms_af_level_right|bronchitis_tms_af_level_left|bronchitis_nares_discharge_right|bronchitis_nares_discharge_left|bronchitis_tms_retracted_right|bronchitis_tms_retracted_left|bronchitis_tms_bulging_right|bronchitis_tms_bulging_left|bronchitis_tms_perforated_right|bronchitis_tms_perforated_left|bronchitis_tms_nares_not_examined") -and @($bronchitisEarNoseFields | Where-Object { $_.type -ne "boolean" }).Count -eq 0 -and $bronchitisEarNosePreview.valid) `
        @{ definitionId=$bronchitisEarNoseDefinition.definitionId; schemaHash=$bronchitisEarNoseDetail.currentRevision.schemaHash; fields=$bronchitisEarNoseFields.key; previewValid=$bronchitisEarNosePreview.valid }

    $bronchitisSinusDefinition = @($catalog.definitions | Where-Object { $_.stableKey -eq "legacy.bronchitissinusoropharynx" }) | Select-Object -First 1
    $bronchitisSinusDetail = if ($null -ne $bronchitisSinusDefinition) { Invoke-Json -Uri "$ApiBaseUrl/api/form-engine/definitions/$($bronchitisSinusDefinition.definitionId)" -RequestHeaders $adminHeaders } else { $null }
    $bronchitisSinusFields = @($bronchitisSinusDetail.currentRevision.definition.fields)
    $bronchitisSinusPreview = if ($null -ne $bronchitisSinusDetail) { Invoke-Json -Uri "$ApiBaseUrl/api/form-engine/preview" -Method "POST" -RequestHeaders $adminHeaders -Body @{ definition=$bronchitisSinusDetail.currentRevision.definition; values=@{ bronchitis_no_sinus_tenderness=$false; bronchitis_oropharynx_normal=$false; bronchitis_sinus_tenderness_frontal_right=$true; bronchitis_sinus_tenderness_frontal_left=$false; bronchitis_oropharynx_erythema=$true; bronchitis_oropharynx_exudate=$true; bronchitis_oropharynx_abcess=$false; bronchitis_oropharynx_ulcers=$false; bronchitis_sinus_tenderness_maxillary_right=$true; bronchitis_sinus_tenderness_maxillary_left=$false; bronchitis_oropharynx_appearance="erythematous"; bronchitis_sinus_tenderness_not_examined=$false; bronchitis_oropharynx_not_examined=$false } } } else { $null }
    Add-Check `
        "Legacy Bronchitis sinus and oropharynx adoption maps bilateral examination fields without PHP execution" `
        ($null -ne $bronchitisSinusDefinition -and $bronchitisSinusDefinition.contextScope -eq "encounter" -and $bronchitisSinusDefinition.signaturePolicy -eq "author-only" -and $bronchitisSinusDetail.currentRevision.status -eq "effective" -and $bronchitisSinusFields.Count -eq 13 -and (($bronchitisSinusFields.key -join "|") -eq "bronchitis_no_sinus_tenderness|bronchitis_oropharynx_normal|bronchitis_sinus_tenderness_frontal_right|bronchitis_sinus_tenderness_frontal_left|bronchitis_oropharynx_erythema|bronchitis_oropharynx_exudate|bronchitis_oropharynx_abcess|bronchitis_oropharynx_ulcers|bronchitis_sinus_tenderness_maxillary_right|bronchitis_sinus_tenderness_maxillary_left|bronchitis_oropharynx_appearance|bronchitis_sinus_tenderness_not_examined|bronchitis_oropharynx_not_examined") -and @($bronchitisSinusFields | Where-Object { $_.type -eq "boolean" }).Count -eq 12 -and ($bronchitisSinusFields | Where-Object { $_.key -eq "bronchitis_oropharynx_appearance" }).maxLength -eq 255 -and $bronchitisSinusPreview.valid) `
        @{ definitionId=$bronchitisSinusDefinition.definitionId; schemaHash=$bronchitisSinusDetail.currentRevision.schemaHash; fields=$bronchitisSinusFields.key; previewValid=$bronchitisSinusPreview.valid }

    $bronchitisCardiacDefinition = @($catalog.definitions | Where-Object { $_.stableKey -eq "legacy.bronchitiscardiacexam" }) | Select-Object -First 1
    $bronchitisCardiacDetail = if ($null -ne $bronchitisCardiacDefinition) { Invoke-Json -Uri "$ApiBaseUrl/api/form-engine/definitions/$($bronchitisCardiacDefinition.definitionId)" -RequestHeaders $adminHeaders } else { $null }
    $bronchitisCardiacFields = @($bronchitisCardiacDetail.currentRevision.definition.fields)
    $bronchitisCardiacPreview = if ($null -ne $bronchitisCardiacDetail) { Invoke-Json -Uri "$ApiBaseUrl/api/form-engine/preview" -Method "POST" -RequestHeaders $adminHeaders -Body @{ definition=$bronchitisCardiacDetail.currentRevision.definition; values=@{ bronchitis_heart_pmi=$false; bronchitis_heart_s3=$false; bronchitis_heart_s4=$false; bronchitis_heart_click=$false; bronchitis_heart_rub=$false; bronchitis_heart_murmur="none"; bronchitis_heart_grade="n/a"; bronchitis_heart_location="n/a"; bronchitis_heart_normal=$true; bronchitis_heart_not_examined=$false } } } else { $null }
    Add-Check `
        "Legacy Bronchitis cardiac exam adoption maps checklist and descriptive findings without PHP execution" `
        ($null -ne $bronchitisCardiacDefinition -and $bronchitisCardiacDefinition.contextScope -eq "encounter" -and $bronchitisCardiacDefinition.signaturePolicy -eq "author-only" -and $bronchitisCardiacDetail.currentRevision.status -eq "effective" -and (($bronchitisCardiacFields.key -join "|") -eq "bronchitis_heart_pmi|bronchitis_heart_s3|bronchitis_heart_s4|bronchitis_heart_click|bronchitis_heart_rub|bronchitis_heart_murmur|bronchitis_heart_grade|bronchitis_heart_location|bronchitis_heart_normal|bronchitis_heart_not_examined") -and @($bronchitisCardiacFields | Where-Object { $_.type -eq "boolean" }).Count -eq 7 -and @($bronchitisCardiacFields | Where-Object { $_.key -in @("bronchitis_heart_murmur", "bronchitis_heart_grade", "bronchitis_heart_location") -and ($_.type -ne "text" -or $_.maxLength -ne 4000) }).Count -eq 0 -and $bronchitisCardiacPreview.valid) `
        @{ definitionId=$bronchitisCardiacDefinition.definitionId; schemaHash=$bronchitisCardiacDetail.currentRevision.schemaHash; fields=$bronchitisCardiacFields.key; previewValid=$bronchitisCardiacPreview.valid }

    $bronchitisLungDefinition = @($catalog.definitions | Where-Object { $_.stableKey -eq "legacy.bronchitislungexam" }) | Select-Object -First 1
    $bronchitisLungDetail = if ($null -ne $bronchitisLungDefinition) { Invoke-Json -Uri "$ApiBaseUrl/api/form-engine/definitions/$($bronchitisLungDefinition.definitionId)" -RequestHeaders $adminHeaders } else { $null }
    $bronchitisLungFields = @($bronchitisLungDetail.currentRevision.definition.fields)
    $bronchitisLungPreview = if ($null -ne $bronchitisLungDetail) { Invoke-Json -Uri "$ApiBaseUrl/api/form-engine/preview" -Method "POST" -RequestHeaders $adminHeaders -Body @{ definition=$bronchitisLungDetail.currentRevision.definition; values=@{ bronchitis_lungs_bs_normal=$false; bronchitis_lungs_bs_reduced=$true; bronchitis_lungs_bs_increased=$false; bronchitis_lungs_crackles_lll=$true; bronchitis_lungs_crackles_rll=$false; bronchitis_lungs_crackles_bll=$false; bronchitis_lungs_rubs_lll=$false; bronchitis_lungs_rubs_rll=$false; bronchitis_lungs_rubs_bll=$false; bronchitis_lungs_wheezes_lll=$true; bronchitis_lungs_wheezes_rll=$false; bronchitis_lungs_wheezes_bll=$false; bronchitis_lungs_wheezes_dll=$false; bronchitis_lungs_normal_exam=$false; bronchitis_lungs_not_examined=$false } } } else { $null }
    Add-Check `
        "Legacy Bronchitis lung exam adoption maps breath sounds and bilateral finding fields without PHP execution" `
        ($null -ne $bronchitisLungDefinition -and $bronchitisLungDefinition.contextScope -eq "encounter" -and $bronchitisLungDefinition.signaturePolicy -eq "author-only" -and $bronchitisLungDetail.currentRevision.status -eq "effective" -and $bronchitisLungFields.Count -eq 15 -and (($bronchitisLungFields.key -join "|") -eq "bronchitis_lungs_bs_normal|bronchitis_lungs_bs_reduced|bronchitis_lungs_bs_increased|bronchitis_lungs_crackles_lll|bronchitis_lungs_crackles_rll|bronchitis_lungs_crackles_bll|bronchitis_lungs_rubs_lll|bronchitis_lungs_rubs_rll|bronchitis_lungs_rubs_bll|bronchitis_lungs_wheezes_lll|bronchitis_lungs_wheezes_rll|bronchitis_lungs_wheezes_bll|bronchitis_lungs_wheezes_dll|bronchitis_lungs_normal_exam|bronchitis_lungs_not_examined") -and @($bronchitisLungFields | Where-Object { $_.type -ne "boolean" }).Count -eq 0 -and $bronchitisLungPreview.valid) `
        @{ definitionId=$bronchitisLungDefinition.definitionId; schemaHash=$bronchitisLungDetail.currentRevision.schemaHash; fields=$bronchitisLungFields.key; previewValid=$bronchitisLungPreview.valid }

    $bronchitisDiagnosisDefinition = @($catalog.definitions | Where-Object { $_.stableKey -eq "legacy.bronchitisdiagnosisplan" }) | Select-Object -First 1
    $bronchitisDiagnosisDetail = if ($null -ne $bronchitisDiagnosisDefinition) { Invoke-Json -Uri "$ApiBaseUrl/api/form-engine/definitions/$($bronchitisDiagnosisDefinition.definitionId)" -RequestHeaders $adminHeaders } else { $null }
    $bronchitisDiagnosisFields = @($bronchitisDiagnosisDetail.currentRevision.definition.fields)
    $bronchitisDiagnosisPreview = if ($null -ne $bronchitisDiagnosisDetail) { Invoke-Json -Uri "$ApiBaseUrl/api/form-engine/preview" -Method "POST" -RequestHeaders $adminHeaders -Body @{ definition=$bronchitisDiagnosisDetail.currentRevision.definition; values=@{ bronchitis_diagnostic_tests="Chest x-ray ordered."; diagnosis1_bronchitis_form="icd9_466_0"; diagnosis2_bronchitis_form="icd9_519_7"; diagnosis3_bronchitis_form="none"; diagnosis4_bronchitis_form="none"; bronchitis_additional_diagnosis="Synthetic additional diagnosis."; bronchitis_treatment="Synthetic treatment plan." } } } else { $null }
    Add-Check `
        "Legacy Bronchitis diagnostic and treatment adoption maps fixed diagnosis vocabulary without PHP execution" `
        ($null -ne $bronchitisDiagnosisDefinition -and $bronchitisDiagnosisDefinition.contextScope -eq "encounter" -and $bronchitisDiagnosisDefinition.signaturePolicy -eq "author-only" -and $bronchitisDiagnosisDetail.currentRevision.status -eq "effective" -and (($bronchitisDiagnosisFields.key -join "|") -eq "bronchitis_diagnostic_tests|diagnosis1_bronchitis_form|diagnosis2_bronchitis_form|diagnosis3_bronchitis_form|diagnosis4_bronchitis_form|bronchitis_additional_diagnosis|bronchitis_treatment") -and @($bronchitisDiagnosisFields | Where-Object { $_.key -like "diagnosis[1-4]_bronchitis_form" -and $_.options.Count -ne 9 }).Count -eq 0 -and @($bronchitisDiagnosisFields | Where-Object { $_.key -in @("bronchitis_diagnostic_tests", "bronchitis_additional_diagnosis", "bronchitis_treatment") -and ($_.type -ne "multiline" -or $_.maxLength -ne 4000) }).Count -eq 0 -and $bronchitisDiagnosisPreview.valid) `
        @{ definitionId=$bronchitisDiagnosisDefinition.definitionId; schemaHash=$bronchitisDiagnosisDetail.currentRevision.schemaHash; fields=$bronchitisDiagnosisFields.key; previewValid=$bronchitisDiagnosisPreview.valid }

    $bronchitisCompositeDefinition = @($catalog.definitions | Where-Object { $_.stableKey -eq "legacy.bronchitis" }) | Select-Object -First 1
    $bronchitisCompositeDetail = if ($null -ne $bronchitisCompositeDefinition) { Invoke-Json -Uri "$ApiBaseUrl/api/form-engine/definitions/$($bronchitisCompositeDefinition.definitionId)" -RequestHeaders $adminHeaders } else { $null }
    $bronchitisCompositeFields = @($bronchitisCompositeDetail.currentRevision.definition.fields)
    $bronchitisCompositeSourceFields = @($bronchitisFields + $bronchitisEarNoseFields + $bronchitisSinusFields + $bronchitisCardiacFields + $bronchitisLungFields + $bronchitisDiagnosisFields)
    $bronchitisCompositePreview = if ($null -ne $bronchitisCompositeDetail) { Invoke-Json -Uri "$ApiBaseUrl/api/form-engine/preview" -Method "POST" -RequestHeaders $adminHeaders -Body @{ definition=$bronchitisCompositeDetail.currentRevision.definition; values=@{ bronchitis_date_of_illness="2026-07-29"; bronchitis_hpi="Synthetic unified acute cough history."; bronchitis_ops_cough=$true; bronchitis_tms_normal_right=$true; bronchitis_oropharynx_erythema=$true; bronchitis_heart_normal=$true; bronchitis_lungs_wheezes_lll=$true; bronchitis_diagnostic_tests="Chest x-ray ordered."; diagnosis1_bronchitis_form="icd9_466_0"; bronchitis_treatment="Synthetic unified treatment plan." } } } else { $null }
    Add-Check `
        "Legacy Bronchitis composition presents all mapped sections as one encounter form without PHP execution" `
        ($null -ne $bronchitisCompositeDefinition -and $bronchitisCompositeDefinition.contextScope -eq "encounter" -and $bronchitisCompositeDefinition.signaturePolicy -eq "author-only" -and $bronchitisCompositeDetail.currentRevision.status -eq "effective" -and $bronchitisCompositeFields.Count -eq 82 -and (($bronchitisCompositeFields.key -join "|") -eq ($bronchitisCompositeSourceFields.key -join "|")) -and ((@($bronchitisCompositeDetail.currentRevision.definition.sections).key -join "|") -eq "illness_history|pertinent_symptoms|history_review|ear_nose_exam|sinus_oropharynx_exam|cardiac_exam|lung_exam|diagnostic_plan") -and $bronchitisCompositePreview.valid) `
        @{ definitionId=$bronchitisCompositeDefinition.definitionId; schemaHash=$bronchitisCompositeDetail.currentRevision.schemaHash; fieldCount=$bronchitisCompositeFields.Count; sectionKeys=@($bronchitisCompositeDetail.currentRevision.definition.sections).key; previewValid=$bronchitisCompositePreview.valid }

    $rosGeneralDefinition = @($catalog.definitions | Where-Object { $_.stableKey -eq "legacy.reviewofsystemsgeneral" }) | Select-Object -First 1
    $rosGeneralDetail = if ($null -ne $rosGeneralDefinition) { Invoke-Json -Uri "$ApiBaseUrl/api/form-engine/definitions/$($rosGeneralDefinition.definitionId)" -RequestHeaders $adminHeaders } else { $null }
    $rosGeneralFields = @($rosGeneralDetail.currentRevision.definition.fields)
    $rosGeneralPreview = if ($null -ne $rosGeneralDetail) { Invoke-Json -Uri "$ApiBaseUrl/api/form-engine/preview" -Method "POST" -RequestHeaders $adminHeaders -Body @{ definition=$rosGeneralDetail.currentRevision.definition; values=@{ fever=$true; chills=$false; night_sweats=$true; weight_loss=$false; poor_appetite=$true; insomnia=$false; fatigued=$true; depressed=$false; hyperactive=$false; exposure_to_foreign_countries=$true } } } else { $null }
    Add-Check `
        "Legacy Review of Systems General adoption maps the complete checklist without PHP execution" `
        ($null -ne $rosGeneralDefinition -and $rosGeneralDefinition.contextScope -eq "encounter" -and $rosGeneralDefinition.signaturePolicy -eq "author-only" -and $rosGeneralDetail.currentRevision.status -eq "effective" -and $rosGeneralFields.Count -eq 10 -and (($rosGeneralFields.key -join "|") -eq "fever|chills|night_sweats|weight_loss|poor_appetite|insomnia|fatigued|depressed|hyperactive|exposure_to_foreign_countries") -and @($rosGeneralFields | Where-Object { $_.type -ne "boolean" }).Count -eq 0 -and $rosGeneralPreview.valid) `
        @{ definitionId=$rosGeneralDefinition.definitionId; schemaHash=$rosGeneralDetail.currentRevision.schemaHash; fields=$rosGeneralFields.key; previewValid=$rosGeneralPreview.valid }

    $rosSkinDefinition = @($catalog.definitions | Where-Object { $_.stableKey -eq "legacy.reviewofsystemsskin" }) | Select-Object -First 1
    $rosSkinDetail = if ($null -ne $rosSkinDefinition) { Invoke-Json -Uri "$ApiBaseUrl/api/form-engine/definitions/$($rosSkinDefinition.definitionId)" -RequestHeaders $adminHeaders } else { $null }
    $rosSkinFields = @($rosSkinDetail.currentRevision.definition.fields)
    $rosSkinPreview = if ($null -ne $rosSkinDetail) { Invoke-Json -Uri "$ApiBaseUrl/api/form-engine/preview" -Method "POST" -RequestHeaders $adminHeaders -Body @{ definition=$rosSkinDetail.currentRevision.definition; values=@{ rashes=$true; infections=$false; ulcerations=$false; pemphigus=$false; herpes=$true } } } else { $null }
    Add-Check `
        "Legacy Review of Systems Skin adoption preserves the distinct herpes field without PHP execution" `
        ($null -ne $rosSkinDefinition -and $rosSkinDefinition.contextScope -eq "encounter" -and $rosSkinDefinition.signaturePolicy -eq "author-only" -and $rosSkinDetail.currentRevision.status -eq "effective" -and (($rosSkinFields.key -join "|") -eq "rashes|infections|ulcerations|pemphigus|herpes") -and @($rosSkinFields | Where-Object { $_.type -ne "boolean" }).Count -eq 0 -and ($rosSkinFields | Where-Object { $_.key -eq "herpes" }).helpText -match "legacy page posts" -and $rosSkinPreview.valid) `
        @{ definitionId=$rosSkinDefinition.definitionId; schemaHash=$rosSkinDetail.currentRevision.schemaHash; fields=$rosSkinFields.key; previewValid=$rosSkinPreview.valid }

    $rosHeentDefinition = @($catalog.definitions | Where-Object { $_.stableKey -eq "legacy.reviewofsystemsheent" }) | Select-Object -First 1
    $rosHeentDetail = if ($null -ne $rosHeentDefinition) { Invoke-Json -Uri "$ApiBaseUrl/api/form-engine/definitions/$($rosHeentDefinition.definitionId)" -RequestHeaders $adminHeaders } else { $null }
    $rosHeentFields = @($rosHeentDetail.currentRevision.definition.fields)
    $rosHeentPreview = if ($null -ne $rosHeentDetail) { Invoke-Json -Uri "$ApiBaseUrl/api/form-engine/preview" -Method "POST" -RequestHeaders $adminHeaders -Body @{ definition=$rosHeentDetail.currentRevision.definition; values=@{ cataracts=$true; cataract_surgery=$false; glaucoma=$true; double_vision=$false; blurred_vision=$true; poor_hearing=$false; headaches=$true; ringing_in_ears=$false; bloody_nose=$true; sinusitis=$false; sinus_surgery=$true; dry_mouth=$false; strep_throat=$true; tonsillectomy=$false; swollen_lymph_nodes=$true; throat_cancer=$false; throat_cancer_surgery=$true } } } else { $null }
    Add-Check `
        "Legacy Review of Systems HEENT adoption maps the complete checklist without PHP execution" `
        ($null -ne $rosHeentDefinition -and $rosHeentDefinition.contextScope -eq "encounter" -and $rosHeentDefinition.signaturePolicy -eq "author-only" -and $rosHeentDetail.currentRevision.status -eq "effective" -and $rosHeentFields.Count -eq 17 -and (($rosHeentFields.key -join "|") -eq "cataracts|cataract_surgery|glaucoma|double_vision|blurred_vision|poor_hearing|headaches|ringing_in_ears|bloody_nose|sinusitis|sinus_surgery|dry_mouth|strep_throat|tonsillectomy|swollen_lymph_nodes|throat_cancer|throat_cancer_surgery") -and @($rosHeentFields | Where-Object { $_.type -ne "boolean" }).Count -eq 0 -and $rosHeentPreview.valid) `
        @{ definitionId=$rosHeentDefinition.definitionId; schemaHash=$rosHeentDetail.currentRevision.schemaHash; fields=$rosHeentFields.key; previewValid=$rosHeentPreview.valid }

    $rosPulmonaryDefinition = @($catalog.definitions | Where-Object { $_.stableKey -eq "legacy.reviewofsystemspulmonary" }) | Select-Object -First 1
    $rosPulmonaryDetail = if ($null -ne $rosPulmonaryDefinition) { Invoke-Json -Uri "$ApiBaseUrl/api/form-engine/definitions/$($rosPulmonaryDefinition.definitionId)" -RequestHeaders $adminHeaders } else { $null }
    $rosPulmonaryFields = @($rosPulmonaryDetail.currentRevision.definition.fields)
    $rosPulmonaryPreview = if ($null -ne $rosPulmonaryDetail) { Invoke-Json -Uri "$ApiBaseUrl/api/form-engine/preview" -Method "POST" -RequestHeaders $adminHeaders -Body @{ definition=$rosPulmonaryDetail.currentRevision.definition; values=@{ emphysema=$true; chronic_bronchitis=$false; interstitial_lung_disease=$true; shortness_of_breath_2=$false; lung_cancer=$true; lung_cancer_surgery=$false; pheumothorax=$true } } } else { $null }
    Add-Check `
        "Legacy Review of Systems Pulmonary adoption preserves the persisted pheumothorax key without PHP execution" `
        ($null -ne $rosPulmonaryDefinition -and $rosPulmonaryDefinition.contextScope -eq "encounter" -and $rosPulmonaryDefinition.signaturePolicy -eq "author-only" -and $rosPulmonaryDetail.currentRevision.status -eq "effective" -and (($rosPulmonaryFields.key -join "|") -eq "emphysema|chronic_bronchitis|interstitial_lung_disease|shortness_of_breath_2|lung_cancer|lung_cancer_surgery|pheumothorax") -and @($rosPulmonaryFields | Where-Object { $_.type -ne "boolean" }).Count -eq 0 -and ($rosPulmonaryFields | Where-Object { $_.key -eq "pheumothorax" }).helpText -match "persisted field spelling" -and $rosPulmonaryPreview.valid) `
        @{ definitionId=$rosPulmonaryDefinition.definitionId; schemaHash=$rosPulmonaryDetail.currentRevision.schemaHash; fields=$rosPulmonaryFields.key; previewValid=$rosPulmonaryPreview.valid }

    $rosCardiovascularDefinition = @($catalog.definitions | Where-Object { $_.stableKey -eq "legacy.reviewofsystemscardiovascular" }) | Select-Object -First 1
    $rosCardiovascularDetail = if ($null -ne $rosCardiovascularDefinition) { Invoke-Json -Uri "$ApiBaseUrl/api/form-engine/definitions/$($rosCardiovascularDefinition.definitionId)" -RequestHeaders $adminHeaders } else { $null }
    $rosCardiovascularFields = @($rosCardiovascularDetail.currentRevision.definition.fields)
    $rosCardiovascularPreview = if ($null -ne $rosCardiovascularDetail) { Invoke-Json -Uri "$ApiBaseUrl/api/form-engine/preview" -Method "POST" -RequestHeaders $adminHeaders -Body @{ definition=$rosCardiovascularDetail.currentRevision.definition; values=@{ heart_attack=$true; irregular_heart_beat=$false; chest_pains=$true; shortness_of_breath=$false; high_blood_pressure=$true; heart_failure=$false; poor_circulation=$true; vascular_surgery=$false; cardiac_catheterization=$true; coronary_artery_bypass=$false; heart_transplant=$true; stress_test=$false } } } else { $null }
    Add-Check `
        "Legacy Review of Systems Cardiovascular adoption maps the complete checklist without PHP execution" `
        ($null -ne $rosCardiovascularDefinition -and $rosCardiovascularDefinition.contextScope -eq "encounter" -and $rosCardiovascularDefinition.signaturePolicy -eq "author-only" -and $rosCardiovascularDetail.currentRevision.status -eq "effective" -and $rosCardiovascularFields.Count -eq 12 -and (($rosCardiovascularFields.key -join "|") -eq "heart_attack|irregular_heart_beat|chest_pains|shortness_of_breath|high_blood_pressure|heart_failure|poor_circulation|vascular_surgery|cardiac_catheterization|coronary_artery_bypass|heart_transplant|stress_test") -and @($rosCardiovascularFields | Where-Object { $_.type -ne "boolean" }).Count -eq 0 -and $rosCardiovascularPreview.valid) `
        @{ definitionId=$rosCardiovascularDefinition.definitionId; schemaHash=$rosCardiovascularDetail.currentRevision.schemaHash; fields=$rosCardiovascularFields.key; previewValid=$rosCardiovascularPreview.valid }

    $rosGastrointestinalDefinition = @($catalog.definitions | Where-Object { $_.stableKey -eq "legacy.reviewofsystemsgastrointestinal" }) | Select-Object -First 1
    $rosGastrointestinalDetail = if ($null -ne $rosGastrointestinalDefinition) { Invoke-Json -Uri "$ApiBaseUrl/api/form-engine/definitions/$($rosGastrointestinalDefinition.definitionId)" -RequestHeaders $adminHeaders } else { $null }
    $rosGastrointestinalFields = @($rosGastrointestinalDetail.currentRevision.definition.fields)
    $rosGastrointestinalPreview = if ($null -ne $rosGastrointestinalDetail) { Invoke-Json -Uri "$ApiBaseUrl/api/form-engine/preview" -Method "POST" -RequestHeaders $adminHeaders -Body @{ definition=$rosGastrointestinalDetail.currentRevision.definition; values=@{ stomach_pains=$true; peptic_ulcer_disease=$false; gastritis=$true; endoscopy=$false; polyps=$true; colonoscopy=$false; colon_cancer=$true; colon_cancer_surgery=$false; ulcerative_colitis=$true; crohns_disease=$false; appendectomy=$true; divirticulitis=$false; divirticulitis_surgery=$true; gall_stones=$false; cholecystectomy=$true; hepatitis=$false; cirrhosis_of_the_liver=$true; splenectomy=$false } } } else { $null }
    Add-Check `
        "Legacy Review of Systems Gastrointestinal adoption preserves its complete checklist without PHP execution" `
        ($null -ne $rosGastrointestinalDefinition -and $rosGastrointestinalDefinition.contextScope -eq "encounter" -and $rosGastrointestinalDefinition.signaturePolicy -eq "author-only" -and $rosGastrointestinalDetail.currentRevision.status -eq "effective" -and $rosGastrointestinalFields.Count -eq 18 -and (($rosGastrointestinalFields.key -join "|") -eq "stomach_pains|peptic_ulcer_disease|gastritis|endoscopy|polyps|colonoscopy|colon_cancer|colon_cancer_surgery|ulcerative_colitis|crohns_disease|appendectomy|divirticulitis|divirticulitis_surgery|gall_stones|cholecystectomy|hepatitis|cirrhosis_of_the_liver|splenectomy") -and @($rosGastrointestinalFields | Where-Object { $_.type -ne "boolean" }).Count -eq 0 -and @($rosGastrointestinalFields | Where-Object { $_.key -match '^divirticulitis' -and $_.helpText -notmatch 'persisted field spelling' }).Count -eq 0 -and $rosGastrointestinalPreview.valid) `
        @{ definitionId=$rosGastrointestinalDefinition.definitionId; schemaHash=$rosGastrointestinalDetail.currentRevision.schemaHash; fields=$rosGastrointestinalFields.key; previewValid=$rosGastrointestinalPreview.valid }

    $rosGenitourinaryDefinition = @($catalog.definitions | Where-Object { $_.stableKey -eq "legacy.reviewofsystemsgenitourinary" }) | Select-Object -First 1
    $rosGenitourinaryDetail = if ($null -ne $rosGenitourinaryDefinition) { Invoke-Json -Uri "$ApiBaseUrl/api/form-engine/definitions/$($rosGenitourinaryDefinition.definitionId)" -RequestHeaders $adminHeaders } else { $null }
    $rosGenitourinaryFields = @($rosGenitourinaryDetail.currentRevision.definition.fields)
    $rosGenitourinaryPreview = if ($null -ne $rosGenitourinaryDetail) { Invoke-Json -Uri "$ApiBaseUrl/api/form-engine/preview" -Method "POST" -RequestHeaders $adminHeaders -Body @{ definition=$rosGenitourinaryDetail.currentRevision.definition; values=@{ kidney_failure=$true; kidney_stones=$false; kidney_cancer=$true; kidney_infections=$false; bladder_infections=$true; bladder_cancer=$false; prostate_problems=$true; prostate_cancer=$false; kidney_transplant=$true; sexually_transmitted_disease=$false; burning_with_urination=$true; discharge_from_urethra=$false } } } else { $null }
    Add-Check `
        "Legacy Review of Systems Genitourinary adoption maps the complete checklist without PHP execution" `
        ($null -ne $rosGenitourinaryDefinition -and $rosGenitourinaryDefinition.contextScope -eq "encounter" -and $rosGenitourinaryDefinition.signaturePolicy -eq "author-only" -and $rosGenitourinaryDetail.currentRevision.status -eq "effective" -and $rosGenitourinaryFields.Count -eq 12 -and (($rosGenitourinaryFields.key -join "|") -eq "kidney_failure|kidney_stones|kidney_cancer|kidney_infections|bladder_infections|bladder_cancer|prostate_problems|prostate_cancer|kidney_transplant|sexually_transmitted_disease|burning_with_urination|discharge_from_urethra") -and @($rosGenitourinaryFields | Where-Object { $_.type -ne "boolean" }).Count -eq 0 -and $rosGenitourinaryPreview.valid) `
        @{ definitionId=$rosGenitourinaryDefinition.definitionId; schemaHash=$rosGenitourinaryDetail.currentRevision.schemaHash; fields=$rosGenitourinaryFields.key; previewValid=$rosGenitourinaryPreview.valid }

    $rosMusculoskeletalDefinition = @($catalog.definitions | Where-Object { $_.stableKey -eq "legacy.reviewofsystemsmusculoskeletal" }) | Select-Object -First 1
    $rosMusculoskeletalDetail = if ($null -ne $rosMusculoskeletalDefinition) { Invoke-Json -Uri "$ApiBaseUrl/api/form-engine/definitions/$($rosMusculoskeletalDefinition.definitionId)" -RequestHeaders $adminHeaders } else { $null }
    $rosMusculoskeletalFields = @($rosMusculoskeletalDetail.currentRevision.definition.fields)
    $rosMusculoskeletalPreview = if ($null -ne $rosMusculoskeletalDetail) { Invoke-Json -Uri "$ApiBaseUrl/api/form-engine/preview" -Method "POST" -RequestHeaders $adminHeaders -Body @{ definition=$rosMusculoskeletalDetail.currentRevision.definition; values=@{ osetoarthritis=$true; rheumotoid_arthritis=$false; lupus=$true; ankylosing_sondlilitis=$false; swollen_joints=$true; stiff_joints=$false; broken_bones=$true; neck_problems=$false; back_problems=$true; back_surgery=$false; scoliosis=$true; herniated_disc=$false; shoulder_problems=$true; elbow_problems=$false; wrist_problems=$true; hand_problems=$false; hip_problems=$true; knee_problems=$false; ankle_problems=$true; foot_problems=$false } } } else { $null }
    Add-Check `
        "Legacy Review of Systems Musculoskeletal adoption preserves all persisted spellings without PHP execution" `
        ($null -ne $rosMusculoskeletalDefinition -and $rosMusculoskeletalDefinition.contextScope -eq "encounter" -and $rosMusculoskeletalDefinition.signaturePolicy -eq "author-only" -and $rosMusculoskeletalDetail.currentRevision.status -eq "effective" -and $rosMusculoskeletalFields.Count -eq 20 -and (($rosMusculoskeletalFields.key -join "|") -eq "osetoarthritis|rheumotoid_arthritis|lupus|ankylosing_sondlilitis|swollen_joints|stiff_joints|broken_bones|neck_problems|back_problems|back_surgery|scoliosis|herniated_disc|shoulder_problems|elbow_problems|wrist_problems|hand_problems|hip_problems|knee_problems|ankle_problems|foot_problems") -and @($rosMusculoskeletalFields | Where-Object { $_.type -ne "boolean" }).Count -eq 0 -and @($rosMusculoskeletalFields | Where-Object { $_.key -in @('osetoarthritis','rheumotoid_arthritis','ankylosing_sondlilitis') -and $_.helpText -notmatch 'persisted field' }).Count -eq 0 -and $rosMusculoskeletalPreview.valid) `
        @{ definitionId=$rosMusculoskeletalDefinition.definitionId; schemaHash=$rosMusculoskeletalDetail.currentRevision.schemaHash; fields=$rosMusculoskeletalFields.key; previewValid=$rosMusculoskeletalPreview.valid }

    $rosEndocrineDefinition = @($catalog.definitions | Where-Object { $_.stableKey -eq "legacy.reviewofsystemsendocrine" }) | Select-Object -First 1
    $rosEndocrineDetail = if ($null -ne $rosEndocrineDefinition) { Invoke-Json -Uri "$ApiBaseUrl/api/form-engine/definitions/$($rosEndocrineDefinition.definitionId)" -RequestHeaders $adminHeaders } else { $null }
    $rosEndocrineFields = @($rosEndocrineDetail.currentRevision.definition.fields)
    $rosEndocrinePreview = if ($null -ne $rosEndocrineDetail) { Invoke-Json -Uri "$ApiBaseUrl/api/form-engine/preview" -Method "POST" -RequestHeaders $adminHeaders -Body @{ definition=$rosEndocrineDetail.currentRevision.definition; values=@{ insulin_dependent_diabetes=$true; noninsulin_dependent_diabetes=$false; hypothyroidism=$true; hyperthyroidism=$false; cushing_syndrom=$true; addison_syndrom=$false } } } else { $null }
    Add-Check `
        "Legacy Review of Systems Endocrine adoption preserves the persisted syndrom keys without PHP execution" `
        ($null -ne $rosEndocrineDefinition -and $rosEndocrineDefinition.contextScope -eq "encounter" -and $rosEndocrineDefinition.signaturePolicy -eq "author-only" -and $rosEndocrineDetail.currentRevision.status -eq "effective" -and (($rosEndocrineFields.key -join "|") -eq "insulin_dependent_diabetes|noninsulin_dependent_diabetes|hypothyroidism|hyperthyroidism|cushing_syndrom|addison_syndrom") -and @($rosEndocrineFields | Where-Object { $_.type -ne "boolean" }).Count -eq 0 -and @($rosEndocrineFields | Where-Object { $_.key -in @('cushing_syndrom','addison_syndrom') -and $_.helpText -notmatch 'persisted field spelling' }).Count -eq 0 -and $rosEndocrinePreview.valid) `
        @{ definitionId=$rosEndocrineDefinition.definitionId; schemaHash=$rosEndocrineDetail.currentRevision.schemaHash; fields=$rosEndocrineFields.key; previewValid=$rosEndocrinePreview.valid }

    $rosAdditionalNotesDefinition = @($catalog.definitions | Where-Object { $_.stableKey -eq "legacy.reviewofsystemsadditionalnotes" }) | Select-Object -First 1
    $rosAdditionalNotesDetail = if ($null -ne $rosAdditionalNotesDefinition) { Invoke-Json -Uri "$ApiBaseUrl/api/form-engine/definitions/$($rosAdditionalNotesDefinition.definitionId)" -RequestHeaders $adminHeaders } else { $null }
    $rosAdditionalNotesFields = @($rosAdditionalNotesDetail.currentRevision.definition.fields)
    $rosAdditionalNotesPreview = if ($null -ne $rosAdditionalNotesDetail) { Invoke-Json -Uri "$ApiBaseUrl/api/form-engine/preview" -Method "POST" -RequestHeaders $adminHeaders -Body @{ definition=$rosAdditionalNotesDetail.currentRevision.definition; values=@{ additional_notes="Legacy-compatible review of systems narrative." } } } else { $null }
    Add-Check `
        "Legacy Review of Systems Additional Notes adoption retains an unbounded longtext narrative without PHP execution" `
        ($null -ne $rosAdditionalNotesDefinition -and $rosAdditionalNotesDefinition.contextScope -eq "encounter" -and $rosAdditionalNotesDefinition.signaturePolicy -eq "author-only" -and $rosAdditionalNotesDetail.currentRevision.status -eq "effective" -and $rosAdditionalNotesFields.Count -eq 1 -and $rosAdditionalNotesFields[0].key -eq "additional_notes" -and $rosAdditionalNotesFields[0].type -eq "multiline" -and $null -eq $rosAdditionalNotesFields[0].maxLength -and $rosAdditionalNotesFields[0].helpText -match "longtext" -and $rosAdditionalNotesPreview.valid) `
        @{ definitionId=$rosAdditionalNotesDefinition.definitionId; schemaHash=$rosAdditionalNotesDetail.currentRevision.schemaHash; field=$rosAdditionalNotesFields[0].key; maxLength=$rosAdditionalNotesFields[0].maxLength; previewValid=$rosAdditionalNotesPreview.valid }

    $rosCompositeDefinition = @($catalog.definitions | Where-Object { $_.stableKey -eq "legacy.reviewofsystems" }) | Select-Object -First 1
    $rosCompositeDetail = if ($null -ne $rosCompositeDefinition) { Invoke-Json -Uri "$ApiBaseUrl/api/form-engine/definitions/$($rosCompositeDefinition.definitionId)" -RequestHeaders $adminHeaders } else { $null }
    $rosCompositeFields = @($rosCompositeDetail.currentRevision.definition.fields)
    $rosCompositePreview = if ($null -ne $rosCompositeDetail) { Invoke-Json -Uri "$ApiBaseUrl/api/form-engine/preview" -Method "POST" -RequestHeaders $adminHeaders -Body @{ definition=$rosCompositeDetail.currentRevision.definition; values=@{ fever=$true; rashes=$false; cataracts=$true; emphysema=$false; heart_attack=$true; stomach_pains=$false; kidney_failure=$true; osetoarthritis=$false; insulin_dependent_diabetes=$true; additional_notes="Composite ROS preview." } } } else { $null }
    Add-Check `
        "Legacy Review of Systems composition presents all mapped sections as one encounter form without PHP execution" `
        ($null -ne $rosCompositeDefinition -and $rosCompositeDefinition.contextScope -eq "encounter" -and $rosCompositeDefinition.signaturePolicy -eq "author-only" -and $rosCompositeDetail.currentRevision.status -eq "effective" -and $rosCompositeFields.Count -eq 108 -and (($rosCompositeFields.key -join "|") -eq (($rosGeneralFields + $rosSkinFields + $rosHeentFields + $rosPulmonaryFields + $rosCardiovascularFields + $rosGastrointestinalFields + $rosGenitourinaryFields + $rosMusculoskeletalFields + $rosEndocrineFields + $rosAdditionalNotesFields).key -join "|")) -and ((@($rosCompositeDetail.currentRevision.definition.sections).key -join "|") -eq "general|skin|heent|pulmonary|cardiovascular|gastrointestinal|genitourinary|musculoskeletal|endocrine|additional_notes") -and $rosCompositePreview.valid) `
        @{ definitionId=$rosCompositeDefinition.definitionId; schemaHash=$rosCompositeDetail.currentRevision.schemaHash; fieldCount=$rosCompositeFields.Count; sectionKeys=@($rosCompositeDetail.currentRevision.definition.sections).key; previewValid=$rosCompositePreview.valid }

    $legacyRosCompatibility = [ordered]@{
        "legacy.rosgeneral" = "weight_change|weakness|fatigue|anorexia|fever|chills|night_sweats|insomnia|irritability|heat_or_cold"
        "legacy.roseyes" = "change_in_vision|glaucoma_history|eye_pain|irritation|redness|excessive_tearing|double_vision|blind_spots|photophobia"
        "legacy.rosearnoseandthroat" = "hearing_loss|discharge|pain|vertigo|tinnitus|frequent_colds|sore_throat|sinus_problems|post_nasal_drip|nosebleed|snoring|apnea"
        "legacy.rosbreastpulmonary" = "breast_mass|breast_discharge|biopsy|abnormal_mammogram|cough|sputum|shortness_of_breath|wheezing|hemoptsyis|asthma|copd"
        "legacy.roscardiovascular" = "chest_pain|palpitation|syncope|pnd|doe|orthopnea|peripheal|edema|legpain_cramping|history_murmur|arrythmia|heart_problem"
        "legacy.rosgastrointestinal" = "dysphagia|heartburn|bloating|belching|flatulence|nausea|vomiting|hematemesis|gastro_pain|food_intolerance|hepatitis|jaundice|hematochezia|changed_bowel|diarrhea|constipation"
        "legacy.rosurinary" = "polyuria|polydypsia|dysuria|hematuria|frequency|urgency|incontinence|renal_stones|utis|hesitancy|dribbling|stream|nocturia|erections|ejaculations"
        "legacy.rosreproductive" = "g|p|ap|lc|mearche|menopause|lmp|f_frequency|f_flow|f_symptoms|abnormal_hair_growth|f_hirsutism"
    }
    $legacyRosProof = @()
    foreach ($legacyRosEntry in $legacyRosCompatibility.GetEnumerator()) {
        $definition = @($catalog.definitions | Where-Object { $_.stableKey -eq $legacyRosEntry.Key }) | Select-Object -First 1
        $detail = if ($null -ne $definition) { Invoke-Json -Uri "$ApiBaseUrl/api/form-engine/definitions/$($definition.definitionId)" -RequestHeaders $adminHeaders } else { $null }
        $fields = @($detail.currentRevision.definition.fields)
        $firstField = $fields | Select-Object -First 1
        $previewValues = [ordered]@{}
        if ($null -ne $firstField) { $previewValues[$firstField.key] = "yes" }
        $preview = if ($null -ne $detail -and $null -ne $firstField) { Invoke-Json -Uri "$ApiBaseUrl/api/form-engine/preview" -Method "POST" -RequestHeaders $adminHeaders -Body @{ definition=$detail.currentRevision.definition; values=$previewValues } } else { $null }
        $legacyRosProof += [pscustomobject]@{ stableKey=$legacyRosEntry.Key; definition=$definition; detail=$detail; fields=$fields; expectedFields=$legacyRosEntry.Value; preview=$preview }
    }
    Add-Check `
        "Legacy form_ros sections preserve three-state storage vocabulary as constrained select fields" `
        (@($legacyRosProof | Where-Object {
            $null -eq $_.definition `
                -or $_.definition.contextScope -ne "encounter" `
                -or $_.definition.signaturePolicy -ne "author-only" `
                -or $_.detail.currentRevision.status -ne "effective" `
                -or ($_.fields.key -join "|") -ne $_.expectedFields `
                -or @($_.fields | Where-Object { $_.type -ne "select" -or $null -ne $_.maxLength -or ($_.options.code -join "|") -ne "yes|no|na" -or ($_.options.display -join "|") -ne "YES|NO|N/A" }).Count -gt 0 `
                -or -not $_.preview.valid
        }).Count -eq 0) `
        @($legacyRosProof | ForEach-Object { @{ stableKey=$_.stableKey; definitionId=$_.definition.definitionId; fields=$_.fields.key; previewValid=$_.preview.valid } })

    $marker = [Guid]::NewGuid().ToString("N").Substring(0, 12)
    $stableKey = "tmp.form.$marker"
    $schema = New-TestSchema `
        -Key $stableKey `
        -Name "Focused form $marker" `
        -IncludeLocalization

    $preview = Invoke-Json `
        -Uri "$ApiBaseUrl/api/form-engine/preview" `
        -Method "POST" `
        -RequestHeaders $adminHeaders `
        -Body ([ordered]@{
            definition = $schema
            values = [ordered]@{
                chief_concern = "Focused preview"
                pain_score = 8
                disposition = "routine"
            }
        })
    Add-Check `
        "Synthetic preview evaluates declarative rules without persistence" `
        ($preview.valid `
            -and @($preview.issues | Where-Object {
                $_.severity -eq "warning" -and $_.ruleKey -eq "warn_high_pain"
            }).Count -eq 1) `
        $preview

    $repeatSchema = New-TestSchema `
        -Key "tmp.form.repeat.$marker" `
        -Name "Bounded repeat $marker"
    $repeatSchema.rules = @()
    $repeatSchema.fields = @(
        New-Field `
            -Key "observations" `
            -Label "Observations" `
            -Type "repeat" `
            -Sequence 10 `
            -RepeatMinimum 1 `
            -RepeatMaximum 3 `
            -Children @(
                New-Field `
                    -Key "score" `
                    -Label "Score" `
                    -Type "integer" `
                    -Sequence 10 `
                    -Required $true `
                    -Minimum 0 `
                    -Maximum 10 `
                    -Precision 0
                New-Field `
                    -Key "decision" `
                    -Label "Decision" `
                    -Type "select" `
                    -Sequence 20 `
                    -Options @(
                        [ordered]@{ code = "yes"; display = "Yes" },
                        [ordered]@{ code = "no"; display = "No" }
                    )
                New-Field `
                    -Key "note" `
                    -Label "Note" `
                    -Type "multiline" `
                    -Sequence 30 `
                    -MaxLength 200
                New-Field `
                    -Key "score_twice" `
                    -Label "Score twice" `
                    -Type "computed" `
                    -Sequence 40 `
                    -Minimum 0 `
                    -Maximum 20 `
                    -Precision 0 `
                    -ReadOnly $true
                New-Field `
                    -Key "score_quadruple" `
                    -Label "Score quadruple" `
                    -Type "computed" `
                    -Sequence 50 `
                    -Minimum 0 `
                    -Maximum 40 `
                    -Precision 0 `
                    -ReadOnly $true
            ) `
            -RowRules @(
                [ordered]@{
                    key = "calculate_score_quadruple"
                    condition = [ordered]@{
                        fieldKey = "score_twice"
                        operator = "is-not-empty"
                    }
                    action = "calculate"
                    targetFieldKey = "score_quadruple"
                    message = $null
                    calculation = [ordered]@{
                        operator = "multiply"
                        operands = @(
                            [ordered]@{ fieldKey = "score_twice"; constant = $null },
                            [ordered]@{ fieldKey = $null; constant = 2 }
                        )
                        precision = 0
                    }
                }
                [ordered]@{
                    key = "calculate_score_twice"
                    condition = [ordered]@{
                        fieldKey = "score"
                        operator = "is-not-empty"
                    }
                    action = "calculate"
                    targetFieldKey = "score_twice"
                    message = $null
                    calculation = [ordered]@{
                        operator = "multiply"
                        operands = @(
                            [ordered]@{ fieldKey = "score"; constant = $null },
                            [ordered]@{ fieldKey = $null; constant = 2 }
                        )
                        precision = 0
                    }
                }
                [ordered]@{
                    key = "require_yes_note"
                    condition = [ordered]@{
                        fieldKey = "decision"
                        operator = "equals"
                        value = "yes"
                    }
                    action = "require"
                    targetFieldKey = "note"
                    message = $null
                    calculation = $null
                }
                [ordered]@{
                    key = "hide_no_note"
                    condition = [ordered]@{
                        fieldKey = "decision"
                        operator = "equals"
                        value = "no"
                    }
                    action = "hide"
                    targetFieldKey = "note"
                    message = $null
                    calculation = $null
                }
            )
    )
    $missingRepeatPreview = Invoke-Json `
        -Uri "$ApiBaseUrl/api/form-engine/preview" `
        -Method "POST" `
        -RequestHeaders $adminHeaders `
        -Body @{ definition = $repeatSchema; values = @{} }
    $completeRepeatPreview = Invoke-Json `
        -Uri "$ApiBaseUrl/api/form-engine/preview" `
        -Method "POST" `
        -RequestHeaders $adminHeaders `
        -Body @{
            definition = $repeatSchema
            values = @{
                observations = @(
                    @{
                        score = 7
                        decision = "yes"
                        note = "Bounded row."
                        score_twice = 999
                    }
                    @{
                        score = 3
                        decision = "no"
                    }
                )
            }
        }
    $incompleteSameRowPreview = Invoke-Json `
        -Uri "$ApiBaseUrl/api/form-engine/preview" `
        -Method "POST" `
        -RequestHeaders $adminHeaders `
        -Body @{
            definition = $repeatSchema
            values = @{
                observations = @(
                    @{
                        score = 7
                        decision = "yes"
                    }
                    @{
                        score = 3
                        decision = "no"
                    }
                )
            }
        }
    Add-Check `
        "Bounded repeats enforce positive minimums and validate typed child rows" `
        (-not $missingRepeatPreview.valid `
            -and @($missingRepeatPreview.issues | Where-Object {
                $_.fieldKey -eq "observations" `
                    -and $_.message -match "must contain 1 to 3 rows"
            }).Count -eq 1 `
            -and $completeRepeatPreview.valid) `
        @{
            missingIssues = $missingRepeatPreview.issues
            completeIssues = $completeRepeatPreview.issues
        }
    Add-Check `
        "Same-row rules isolate rows, compute sibling outputs, and apply row-specific validation" `
        (-not $incompleteSameRowPreview.valid `
            -and $completeRepeatPreview.valid `
            -and $completeRepeatPreview.values.observations[0].score_twice -eq 14 `
            -and $completeRepeatPreview.values.observations[1].score_twice -eq 6 `
            -and $completeRepeatPreview.values.observations[0].score_quadruple -eq 28 `
            -and $completeRepeatPreview.values.observations[1].score_quadruple -eq 12 `
            -and @($completeRepeatPreview.repeatRows).Count -eq 2 `
            -and $completeRepeatPreview.repeatRows[0].requiredFields.note `
            -and -not $completeRepeatPreview.repeatRows[1].visibleFields.note `
            -and @($incompleteSameRowPreview.repeatRows[0].issues |
                Where-Object {
                    $_.fieldKey -eq "note" `
                        -and $_.rowIndex -eq 0 `
                        -and $_.message -match "Note is required"
                }).Count -eq 1 `
            -and @($incompleteSameRowPreview.repeatRows[1].issues).Count -eq 0) `
        @{
            incomplete = $incompleteSameRowPreview
            complete = $completeRepeatPreview
        }

    $rowRuleSchema = $repeatSchema |
        ConvertTo-Json -Depth 30 |
        ConvertFrom-Json
    $rowRuleSchema.rules = @(
        [ordered]@{
            key = "unsafe_row_rule"
            condition = [ordered]@{
                fieldKey = "score"
                operator = "greater-than"
                value = 5
            }
            action = "warning"
            targetFieldKey = "observations"
            message = "A row-scoped rule must not escape its repeat."
            calculation = $null
        }
    )
    $rowRulePreview = Invoke-Api `
        -Uri "$ApiBaseUrl/api/form-engine/preview" `
        -Method "POST" `
        -RequestHeaders $adminHeaders `
        -Body @{ definition = $rowRuleSchema; values = @{} }
    Add-Check `
        "Repeat children cannot be addressed by non-row-scoped form rules" `
        ($rowRulePreview.Status -eq 400 `
            -and $rowRulePreview.Content -match "unknown condition field") `
        @{
            status = $rowRulePreview.Status
            body = $rowRulePreview.Json
        }

    $unsafeSchema = New-TestSchema `
        -Key "tmp.form.unsafe.$marker" `
        -Name "Unsafe form $marker"
    $unsafeSchema.purpose = "fetch(https://unapproved.example.test)"
    $unsafePreview = Invoke-Api `
        -Uri "$ApiBaseUrl/api/form-engine/preview" `
        -Method "POST" `
        -RequestHeaders $adminHeaders `
        -Body @{ definition = $unsafeSchema; values = @{} }

    $cyclicSchema = New-TestSchema `
        -Key "tmp.form.cycle.$marker" `
        -Name "Cyclic form $marker"
    $cyclicSchema.rules[0].targetFieldKey = "pain_score"
    $cyclicPreview = Invoke-Api `
        -Uri "$ApiBaseUrl/api/form-engine/preview" `
        -Method "POST" `
        -RequestHeaders $adminHeaders `
        -Body @{ definition = $cyclicSchema; values = @{} }

    $unsupportedSchema = New-TestSchema `
        -Key "tmp.form.unsupported.$marker" `
        -Name "Unsupported form $marker"
    $unsupportedSchema.fields[0].type = "executable"
    $unsupportedPreview = Invoke-Api `
        -Uri "$ApiBaseUrl/api/form-engine/preview" `
        -Method "POST" `
        -RequestHeaders $adminHeaders `
        -Body @{ definition = $unsupportedSchema; values = @{} }

    $incompleteLocalizationSchema = (
        ConvertTo-RequestJson $schema |
            ConvertFrom-Json
    )
    $incompleteLocalizationSchema.localizations[0].fields = @(
        $incompleteLocalizationSchema.localizations[0].fields |
            Select-Object -First 2
    )
    $incompleteLocalizationPreview = Invoke-Api `
        -Uri "$ApiBaseUrl/api/form-engine/preview" `
        -Method "POST" `
        -RequestHeaders $adminHeaders `
        -Body @{
            definition = $incompleteLocalizationSchema
            values = @{}
        }

    $unknownTopLevel = Invoke-Api `
        -Uri "$ApiBaseUrl/api/form-engine/preview" `
        -Method "POST" `
        -RequestHeaders $adminHeaders `
        -Body @{
            definition = $schema
            values = @{}
            executableHook = "return true"
        }
    Add-Check `
        "Unsafe, cyclic, unsupported, and unknown form contracts are rejected" `
        ($unsafePreview.Status -eq 400 `
            -and $cyclicPreview.Status -eq 400 `
            -and $unsupportedPreview.Status -eq 400 `
            -and $unknownTopLevel.Status -eq 400) `
        @{
            unsafe = $unsafePreview.Status
            cyclic = $cyclicPreview.Status
            unsupported = $unsupportedPreview.Status
            unknownTopLevel = $unknownTopLevel.Status
        }
    Add-Check `
        "Incomplete localized clinical content is rejected" `
        ($incompleteLocalizationPreview.Status -eq 400 `
            -and $incompleteLocalizationPreview.Content -match "translate every field") `
        @{
            status = $incompleteLocalizationPreview.Status
            body = $incompleteLocalizationPreview.Json
        }

    $providerGovernance = Invoke-Api `
        -Uri "$ApiBaseUrl/api/form-engine/definitions" `
        -RequestHeaders $providerHeaders
    Add-Check `
        "Definition governance remains administrator restricted" `
        ($providerGovernance.Status -eq 403) `
        @{ status = $providerGovernance.Status }

    $createRequest = [ordered]@{
        definition = $schema
        reason = "Create focused synthetic governed form."
    }
    $created = Invoke-Json `
        -Uri "$ApiBaseUrl/api/form-engine/definitions" `
        -Method "POST" `
        -RequestHeaders $adminHeaders `
        -Body $createRequest
    $definitionId = $created.definition.definitionId
    Add-Check `
        "Definition starts as immutable revision-one draft" `
        ($created.currentRevision.revision -eq 1 `
            -and $created.currentRevision.status -eq "draft" `
            -and $created.currentRevision.version -eq 0 `
            -and $created.currentRevision.schemaHash.Length -eq 64 `
            -and @($created.currentRevision.definition.localizations).Count -eq 1 `
            -and $created.currentRevision.definition.localizations[0].locale -eq "es-US" `
            -and @($created.currentRevision.definition.localizations[0].fields).Count -eq 3 `
            -and $created.currentRevision.definition.localizations[0].rules[0].message `
                -eq "Una puntuación alta de dolor requiere atención clínica." `
            -and @($created.events).Count -eq 1) `
        $created.currentRevision

    $duplicate = Invoke-Api `
        -Uri "$ApiBaseUrl/api/form-engine/definitions" `
        -Method "POST" `
        -RequestHeaders $adminHeaders `
        -Body $createRequest
    Add-Check `
        "Stable definition keys reject duplicates" `
        ($duplicate.Status -eq 409) `
        @{ status = $duplicate.Status; body = $duplicate.Json }

    $reviewed = Move-Definition -Action "review" -Revision 1 -ExpectedVersion 0
    $staleReview = Invoke-Api `
        -Uri "$ApiBaseUrl/api/form-engine/definitions/$definitionId/review" `
        -Method "POST" `
        -RequestHeaders $adminHeaders `
        -Body @{
            revision = 1
            expectedVersion = 0
            reason = "Prove stale conflict."
            effectiveFrom = $null
            effectiveTo = $null
        }
    $approved = Move-Definition -Action "approve" -Revision 1 -ExpectedVersion 1
    $activated = Move-Definition -Action "activate" -Revision 1 -ExpectedVersion 2
    Add-Check `
        "Review, approval, activation, and stale-write controls" `
        ($reviewed.currentRevision.status -eq "in-review" `
            -and $approved.currentRevision.status -eq "approved" `
            -and $activated.currentRevision.status -eq "effective" `
            -and $activated.currentRevision.version -eq 3 `
            -and $activated.definition.effectiveRevision -eq 1 `
            -and $staleReview.Status -eq 409 `
            -and @($activated.events).Count -eq 4) `
        @{
            reviewed = $reviewed.currentRevision.version
            approved = $approved.currentRevision.version
            activated = $activated.currentRevision.version
            staleStatus = $staleReview.Status
            eventCount = @($activated.events).Count
        }

    $catalogAfterActivation = Invoke-Json `
        -Uri "$ApiBaseUrl/api/form-engine/catalog?search=$stableKey" `
        -RequestHeaders $adminHeaders
    Add-Check `
        "Only the effective definition enters the clinical catalog" `
        ($catalogAfterActivation.total -eq 1 `
            -and $catalogAfterActivation.definitions[0].definitionId -eq $definitionId) `
        $catalogAfterActivation

    $invalidCreateRequest = [ordered]@{
        definitionId = $definitionId
        revision = $null
        encounterId = 1000013
        idempotencyKey = "clinical-form-invalid-$marker"
        values = @{}
        reason = "Create intentionally incomplete draft."
    }
    $invalidDraft = Invoke-Json `
        -Uri "$ApiBaseUrl/api/form-engine/patients/MOD-PAT-0001/instances" `
        -Method "POST" `
        -RequestHeaders $adminHeaders `
        -Body $invalidCreateRequest
    $instanceIds.Add([string]$invalidDraft.instance.instanceId)
    $invalidFinalize = Invoke-Api `
        -Uri "$ApiBaseUrl/api/form-engine/instances/$($invalidDraft.instance.instanceId)/finalize" `
        -Method "POST" `
        -RequestHeaders $adminHeaders `
        -Body @{
            expectedVersion = $invalidDraft.instance.version
            reason = "Prove invalid finalization rejection."
        }
    Add-Check `
        "Incomplete drafts are retained but cannot be finalized" `
        ($invalidDraft.instance.state -eq "draft" `
            -and -not $invalidDraft.validation.valid `
            -and $invalidFinalize.Status -eq 400) `
        @{
            issueCount = @($invalidDraft.validation.issues).Count
            finalizeStatus = $invalidFinalize.Status
        }

    $createInstanceRequest = [ordered]@{
        definitionId = $definitionId
        revision = $null
        encounterId = 1000013
        idempotencyKey = "clinical-form-valid-$marker"
        values = [ordered]@{
            chief_concern = "Focused <script>alert('escaped')</script> verification"
            pain_score = 8
            disposition = "routine"
        }
        reason = "Create valid focused clinical form."
    }
    $instance = Invoke-Json `
        -Uri "$ApiBaseUrl/api/form-engine/patients/MOD-PAT-0001/instances" `
        -Method "POST" `
        -RequestHeaders $adminHeaders `
        -Body $createInstanceRequest
    $instanceIds.Add([string]$instance.instance.instanceId)
    $replay = Invoke-Json `
        -Uri "$ApiBaseUrl/api/form-engine/patients/MOD-PAT-0001/instances" `
        -Method "POST" `
        -RequestHeaders $adminHeaders `
        -Body $createInstanceRequest
    $conflictingReplayRequest = [ordered]@{
        definitionId = $definitionId
        revision = $null
        encounterId = 1000013
        idempotencyKey = "clinical-form-valid-$marker"
        values = [ordered]@{
            chief_concern = "Different payload"
            pain_score = 2
            disposition = "routine"
        }
        reason = "Create a conflicting idempotent payload."
    }
    $conflictingReplay = Invoke-Api `
        -Uri "$ApiBaseUrl/api/form-engine/patients/MOD-PAT-0001/instances" `
        -Method "POST" `
        -RequestHeaders $adminHeaders `
        -Body $conflictingReplayRequest
    Add-Check `
        "Effective revision pinning and actor-scoped idempotency" `
        ($instance.instance.definitionRevision -eq 1 `
            -and $instance.instance.instanceId -eq $replay.instance.instanceId `
            -and $conflictingReplay.Status -eq 409 `
            -and $instance.validation.valid) `
        @{
            instanceId = $instance.instance.instanceId
            revision = $instance.instance.definitionRevision
            replayId = $replay.instance.instanceId
            conflictStatus = $conflictingReplay.Status
        }

    $saved = Invoke-Json `
        -Uri "$ApiBaseUrl/api/form-engine/instances/$($instance.instance.instanceId)" `
        -Method "PUT" `
        -RequestHeaders $adminHeaders `
        -Body @{
            expectedVersion = $instance.instance.version
            values = $createInstanceRequest.values
            reason = "Confirm typed values and warning evidence."
        }
    $staleSave = Invoke-Api `
        -Uri "$ApiBaseUrl/api/form-engine/instances/$($instance.instance.instanceId)" `
        -Method "PUT" `
        -RequestHeaders $adminHeaders `
        -Body @{
            expectedVersion = $instance.instance.version
            values = $createInstanceRequest.values
            reason = "Prove stale instance conflict."
        }
    Add-Check `
        "Typed save records warnings and rejects stale updates" `
        ($saved.instance.version -eq 1 `
            -and $saved.validation.valid `
            -and @($saved.validation.issues | Where-Object {
                $_.severity -eq "warning"
            }).Count -eq 1 `
            -and $staleSave.Status -eq 409) `
        @{
            version = $saved.instance.version
            warnings = @($saved.validation.issues).Count
            staleStatus = $staleSave.Status
        }

    $finalized = Move-Instance `
        -InstanceId $instance.instance.instanceId `
        -Action "finalize" `
        -ExpectedVersion $saved.instance.version `
        -Headers $adminHeaders
    $authorSigned = Move-Instance `
        -InstanceId $instance.instance.instanceId `
        -Action "sign" `
        -ExpectedVersion $finalized.instance.version `
        -Headers $adminHeaders
    $sameAuthorCosign = Invoke-Api `
        -Uri "$ApiBaseUrl/api/form-engine/instances/$($instance.instance.instanceId)/cosign" `
        -Method "POST" `
        -RequestHeaders $adminHeaders `
        -Body @{
            expectedVersion = $authorSigned.instance.version
            reason = "Prove distinct co-signer enforcement."
        }
    $providerCosignBeforeGrant = Invoke-Api `
        -Uri "$ApiBaseUrl/api/form-engine/instances/$($instance.instance.instanceId)/cosign" `
        -Method "POST" `
        -RequestHeaders $providerHeaders `
        -Body @{
            expectedVersion = $authorSigned.instance.version
            reason = "Prove encounter-write authorization."
        }
    Add-Check `
        "Author-first signature and distinct co-signer policy" `
        ($finalized.instance.state -eq "ready-for-signature" `
            -and $authorSigned.instance.state -eq "awaiting-co-sign" `
            -and @($authorSigned.signatures).Count -eq 1 `
            -and $authorSigned.signatures[0].signer -eq "admin" `
            -and $sameAuthorCosign.Status -eq 400 `
            -and $providerCosignBeforeGrant.Status -eq 403) `
        @{
            authorState = $authorSigned.instance.state
            sameAuthorStatus = $sameAuthorCosign.Status
            unprivilegedProviderStatus = $providerCosignBeforeGrant.Status
        }

    $directory = Invoke-Json `
        -Uri "$ApiBaseUrl/api/administration/directory" `
        -RequestHeaders $adminHeaders
    $providerPhysicianMembershipPresent = @(
        $directory.accessControl.userMemberships |
            Where-Object {
                $_.userValue -eq "gold-provider-01" `
                    -and $_.groupValue -eq "doc"
            }
    ).Count -gt 0
    if (-not $providerPhysicianMembershipPresent) {
        Invoke-Json `
            -Uri "$ApiBaseUrl/api/administration/access-control/user-memberships" `
            -Method "PUT" `
            -RequestHeaders $adminHeaders `
            -Body @{
                userValue = "gold-provider-01"
                groupValue = "doc"
            } | Out-Null
        $providerPhysicianMembershipAdded = $true
    }

    $coSigned = Move-Instance `
        -InstanceId $instance.instance.instanceId `
        -Action "cosign" `
        -ExpectedVersion $authorSigned.instance.version `
        -Headers $providerHeaders
    Add-Check `
        "Two active local accounts complete co-signature" `
        ($coSigned.instance.state -eq "signed" `
            -and @($coSigned.signatures).Count -eq 2 `
            -and @($coSigned.signatures | Where-Object {
                $_.role -eq "co-signer" `
                    -and $_.signer -eq "gold-provider-01" `
                    -and $_.contentHash.Length -eq 64
            }).Count -eq 1) `
        @{
            state = $coSigned.instance.state
            signatures = $coSigned.signatures
        }

    $amendment = Invoke-Json `
        -Uri "$ApiBaseUrl/api/form-engine/instances/$($instance.instance.instanceId)/amend" `
        -Method "POST" `
        -RequestHeaders $adminHeaders `
        -Body @{
            expectedVersion = $coSigned.instance.version
            reason = "Correct through a signed successor."
            idempotencyKey = "clinical-form-amend-$marker"
        }
    $instanceIds.Add([string]$amendment.instance.instanceId)
    $pendingPredecessor = Invoke-Json `
        -Uri "$ApiBaseUrl/api/form-engine/instances/$($instance.instance.instanceId)" `
        -RequestHeaders $adminHeaders
    $amendmentReplay = Invoke-Json `
        -Uri "$ApiBaseUrl/api/form-engine/instances/$($instance.instance.instanceId)/amend" `
        -Method "POST" `
        -RequestHeaders $adminHeaders `
        -Body @{
            expectedVersion = $coSigned.instance.version
            reason = "Correct through a signed successor."
            idempotencyKey = "clinical-form-amend-$marker"
        }
    Add-Check `
        "Amendment preserves signed predecessor until correction is signed" `
        ($amendment.instance.state -eq "draft" `
            -and $amendment.instance.predecessorInstanceId -eq $instance.instance.instanceId `
            -and $pendingPredecessor.instance.state -eq "signed" `
            -and $pendingPredecessor.instance.successorInstanceId -eq $amendment.instance.instanceId `
            -and $amendmentReplay.instance.instanceId -eq $amendment.instance.instanceId) `
        @{
            successorId = $amendment.instance.instanceId
            predecessorState = $pendingPredecessor.instance.state
            replayId = $amendmentReplay.instance.instanceId
        }

    $corrected = Invoke-Json `
        -Uri "$ApiBaseUrl/api/form-engine/instances/$($amendment.instance.instanceId)" `
        -Method "PUT" `
        -RequestHeaders $adminHeaders `
        -Body @{
            expectedVersion = $amendment.instance.version
            values = @{
                chief_concern = "Corrected focused verification"
                pain_score = 3
                disposition = "routine"
            }
            reason = "Correct the synthetic observation."
        }
    $correctionFinalized = Move-Instance `
        -InstanceId $amendment.instance.instanceId `
        -Action "finalize" `
        -ExpectedVersion $corrected.instance.version `
        -Headers $adminHeaders
    $correctionSigned = Move-Instance `
        -InstanceId $amendment.instance.instanceId `
        -Action "sign" `
        -ExpectedVersion $correctionFinalized.instance.version `
        -Headers $adminHeaders
    $correctionCoSigned = Move-Instance `
        -InstanceId $amendment.instance.instanceId `
        -Action "cosign" `
        -ExpectedVersion $correctionSigned.instance.version `
        -Headers $providerHeaders
    $completedPredecessor = Invoke-Json `
        -Uri "$ApiBaseUrl/api/form-engine/instances/$($instance.instance.instanceId)" `
        -RequestHeaders $adminHeaders
    Add-Check `
        "Signed correction atomically marks predecessor amended" `
        ($correctionCoSigned.instance.state -eq "signed" `
            -and $completedPredecessor.instance.state -eq "amended" `
            -and @($completedPredecessor.events | Where-Object {
                $_.action -eq "amended-by-successor"
            }).Count -eq 1 `
            -and @($completedPredecessor.events | Where-Object {
                $_.snapshotHash.Length -ne 64
            }).Count -eq 0) `
        @{
            successorState = $correctionCoSigned.instance.state
            predecessorState = $completedPredecessor.instance.state
            predecessorVersion = $completedPredecessor.instance.version
            eventCount = @($completedPredecessor.events).Count
        }

    $revisionTwoSchema = New-TestSchema `
        -Key $stableKey `
        -Name "Focused form $marker revision two" `
        -IncludeFollowUp $true `
        -IncludeLocalization
    $revisionTwo = Invoke-Json `
        -Uri "$ApiBaseUrl/api/form-engine/definitions/$definitionId/revisions" `
        -Method "POST" `
        -RequestHeaders $adminHeaders `
        -Body @{
            definition = $revisionTwoSchema
            expectedLatestRevision = 1
            reason = "Create governed successor definition."
        }
    $revisionTwoReviewed = Move-Definition `
        -Action "review" `
        -Revision 2 `
        -ExpectedVersion 0
    $revisionTwoApproved = Move-Definition `
        -Action "approve" `
        -Revision 2 `
        -ExpectedVersion 1
    $revisionTwoActivated = Move-Definition `
        -Action "activate" `
        -Revision 2 `
        -ExpectedVersion 2
    $priorRevision = @(
        $revisionTwoActivated.revisions |
            Where-Object { $_.revision -eq 1 }
    ) | Select-Object -First 1
    Add-Check `
        "Successor definition supersedes but retains prior revision" `
        ($revisionTwo.currentRevision.revision -eq 2 `
            -and $revisionTwoReviewed.currentRevision.status -eq "in-review" `
            -and $revisionTwoApproved.currentRevision.status -eq "approved" `
            -and $revisionTwoActivated.definition.effectiveRevision -eq 2 `
            -and $priorRevision.status -eq "superseded" `
            -and $priorRevision.schemaHash.Length -eq 64) `
        @{
            effectiveRevision = $revisionTwoActivated.definition.effectiveRevision
            priorStatus = $priorRevision.status
            revisionCount = @($revisionTwoActivated.revisions).Count
        }

    $revisionTwoInstance = Invoke-Json `
        -Uri "$ApiBaseUrl/api/form-engine/patients/MOD-PAT-0001/instances" `
        -Method "POST" `
        -RequestHeaders $adminHeaders `
        -Body @{
            definitionId = $definitionId
            revision = $null
            encounterId = 1000013
            idempotencyKey = "clinical-form-revision-two-$marker"
            values = @{
                chief_concern = "Revision two verification"
                pain_score = 2
                disposition = "routine"
                follow_up = $false
            }
            reason = "Pin the newly effective definition."
        }
    $instanceIds.Add([string]$revisionTwoInstance.instance.instanceId)
    $historicRender = Invoke-Json `
        -Uri "$ApiBaseUrl/api/form-engine/instances/$($instance.instance.instanceId)/render" `
        -RequestHeaders $adminHeaders
    $historicExport = Invoke-Api `
        -Uri "$ApiBaseUrl/api/form-engine/instances/$($instance.instance.instanceId)/export" `
        -RequestHeaders $adminHeaders
    $historicLocalizedExport = Invoke-Api `
        -Uri "$ApiBaseUrl/api/form-engine/instances/$($instance.instance.instanceId)/export?locale=es-US" `
        -RequestHeaders $adminHeaders
    $historicStructuredExport = Invoke-Json `
        -Uri "$ApiBaseUrl/api/form-engine/instances/$($instance.instance.instanceId)/structured-export" `
        -RequestHeaders $adminHeaders
    $historicFieldDictionary = Invoke-Json `
        -Uri "$ApiBaseUrl/api/form-engine/instances/$($instance.instance.instanceId)/field-dictionary" `
        -RequestHeaders $adminHeaders
    Add-Check `
        "Historical render, portable structured export, and field dictionary remain revision-pinned" `
        ($revisionTwoInstance.instance.definitionRevision -eq 2 `
            -and $historicRender.instance.definitionRevision -eq 1 `
            -and $historicRender.definition.name -eq "Focused form $marker" `
            -and $historicRender.rendererVersion -eq "local-clinical-form-renderer-v1" `
            -and $historicRender.contentHash.Length -eq 64 `
            -and $historicExport.Status -eq 200 `
            -and $historicExport.ContentType -eq "text/html" `
            -and $historicExport.Content -notmatch "<script>alert" `
            -and $historicExport.Content -match "&lt;script&gt;" `
            -and $historicLocalizedExport.Status -eq 200 `
            -and $historicLocalizedExport.Content -match '<html lang="es-US">' `
            -and $historicLocalizedExport.Content -match "Formulario focalizado" `
            -and $historicLocalizedExport.Content -match "Motivo principal" `
            -and $historicStructuredExport.exportFormat -eq "application/vnd.legacy-ehr.clinical-form+json;version=1" `
            -and $historicStructuredExport.instance.definitionRevision -eq 1 `
            -and $historicStructuredExport.schemaHash.Length -eq 64 `
            -and $historicStructuredExport.fieldDictionary.revision -eq 1 `
            -and $historicStructuredExport.fieldDictionary.fields.Count -eq 3 `
            -and @($historicStructuredExport.fieldDictionary.fields | Where-Object {
                $_.fieldKey -eq "chief_concern" `
                    -and $_.reportColumn -eq "clinical_form.$stableKey.r1.chief_concern"
            }).Count -eq 1 `
            -and @($historicStructuredExport.fieldDictionary.fields | Where-Object {
                $_.fieldKey -eq "follow_up"
            }).Count -eq 0 `
            -and $historicFieldDictionary.schemaHash -eq $historicStructuredExport.schemaHash `
            -and $historicFieldDictionary.fields.Count -eq $historicStructuredExport.fieldDictionary.fields.Count) `
        @{
            newRevision = $revisionTwoInstance.instance.definitionRevision
            historicRevision = $historicRender.instance.definitionRevision
            renderer = $historicRender.rendererVersion
            exportStatus = $historicExport.Status
            exportContentType = $historicExport.ContentType
            localizedExportStatus = $historicLocalizedExport.Status
            structuredExportFormat = $historicStructuredExport.exportFormat
            fieldCount = $historicStructuredExport.fieldDictionary.fields.Count
            schemaHash = $historicStructuredExport.schemaHash
        }
}
catch {
    Add-Check "Unhandled clinical-form engine test error" $false $_.Exception.Message
}
finally {
    if ($providerPhysicianMembershipAdded -and $null -ne $adminHeaders) {
        $membershipCleanup = Invoke-Api `
            -Uri "$ApiBaseUrl/api/administration/access-control/user-memberships/gold-provider-01/doc" `
            -Method "DELETE" `
            -RequestHeaders $adminHeaders
        Add-Check `
            "Temporary co-signer permission cleanup" `
            ($membershipCleanup.Status -eq 200) `
            @{ status = $membershipCleanup.Status }
    }

    if ($null -ne $definitionId -and $null -ne $adminHeaders) {
        $fixtureCleanup = Invoke-Api `
            -Uri "$ApiBaseUrl/api/form-engine/definitions/$definitionId/test-fixture" `
            -Method "DELETE" `
            -RequestHeaders $adminHeaders
        $definitionAfterCleanup = Invoke-Api `
            -Uri "$ApiBaseUrl/api/form-engine/definitions/$definitionId" `
            -RequestHeaders $adminHeaders
        $instancesAfterCleanup = Invoke-Api `
            -Uri "$ApiBaseUrl/api/form-engine/patients/MOD-PAT-0001/instances?encounterId=1000013" `
            -RequestHeaders $adminHeaders
        $remainingIds = if ($null -ne $instancesAfterCleanup.Json) {
            @(
                $instancesAfterCleanup.Json.instances |
                    Where-Object { $instanceIds.Contains([string]$_.instanceId) }
            )
        }
        else {
            @("instance-list-unavailable")
        }
        Add-Check `
            "Synthetic definition, instances, signatures, and events cleanup" `
            ($fixtureCleanup.Status -eq 204 `
                -and $definitionAfterCleanup.Status -eq 404 `
                -and $instancesAfterCleanup.Status -eq 200 `
                -and @($remainingIds).Count -eq 0) `
            @{
                cleanupStatus = $fixtureCleanup.Status
                definitionStatus = $definitionAfterCleanup.Status
                instanceListStatus = $instancesAfterCleanup.Status
                remainingInstanceIds = $remainingIds
            }
    }

    $result = [ordered]@{
        status = $status
        generatedAtUtc = (Get-Date).ToUniversalTime().ToString("O")
        apiBaseUrl = $ApiBaseUrl
        definitionStableKey = $stableKey
        checks = $checks
    }
    $result |
        ConvertTo-Json -Depth 30 |
        Set-Content -LiteralPath $resultPath -Encoding utf8
    $result | ConvertTo-Json -Depth 30
}

if ($status -ne "passed") {
    exit 1
}
