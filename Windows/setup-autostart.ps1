# VUWare AutoStart Setup Script
# 
# === HOW TO RUN ===
# This script must be run as Administrator in PowerShell.
#
# If you get "running scripts is disabled" error, use ONE of these methods:
#
# METHOD 1 (Recommended - Bypass for this session only):
#   Right-click PowerShell ? "Run as Administrator"
#   cd C:\Repos\VUWare
#   PowerShell -ExecutionPolicy Bypass -File .\setup-autostart.ps1
#
# METHOD 2 (Enable for current user permanently):
#   Right-click PowerShell ? "Run as Administrator"
#   Set-ExecutionPolicy -ExecutionPolicy RemoteSigned -Scope CurrentUser
#   cd C:\Repos\VUWare
#   .\setup-autostart.ps1
#
# METHOD 3 (One-liner - Copy and paste into Admin PowerShell):
#   See the one-liner command at the bottom of this file
#
# === USAGE ===
#   .\setup-autostart.ps1                    # Auto-detect and install
#   .\setup-autostart.ps1 -Debug             # Force Debug build
#   .\setup-autostart.ps1 -Release           # Force Release build
#   .\setup-autostart.ps1 -Platform x64      # Force specific platform
#   .\setup-autostart.ps1 -Remove            # Uninstall the task

param(
    [switch]$Remove,
    [switch]$Debug,
    [switch]$Release,
    [string]$Platform = ""
)

$TaskName = "VUWare App AutoStart"
$ProjectRoot = $PSScriptRoot

Write-Host "`n=== VUWare AutoStart Setup ===" -ForegroundColor Cyan

