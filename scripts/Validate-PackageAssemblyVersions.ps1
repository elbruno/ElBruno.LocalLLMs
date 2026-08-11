<#
.SYNOPSIS
    Validates that each packed assembly version matches its NuGet package version.

.DESCRIPTION
    Opens every .nupkg in a directory, reads the nuspec package version, extracts
    each lib/**/*.dll to a repo-local scratch folder, and verifies the assembly
    version matches the package version (normalized to four parts).
#>

[CmdletBinding(SupportsShouldProcess = $false)]
param(
    [string]$PackageDirectory = 'artifacts'
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.IO.Compression.FileSystem

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

function Get-ExpectedAssemblyVersion {
    param([Parameter(Mandatory = $true)][string]$PackageVersion)

    $versionCore = $PackageVersion.Split('-', 2)[0]
    $segments = $versionCore.Split('.')

    switch ($segments.Count) {
        2 { $versionCore = "$versionCore.0.0" }
        3 { $versionCore = "$versionCore.0" }
        4 { }
        default { throw "Unsupported package version format '$PackageVersion'." }
    }

    return [Version]$versionCore
}

function Copy-ZipEntryToFile {
    param(
        [Parameter(Mandatory = $true)]$Entry,
        [Parameter(Mandatory = $true)][string]$DestinationPath
    )

    $destinationDir = Split-Path $DestinationPath -Parent
    if (-not (Test-Path -LiteralPath $destinationDir)) {
        $null = New-Item -ItemType Directory -Path $destinationDir -Force
    }

    $entryStream = $Entry.Open()
    $fileStream = [System.IO.File]::Create($DestinationPath)

    try {
        $entryStream.CopyTo($fileStream)
    }
    finally {
        $fileStream.Dispose()
        $entryStream.Dispose()
    }
}

$repoRoot = Get-RepoRoot
if (-not [System.IO.Path]::IsPathRooted($PackageDirectory)) {
    $PackageDirectory = Join-Path $repoRoot $PackageDirectory
}

$PackageDirectory = (Resolve-Path -LiteralPath $PackageDirectory).Path
$packages = @(Get-ChildItem -LiteralPath $PackageDirectory -Filter '*.nupkg' | Sort-Object Name)

if ($packages.Count -eq 0) {
    throw "No .nupkg files found under '$PackageDirectory'."
}

$validationRoot = Join-Path $PackageDirectory '_assembly-version-validation'
if (Test-Path -LiteralPath $validationRoot) {
    Remove-Item -LiteralPath $validationRoot -Recurse -Force
}

$null = New-Item -ItemType Directory -Path $validationRoot -Force
$failures = [System.Collections.Generic.List[string]]::new()

try {
    foreach ($package in $packages) {
        Write-Host "Validating $($package.Name)..." -ForegroundColor Cyan

        $packageScratchDir = Join-Path $validationRoot $package.BaseName
        $null = New-Item -ItemType Directory -Path $packageScratchDir -Force

        $archive = [System.IO.Compression.ZipFile]::OpenRead($package.FullName)
        try {
            $nuspecEntry = $archive.Entries | Where-Object { $_.FullName.EndsWith('.nuspec', [System.StringComparison]::OrdinalIgnoreCase) } | Select-Object -First 1
            if (-not $nuspecEntry) {
                throw "Package '$($package.Name)' does not contain a .nuspec."
            }

            $reader = [System.IO.StreamReader]::new($nuspecEntry.Open())
            try {
                $nuspec = [xml]$reader.ReadToEnd()
            }
            finally {
                $reader.Dispose()
            }

            $packageId = [string]$nuspec.package.metadata.id
            $packageVersion = [string]$nuspec.package.metadata.version
            $expectedAssemblyVersion = Get-ExpectedAssemblyVersion -PackageVersion $packageVersion

            $assemblyEntries = $archive.Entries | Where-Object {
                $_.FullName.StartsWith('lib/', [System.StringComparison]::OrdinalIgnoreCase) -and
                $_.FullName.EndsWith('.dll', [System.StringComparison]::OrdinalIgnoreCase)
            }

            if (-not $assemblyEntries) {
                throw "Package '$($package.Name)' does not contain any lib assemblies."
            }

            foreach ($assemblyEntry in $assemblyEntries) {
                $destinationPath = Join-Path $packageScratchDir ($assemblyEntry.FullName -replace '/', [System.IO.Path]::DirectorySeparatorChar)
                Copy-ZipEntryToFile -Entry $assemblyEntry -DestinationPath $destinationPath

                $assemblyVersion = [System.Reflection.AssemblyName]::GetAssemblyName($destinationPath).Version
                if ($assemblyVersion -ne $expectedAssemblyVersion) {
                    $failures.Add("$($package.Name) [$packageId] => $($assemblyEntry.FullName) has assembly version $assemblyVersion, expected $expectedAssemblyVersion.")
                    continue
                }

                Write-Host "  OK $($assemblyEntry.FullName) => $assemblyVersion" -ForegroundColor Green
            }
        }
        finally {
            $archive.Dispose()
        }
    }

    if ($failures.Count -gt 0) {
        Write-Host ''
        foreach ($failure in $failures) {
            Write-Host "❌ $failure" -ForegroundColor Red
        }

        throw "Assembly version validation failed for $($failures.Count) packed assembly file(s)."
    }

    Write-Host ''
    Write-Host "Validated $($packages.Count) package(s); all packed assembly versions match their package versions." -ForegroundColor Green
}
finally {
    if (Test-Path -LiteralPath $validationRoot) {
        Remove-Item -LiteralPath $validationRoot -Recurse -Force
    }
}