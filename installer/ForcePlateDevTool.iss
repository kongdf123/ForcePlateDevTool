#define MyAppName "ForcePlateDevTool"
#define MyAppVersion GetEnv("RELEASE_VERSION")
#define MyAppPublisher "Kunwei"
#define MyAppExeName "ForcePlateDevTool.exe"

#if MyAppVersion == ""
  #error "RELEASE_VERSION must be set before compiling the installer."
#endif

[Setup]
; Keep a stable AppId across releases for proper upgrades/uninstall behavior.
; AppId should be a GUID surrounded by braces in the final compiled script. Use double braces here
; to ensure a literal brace is emitted by the preprocessor.
AppId={{A2C5D5C1-6A41-4B91-A8D2-5A62A1F79E10}}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
DefaultGroupName=ForcePlateDevTool

; DefaultDirName chosen dynamically via {code:GetDefaultDir} below to favor per-user installs when not admin,
; and Program Files when admin.
DefaultDirName={code:GetDefaultDir}

PrivilegesRequired=admin
PrivilegesRequiredOverridesAllowed=dialog

ArchitecturesAllowed=x64
ArchitecturesInstallIn64BitMode=x64

UninstallDisplayIcon={app}\{#MyAppExeName}
OutputDir=..\artifacts
OutputBaseFilename=ForcePlateDevTool-Setup-v{#MyAppVersion}

WizardStyle=modern
Compression=lzma2
SolidCompression=yes
CloseApplications=yes
RestartApplications=no
Uninstallable=yes

; Do NOT remove user-specific config, calibration data, or logs stored under %LOCALAPPDATA% on uninstall.
; User data must be preserved during upgrade/uninstall.

[Languages]
Name: "english"

[Tasks]
Name: "desktopicon"; Description: "Create a desktop shortcut"; GroupDescription: "Additional shortcuts:"; Flags: unchecked

[Files]
Source: "..\artifacts\publish\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\ForcePlateDevTool"; Filename: "{app}\{#MyAppExeName}"
Name: "{autodesktop}\ForcePlateDevTool"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "Launch ForcePlateDevTool"; Flags: postinstall nowait skipifsilent

[Code]
// Choose an appropriate default install directory:
// - if installer runs elevated (admin) prefer Program Files
// - otherwise prefer per-user install under LocalAppData\Programs
function GetDefaultDir(Default: String): String;
begin
  if IsAdmin() then
    Result := ExpandConstant('{autopf}\ForcePlateDevTool')
  else
    Result := ExpandConstant('{localappdata}\Programs\ForcePlateDevTool');
end;

// Prevent uninstall from removing user-specific localappdata files:
// (no explicit UninstallDelete entries for user data)