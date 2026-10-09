; Builds DailyPlannerSetup.exe (run by GitHub Actions)
#ifndef AppVersion
  #define AppVersion "1.0.0"
#endif

[Setup]
AppId={{6B0E3C4A-7D51-4E57-9C8B-2F8D1A3E5B11}
AppName=Daily Planner
AppVersion={#AppVersion}
AppPublisher=Daily Planner
AppPublisherURL=https://aidynmcdaniel24-alt.github.io/Daily-planner/
DefaultDirName={localappdata}\Programs\Daily Planner
DefaultGroupName=Daily Planner
DisableProgramGroupPage=yes
PrivilegesRequired=lowest
OutputDir=..\out
OutputBaseFilename=DailyPlannerSetup
SetupIconFile=..\src\DailyPlanner\Assets\AppIcon.ico
UninstallDisplayIcon={app}\DailyPlanner.exe
Compression=lzma2/max
SolidCompression=yes
WizardStyle=modern
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible

[Tasks]
Name: "desktopicon"; Description: "Create a desktop shortcut"; GroupDescription: "Shortcuts:"

[Files]
Source: "..\publish\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\Daily Planner"; Filename: "{app}\DailyPlanner.exe"
Name: "{userdesktop}\Daily Planner"; Filename: "{app}\DailyPlanner.exe"; Tasks: desktopicon

[Run]
Filename: "{app}\DailyPlanner.exe"; Description: "Open Daily Planner"; Flags: nowait postinstall skipifsilent
