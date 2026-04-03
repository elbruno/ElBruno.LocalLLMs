# PowerShell wrapper for Gemma 4 ONNX conversion
# Usage:
#   .\convert_gemma4.ps1 -ModelSize e2b -OutputDir .\models\gemma4-e2b
#   .\convert_gemma4.ps1 -ModelSize 26b -OutputDir .\models\gemma4-26b -Quantize int8

param(
    [Parameter(Mandatory=$true)]
    [ValidateSet("e2b", "e4b", "26b", "31b")]
    [string]$ModelSize,
    
    [Parameter(Mandatory=$true)]
    [string]$OutputDir,
    
    [ValidateSet("int4", "int8", "fp16")]
    [string]$Quantize = "int4",
    
    [switch]$SkipValidation
)

# Color output helpers
function Write-Success { Write-Host $args -ForegroundColor Green }
function Write-Info { Write-Host $args -ForegroundColor Cyan }
function Write-Warning { Write-Host $args -ForegroundColor Yellow }
function Write-Error { Write-Host $args -ForegroundColor Red }

Write-Info "=" * 70
Write-Info "🤖 Gemma 4 ONNX Conversion (PowerShell Wrapper)"
Write-Info "=" * 70
Write-Host ""

# Check Python installation
Write-Info "🔍 Checking Python installation..."
try {
    $pythonVersion = python --version 2>&1
    Write-Success "  ✓ Python found: $pythonVersion"
} catch {
    Write-Error "❌ Python not found!"
    Write-Host "Please install Python 3.10 or later from https://www.python.org/"
    exit 1
}

# Check pip
try {
    $pipVersion = pip --version 2>&1
    Write-Success "  ✓ pip found"
} catch {
    Write-Error "❌ pip not found!"
    Write-Host "Please ensure pip is installed with Python."
    exit 1
}

Write-Host ""

# Check if dependencies are installed
Write-Info "🔍 Checking Python dependencies..."
$missingPackages = @()

$packages = @("onnxruntime-genai", "transformers", "torch", "huggingface-hub")
foreach ($package in $packages) {
    $check = pip show $package 2>&1 | Out-String
    if ($check -match "Name: $package") {
        Write-Success "  ✓ $package"
    } else {
        Write-Warning "  ✗ $package (missing)"
        $missingPackages += $package
    }
}

Write-Host ""

# Install missing packages
if ($missingPackages.Count -gt 0) {
    Write-Warning "⚠️  Missing packages detected!"
    Write-Host "The following packages need to be installed:"
    $missingPackages | ForEach-Object { Write-Host "  - $_" }
    Write-Host ""
    
    $install = Read-Host "Install missing packages now? [Y/n]"
    if ($install -eq "" -or $install -eq "y" -or $install -eq "Y") {
        Write-Info "📦 Installing dependencies..."
        
        # Install from requirements.txt if it exists, otherwise install individually
        $requirementsPath = Join-Path $PSScriptRoot "requirements.txt"
        if (Test-Path $requirementsPath) {
            pip install -r $requirementsPath
        } else {
            pip install onnxruntime-genai huggingface-hub transformers torch
        }
        
        if ($LASTEXITCODE -ne 0) {
            Write-Error "❌ Failed to install dependencies!"
            exit 1
        }
        Write-Success "✅ Dependencies installed successfully!"
        Write-Host ""
    } else {
        Write-Error "❌ Cannot proceed without dependencies. Exiting."
        exit 1
    }
}

# Run the conversion script
Write-Info "🚀 Starting Gemma 4 conversion..."
Write-Host ""

$scriptPath = Join-Path $PSScriptRoot "convert_gemma4.py"

if (-not (Test-Path $scriptPath)) {
    Write-Error "❌ Conversion script not found: $scriptPath"
    exit 1
}

# Build command arguments
$args = @(
    $scriptPath,
    "--model-size", $ModelSize,
    "--output-dir", $OutputDir,
    "--quantize", $Quantize
)

if ($SkipValidation) {
    $args += "--skip-validation"
}

# Execute the Python script
python $args

if ($LASTEXITCODE -ne 0) {
    Write-Error "❌ Conversion failed!"
    exit $LASTEXITCODE
}

Write-Success "`n✅ Conversion completed successfully!"
