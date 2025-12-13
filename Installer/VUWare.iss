; VUWare Inno Setup Script
; Copyright (c) 2025 Uwe Baumann
; Licensed under the MIT License
;
; Requirements:
; - Inno Setup 6.2+ (https://jrsoftware.org/isinfo.php)
; - .NET 8.0 Runtime (bundled or required on target)
;
; Build Instructions:
; 1. First, publish the application: dotnet publish VUWare.App -c Release -r win-x64 --self-contained
; 2. Run this script with Inno Setup Compiler (ISCC.exe)
; 3. Or use the provided build-installer.ps1 PowerShell script

#define MyAppName "VUWare"
#define MyAppVersion "1.0.0"
#define MyAppPublisher "Uwe Baumann"
#define MyAppURL "https://github.com/uweinside/VUWare"
#define MyAppExeName "VUWare.App.exe"
#define MyAppDescription "VUWare - VU1 Gauge Hub Controller for HWInfo64 Sensor Monitoring"

; Source paths (relative to this .iss file)
#define SourcePath "..\VUWare.App\bin\Release\net8.0-windows\win-x64\publish"
#define IconPath "..\VUWare.App\VU1_Icon.ico"

[Setup]
; Application identity
AppId={{8A7E5C3D-4B2F-4E1A-9D8C-7F6E5A4B3C2D}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppVerName={#MyAppName} {#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppPublisherURL={#MyAppURL}
AppSupportURL={#MyAppURL}/issues
AppUpdatesURL={#MyAppURL}/releases
AppCopyright=Copyright (c) 2025 {#MyAppPublisher}
VersionInfoVersion={#MyAppVersion}
VersionInfoDescription={#MyAppDescription}

; Installation settings
DefaultDirName={autopf}\{#MyAppName}
DefaultGroupName={#MyAppName}
DisableProgramGroupPage=yes
AllowNoIcons=yes

; Output settings
OutputDir=Output
OutputBaseFilename=VUWare-Setup-{#MyAppVersion}
SetupIconFile={#IconPath}
UninstallDisplayIcon={app}\VU1_Icon.ico

; Compression
Compression=lzma2/ultra64
SolidCompression=yes
LZMANumBlockThreads=4

; Windows compatibility
MinVersion=10.0.17763
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible

; Privileges
PrivilegesRequired=admin
PrivilegesRequiredOverridesAllowed=dialog

; UI settings
WizardStyle=modern
WizardSizePercent=100
DisableWelcomePage=no
ShowLanguageDialog=auto

; License and info pages (uncomment if you have these files)
; LicenseFile=..\LICENSE
; InfoBeforeFile=..\README.md

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked
Name: "autostart"; Description: "Start VUWare automatically when Windows starts (with elevated permissions)"; GroupDescription: "Startup Options:"; Flags: unchecked

[Files]
; Main application files - all files from publish directory
Source: "{#SourcePath}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

; Icon file (ensure it's in the app directory for uninstall display)
Source: "{#IconPath}"; DestDir: "{app}"; Flags: ignoreversion

; Configuration template (don't overwrite existing config during upgrades)
Source: "{#SourcePath}\Config\*"; DestDir: "{app}\Config"; Flags: ignoreversion recursesubdirs createallsubdirs onlyifdoesntexist uninsneveruninstall

[Dirs]
; Ensure Config directory exists and persists
Name: "{app}\Config"; Flags: uninsneveruninstall

[Icons]
; Start Menu shortcut
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; IconFilename: "{app}\VU1_Icon.ico"; Comment: "{#MyAppDescription}"
Name: "{group}\{cm:UninstallProgram,{#MyAppName}}"; Filename: "{uninstallexe}"

; Desktop shortcut (optional)
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; IconFilename: "{app}\VU1_Icon.ico"; Tasks: desktopicon; Comment: "{#MyAppDescription}"

[Registry]
; Autostart with elevated permissions using Task Scheduler instead of registry for elevation
; The registry entry below is for non-elevated startup (kept for reference)
; For elevated startup, we use a scheduled task (see [Run] section)
Root: HKCU; Subkey: "Software\VUWare"; Flags: uninsdeletekeyifempty
Root: HKCU; Subkey: "Software\VUWare\Settings"; Flags: uninsdeletekeyifempty

[Run]
; Option to run after installation
Filename: "{app}\{#MyAppExeName}"; Description: "{cm:LaunchProgram,{#StringChange(MyAppName, '&', '&&')}}"; Flags: nowait postinstall skipifsilent runascurrentuser

[UninstallRun]
; Remove scheduled task on uninstall (if it was created)
Filename: "schtasks.exe"; Parameters: "/Delete /TN ""VUWare Autostart"" /F"; Flags: runhidden; RunOnceId: "RemoveScheduledTask"

[UninstallDelete]
; Clean up any generated files (but keep user config)
Type: filesandordirs; Name: "{app}\logs"

[Code]
var
  AutostartTaskCreated: Boolean;

// Function to create a scheduled task for elevated autostart
procedure CreateElevatedAutostartTask();
var
  ResultCode: Integer;
  TaskXml: String;
  TempFile: String;
begin
  TempFile := ExpandConstant('{tmp}\vuware_task.xml');
  
  // Create XML for scheduled task with elevated permissions
  TaskXml := '<?xml version="1.0" encoding="UTF-16"?>' + #13#10 +
    '<Task version="1.2" xmlns="http://schemas.microsoft.com/windows/2004/02/mit/task">' + #13#10 +
    '  <RegistrationInfo>' + #13#10 +
    '    <Description>Start VUWare with elevated permissions at logon</Description>' + #13#10 +
    '  </RegistrationInfo>' + #13#10 +
    '  <Triggers>' + #13#10 +
    '    <LogonTrigger>' + #13#10 +
    '      <Enabled>true</Enabled>' + #13#10 +
    '    </LogonTrigger>' + #13#10 +
    '  </Triggers>' + #13#10 +
    '  <Principals>' + #13#10 +
    '    <Principal id="Author">' + #13#10 +
    '      <LogonType>InteractiveToken</LogonType>' + #13#10 +
    '      <RunLevel>HighestAvailable</RunLevel>' + #13#10 +
    '    </Principal>' + #13#10 +
    '  </Principals>' + #13#10 +
    '  <Settings>' + #13#10 +
    '    <MultipleInstancesPolicy>IgnoreNew</MultipleInstancesPolicy>' + #13#10 +
    '    <DisallowStartIfOnBatteries>false</DisallowStartIfOnBatteries>' + #13#10 +
    '    <StopIfGoingOnBatteries>false</StopIfGoingOnBatteries>' + #13#10 +
    '    <AllowHardTerminate>true</AllowHardTerminate>' + #13#10 +
    '    <StartWhenAvailable>false</StartWhenAvailable>' + #13#10 +
    '    <RunOnlyIfNetworkAvailable>false</RunOnlyIfNetworkAvailable>' + #13#10 +
    '    <AllowStartOnDemand>true</AllowStartOnDemand>' + #13#10 +
    '    <Enabled>true</Enabled>' + #13#10 +
    '    <Hidden>false</Hidden>' + #13#10 +
    '    <RunOnlyIfIdle>false</RunOnlyIfIdle>' + #13#10 +
    '    <WakeToRun>false</WakeToRun>' + #13#10 +
    '    <ExecutionTimeLimit>PT0S</ExecutionTimeLimit>' + #13#10 +
    '    <Priority>7</Priority>' + #13#10 +
    '  </Settings>' + #13#10 +
    '  <Actions Context="Author">' + #13#10 +
    '    <Exec>' + #13#10 +
    '      <Command>' + ExpandConstant('{app}\{#MyAppExeName}') + '</Command>' + #13#10 +
    '      <WorkingDirectory>' + ExpandConstant('{app}') + '</WorkingDirectory>' + #13#10 +
    '    </Exec>' + #13#10 +
    '  </Actions>' + #13#10 +
    '</Task>';
  
  // Save XML to temp file
  SaveStringToFile(TempFile, TaskXml, False);
  
  // Create the scheduled task
  Exec('schtasks.exe', '/Create /TN "VUWare Autostart" /XML "' + TempFile + '" /F', 
       '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
  
  if ResultCode = 0 then
  begin
    AutostartTaskCreated := True;
    Log('Scheduled task created successfully for elevated autostart');
  end
  else
  begin
    Log('Failed to create scheduled task. Error code: ' + IntToStr(ResultCode));
  end;
  
  // Clean up temp file
  DeleteFile(TempFile);
end;

procedure CurStepChanged(CurStep: TSetupStep);
begin
  if CurStep = ssPostInstall then
  begin
    // Create elevated autostart task if user selected the option
    if WizardIsTaskSelected('autostart') then
    begin
      CreateElevatedAutostartTask();
    end;
  end;
end;

// Show a message about requirements before installation
function InitializeSetup(): Boolean;
begin
  Result := True;
  
  // Check if HWInfo64 is likely installed (optional check)
  // We just show an informational message
  MsgBox('VUWare requires the following to function properly:' + #13#10 + #13#10 +
         '• VU1 Gauge Hub connected via USB' + #13#10 +
         '• HWInfo64 running with "Shared Memory Support" enabled' + #13#10 + #13#10 +
         'Please ensure these prerequisites are met after installation.',
         mbInformation, MB_OK);
end;

// Warn about running instances before uninstall
function InitializeUninstall(): Boolean;
var
  ResultCode: Integer;
begin
  Result := True;
  
  // Check if VUWare is running
  if Exec('tasklist.exe', '/FI "IMAGENAME eq VUWare.App.exe" /NH', '', SW_HIDE, ewWaitUntilTerminated, ResultCode) then
  begin
    // This is a simple check - in practice you might want to use a more robust method
    MsgBox('Please close VUWare before uninstalling.', mbInformation, MB_OK);
  end;
end;
