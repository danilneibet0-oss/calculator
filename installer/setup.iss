; Inno Setup script. Build with:  .\build-installer.ps1
#define AppName "Калькулятор"
#define AppVersion "1.3.0"
#define AppExe "Calculator.exe"

[Setup]
AppId={{6B1F6E52-3C0D-4C8E-9A57-1F2A7B9D4E10}
AppName={#AppName}
AppVersion={#AppVersion}
AppVerName={#AppName} {#AppVersion}
DefaultDirName={autopf}\{#AppName}
DefaultGroupName={#AppName}
UninstallDisplayIcon={app}\{#AppExe}
UninstallDisplayName={#AppName}
SetupIconFile=..\assets\app.ico
LicenseFile=license.txt
OutputDir=..\dist
OutputBaseFilename=Calculator-Setup-{#AppVersion}
Compression=lzma2/max
SolidCompression=yes
WizardStyle=modern
; Page "choose install folder" and page "choose Start menu folder" are both shown.
DisableDirPage=no
DisableProgramGroupPage=no
; Lets the user pick "for me only" (no admin) or "for all users" (admin) on the first page.
PrivilegesRequired=lowest
PrivilegesRequiredOverridesAllowed=dialog
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0

[Languages]
Name: "russian"; MessagesFile: "compiler:Languages\Russian.isl"
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"
Name: "startmenuicon"; Description: "Создать ярлык в меню «Пуск»"; GroupDescription: "{cm:AdditionalIcons}"

[Files]
Source: "..\build\Calculator.exe"; DestDir: "{app}"; Flags: ignoreversion
Source: "license.txt"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
Name: "{autoprograms}\{groupname}\{#AppName}"; Filename: "{app}\{#AppExe}"; Tasks: startmenuicon
Name: "{autodesktop}\{#AppName}"; Filename: "{app}\{#AppExe}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#AppExe}"; Description: "{cm:LaunchProgram,{#AppName}}"; Flags: nowait postinstall skipifsilent
