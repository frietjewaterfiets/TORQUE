#ifndef AppSourceDir
  #error AppSourceDir is not defined.
#endif

#ifndef OutputDir
  #error OutputDir is not defined.
#endif

[Setup]
AppId={{7DDE8CAC-4E95-4C40-A65D-457480EED862}
AppName=TORQUE
AppVersion=1.0
AppVerName=TORQUE v1.0
AppPublisher=TORQUE
DefaultDirName={commonpf64}\TORQUE
DefaultGroupName=TORQUE
DisableProgramGroupPage=yes
AllowNoIcons=yes
UsePreviousAppDir=no
UsePreviousTasks=yes
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
PrivilegesRequired=admin
SetupIconFile={#AppSourceDir}\branding\icon.ico
Compression=lzma2/max
SolidCompression=yes
WizardStyle=modern
OutputDir={#OutputDir}
OutputBaseFilename=TORQUE-Setup
UninstallDisplayIcon={app}\branding\icon.ico
CloseApplications=yes
RestartApplications=no
VersionInfoVersion=1.0.0.0
VersionInfoTextVersion=v1.0
ShowLanguageDialog=no

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"

[Files]
Source: "{#AppSourceDir}\*"; DestDir: "{app}"; Excludes: "*.zip"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{autoprograms}\TORQUE"; Filename: "{app}\TORQUE.exe"; IconFilename: "{app}\branding\icon.ico"
Name: "{autodesktop}\TORQUE"; Filename: "{app}\TORQUE.exe"; IconFilename: "{app}\branding\icon.ico"; Tasks: desktopicon

[Run]
Filename: "{app}\TORQUE.exe"; Description: "{cm:LaunchProgram,TORQUE}"; Flags: nowait postinstall skipifsilent
