#define MyAppName "ForcePlateDevTool"
#define MyAppVersion GetEnv("RELEASE_VERSION")
#define MyAppPublisher "Kunwei"
#define MyAppExeName "ForcePlateDevTool.exe"

#if MyAppVersion == ""
  #error "RELEASE_VERSION must be set before compiling the installer."
#endif

[Setup]
AppId={{A2C5D5C1-6A41-4B91-A8D2-5A62A1F79E10}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}

DefaultDirName={autopf}\ForcePlateDevTool
DefaultGroupName=ForcePlateDevTool

PrivilegesRequired=admin
PrivilegesRequiredOverridesAllowed=dialog

ArchitecturesAllowed=x64
ArchitecturesInstallIn64BitMode=x64

UninstallDisplayIcon={app}\ForcePlateDevTool.exe
OutputDir=..\artifacts
OutputBaseFilename=ForcePlateDevTool-Setup-v{#MyAppVersion}

WizardStyle=modern
Compression=lzma2
SolidCompression=yes
CloseApplications=yes
RestartApplications=no
Uninstallable=yes

[Tasks]
Name: "desktopicon"; \
    Description: "Create a desktop shortcut"; \
    GroupDescription: "Additional shortcuts:"; \
    Flags: unchecked

[Files]
Source: "..\artifacts\publish\*"; \
    DestDir: "{app}"; \
    Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\ForcePlateDevTool"; \
    Filename: "{app}\{#MyAppExeName}"

Name: "{autodesktop}\ForcePlateDevTool"; \
    Filename: "{app}\{#MyAppExeName}"; \
    Tasks: desktopicon

[Run]
Filename: "{app}\{#MyAppExeName}"; \
    Description: "Launch ForcePlateDevTool"; \
    Flags: postinstall nowait skipifsilent