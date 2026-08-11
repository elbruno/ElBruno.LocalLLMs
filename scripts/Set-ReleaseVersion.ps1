<#
.SYNOPSIS
    Bumps the repository's package version in every file that must stay in sync.

.DESCRIPTION
    Updates the following in one pass, so releases never drift the way 0.20.11
    did (Directory.Build.props left at an old version, issue #49):

    1. Directory.Build.props   -> <PublishedSiblingPackageVersion>
    2. README.md               -> "## What's New" section (prepends a new bullet,
                                    keeps exactly the last 5 entries)
    3. docs/CHANGELOG.md       -> inserts a new "## [Version] - Date" section
                                    right after "## [Unreleased]"

    Run Validate-ReleaseVersion.ps1 afterwards (and before publishing) to confirm
    every file agrees on the new version.

.PARAMETER Version
    The new package version, e.g. "0.21.0". Must be a valid SemVer core
    (major.minor.patch, optional prerelease suffix).

.PARAMETER Highlight
    The README "What's New" bullet text for this release, without the leading
    "- " marker. Example:
    '🚀 **`v0.21.0`** — Republishes with a clean version bump after a NuGet indexing issue on 0.20.12.'

    If it does not already mention "v$Version", the script fails fast so the
    bullet can't silently point at the wrong release.

.PARAMETER ChangelogBody
    Optional changelog body lines (an array of strings, e.g. "### Fixed", "- ...").
    If omitted, a placeholder "### Changed" / "- TODO" block is inserted so the
    section is never left completely empty.

.PARAMETER Date
    Changelog date stamp (yyyy-MM-dd). Defaults to today.

.EXAMPLE
    ./scripts/Set-ReleaseVersion.ps1 -Version 0.21.0 `
        -Highlight '🚀 **`v0.21.0`** — Clean republish after v0.20.12 NuGet indexing issue.' `
        -ChangelogBody @('### Fixed', '- Republished as 0.21.0 after 0.20.12 failed to propagate on NuGet.org.')
#>

[CmdletBinding(SupportsShouldProcess = $true)]
param(
    [Parameter(Mandatory = $true)]
    [ValidatePattern('^\d+\.\d+\.\d+(-[0-9A-Za-z\.-]+)?$')]
    [string]$Version,

    [Parameter(Mandatory = $true)]
    [string]$Highlight,

    [string[]]$ChangelogBody,

    [string]$Date = (Get-Date -Format 'yyyy-MM-dd')
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

if (-not ($Highlight -match [regex]::Escape("v$Version"))) {
    throw "Highlight text must mention 'v$Version' so the What's New bullet matches the release. Got: '$Highlight'"
}

if (-not $Highlight.TrimStart().StartsWith('-')) {
    $bulletLine = "- $Highlight"
} else {
    $bulletLine = $Highlight
}

# --- 1. Directory.Build.props ------------------------------------------------

Write-Host "Updating $propsPath..." -ForegroundColor Cyan
$propsContent = Get-Content -LiteralPath $propsPath -Raw
$propsPattern = '<PublishedSiblingPackageVersion>[^<]*</PublishedSiblingPackageVersion>'

if ($propsContent -notmatch $propsPattern) {
    throw "Could not find <PublishedSiblingPackageVersion> in $propsPath."
}

$oldVersionMatch = [regex]::Match($propsContent, '<PublishedSiblingPackageVersion>([^<]*)</PublishedSiblingPackageVersion>')
$oldVersion = $oldVersionMatch.Groups[1].Value

$propsContent = [regex]::Replace(
    $propsContent,
    $propsPattern,
    "<PublishedSiblingPackageVersion>$Version</PublishedSiblingPackageVersion>"
)

if ($PSCmdlet.ShouldProcess($propsPath, "Set PublishedSiblingPackageVersion $oldVersion -> $Version")) {
    Set-Content -LiteralPath $propsPath -Value $propsContent -NoNewline
}

Write-Host "  $oldVersion -> $Version" -ForegroundColor Green

# --- 2. README.md What's New --------------------------------------------------

Write-Host "Updating $readmePath..." -ForegroundColor Cyan
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
    throw "Could not find a '## What's New' section in $readmePath."
}

$sectionLines = $readmeLines[($startIdx + 1)..($endIdx - 1)]
$bulletIdxs = @()
for ($i = 0; $i -lt $sectionLines.Count; $i++) {
    if ($sectionLines[$i] -match '^- ') {
        $bulletIdxs += $i
    }
}

if ($bulletIdxs.Count -eq 0) {
    throw "Could not find any '- ' bullet entries under '## What's New' in $readmePath."
}

$preambleLines = if ($bulletIdxs[0] -gt 0) { $sectionLines[0..($bulletIdxs[0] - 1)] } else { @() }
$lastBulletIdx = $bulletIdxs[$bulletIdxs.Count - 1]
$trailingLines = if ($lastBulletIdx -lt $sectionLines.Count - 1) { $sectionLines[($lastBulletIdx + 1)..($sectionLines.Count - 1)] } else { @() }
$existingBullets = @($bulletIdxs | ForEach-Object { $sectionLines[$_] })
$newBullets = @($bulletLine) + $existingBullets
if ($newBullets.Count -gt 5) {
    $droppedBullets = $newBullets[5..($newBullets.Count - 1)]
    $newBullets = $newBullets[0..4]
    foreach ($dropped in $droppedBullets) {
        Write-Host "  Dropping oldest What's New entry to keep exactly 5: $dropped" -ForegroundColor Yellow
    }
}

$newSectionLines = @($preambleLines) + @($newBullets) + @($trailingLines)
$newReadmeLines = @($readmeLines[0..$startIdx]) + $newSectionLines + @($readmeLines[$endIdx..($readmeLines.Count - 1)])

if ($PSCmdlet.ShouldProcess($readmePath, "Prepend What's New bullet for v$Version")) {
    Set-Content -LiteralPath $readmePath -Value $newReadmeLines
}

Write-Host "  Prepended bullet for v$Version; What's New now has $($newBullets.Count) entries." -ForegroundColor Green

# --- 3. docs/CHANGELOG.md -----------------------------------------------------

Write-Host "Updating $changelogPath..." -ForegroundColor Cyan
$changelogLines = Get-Content -LiteralPath $changelogPath

$unreleasedIdx = -1
for ($i = 0; $i -lt $changelogLines.Count; $i++) {
    if ($changelogLines[$i] -match '^## \[Unreleased\]') {
        $unreleasedIdx = $i
        break
    }
}

if ($unreleasedIdx -lt 0) {
    throw "Could not find a '## [Unreleased]' section in $changelogPath."
}

if ($changelogLines -join "`n" -match [regex]::Escape("## [$Version]")) {
    Write-Host "  A '## [$Version]' section already exists; leaving $changelogPath untouched." -ForegroundColor Yellow
} else {
    if (-not $ChangelogBody -or $ChangelogBody.Count -eq 0) {
        $ChangelogBody = @('### Changed', '- TODO: describe the changes in this release.')
    }

    $newEntry = @('', "## [$Version] - $Date") + $ChangelogBody
    $insertAt = $unreleasedIdx + 1
    $newChangelogLines = @($changelogLines[0..$unreleasedIdx]) + $newEntry + @($changelogLines[$insertAt..($changelogLines.Count - 1)])

    if ($PSCmdlet.ShouldProcess($changelogPath, "Insert [$Version] - $Date section")) {
        Set-Content -LiteralPath $changelogPath -Value $newChangelogLines
    }

    Write-Host "  Inserted '## [$Version] - $Date' section." -ForegroundColor Green
}

Write-Host ''
Write-Host "Version bump to $Version complete. Run scripts/Validate-ReleaseVersion.ps1 -Version $Version next." -ForegroundColor Cyan
