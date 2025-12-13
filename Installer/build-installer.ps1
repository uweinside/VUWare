# VUWare Installer Build Script
# Copyright (c) 2025 Uwe Baumann
# Licensed under the MIT License
#
# This script automates building the VUWare installer:
# 1. Publishes the VUWare.App project as a self-contained application
# 2. Compiles the Inno Setup script to create the installer
#
# Prerequisites:
# - .NET 8.0 SDK installed
# - Inno Setup 6.2+ installed (https://jrsoftware.org/isinfo.php)
#
# Usage:
#   .\build-installer.ps1                    # Build with default settings
#   .\build-installer.ps1 -Version "1.2.0"   # Build with custom version
#   .\build-installer.ps1 -SkipPublish       # Skip dotnet publish (use existing build)
#   .\build-installer.ps1 -Verbose           # Show detailed output

[CmdletBinding()]
param(
    [Parameter(Mandatory = $false)]
    [string]$Version = "1.0.0",
    
    [Parameter(Mandatory = $false)]
    [switch]$SkipPublish,
    
    [Parameter(Mandatory = $false)]
    [string]$Configuration = "Release",
    
    [Parameter(Mandatory = $false)]
    [string]$Runtime = "win-x64",
    
    [Parameter(Mandatory = $false)]
    [switch]$SelfContained = $true,
    
    [Parameter(Mandatory = $false)]
    [string]$InnoSetupPath
)

$ErrorActionPreference = "Stop"

# Script location
$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$RepoRoot = Split-Path -Parent $ScriptDir
$ProjectPath = Join-Path $RepoRoot "VUWare.App"
$IssFile = Join-Path $ScriptDir "VUWare.iss"
$OutputDir = Join-Path $ScriptDir "Output"

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "  VUWare Installer Build Script" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "Version:       $Version" -ForegroundColor White
Write-Host "Configuration: $Configuration" -ForegroundColor White
Write-Host "Runtime:       $Runtime" -ForegroundColor White
Write-Host "Self-Contained: $SelfContained" -ForegroundColor White
Write-Host ""

# Find Inno Setup Compiler
function Find-InnoSetup {
    $possiblePaths = @(
        $InnoSetupPath,
        "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe",
        "${env:ProgramFiles}\Inno Setup 6\ISCC.exe",
        "${env:ProgramFiles(x86)}\Inno Setup 5\ISCC.exe",
        "${env:ProgramFiles}\Inno Setup 5\ISCC.exe"
    )
    
    foreach ($path in $possiblePaths) {
        if ($path -and (Test-Path $path)) {
            return $path
        }
    }
    
    return $null
}

$IsccPath = Find-InnoSetup

if (-not $IsccPath) {
    Write-Host "ERROR: Inno Setup Compiler (ISCC.exe) not found!" -ForegroundColor Red
    Write-Host ""
    Write-Host "Please install Inno Setup from: https://jrsoftware.org/isinfo.php" -ForegroundColor Yellow
    Write-Host "Or specify the path using: -InnoSetupPath 'C:\Path\To\ISCC.exe'" -ForegroundColor Yellow
    exit 1
}

Write-Host "Inno Setup found: $IsccPath" -ForegroundColor Green
Write-Host ""

# Step 1: Publish the application
if (-not $SkipPublish) {
    Write-Host "Step 1: Publishing VUWare.App..." -ForegroundColor Yellow
    Write-Host "----------------------------------------" -ForegroundColor Gray
    
    $publishArgs = @(
        "publish",
        $ProjectPath,
        "-c", $Configuration,
        "-r", $Runtime,
        "--self-contained", $SelfContained.ToString().ToLower(),
        "-p:PublishSingleFile=false",
        "-p:Version=$Version",
        "-p:AssemblyVersion=$Version",
        "-p:FileVersion=$Version"
    )
    
    Write-Verbose "Running: dotnet $($publishArgs -join ' ')"
    
    $result = & dotnet @publishArgs
    
    if ($LASTEXITCODE -ne 0) {
        Write-Host "ERROR: dotnet publish failed!" -ForegroundColor Red
        Write-Host $result -ForegroundColor Red
        exit 1
    }
    
    Write-Host "Publish completed successfully!" -ForegroundColor Green
    Write-Host ""
} else {
    Write-Host "Step 1: Skipping publish (using existing build)" -ForegroundColor Yellow
    Write-Host ""
}

# Verify publish output exists
$publishPath = Join-Path $ProjectPath "bin\$Configuration\net8.0-windows\$Runtime\publish"
if (-not (Test-Path $publishPath)) {
    Write-Host "ERROR: Publish output not found at: $publishPath" -ForegroundColor Red
    Write-Host "Please run without -SkipPublish to build the application first." -ForegroundColor Yellow
    exit 1
}

Write-Host "Publish output found at: $publishPath" -ForegroundColor Green
Write-Host ""

# Step 2: Build the installer
Write-Host "Step 2: Building Installer..." -ForegroundColor Yellow
Write-Host "----------------------------------------" -ForegroundColor Gray

# Create output directory if it doesn't exist
if (-not (Test-Path $OutputDir)) {
    New-Item -ItemType Directory -Path $OutputDir -Force | Out-Null
}

# Update version in .iss file (create a temporary modified version)
$issContent = Get-Content $IssFile -Raw
$modifiedIssContent = $issContent -replace '#define MyAppVersion ".*"', "#define MyAppVersion `"$Version`""

$tempIssFile = Join-Path $ScriptDir "VUWare_temp.iss"
$modifiedIssContent | Set-Content $tempIssFile -Encoding UTF8

try {
    # Run Inno Setup Compiler
    Write-Verbose "Running: $IsccPath $tempIssFile"
    
    $isccArgs = @(
        "/O$OutputDir",
        $tempIssFile
    )
    
    $result = & $IsccPath @isccArgs
    
    if ($LASTEXITCODE -ne 0) {
        Write-Host "ERROR: Inno Setup compilation failed!" -ForegroundColor Red
        Write-Host $result -ForegroundColor Red
        exit 1
    }
    
    Write-Host "Installer built successfully!" -ForegroundColor Green
}
finally {
    # Clean up temp file
    if (Test-Path $tempIssFile) {
        Remove-Item $tempIssFile -Force
    }
}

Write-Host ""
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "  Build Complete!" -ForegroundColor Green
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

# List output files
$outputFiles = Get-ChildItem $OutputDir -Filter "*.exe" | Sort-Object LastWriteTime -Descending | Select-Object -First 5
Write-Host "Output files:" -ForegroundColor White
foreach ($file in $outputFiles) {
    $sizeMB = [math]::Round($file.Length / 1MB, 2)
    Write-Host "  - $($file.Name) ($sizeMB MB)" -ForegroundColor Gray
}

Write-Host ""
Write-Host "Installer location: $OutputDir" -ForegroundColor Cyan
