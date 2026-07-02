; Inno Setup script for Claude Usage Tracker (Windows).
;
; Build steps:
;   1. Publish a self-contained build:
;        dotnet publish src\ClaudeUsageTracker.Windows\ClaudeUsageTracker.Windows.csproj ^
;          -c Release -r win-x64 --self-contained true -p:PublishSingleFile=false ^
;          -o installer\publish
;   2. Compile this script (requires Inno Setup 6+, https://jrsoftware.org/isinfo.php):
;        ISCC installer\setup.iss
;      Optionally override the version (e.g. from a CI release tag):
;        ISCC /DMyAppVersion=1.2.3 installer\setup.iss
;   The resulting installer is written to installer\output\.

#define MyAppName "Claude Usage Tracker"
#ifndef MyAppVersion
  #define MyAppVersion "0.1.0"
#endif
#define MyAppPublisher "Claude Usage Tracker contributors"
#define MyAppExeName "ClaudeUsageTracker.Windows.exe"
#define MyAppURL "https://github.com/hamed-elfayome/Claude-Usage-Tracker"

[Setup]
AppId={{E4C1F7B4-7C7D-4C5B-9C2E-3B6E7F2E8D41}}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppPublisherURL={#MyAppURL}
AppSupportURL={#MyAppURL}
AppUpdatesURL={#MyAppURL}
SetupIconFile=..\src\ClaudeUsageTracker.Windows\Assets\AppIcon.ico
DefaultDirName={autopf}\{#MyAppName}
DefaultGroupName={#MyAppName}
DisableProgramGroupPage=yes
; Per-user install — no elevation required, so silent auto-updates trigger no UAC prompt.
PrivilegesRequired=lowest
OutputDir=output
OutputBaseFilename=ClaudeUsageTracker-Setup-{#MyAppVersion}
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
UninstallDisplayIcon={app}\{#MyAppExeName}
CloseApplications=yes
RestartApplications=no

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[Files]
Source: "publish\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{group}\{cm:UninstallProgram,{#MyAppName}}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "{cm:LaunchProgram,{#StringChange(MyAppName, '&', '&&')}}"; Flags: nowait postinstall

[UninstallDelete]
; Credentials are stored in Windows Credential Manager (not under {app}), so nothing extra to clean there.
Type: filesandordirs; Name: "{app}"

[Code]
function IsWebView2RuntimeInstalled: Boolean;
var
  Version: string;
begin
  Result :=
    RegQueryStringValue(HKLM64, 'SOFTWARE\WOW6432Node\Microsoft\EdgeUpdate\Clients\{F3017226-FE2A-4295-8BDF-00C3A9A7E4C5}', 'pv', Version) or
    RegQueryStringValue(HKLM64, 'SOFTWARE\Microsoft\EdgeUpdate\Clients\{F3017226-FE2A-4295-8BDF-00C3A9A7E4C5}', 'pv', Version) or
    RegQueryStringValue(HKCU, 'SOFTWARE\Microsoft\EdgeUpdate\Clients\{F3017226-FE2A-4295-8BDF-00C3A9A7E4C5}', 'pv', Version);
end;

procedure InitializeWizard;
begin
  if not IsWebView2RuntimeInstalled then
  begin
    MsgBox('The Microsoft Edge WebView2 Runtime was not detected on this machine. ' +
      'Claude Usage Tracker requires it to fetch usage data. Windows 11 includes it by default; ' +
      'on Windows 10 you may need to install it from https://developer.microsoft.com/microsoft-edge/webview2/ ' +
      'before the app will work correctly.'#13#10#13#10'Setup will continue.',
      mbInformation, MB_OK);
  end;
end;
