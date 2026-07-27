<#
.SYNOPSIS
    Runs build, unit tests, and integration tests for ElBruno.LocalLLMs.

.DESCRIPTION
    Orchestrates a full or partial test run against the ElBruno.LocalLLMs solution.
    Steps: (1) dotnet build, (2) unit tests, (3) integration tests.
    Each step can be skipped independently via switches.
    After integration tests, the markdown report written by TestRunReporter is located
    and its path is printed to the console.

    Exit codes:
        0  - All requested steps passed
        1  - Build failed
        2  - Unit tests failed
        3  - Integration tests failed
        99 - Unexpected / unhandled error

.PARAMETER SkipBuild
    Skip the dotnet build step.

.PARAMETER NoBuild
    Alias for -SkipBuild (mirrors the common dotnet CLI convention).

.PARAMETER SkipUnitTests
    Skip the unit test project run.

.PARAMETER SkipIntegrationTests
    Skip the integration test project run (useful for fast CI pre-commit checks).

.PARAMETER Framework
    Target framework passed to dotnet build and dotnet test. Defaults to 'net8.0'.

.PARAMETER HfToken
    HuggingFace token for private model repositories. Sets the HF_TOKEN environment variable
    for the duration of the integration test run.

.PARAMETER Filter
    xUnit --filter expression applied to integration tests only (e.g. "FullyQualifiedName~LifecycleTests").
    Has no effect when -SkipIntegrationTests is specified.

.EXAMPLE
    .\run-tests.ps1
    Full run: build, unit tests, integration tests.

.EXAMPLE
    .\run-tests.ps1 -SkipIntegrationTests
    Build and unit tests only — fast CI pre-commit check.

.EXAMPLE
    .\run-tests.ps1 -SkipUnitTests -Filter "FullyQualifiedName~LifecycleTests"
    Build then run only matching integration tests.

.EXAMPLE
    .\run-tests.ps1 -HfToken "hf_xxxx"
    Full run with a HuggingFace token for private model repos.

.EXAMPLE
    .\run-tests.ps1 -SkipBuild
    Skip build, run all tests (assumes already built).

.NOTES
    Scheduling examples
    -------------------
    # Windows Task Scheduler (daily at 2 AM):
    # Action:    powershell.exe
    # Arguments: -NonInteractive -ExecutionPolicy Bypass -File "C:\src\ElBruno.LocalLLMs\scripts\run-tests.ps1" -SkipBuild

    # Run with HuggingFace token for private repos:
    # .\run-tests.ps1 -HfToken "hf_xxxx"

    # Run only unit tests (fast, for CI pre-commit):
    # .\run-tests.ps1 -SkipIntegrationTests

    # Run only integration tests, filtering to lifecycle tests:
    # .\run-tests.ps1 -SkipUnitTests -Filter "FullyQualifiedName~LifecycleTests"
#>

