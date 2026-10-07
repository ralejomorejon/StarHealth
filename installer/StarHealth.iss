; StarHealth installer for regular Windows users.
; Bundles the self-contained unpackaged publish output (dotnet + Windows App
; SDK included), so no runtime downloads are needed on the target machine.
; Build locally: ISCC.exe installer/StarHealth.iss   (output -> installer/output/)
; CI passes /DAppVersion=<tag>.
#ifndef AppVersion
#define AppVersion "0.0.0-dev"
#endif
#define PublishDir "..\src\StarHealth.App\bin\Release\net10.0-windows10.0.26100.0\win-x64\publish"

[Setup]
AppId={{E4DD2AA9-53FF-40CA-8682-3406B65164C4}}
AppName=StarHealth
AppVersion={#AppVersion}
AppPublisher=ralejomorejon
AppPublisherURL=https://github.com/ralejomorejon/StarHealth
DefaultDirName={autopf}\StarHealth
DefaultGroupName=StarHealth
OutputDir=output
OutputBaseFilename=StarHealth-Setup-{#AppVersion}-win-x64
Compression=lzma2/max
SolidCompression=yes
PrivilegesRequired=lowest
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
WizardStyle=modern
UninstallDisplayName=StarHealth {#AppVersion}
; Unsigned alpha build: Windows SmartScreen will warn on first run.
; Signing with a trusted cert removes that warning (a later release step).

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[Files]
Source: "{#PublishDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\StarHealth"; Filename: "{app}\StarHealth.App.exe"
Name: "{autodesktop}\StarHealth"; Filename: "{app}\StarHealth.App.exe"; Tasks: desktopicon

[Run]
Filename: "{app}\StarHealth.App.exe"; Description: "{cm:LaunchProgram,StarHealth}"; Flags: nowait postinstall skipifsilent
