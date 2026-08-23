# SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
[CmdletBinding()]
param(
    [ValidateSet('Inspect', 'Update', 'Check', 'PortableCheck')]
    [string]$Mode = 'Update'
)

$repositoryRoot = Split-Path -Parent $PSScriptRoot
$graphify = Join-Path $repositoryRoot 'tools\graphify\node_modules\.bin\graphify.cmd'

if (-not (Test-Path -LiteralPath $graphify)) {
    throw "Graphify is not installed. Run 'npm install --prefix tools/graphify' from the repository root."
}

Push-Location $repositoryRoot
try {
    switch ($Mode) {
        'Inspect' { & $graphify scope inspect $repositoryRoot --scope committed }
        'Check' { & $graphify check-update $repositoryRoot }
        'PortableCheck' {
            # Graphify keeps local manifest and worktree metadata alongside the
            # durable graph. Validate only the artifacts intended for version
            # control, because the manifest deliberately records local paths.
            $temporaryRoot = [System.IO.Path]::GetFullPath([System.IO.Path]::GetTempPath())
            $temporaryDirectory = Join-Path $temporaryRoot ("avenchart-graphify-portable-" + [guid]::NewGuid().ToString('N'))
            $resolvedTemporaryDirectory = [System.IO.Path]::GetFullPath($temporaryDirectory)

            if (-not $resolvedTemporaryDirectory.StartsWith($temporaryRoot, [System.StringComparison]::OrdinalIgnoreCase)) {
                throw 'Refusing to create a portability-check directory outside the system temporary directory.'
            }

            try {
                New-Item -ItemType Directory -Path $resolvedTemporaryDirectory -ErrorAction Stop | Out-Null

                foreach ($artifact in @('graph.json', 'GRAPH_REPORT.md')) {
                    $source = Join-Path $repositoryRoot ".graphify\\$artifact"
                    if (-not (Test-Path -LiteralPath $source)) {
                        throw "Required Graphify artifact is missing: $source"
                    }

                    Copy-Item -LiteralPath $source -Destination $resolvedTemporaryDirectory -ErrorAction Stop
                }

                & $graphify portable-check $resolvedTemporaryDirectory
                if ($LASTEXITCODE -ne 0) {
                    throw 'Graphify portability validation failed for the durable artifacts.'
                }
            }
            finally {
                if (Test-Path -LiteralPath $resolvedTemporaryDirectory) {
                    Remove-Item -LiteralPath $resolvedTemporaryDirectory -Recurse -Force
                }
            }
        }
        'Update' {
            # Keep the committed graph deterministic and local: no provider backend,
            # node descriptions, or community labels are generated in this command.
            & $graphify update $repositoryRoot --scope committed --no-description --no-label
        }
    }

    if ($LASTEXITCODE -ne 0) {
        exit $LASTEXITCODE
    }
}
finally {
    Pop-Location
}
