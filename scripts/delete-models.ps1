<#
.SYNOPSIS
    Lists or deletes downloaded ONNX models from the local cache.

.DESCRIPTION
    Removes model files cached by ElBruno.LocalLLMs from the default cache directory
    (%LOCALAPPDATA%\ElBruno\LocalLLMs\models). Without parameters, it lists cached
    models with size and last updated time.

    Use -ModelId to delete a specific model, or -All to delete everything.
    Supports built-in PowerShell -WhatIf and -Confirm for safe preview/confirmation.

.PARAMETER ModelId
    The ID of a specific model to delete (e.g., "phi-3.5-mini-instruct").

.PARAMETER All
    Delete all cached models.

.PARAMETER List
    Explicitly list cached models. This is also the default behavior when no delete
    parameters are specified.

.PARAMETER PassThru
    Emit model cache entries as objects, useful for scripting and reporting.

.EXAMPLE
    .\delete-models.ps1
    Lists cached models.

.EXAMPLE
    .\delete-models.ps1 -ModelId "phi-3.5-mini-instruct"
    Deletes only the Phi-3.5 mini model.

.EXAMPLE
    .\delete-models.ps1 -All -WhatIf
    Shows what would be deleted without removing anything.

.EXAMPLE
    .\delete-models.ps1 -PassThru | Sort-Object SizeBytes -Descending
    Returns cache entries as objects, sorted by size.
#>

[CmdletBinding(DefaultParameterSetName = 'List', SupportsShouldProcess = $true, ConfirmImpact = 'High')]
param(
    [Parameter(ParameterSetName = 'Single', Mandatory = $true)]
    [ValidateNotNullOrEmpty()]
    [string]$ModelId,

    [Parameter(ParameterSetName = 'All', Mandatory = $true)]
    [switch]$All,

    [Parameter(ParameterSetName = 'List')]
    [switch]$List,

    [switch]$PassThru
)

$cacheDir = [System.IO.Path]::Combine($env:LOCALAPPDATA, "ElBruno", "LocalLLMs", "models")

function Convert-SizeToDisplay {
    param([long]$SizeBytes)

    if ($SizeBytes -ge 1GB) { return "{0:N1} GB" -f ($SizeBytes / 1GB) }
    if ($SizeBytes -ge 1MB) { return "{0:N1} MB" -f ($SizeBytes / 1MB) }
    return "{0:N0} KB" -f ($SizeBytes / 1KB)
}

function Get-CachedModelEntries {
    param([Parameter(Mandatory = $true)][string]$RootPath)

    $directories = Get-ChildItem -Path $RootPath -Directory -ErrorAction SilentlyContinue
    foreach ($dir in $directories) {
        $sizeBytes = (Get-ChildItem -Path $dir.FullName -Recurse -File -ErrorAction SilentlyContinue |
                Measure-Object -Property Length -Sum).Sum
        if ($null -eq $sizeBytes) { $sizeBytes = 0L }

        [pscustomobject]@{
            Name         = $dir.Name
            FullPath     = $dir.FullName
            SizeBytes    = [long]$sizeBytes
            Size         = Convert-SizeToDisplay -SizeBytes ([long]$sizeBytes)
            LastWriteUtc = $dir.LastWriteTimeUtc
        }
    }
}

if (-not (Test-Path -Path $cacheDir)) {
    Write-Host "No models cache found at: $cacheDir" -ForegroundColor Yellow
    exit 0
}

$models = @(Get-CachedModelEntries -RootPath $cacheDir)

if ($PSCmdlet.ParameterSetName -eq 'List') {
    Write-Host ""
    Write-Host "ElBruno.LocalLLMs — Model Cache Cleanup" -ForegroundColor Cyan
    Write-Host "========================================" -ForegroundColor Cyan
    Write-Host ""
    Write-Host "Cache directory: $cacheDir" -ForegroundColor Gray
    Write-Host ""

    if ($models.Count -eq 0) {
        Write-Host "No cached models found." -ForegroundColor Yellow
        exit 0
    }

    $sortedModels = $models | Sort-Object -Property SizeBytes -Descending
    $totalSize = ($sortedModels | Measure-Object -Property SizeBytes -Sum).Sum
    Write-Host ("Cached models: {0} (Total: {1})" -f $models.Count, (Convert-SizeToDisplay -SizeBytes ([long]$totalSize))) -ForegroundColor White
    $sortedModels |
        Select-Object Name, Size, LastWriteUtc |
        Format-Table -AutoSize |
        Out-Host

    Write-Host ""
    Write-Host "Usage:" -ForegroundColor White
    Write-Host "  .\delete-models.ps1                                     # List models"
    Write-Host "  .\delete-models.ps1 -All -WhatIf                       # Preview delete all"
    Write-Host "  .\delete-models.ps1 -ModelId 'phi-3.5-mini-instruct'   # Delete one model"
    Write-Host "  .\delete-models.ps1 -PassThru | ConvertTo-Json -Depth 3 # Script-friendly output"
    Write-Host ""

    if ($PassThru) {
        $sortedModels
    }

    exit 0
}

if ($All) {
    if ($models.Count -eq 0) {
        Write-Host "No cached models found." -ForegroundColor Yellow
        exit 0
    }

    $deletedCount = 0
    foreach ($entry in $models) {
        if ($PSCmdlet.ShouldProcess($entry.FullPath, "Delete cached model '$($entry.Name)' ($($entry.Size))")) {
            Write-Host "Deleting: $($entry.Name) ($($entry.Size))..." -ForegroundColor Red
            Remove-Item -Path $entry.FullPath -Recurse -Force
            $deletedCount++
        }
    }

    if ($deletedCount -gt 0) {
        Write-Host ""
        Write-Host "Deleted $deletedCount cached model folder(s)." -ForegroundColor Green
    }
}
elseif ($ModelId) {
    $modelDir = Join-Path $cacheDir $ModelId
    if (-not (Test-Path -Path $modelDir)) {
        Write-Host "Model '$ModelId' not found in cache." -ForegroundColor Yellow
        Write-Host "Cache directory: $cacheDir" -ForegroundColor Gray

        if ($models.Count -gt 0) {
            Write-Host ""
            Write-Host "Available models:" -ForegroundColor White
            foreach ($m in ($models | Sort-Object Name)) {
                Write-Host "  - $($m.Name)" -ForegroundColor Green
            }
        }
        exit 1
    }

    $target = $models | Where-Object { $_.Name -eq $ModelId } | Select-Object -First 1
    if ($null -eq $target) {
        $target = [pscustomobject]@{ Name = $ModelId; FullPath = $modelDir; Size = "unknown" }
    }

    if ($PSCmdlet.ShouldProcess($target.FullPath, "Delete cached model '$($target.Name)' ($($target.Size))")) {
        Write-Host "Deleting: $($target.Name) ($($target.Size))..." -ForegroundColor Red
        Remove-Item -Path $modelDir -Recurse -Force
        Write-Host "Model '$ModelId' deleted." -ForegroundColor Green
    }
}
