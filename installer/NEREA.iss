; NEREA - Premium Native Windows Music Player & Living Aquatic Worlds
; Inno Setup script. Built by .github/workflows/build-windows.yml with:
;   ISCC /DAppVersion=x.y.z /DPublishDir=<publish folder> installer\NEREA.iss

#ifndef AppVersion
#define AppVersion "1.0.0"
#endif

#ifndef PublishDir
#define PublishDir "..\publish"
#endif

[Setup]
AppId={{6C0F3D2A-4E1B-4C5A-9D3E-8A2B1F4C5D6E}
AppName=NEREA
AppVersion={#AppVersion}
AppPublisher=NEREA
AppComments=Let the music create the world.
AppCopyright=Copyright (c) NEREA contributors. MIT licensed.
DefaultDirName={autopf}\NEREA
DefaultGroupName=NEREA
DisableProgramGroupPage=yes
UninstallDisplayIcon={app}\NEREA.exe
SetupIconFile=..\src\NEREA.App\Assets\NEREA.ico
WizardStyle=modern
Compression=lzma2/max
SolidCompression=yes
ArchitecturesInstallIn64BitMode=x64compatible
ArchitecturesAllowed=x64compatible
PrivilegesRequired=admin
MinVersion=10.0

[Tasks]
Name: "desktopicon"; Description: "Create a &desktop shortcut"; GroupDescription: "Shortcuts:"; Flags: unchecked

[Files]
Source: "{#PublishDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\NEREA"; Filename: "{app}\NEREA.exe"
Name: "{autodesktop}\NEREA"; Filename: "{app}\NEREA.exe"; Tasks: desktopicon

[Run]
Filename: "{app}\NEREA.exe"; Description: "Launch NEREA"; Flags: nowait postinstall skipifsilent

[UninstallDelete]
; Application logs only - the music library and aquariums are kept for reinstall.
Type: filesandordirs; Name: "{localappdata}\NEREA\logs"

[Messages]
WelcomeLabel2=This will install NEREA on your computer.%n%nA living aquatic world that breathes with your music.
