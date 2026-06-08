; ══════════════════════════════════════════════════════════════════════════
;  TradingJournal — Script de instalador (Inno Setup 6)
;  Versión: 0.1.0 Beta
;  Generado automáticamente
; ══════════════════════════════════════════════════════════════════════════

#define AppName      "TradingJournal"
#define AppVersion   "0.1.0"
#define AppPublisher "TradingJournal"
#define AppExe       "Application.WPF.exe"
#define AppId        "{{A3F7C2D1-8B4E-4F9A-BC12-3D6E5A7F8901}"
#define SourceDir    "publish"
#define OutputDir    "output"

[Setup]
AppId={#AppId}
AppName={#AppName}
AppVersion={#AppVersion}
AppPublisherURL=https://github.com/yarojo8327/TradingJournal
AppSupportURL=https://github.com/yarojo8327/TradingJournal
AppUpdatesURL=https://github.com/yarojo8327/TradingJournal
DefaultDirName={autopf}\{#AppName}
DefaultGroupName={#AppName}
AllowNoIcons=yes
LicenseFile=
PrivilegesRequired=lowest
PrivilegesRequiredOverridesAllowed=dialog
OutputDir={#OutputDir}
OutputBaseFilename=TradingJournal-Setup-v{#AppVersion}-beta
Compression=lzma2/ultra64
SolidCompression=yes
WizardStyle=modern
WizardResizable=yes
; Runtime .NET 7 ya incluido (self-contained)
ArchitecturesInstallIn64BitMode=x64
ArchitecturesAllowed=x64

; ── Colores del wizard (tema oscuro) ──────────────────────────────────────
WizardSizePercent=100

[Languages]
Name: "spanish"; MessagesFile: "compiler:Languages\Spanish.isl"

[CustomMessages]
spanish.LaunchAfterInstall=Iniciar TradingJournal ahora
spanish.BetaWarning=Esta es una versión BETA. Puede contener errores.%nLa base de datos se guardará en:%n  %%APPDATA%%\TradingJournal\

[Tasks]
Name: "desktopicon";    Description: "{cm:CreateDesktopIcon}";        GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked
Name: "startmenuicon";  Description: "Crear acceso en Menú Inicio";   GroupDescription: "{cm:AdditionalIcons}"; Flags: checkedonce
Name: "launchapp";      Description: "{cm:LaunchAfterInstall}";       GroupDescription: "Final:"; Flags: checkedonce

[Files]
; Todos los archivos de la publicación (self-contained .NET 7)
Source: "{#SourceDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
; Menú Inicio
Name: "{group}\{#AppName}";                     Filename: "{app}\{#AppExe}"; Comment: "Aplicación de bitácora de trading"
Name: "{group}\Desinstalar {#AppName}";         Filename: "{uninstallexe}"

; Escritorio (opcional)
Name: "{autodesktop}\{#AppName}";               Filename: "{app}\{#AppExe}"; Tasks: desktopicon; Comment: "Aplicación de bitácora de trading"

[Run]
; Lanzar la app al terminar la instalación
Filename: "{app}\{#AppExe}"; Description: "{cm:LaunchAfterInstall}"; Flags: nowait postinstall skipifsilent; Tasks: launchapp

[UninstallDelete]
; Al desinstalar, NO borrar la base de datos del usuario (en %AppData%)
; Solo se eliminan los archivos del directorio de instalación
Type: filesandordirs; Name: "{app}"

[Code]
// ── Mostrar advertencia beta antes de instalar ────────────────────────────
procedure InitializeWizard();
begin
  MsgBox(ExpandConstant('{cm:BetaWarning}'), mbInformation, MB_OK);
end;

// ── Verificar que la arquitectura sea x64 ────────────────────────────────
function InitializeSetup(): Boolean;
begin
  if not Is64BitInstallMode then
  begin
    MsgBox('TradingJournal requiere Windows 64-bit (x64).', mbError, MB_OK);
    Result := False;
  end
  else
    Result := True;
end;
