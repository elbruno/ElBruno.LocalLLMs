<#
.SYNOPSIS
    Deletes downloaded ONNX models from the local cache.

.DESCRIPTION
    Removes model files cached by ElBruno.LocalLLMs from the default cache directory
    (%LOCALAPPDATA%\ElBruno\LocalLLMs\models).
    Use -ModelId to delete a specific model, or -All to delete everything.

.PARAMETER ModelId
    The ID of a specific model to delete (e.g., "phi-3.5-mini-instruct").

.PARAMETER All
    Delete all cached models.

.PARAMETER WhatIf
    Show what would be deleted without actually deleting.

.EXAMPLE
    .\delete-models.ps1 -All
    Deletes all cached models.

.EXAMPLE
    .\delete-models.ps1 -ModelId "phi-3.5-mini-instruct"
    Deletes only the Phi-3.5 mini model.

.EXAMPLE
    .\delete-models.ps1 -All -WhatIf
    Shows what would be deleted without removing anything.
#>

param(
    [string]$ModelId,
    [switch]$All,
    [switch]$WhatIf
)

$cacheDir = [System.IO.Path]::Combine($env:LOCALAPPDATA, "ElBruno", "LocalLLMs", "models")

if (-not (Test-Path $cacheDir)) {
    Write-Host "No models cache found at: $cacheDir" -ForegroundColor Yellow
    exit 0
}

if (-not $All -and -not $ModelId) {
    Write-Host ""
    Write-Host "ElBruno.LocalLLMs — Model Cache Cleanup" -ForegroundColor Cyan
    Write-Host "========================================" -ForegroundColor Cyan
    Write-Host ""
    Write-Host "Cache directory: $cacheDir" -ForegroundColor Gray
    Write-Host ""

    $models = Get-ChildItem -Path $cacheDir -Directory -ErrorAction SilentlyContinue
    if ($models.Count -eq 0) {
        Write-Host "No cached models found." -ForegroundColor Yellow
        exit 0
    }

    Write-Host "Cached models:" -ForegroundColor White
    foreach ($model in $models) {
        $size = (Get-ChildItem -Path $model.FullName -Recurse -File | Measure-Object -Property Length -Sum).Sum
        $sizeStr = if ($size -gt 1GB) { "{0:N1} GB" -f ($size / 1GB) }
                   elseif ($size -gt 1MB) { "{0:N1} MB" -f ($size / 1MB) }
                   else { "{0:N0} KB" -f ($size / 1KB) }
        Write-Host "  - $($model.Name) ($sizeStr)" -ForegroundColor Green
    }

    Write-Host ""
    Write-Host "Usage:" -ForegroundColor White
    Write-Host "  .\delete-models.ps1 -All                              # Delete all models"
    Write-Host "  .\delete-models.ps1 -ModelId 'phi-3.5-mini-instruct'  # Delete specific model"
    Write-Host "  .\delete-models.ps1 -All -WhatIf                      # Preview deletions"
    Write-Host ""
    exit 0
}

if ($All) {
    $targets = Get-ChildItem -Path $cacheDir -Directory -ErrorAction SilentlyContinue
    if ($targets.Count -eq 0) {
        Write-Host "No cached models found." -ForegroundColor Yellow
        exit 0
    }

    foreach ($dir in $targets) {
        $size = (Get-ChildItem -Path $dir.FullName -Recurse -File | Measure-Object -Property Length -Sum).Sum
        $sizeStr = if ($size -gt 1GB) { "{0:N1} GB" -f ($size / 1GB) }
                   elseif ($size -gt 1MB) { "{0:N1} MB" -f ($size / 1MB) }
                   else { "{0:N0} KB" -f ($size / 1KB) }

        if ($WhatIf) {
            Write-Host "[WhatIf] Would delete: $($dir.Name) ($sizeStr)" -ForegroundColor Yellow
        } else {
            Write-Host "Deleting: $($dir.Name) ($sizeStr)..." -ForegroundColor Red
            Remove-Item -Path $dir.FullName -Recurse -Force
        }
    }

    if (-not $WhatIf) {
        Write-Host ""
        Write-Host "All cached models deleted." -ForegroundColor Green
    }
}
elseif ($ModelId) {
    $modelDir = Join-Path $cacheDir $ModelId
    if (-not (Test-Path $modelDir)) {
        Write-Host "Model '$ModelId' not found in cache." -ForegroundColor Yellow
        Write-Host "Cache directory: $cacheDir" -ForegroundColor Gray

        $available = Get-ChildItem -Path $cacheDir -Directory -ErrorAction SilentlyContinue
        if ($available.Count -gt 0) {
            Write-Host ""
            Write-Host "Available models:" -ForegroundColor White
            foreach ($m in $available) {
                Write-Host "  - $($m.Name)" -ForegroundColor Green
            }
        }
        exit 1
    }

    $size = (Get-ChildItem -Path $modelDir -Recurse -File | Measure-Object -Property Length -Sum).Sum
    $sizeStr = if ($size -gt 1GB) { "{0:N1} GB" -f ($size / 1GB) }
               elseif ($size -gt 1MB) { "{0:N1} MB" -f ($size / 1MB) }
               else { "{0:N0} KB" -f ($size / 1KB) }

    if ($WhatIf) {
        Write-Host "[WhatIf] Would delete: $ModelId ($sizeStr)" -ForegroundColor Yellow
    } else {
        Write-Host "Deleting: $ModelId ($sizeStr)..." -ForegroundColor Red
        Remove-Item -Path $modelDir -Recurse -Force
        Write-Host "Model '$ModelId' deleted." -ForegroundColor Green
    }
}
