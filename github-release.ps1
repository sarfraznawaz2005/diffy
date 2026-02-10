#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Automates release process for Diffy
.DESCRIPTION
    Updates version in .csproj, creates git tag, and publishes GitHub release with auto-generated notes
.PARAMETER Version
    The version number (e.g., "1.0.1")
.PARAMETER Notes
    Optional custom release notes. If provided, prepends to auto-generated notes.
.PARAMETER Draft
    Create as draft release instead of publishing immediately
.PARAMETER NoAutoNotes
    Disable auto-generated release notes (What's Changed, New Contributors)
.EXAMPLE
    .\github-release.ps1 -Version "1.0.1"
    Creates release with auto-generated notes
.EXAMPLE
    .\github-release.ps1 -Version "1.0.1" -Notes "## Highlights`n- Major feature added`n- Critical bug fixed" -Draft
    Creates draft release with custom notes prepended to auto-generated notes
.EXAMPLE
    .\github-release.ps1 -Version "1.0.1" -Notes "Custom notes only" -NoAutoNotes
    Creates release with only custom notes (no auto-generation)
#>

param(
    [Parameter(Mandatory=$true, HelpMessage="Version number (e.g., '1.0.0')")]
    [ValidatePattern('^\d+\.\d+\.\d+(-\w+)?$')]
    [string]$Version,

    [Parameter(Mandatory=$false)]
    [string]$Notes,

    [Parameter(Mandatory=$false)]
    [switch]$Draft,

    [Parameter(Mandatory=$false)]
    [switch]$NoAutoNotes
)

$ErrorActionPreference = "Stop"

# Configuration
$ProjectFile = "src/Diffy.App/Diffy.App.csproj"
$TagName = "v$Version"
$ReleaseTitle = "v$Version"

# Helper functions
function Write-ColorOutput {
    param([string]$Message, [string]$Color = "Green")
    Write-Host $Message -ForegroundColor $Color
}

function Test-Command {
    param([string]$Command)
    try {
        $null = Get-Command $Command -ErrorAction Stop
        return $true
    }
    catch {
        return $false
    }
}

# Check prerequisites
Write-ColorOutput "=== Diffy Release Script ===" "Cyan"

if (-not (Test-Command "git")) {
    Write-Error "Git is not installed or not in PATH"
    exit 1
}

if (-not (Test-Command "gh")) {
    Write-Error "GitHub CLI (gh) is not installed. Install from https://cli.github.com/"
    exit 1
}

# Check git status
$gitStatus = git status --porcelain 2>&1
if ($gitStatus -and $gitStatus.Length -gt 0) {
    Write-Warning "You have uncommitted changes:"
    Write-Host $gitStatus
    $response = Read-Host "Continue anyway? (y/N)"
    if ($response -ne "y" -and $response -ne "Y") {
        Write-ColorOutput "Aborted." "Yellow"
        exit 1
    }
}

# Step 1: Update version in .csproj
Write-ColorOutput "`n[1/5] Updating version in $ProjectFile..." "Cyan"
$projectContent = Get-Content $ProjectFile -Raw

$versionPattern = "<Version>.*?</Version>"
if ($projectContent -match $versionPattern) {
    $projectContent = $projectContent -replace $versionPattern, "<Version>$Version</Version>"
    Set-Content $ProjectFile -Value $projectContent -NoNewline
    Write-ColorOutput "  ✓ Updated version to $Version" "Green"
}
else {
    $errorMsg = "  ✗ Could not find <Version> tag in $ProjectFile"
    Write-Error $errorMsg
    exit 1
}

# Step 2: Commit version change
Write-ColorOutput "`n[2/5] Committing version change..." "Cyan"
git add $ProjectFile
git commit -m "Release v$Version"
Write-ColorOutput "  ✓ Committed version bump" "Green"

# Step 3: Create git tag
Write-ColorOutput "`n[3/5] Creating git tag $TagName..." "Cyan"
git tag -a $TagName -m "Release v$Version"
Write-ColorOutput "  ✓ Tag created: $TagName" "Green"

# Step 4: Push changes and tag
Write-ColorOutput "`n[4/5] Pushing to remote..." "Cyan"
git push
git push origin $TagName
Write-ColorOutput "  ✓ Pushed to remote" "Green"

# Step 5: Create GitHub release with notes
Write-ColorOutput "`n[5/5] Creating GitHub release..." "Cyan"

$releaseArgs = @(
    "release", "create", $TagName,
    "--title", $ReleaseTitle
)

# Add draft flag if specified
if ($Draft) {
    $releaseArgs += "--draft"
    Write-ColorOutput "  Creating as DRAFT release" "Yellow"
}

# Handle release notes
if ($NoAutoNotes) {
    # Use only custom notes if provided, otherwise minimal notes
    if ($Notes) {
        $releaseArgs += "--notes"
        $releaseArgs += $Notes
        Write-ColorOutput "  Using custom notes only" "White"
    } else {
        $releaseArgs += "--notes"
        $releaseArgs += "Release v$Version"
        Write-ColorOutput "  Using minimal notes" "White"
    }
} else {
    # Use auto-generated notes (What's Changed, New Contributors, etc.)
    $releaseArgs += "--generate-notes"
    Write-ColorOutput "  Auto-generating release notes (What's Changed, New Contributors)" "White"
    
    # If custom notes provided, prepend them
    if ($Notes) {
        $releaseArgs += "--notes-start-tag"
        $releaseArgs += (git describe --tags --abbrev=0 HEAD^ 2>$null)
        $releaseArgs += "--notes"
        $releaseArgs += $Notes
        Write-ColorOutput "  Prepending custom notes to auto-generated content" "White"
    }
}

& gh @releaseArgs

if ($LASTEXITCODE -eq 0) {
    Write-ColorOutput "`n✓ Release $ReleaseTitle created successfully!" "Green"
    $releaseUrl = "https://github.com/sarfraznawaz2005/diffy/releases/tag/$TagName"
    Write-Host "  Release URL: $releaseUrl" -ForegroundColor "Cyan"
    
    if (-not $NoAutoNotes) {
        Write-Host ""
        Write-ColorOutput "  📋 Release notes include:" "Cyan"
        Write-ColorOutput "     • What's Changed (commits grouped by PR)" "White"
        Write-ColorOutput "     • New Contributors (first-time contributors)" "White"
        Write-ColorOutput "     • Full Changelog link" "White"
    }
} else {
    Write-Error "Failed to create GitHub release"
    exit 1
}
