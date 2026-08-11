<#
.SYNOPSIS
    Validates that a release version is consistently reflected across the repository.

.DESCRIPTION
    Companion to Set-ReleaseVersion.ps1. Confirms, before tagging/publishing a
    release, that:

    1. Directory.Build.props' <PublishedSiblingPackageVersion> equals -Version.
    2. README.md's "## What's New" section has exactly 5 bullet entries and the
       first one mentions "v<Version>".
    3. docs/CHANGELOG.md has a "## [<Version>]" section.
    4. (Optional) if -PackageDirectory is supplied and exists, every packed
       assembly version matches the package version by delegating to
       Validate-PackageAssemblyVersions.ps1.

    Exits non-zero and prints every failure found (does not stop at the first one)
    so all problems can be fixed in one pass.

.PARAMETER Version
    The expected release version, e.g. "0.21.0".

.PARAMETER PackageDirectory
    Optional path to a directory containing packed .nupkg files (e.g. ./artifacts).
    When present, also runs the packed assembly-version validation.

.EXAMPLE
    ./scripts/Validate-ReleaseVersion.ps1 -Version 0.21.0

.EXAMPLE
    ./scripts/Validate-ReleaseVersion.ps1 -Version 0.21.0 -PackageDirectory ./artifacts
#>

[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [ValidatePattern('^\d+\.\d+\.\d+(-[0-9A-Za-z\.-]+)?$')]
    [string]$Version,

    [string]$PackageDirectory
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Get-RepoRoot {
    $searchDir = $PSScriptRoot

    while ($searchDir -and $searchDir -ne [System.IO.Path]::GetPathRoot($searchDir)) {
        if (Test-Path -LiteralPath (Join-Path $searchDir 'ElBruno.LocalLLMs.slnx')) {
            return $searchDir
        }

        $searchDir = Split-Path $searchDir -Parent
    }

    throw 'Could not find ElBruno.LocalLLMs.slnx in any parent directory.'
}

$repoRoot = Get-RepoRoot
$propsPath = Join-Path $repoRoot 'Directory.Build.props'
$readmePath = Join-Path $repoRoot 'README.md'
$changelogPath = Join-Path $repoRoot 'docs\CHANGELOG.md'

$failures = [System.Collections.Generic.List[string]]::new()

# --- 1. Directory.Build.props ------------------------------------------------

Write-Host "Checking $propsPath..." -ForegroundColor Cyan
$propsContent = Get-Content -LiteralPath $propsPath -Raw
$propsMatch = [regex]::Match($propsContent, '<PublishedSiblingPackageVersion>([^<]*)</PublishedSiblingPackageVersion>')

if (-not $propsMatch.Success) {
    $failures.Add("Directory.Build.props does not contain <PublishedSiblingPackageVersion>.")
} elseif ($propsMatch.Groups[1].Value -ne $Version) {
    $failures.Add("Directory.Build.props PublishedSiblingPackageVersion is '$($propsMatch.Groups[1].Value)', expected '$Version'.")
} else {
    Write-Host "  OK PublishedSiblingPackageVersion = $Version" -ForegroundColor Green
}

# --- 2. README.md What's New --------------------------------------------------

Write-Host "Checking $readmePath..." -ForegroundColor Cyan
$readmeLines = Get-Content -LiteralPath $readmePath

$startIdx = -1
$endIdx = $readmeLines.Count

for ($i = 0; $i -lt $readmeLines.Count; $i++) {
    if ($readmeLines[$i] -match "^## What's New") {
        $startIdx = $i
        continue
    }

    if ($startIdx -ge 0 -and $i -gt $startIdx -and $readmeLines[$i] -match '^## ') {
        $endIdx = $i
        break
    }
}

if ($startIdx -lt 0) {
    $failures.Add("README.md does not contain a '## What's New' section.")
} else {
    $sectionLines = $readmeLines[($startIdx + 1)..($endIdx - 1)]
    $bullets = @($sectionLines | Where-Object { $_ -match '^- ' })

    if ($bullets.Count -ne 5) {
        $failures.Add("README.md '## What's New' has $($bullets.Count) bullet(s), expected exactly 5.")
    } else {
        Write-Host "  OK What's New has 5 entries" -ForegroundColor Green
    }

    if ($bullets.Count -eq 0 -or -not ($bullets[0] -match [regex]::Escape("v$Version"))) {
        $failures.Add("README.md '## What's New' first bullet does not mention 'v$Version'. First bullet: '$($bullets | Select-Object -First 1)'")
    } else {
        Write-Host "  OK First bullet mentions v$Version" -ForegroundColor Green
    }
}

# --- 3. docs/CHANGELOG.md -----------------------------------------------------

Write-Host "Checking $changelogPath..." -ForegroundColor Cyan
$changelogContent = Get-Content -LiteralPath $changelogPath -Raw

if ($changelogContent -notmatch [regex]::Escape("## [$Version]")) {
    $failures.Add("docs/CHANGELOG.md does not contain a '## [$Version]' section.")
} else {
    Write-Host "  OK '## [$Version]' section present" -ForegroundColor Green
}

# --- 4. Optional packed assembly-version check --------------------------------

if ($PackageDirectory) {
    $resolvedPackageDir = $PackageDirectory
    if (-not [System.IO.Path]::IsPathRooted($resolvedPackageDir)) {
        $resolvedPackageDir = Join-Path $repoRoot $resolvedPackageDir
    }

    if (-not (Test-Path -LiteralPath $resolvedPackageDir)) {
        $failures.Add("PackageDirectory '$resolvedPackageDir' does not exist.")
    } else {
        Write-Host "Checking packed assemblies under $resolvedPackageDir..." -ForegroundColor Cyan
        $validateScript = Join-Path $repoRoot 'scripts\Validate-PackageAssemblyVersions.ps1'

        try {
            & $validateScript -PackageDirectory $resolvedPackageDir
            Write-Host "  OK packed assembly versions match $Version" -ForegroundColor Green
        } catch {
            $failures.Add("Packed assembly-version validation failed: $($_.Exception.Message)")
        }
    }
}

# --- Summary -------------------------------------------------------------------

Write-Host ''
if ($failures.Count -gt 0) {
    foreach ($failure in $failures) {
        Write-Host "❌ $failure" -ForegroundColor Red
    }

    throw "Release version validation failed for '$Version' with $($failures.Count) issue(s)."
}

Write-Host "✅ Release version $Version is consistent across Directory.Build.props, README.md, and docs/CHANGELOG.md." -ForegroundColor Green
