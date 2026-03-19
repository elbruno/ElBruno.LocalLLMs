<#
.SYNOPSIS
    Manages downloaded model cache for ElBruno.LocalLLMs.

.DESCRIPTION
    Lists downloaded models, reports sizes, shows storage locations, and deletes one or all models.
    Default cache root matches the library behavior: %LOCALAPPDATA%\ElBruno\LocalLLMs\models.

.PARAMETER List
    Lists downloaded models in the cache location(s). This is the default action.

.PARAMETER Locations
    Shows model storage location(s) and whether they currently exist.

.PARAMETER Report
    Shows model list plus totals (count, files, size).

.PARAMETER Delete
    Deletes one model by name, id fragment, or path fragment.

.PARAMETER DeleteAll
    Deletes all models found in the cache location(s).

.PARAMETER Model
    Model selector used with -Delete. Supports name, id fragment, or path fragment matching.

.PARAMETER CacheDirectory
    One or more cache roots to inspect/manage. If omitted, defaults to:
    %LOCALAPPDATA%\ElBruno\LocalLLMs\models

.PARAMETER DryRun
    Shows what would be deleted without deleting anything.

.PARAMETER Force
    Skips interactive safety confirmation for delete operations.

.PARAMETER CleanupEmptyFolders
    After deletion, removes empty folders under each cache root.

.EXAMPLE
    .\manage-models.ps1
    Lists downloaded models in the default cache.

.EXAMPLE
    .\manage-models.ps1 -Locations
    Shows model cache location(s).

.EXAMPLE
    .\manage-models.ps1 -Report
    Lists models and displays totals.

.EXAMPLE
    .\manage-models.ps1 -Delete -Model phi-3.5 -DryRun
    Previews deletion for a model matching "phi-3.5".

.EXAMPLE
    .\manage-models.ps1 -DeleteAll -Force
    Deletes all models without interactive confirmation.

.EXAMPLE
    .\manage-models.ps1 -Delete -Model qwen -CleanupEmptyFolders
    Deletes matching model and removes empty folders afterwards.
#>