[CmdletBinding(SupportsShouldProcess = $false)]
param(
    [switch]$SkipBuild,
    [switch]$NoBuild,
    [switch]$SkipUnitTests,
    [switch]$SkipIntegrationTests,
    [string]$Framework = 'net8.0',
    [string]$HfToken,
    [string]$Filter
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

# ---------------------------------------------------------------------------
# Helpers
# ---------------------------------------------------------------------------

function Write-Banner {
    param([string]$Message, [string]$Color = 'Cyan')
    Write-Host ''
    Write-Host ('=' * 70) -ForegroundColor $Color
    Write-Host "  $Message" -ForegroundColor $Color
    Write-Host ('=' * 70) -ForegroundColor $Color
    Write-Host ''
}

function Write-Step {
    param([string]$Message)
    Write-Host "[$(Get-Date -Format 'HH:mm:ss')]  $Message" -ForegroundColor White
}

function Write-Success {
    param([string]$Message)
    Write-Host "[$(Get-Date -Format 'HH:mm:ss')]  $Message" -ForegroundColor Green
}

function Write-Failure {
    param([string]$Message)
    Write-Host "[$(Get-Date -Format 'HH:mm:ss')]  ERROR: $Message" -ForegroundColor Red
}

function Write-Info {
    param([string]$Message)
    Write-Host "  $Message" -ForegroundColor DarkGray
}

function Get-ElapsedSeconds {
    param([datetime]$Start)
    return [Math]::Round(((Get-Date) - $Start).TotalSeconds, 1)
}

# ---------------------------------------------------------------------------
# Locate repo root (walk up from $PSScriptRoot looking for the .slnx file)
# ---------------------------------------------------------------------------

$repoRoot = $null
$searchDir = $PSScriptRoot

while ($searchDir -and $searchDir -ne [System.IO.Path]::GetPathRoot($searchDir)) {
    if (Test-Path -LiteralPath (Join-Path $searchDir 'ElBruno.LocalLLMs.slnx')) {
        $repoRoot = $searchDir
        break
    }
    $searchDir = Split-Path $searchDir -Parent
}

if (-not $repoRoot) {
    Write-Host 'ERROR: Could not find ElBruno.LocalLLMs.slnx in any parent directory.' -ForegroundColor Red
    exit 99
}

$solutionFile = Join-Path $repoRoot 'ElBruno.LocalLLMs.slnx'
$unitTestProj = Join-Path $repoRoot 'src\tests\ElBruno.LocalLLMs.Tests\ElBruno.LocalLLMs.Tests.csproj'
$integrationTestProj = Join-Path $repoRoot 'src\tests\ElBruno.LocalLLMs.IntegrationTests\ElBruno.LocalLLMs.IntegrationTests.csproj'

# ---------------------------------------------------------------------------
# Banner
# ---------------------------------------------------------------------------

$scriptStart = Get-Date

Write-Banner -Message "run-tests.ps1  |  $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')  |  $repoRoot"
Write-Info "Solution : $solutionFile"
Write-Info "Framework: $Framework"
Write-Info "SkipBuild: $($SkipBuild -or $NoBuild)  |  SkipUnitTests: $SkipUnitTests  |  SkipIntegrationTests: $SkipIntegrationTests"
Write-Host ''

# ---------------------------------------------------------------------------
# Step 1: Build
# ---------------------------------------------------------------------------

$shouldSkipBuild = $SkipBuild -or $NoBuild

if ($shouldSkipBuild) {
    Write-Step 'Build step skipped (-SkipBuild / -NoBuild).'
}
else {
    $buildStart = Get-Date
    Write-Step 'Building solution...'
    Write-Info "dotnet build $solutionFile"
    Write-Host ''

    try {
        & dotnet build $solutionFile
        if ($LASTEXITCODE -ne 0) {
            throw "dotnet build exited with code $LASTEXITCODE"
        }
    }
    catch {
        Write-Failure "Build failed: $_"
        exit 1
    }

    Write-Host ''
    Write-Success "Build succeeded in $(Get-ElapsedSeconds $buildStart)s."
}

# ---------------------------------------------------------------------------
# Step 2: Unit tests
# ---------------------------------------------------------------------------

if ($SkipUnitTests) {
    Write-Step 'Unit tests skipped (-SkipUnitTests).'
}
else {
    $unitStart = Get-Date
    Write-Step 'Running unit tests...'
    Write-Info "dotnet test $unitTestProj --framework $Framework --no-build --logger console;verbosity=minimal"
    Write-Host ''

    try {
        & dotnet test $unitTestProj `
            --framework $Framework `
            --no-build `
            --logger 'console;verbosity=minimal'

        if ($LASTEXITCODE -ne 0) {
            throw "dotnet test (unit) exited with code $LASTEXITCODE"
        }
    }
    catch {
        Write-Failure "Unit tests failed: $_"
        exit 2
    }

    Write-Host ''
    Write-Success "Unit tests passed in $(Get-ElapsedSeconds $unitStart)s."
}

# ---------------------------------------------------------------------------
# Step 3: Integration tests
# ---------------------------------------------------------------------------

if ($SkipIntegrationTests) {
    Write-Step 'Integration tests skipped (-SkipIntegrationTests).'
}
else {
    $integrationStart = Get-Date

    # Set required env vars
    $env:RUN_INTEGRATION_TESTS = 'true'

    if ($HfToken) {
        $env:HF_TOKEN = $HfToken
        Write-Step 'HF_TOKEN set from -HfToken parameter.'
    }

    # Build the dotnet test argument list
    $testArgs = @(
        'test',
        $integrationTestProj,
        '--framework', $Framework,
        '--no-build',
        '--logger', 'console;verbosity=minimal'
    )

    if ($Filter) {
        $testArgs += '--filter'
        $testArgs += $Filter
    }

    Write-Step 'Running integration tests...'
    Write-Info "dotnet $($testArgs -join ' ')"
    Write-Host ''

    $integrationFailed = $false
    $integrationError  = $null

    try {
        & dotnet @testArgs

        if ($LASTEXITCODE -ne 0) {
            $integrationFailed = $true
            $integrationError  = "dotnet test (integration) exited with code $LASTEXITCODE"
        }
    }
    catch {
        $integrationFailed = $true
        $integrationError  = "$_"
    }

    # ------------------------------------------------------------------
    # Find and display the latest run-results report (written by TestRunReporter)
    # regardless of pass/fail so the user can see what happened.
    # ------------------------------------------------------------------
    $docsTestsDir = Join-Path $repoRoot 'docs\tests'

    if (Test-Path -LiteralPath $docsTestsDir) {
        $cutoff = (Get-Date).AddMinutes(-5)
        $latestReport = Get-ChildItem -LiteralPath $docsTestsDir -Filter '*-run-results.md' -File -ErrorAction SilentlyContinue |
            Where-Object { $_.LastWriteTime -ge $cutoff } |
            Sort-Object LastWriteTime -Descending |
            Select-Object -First 1

        if ($latestReport) {
            Write-Host ''
            Write-Host '  Test run report written to:' -ForegroundColor Cyan
            Write-Host "  $($latestReport.FullName)" -ForegroundColor Yellow
        }
        else {
            Write-Host ''
            Write-Info 'No new run-results report found in docs/tests/ (written within the last 5 minutes).'
        }
    }
    else {
        Write-Info "docs/tests/ directory does not exist yet at: $docsTestsDir"
    }

    if ($integrationFailed) {
        Write-Host ''
        Write-Failure "Integration tests failed: $integrationError"
        exit 3
    }

    Write-Host ''
    Write-Success "Integration tests passed in $(Get-ElapsedSeconds $integrationStart)s."
}

# ---------------------------------------------------------------------------
# Summary
# ---------------------------------------------------------------------------

Write-Host ''
Write-Host ('=' * 70) -ForegroundColor Green
Write-Host ("  All checks passed in $(Get-ElapsedSeconds $scriptStart)s.") -ForegroundColor Green
Write-Host ('=' * 70) -ForegroundColor Green
Write-Host ''

exit 0