# Check if running as Administrator
$isAdmin = ([Security.Principal.WindowsPrincipal] [Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
if (-not $isAdmin) {
    Write-Error "This script must be run as Administrator!"
    Write-Host "Right-click PowerShell and select 'Run as Administrator', then run this script again." -ForegroundColor Yellow
    Write-Host "`nQuick command (copy and paste into Admin PowerShell):" -ForegroundColor Cyan
    Write-Host "  cd '$ProjectRoot'; PowerShell -ExecutionPolicy Bypass -File .\setup-autostart.ps1`n" -ForegroundColor Yellow
    exit 1
}

# Remove existing task if requested
if ($Remove) {
    try {
        $existingTask = Get-ScheduledTask -TaskName $TaskName -ErrorAction SilentlyContinue
        if ($existingTask) {
            Unregister-ScheduledTask -TaskName $TaskName -Confirm:$false
            Write-Host "? Task removed successfully: $TaskName" -ForegroundColor Green
        } else {
            Write-Host "Task not found: $TaskName" -ForegroundColor Yellow
        }
    } catch {
        Write-Error "Failed to remove task: $_"
    }
    exit 0
}

# Auto-detect the executable location
Write-Host "Searching for VUWare.App.exe..." -ForegroundColor Yellow

$BinPath = Join-Path $ProjectRoot "VUWare.App\bin"
$PossiblePaths = @()

# Define possible build configurations and platforms
$Configs = @("Release", "Debug")
$Platforms = @("x64", "AnyCPU", "x86")

# If user specified preferences, prioritize them
if ($Release) {
    $Configs = @("Release") + ($Configs | Where-Object { $_ -ne "Release" })
}
if ($Debug) {
    $Configs = @("Debug") + ($Configs | Where-Object { $_ -ne "Debug" })
}
if ($Platform) {
    $Platforms = @($Platform) + ($Platforms | Where-Object { $_ -ne $Platform })
}

# Search for the executable in all possible locations
foreach ($Config in $Configs) {
    foreach ($Plat in $Platforms) {
        $TestPath = Join-Path $BinPath "$Plat\$Config\net8.0-windows\VUWare.App.exe"
        if (Test-Path $TestPath) {
            $PossiblePaths += @{
                Path = $TestPath
                Config = $Config
                Platform = $Plat
            }
        }
    }
}

# If no executable found, show error and exit
if ($PossiblePaths.Count -eq 0) {
    Write-Error "No VUWare.App.exe found in bin directory!"
    Write-Host "`nSearched locations:" -ForegroundColor Yellow
    foreach ($Config in @("Release", "Debug")) {
        foreach ($Plat in @("x64", "AnyCPU", "x86")) {
            Write-Host "  - $BinPath\$Plat\$Config\net8.0-windows\VUWare.App.exe" -ForegroundColor Gray
        }
    }
    Write-Host "`nPlease build the project first:" -ForegroundColor Yellow
    Write-Host "  dotnet build VUWare.App\VUWare.App.csproj -c Release -p:Platform=x64`n" -ForegroundColor Cyan
    exit 1
}

# Use the first (most preferred) executable found
$SelectedExe = $PossiblePaths[0]
$ExePath = $SelectedExe.Path
$BuildConfig = $SelectedExe.Config
$BuildPlatform = $SelectedExe.Platform

Write-Host "? Found executable:" -ForegroundColor Green
Write-Host "  Path:     $ExePath" -ForegroundColor Gray
Write-Host "  Config:   $BuildConfig" -ForegroundColor Gray
Write-Host "  Platform: $BuildPlatform`n" -ForegroundColor Gray

# Show other available builds if any
if ($PossiblePaths.Count -gt 1) {
    Write-Host "Other available builds:" -ForegroundColor Yellow
    for ($i = 1; $i -lt $PossiblePaths.Count; $i++) {
        $alt = $PossiblePaths[$i]
        Write-Host "  - $($alt.Config) / $($alt.Platform)" -ForegroundColor Gray
    }
    Write-Host ""
}

$WorkingDir = Split-Path $ExePath

Write-Host "Task Name: $TaskName" -ForegroundColor Gray
Write-Host ""

# Create scheduled task components
try {
    Write-Host "Creating scheduled task..." -ForegroundColor Yellow
    
    # Action: Start the application
    $Action = New-ScheduledTaskAction `
        -Execute $ExePath `
        -WorkingDirectory $WorkingDir
    
    # Trigger: At user logon
    $Trigger = New-ScheduledTaskTrigger `
        -AtLogOn `
        -User $env:USERNAME
    
    # Settings: Configure task behavior
    $Settings = New-ScheduledTaskSettingsSet `
        -AllowStartIfOnBatteries `
        -DontStopIfGoingOnBatteries `
        -StartWhenAvailable `
        -RunOnlyIfNetworkAvailable:$false `
        -DontStopOnIdleEnd `
        -ExecutionTimeLimit (New-TimeSpan -Hours 0) `
        -RestartCount 3 `
        -RestartInterval (New-TimeSpan -Minutes 1)
    
    # Principal: Run with highest privileges to avoid UAC prompts
    $Principal = New-ScheduledTaskPrincipal `
        -UserId $env:USERNAME `
        -LogonType Interactive `
        -RunLevel Highest
    
    # Register the task (use -Force to update if exists)
    Register-ScheduledTask `
        -TaskName $TaskName `
        -Action $Action `
        -Trigger $Trigger `
        -Settings $Settings `
        -Principal $Principal `
        -Description "Auto-start VUWare monitoring application on Windows logon. Runs with elevated privileges to access hardware monitoring and serial devices." `
        -Force | Out-Null
    
    Write-Host "`n? Task created successfully!" -ForegroundColor Green
    Write-Host "? Application will start automatically when you log in" -ForegroundColor Green
    Write-Host "? No UAC prompt will appear (runs with highest privileges)" -ForegroundColor Green
    
    Write-Host "`n=== Task Details ===" -ForegroundColor Cyan
    Write-Host "Name:        $TaskName" -ForegroundColor Gray
    Write-Host "User:        $env:USERNAME" -ForegroundColor Gray
    Write-Host "Trigger:     At logon" -ForegroundColor Gray
    Write-Host "Privileges:  Highest (Administrator)" -ForegroundColor Gray
    Write-Host "Build:       $BuildConfig / $BuildPlatform" -ForegroundColor Gray
    Write-Host "Executable:  $ExePath" -ForegroundColor Gray
    
    Write-Host "`n=== Next Steps ===" -ForegroundColor Cyan
    Write-Host "• Test the task: Open Task Scheduler (taskschd.msc) and run '$TaskName' manually" -ForegroundColor Gray
    Write-Host "• Or simply log out and log back in to test automatic startup" -ForegroundColor Gray
    Write-Host "• To remove the task, run: PowerShell -ExecutionPolicy Bypass -File .\setup-autostart.ps1 -Remove" -ForegroundColor Gray
    
} catch {
    Write-Error "Failed to create scheduled task: $_"
    Write-Host "`nError Details:" -ForegroundColor Red
    Write-Host $_.Exception.Message -ForegroundColor Red
    exit 1
}
<#
=== ONE-LINER INSTALLATION (Copy this entire block into Admin PowerShell) ===

$TaskName = "VUWare App AutoStart"; $ExePath = "C:\Repos\VUWare\VUWare.App\bin\Release\net8.0-windows\VUWare.App.exe"; if (-not (Test-Path $ExePath)) { Write-Error "Build the project first: dotnet build VUWare.App\VUWare.App.csproj -c Release"; exit 1 }; $Action = New-ScheduledTaskAction -Execute $ExePath -WorkingDirectory (Split-Path $ExePath); $Trigger = New-ScheduledTaskTrigger -AtLogOn -User $env:USERNAME; $Settings = New-ScheduledTaskSettingsSet -AllowStartIfOnBatteries -DontStopIfGoingOnBatteries -StartWhenAvailable -RunOnlyIfNetworkAvailable:$false -DontStopOnIdleEnd -ExecutionTimeLimit (New-TimeSpan -Hours 0); $Principal = New-ScheduledTaskPrincipal -UserId $env:USERNAME -LogonType Interactive -RunLevel Highest; Register-ScheduledTask -TaskName $TaskName -Action $Action -Trigger $Trigger -Settings $Settings -Principal $Principal -Description "Auto-start VUWare monitoring application" -Force | Out-Null; Write-Host "? Task created: $TaskName" -ForegroundColor Green

#>
