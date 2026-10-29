; Instalador de VELY157 Cleaner. Compilar con Inno Setup 6 (ISCC.exe).
; El programa se instala libre, pero no limpia nada hasta que el cliente pone una key válida.

#define AppName "VELY157 Cleaner"
#define AppVersion "1.0.0"
#define AppExe "VELY157 Cleaner.exe"

[Setup]
AppId={{6E0B7C1F-3D5A-4F0E-9A57-157VELY00001}
AppName={#AppName}
AppVersion={#AppVersion}
AppPublisher=Vely Media LLC
AppPublisherURL=https://velymediallc.com
DefaultDirName={autopf}\VELY157 Cleaner
DefaultGroupName=VELY157 Cleaner
DisableProgramGroupPage=yes
PrivilegesRequired=admin
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
OutputBaseFilename=Instalar VELY157 Cleaner
Compression=lzma2/max
SolidCompression=yes
WizardStyle=modern
UninstallDisplayIcon={app}\{#AppExe}
UninstallDisplayName={#AppName}

[Languages]
Name: "es"; MessagesFile: "compiler:Languages\Spanish.isl"

[Tasks]
Name: "desktopicon"; Description: "Crear acceso directo en el escritorio"; GroupDescription: "Accesos directos:"

[Files]
Source: "publish\{#AppExe}"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
Name: "{group}\{#AppName}"; Filename: "{app}\{#AppExe}"
Name: "{group}\Desinstalar {#AppName}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#AppName}"; Filename: "{app}\{#AppExe}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#AppExe}"; Description: "Abrir {#AppName} ahora"; Flags: nowait postinstall skipifsilent shellexec

[UninstallDelete]
Type: filesandordirs; Name: "{localappdata}\VELY157"
