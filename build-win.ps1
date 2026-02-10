# ============================================
# Diffy - Production Release Build Script (PowerShell)
# ============================================

$ErrorActionPreference = "Stop"
$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path

# Color functions
function Write-Header { param($text) Write-Host "============================================================" -ForegroundColor Cyan; Write-Host "    $text" -ForegroundColor White; Write-Host "============================================================" -ForegroundColor Cyan }
function Write-Step { param($num, $total, $text) Write-Host ""; Write-Host "[STEP $num/$total] $text" -ForegroundColor Yellow }
function Write-Success { param($text) Write-Host "$text" -ForegroundColor Green }
function Write-Error { param($text) Write-Host "ERROR: $text" -ForegroundColor Red; exit 1 }
function Write-Info { param($text) Write-Host "   $text" -ForegroundColor White }

Write-Header "DIFFY PRODUCTION RELEASE BUILD"
Write-Host ""
Write-Step 1 4 "Starting Production Release Build..."
Write-Info "This will create a distribution-ready build with optimizations."
Write-Info "Previous artifacts will be cleaned."
Write-Host ""

# Configuration
$ProjectDir = $ScriptDir
$ProjectName = "Diffy"
$OutputDir = "src\Diffy.App\bin\Release\net8.0"
$DistDir = "dist"

Write-Info "Project: $ProjectName"
Write-Info "Configuration: Release"
Write-Info "Output: $OutputDir"
Write-Info "Distribution: $DistDir"

# Clean previous builds
Write-Step 2 4 "Cleaning previous build artifacts..."
if (Test-Path $DistDir) {
    Write-Info "Removing existing dist folder..."
    Remove-Item -Path $DistDir -Recurse -Force
}

# Run dotnet clean for fresh build
Write-Info "Running dotnet clean on Diffy.App..."
& dotnet clean "src\Diffy.App\Diffy.App.csproj" -c Release 2>&1 | Out-Null
& dotnet clean "src\Diffy.Core\Diffy.Core.csproj" -c Release 2>&1 | Out-Null

if (Test-Path $OutputDir) {
    Write-Info "Removing existing Release output..."
    Remove-Item -Path $OutputDir -Recurse -Force
}
Write-Success "Cleanup complete."

# Build Release
Write-Step 3 4 "Building $ProjectName in Release configuration..."
$buildResult = & dotnet build "src\Diffy.App\Diffy.App.csproj" -c Release --no-restore -warnaserror 2>&1
if ($LASTEXITCODE -ne 0) {
    Write-Error "Build failed. Please check errors above."
}
Write-Success "Build completed successfully!"

# Create distribution folder
Write-Step 4 4 "Creating distribution package..."
if (-not (Test-Path $DistDir)) {
    New-Item -ItemType Directory -Path $DistDir
}

# Copy files to dist
Write-Info "Copying application files..."
Copy-Item -Path "$OutputDir\*" -Destination "$DistDir\" -Recurse -Force
if (-not (Test-Path "$DistDir\Assets")) {
    Write-Error "Assets folder missing!"
}
if (-not (Test-Path "$DistDir\Assets\Icons\icon.ico")) {
    Write-Host "WARNING: icon.ico missing in Assets\Icons\" -ForegroundColor Yellow
}
Write-Success "Distribution package created!"

Write-Host ""
Write-Header "BUILD COMPLETED SUCCESSFULLY!"
Write-Host ""
Write-Success "Ready to zip and distribute!"