[CmdletBinding(SupportsShouldProcess = $true, ConfirmImpact = 'High', DefaultParameterSetName = 'List')]
param(
    [Parameter(ParameterSetName = 'List')]
    [switch]$List,

    [Parameter(Mandatory = $true, ParameterSetName = 'Locations')]
    [switch]$Locations,

    [Parameter(Mandatory = $true, ParameterSetName = 'Report')]
    [switch]$Report,

    [Parameter(Mandatory = $true, ParameterSetName = 'DeleteOne')]
    [switch]$Delete,

    [Parameter(Mandatory = $true, ParameterSetName = 'DeleteAll')]
    [switch]$DeleteAll,

    [Parameter(Mandatory = $true, ParameterSetName = 'DeleteOne')]
    [ValidateNotNullOrEmpty()]
    [string]$Model,

    [Parameter()]
    [string[]]$CacheDirectory,

    [Parameter()]
    [switch]$DryRun,

    [Parameter()]
    [switch]$Force,

    [Parameter()]
    [switch]$CleanupEmptyFolders
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Get-DefaultCacheDirectory {
    $localAppData = [Environment]::GetFolderPath([Environment+SpecialFolder]::LocalApplicationData)
    return [System.IO.Path]::Combine($localAppData, 'ElBruno', 'LocalLLMs', 'models')
}

function Resolve-CacheRoots {
    param([string[]]$InputPaths)

    if ($InputPaths -and $InputPaths.Count -gt 0) {
        $resolved = foreach ($path in $InputPaths) {
            if ([string]::IsNullOrWhiteSpace($path)) {
                continue
            }

            $expanded = [Environment]::ExpandEnvironmentVariables($path)
            try {
                $resolvedPath = $ExecutionContext.SessionState.Path.GetUnresolvedProviderPathFromPSPath($expanded)
            }
            catch {
                $resolvedPath = [System.IO.Path]::GetFullPath($expanded)
            }

            [PSCustomObject]@{
                Path = $resolvedPath
                Source = 'UserProvided'
            }
        }

        return $resolved | Group-Object -Property Path | ForEach-Object { $_.Group[0] }
    }

    return [PSCustomObject]@{
        Path = Get-DefaultCacheDirectory
        Source = 'Default'
    }
}

function Get-StorageLocations {
    param([PSCustomObject[]]$Roots)

    foreach ($root in $Roots) {
        [PSCustomObject]@{
            Path = $root.Path
            Exists = Test-Path -LiteralPath $root.Path -PathType Container
            Source = $root.Source
        }
    }
}

function Get-DirectorySizeBytes {
    param([Parameter(Mandatory = $true)][string]$Path)

    $sum = (Get-ChildItem -LiteralPath $Path -Recurse -File -ErrorAction SilentlyContinue | Measure-Object -Property Length -Sum).Sum
    if ($null -eq $sum) {
        return [Int64]0
    }

    return [Int64]$sum
}

function Format-ByteSize {
    param([Parameter(Mandatory = $true)][Int64]$Bytes)

    if ($Bytes -ge 1TB) {
        return ('{0:N2} TB' -f ($Bytes / 1TB))
    }

    if ($Bytes -ge 1GB) {
        return ('{0:N2} GB' -f ($Bytes / 1GB))
    }

    if ($Bytes -ge 1MB) {
        return ('{0:N2} MB' -f ($Bytes / 1MB))
    }

    if ($Bytes -ge 1KB) {
        return ('{0:N2} KB' -f ($Bytes / 1KB))
    }

    return ('{0} B' -f $Bytes)
}

function Get-ModelRecords {
    param([PSCustomObject[]]$Roots)

    $records = New-Object System.Collections.Generic.List[object]

    foreach ($root in $Roots) {
        if (-not (Test-Path -LiteralPath $root.Path -PathType Container)) {
            continue
        }

        $dirs = Get-ChildItem -LiteralPath $root.Path -Directory -ErrorAction SilentlyContinue
        foreach ($dir in $dirs) {
            $sizeBytes = Get-DirectorySizeBytes -Path $dir.FullName
            $fileCount = (Get-ChildItem -LiteralPath $dir.FullName -Recurse -File -ErrorAction SilentlyContinue | Measure-Object).Count

            $records.Add([PSCustomObject]@{
                Name = $dir.Name
                Size = Format-ByteSize -Bytes $sizeBytes
                SizeBytes = $sizeBytes
                Files = $fileCount
                CacheRoot = $root.Path
                FullPath = $dir.FullName
            })
        }
    }

    return $records
}

function Show-UsageExamples {
    Write-Host ''
    Write-Host 'Usage examples:' -ForegroundColor Cyan
    Write-Host '  .\manage-models.ps1' -ForegroundColor Gray
    Write-Host '  .\manage-models.ps1 -Locations' -ForegroundColor Gray
    Write-Host '  .\manage-models.ps1 -Report' -ForegroundColor Gray
    Write-Host '  .\manage-models.ps1 -Delete -Model phi-3.5 -DryRun' -ForegroundColor Gray
    Write-Host '  .\manage-models.ps1 -Delete -Model phi-3.5 -WhatIf' -ForegroundColor Gray
    Write-Host '  .\manage-models.ps1 -Delete -Model phi-3.5 -Force' -ForegroundColor Gray
    Write-Host '  .\manage-models.ps1 -DeleteAll -DryRun' -ForegroundColor Gray
    Write-Host '  .\manage-models.ps1 -DeleteAll -WhatIf' -ForegroundColor Gray
    Write-Host '  .\manage-models.ps1 -DeleteAll -Force -CleanupEmptyFolders' -ForegroundColor Gray
    Write-Host ''
}

function Confirm-Deletion {
    param(
        [string]$Prompt,
        [switch]$ForceDelete
    )

    if ($ForceDelete) {
        return $true
    }

    if ($DryRun) {
        return $true
    }

    # Native WhatIf should always remain non-interactive.
    if ($WhatIfPreference) {
        return $true
    }

    $answer = Read-Host "$Prompt Type DELETE to continue"
    return $answer -ceq 'DELETE'
}

function Remove-EmptyFolders {
    param([PSCustomObject[]]$Roots)

    $removed = 0

    foreach ($root in $Roots) {
        if (-not (Test-Path -LiteralPath $root.Path -PathType Container)) {
            continue
        }

        $emptyDirs = Get-ChildItem -LiteralPath $root.Path -Directory -Recurse -ErrorAction SilentlyContinue |
            Sort-Object { $_.FullName.Length } -Descending |
            Where-Object {
                (Get-ChildItem -LiteralPath $_.FullName -Force -ErrorAction SilentlyContinue | Measure-Object).Count -eq 0
            }

        foreach ($emptyDir in $emptyDirs) {
            if ($PSCmdlet.ShouldProcess($emptyDir.FullName, 'Remove empty folder')) {
                Remove-Item -LiteralPath $emptyDir.FullName -Force -ErrorAction SilentlyContinue
                $removed++
            }
        }
    }

    if ($removed -gt 0) {
        Write-Host "Removed $removed empty folder(s)." -ForegroundColor Green
    }
    else {
        Write-Host 'No empty folders to clean.' -ForegroundColor DarkGray
    }
}

$roots = Resolve-CacheRoots -InputPaths $CacheDirectory
$locationsInfo = Get-StorageLocations -Roots $roots
$models = Get-ModelRecords -Roots $roots

if ($DryRun) {
    $WhatIfPreference = $true
}

switch ($PSCmdlet.ParameterSetName) {
    'Locations' {
        Write-Host ''
        Write-Host 'Model storage location(s)' -ForegroundColor Cyan
        $locationsInfo | Sort-Object Path | Format-Table Path, Exists, Source -AutoSize
        Show-UsageExamples
        break
    }

    'Report' {
        Write-Host ''
        Write-Host 'Downloaded models report' -ForegroundColor Cyan

        if ($models.Count -eq 0) {
            Write-Host 'No downloaded models found.' -ForegroundColor Yellow
            $locationsInfo | Sort-Object Path | Format-Table Path, Exists, Source -AutoSize
            Show-UsageExamples
            break
        }

        $models |
            Sort-Object Name |
            Select-Object Name, Size, Files, CacheRoot, FullPath |
            Format-Table -AutoSize

        $totalBytes = ($models | Measure-Object -Property SizeBytes -Sum).Sum
        $totalFiles = ($models | Measure-Object -Property Files -Sum).Sum
        $totalModels = $models.Count

        Write-Host ''
        Write-Host ('Totals: {0} model(s), {1} file(s), {2}' -f $totalModels, $totalFiles, (Format-ByteSize -Bytes $totalBytes)) -ForegroundColor Green
        Show-UsageExamples
        break
    }

    'DeleteOne' {
        $matches = @($models | Where-Object {
            $_.Name -like "*$Model*" -or $_.FullPath -like "*$Model*"
        })

        if ($matches.Count -eq 0) {
            Write-Host "No model match found for '$Model'." -ForegroundColor Yellow
            Write-Host 'Use -Report to inspect available models.' -ForegroundColor DarkGray
            exit 1
        }

        if ($matches.Count -gt 1) {
            Write-Host "Ambiguous model selector '$Model'. Multiple matches found:" -ForegroundColor Yellow
            $matches | Select-Object Name, Size, CacheRoot, FullPath | Format-Table -AutoSize
            Write-Host 'Use a more specific value, or pass the full directory name.' -ForegroundColor DarkGray
            exit 1
        }

        $target = $matches[0]

        if (-not (Confirm-Deletion -Prompt "Delete model '$($target.Name)'?" -ForceDelete:$Force)) {
            Write-Host 'Deletion cancelled.' -ForegroundColor Yellow
            exit 0
        }

        if ($PSCmdlet.ShouldProcess($target.FullPath, 'Delete model')) {
            Remove-Item -LiteralPath $target.FullPath -Recurse -Force
            Write-Host "Deleted model: $($target.Name) ($($target.Size))" -ForegroundColor Green
        }

        if ($CleanupEmptyFolders) {
            Remove-EmptyFolders -Roots $roots
        }

        break
    }

    'DeleteAll' {
        if ($models.Count -eq 0) {
            Write-Host 'No downloaded models found.' -ForegroundColor Yellow
            exit 0
        }

        Write-Host ''
        Write-Host 'Models selected for deletion:' -ForegroundColor Cyan
        $models | Select-Object Name, Size, CacheRoot, FullPath | Format-Table -AutoSize

        if (-not (Confirm-Deletion -Prompt 'Delete ALL listed models?' -ForceDelete:$Force)) {
            Write-Host 'Delete-all cancelled.' -ForegroundColor Yellow
            exit 0
        }

        foreach ($target in $models) {
            if ($PSCmdlet.ShouldProcess($target.FullPath, 'Delete model')) {
                Remove-Item -LiteralPath $target.FullPath -Recurse -Force
                Write-Host "Deleted model: $($target.Name) ($($target.Size))" -ForegroundColor Green
            }
        }

        if ($CleanupEmptyFolders) {
            Remove-EmptyFolders -Roots $roots
        }

        break
    }

    default {
        Write-Host ''
        Write-Host 'Downloaded models' -ForegroundColor Cyan

        if ($models.Count -eq 0) {
            Write-Host 'No downloaded models found.' -ForegroundColor Yellow
            $locationsInfo | Sort-Object Path | Format-Table Path, Exists, Source -AutoSize
            Show-UsageExamples
            break
        }

        $models |
            Sort-Object Name |
            Select-Object Name, Size, Files, CacheRoot, FullPath |
            Format-Table -AutoSize

        $totalBytes = ($models | Measure-Object -Property SizeBytes -Sum).Sum
        Write-Host ''
        Write-Host ('Total size: {0}' -f (Format-ByteSize -Bytes $totalBytes)) -ForegroundColor Green
        Show-UsageExamples
        break
    }
}
