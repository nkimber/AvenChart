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
        [bool]$ReadOnly = $false
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
    }
}

function New-TestSchema {
    param(
        [string]$Key,
        [string]$Name,
        [bool]$IncludeFollowUp = $false
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

    return [ordered]@{
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
        rules = @(
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
    }
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
        $policy.revision -eq "local-clinical-form-v1" `
        -and $policy.rendererVersion -eq "local-clinical-form-renderer-v1" `
        -and $policy.signaturePolicyRevision -eq "local-clinical-signature-v1" `
        -and (($actualFieldTypes -join "|") -eq ($expectedFieldTypes -join "|")) `
        -and @($policy.supportedRuleActions).Count -eq 5 `
        -and @($policy.forbiddenCapabilities).Count -ge 7 `
        -and -not $policy.arbitraryScriptsAllowed `
        -and -not $policy.rawHtmlAllowed `
        -and -not $policy.externalFetchAllowed `
        -and -not $policy.previewPersistsClinicalData `
        -and -not $policy.productionSignatureStandardApproved
    Add-Check "Constrained form runtime policy" $policyPassed $policy

    $catalog = Invoke-Json `
        -Uri "$ApiBaseUrl/api/form-engine/catalog?page=1&pageSize=20" `
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

    $marker = [Guid]::NewGuid().ToString("N").Substring(0, 12)
    $stableKey = "tmp.form.$marker"
    $schema = New-TestSchema `
        -Key $stableKey `
        -Name "Focused form $marker"

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
        -IncludeFollowUp $true
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
