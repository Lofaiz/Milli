; Установщик Милли (версия на C#). Собирается Inno Setup 6:
;   iscc.exe milli.iss
; Ставится только в профиль пользователя, права администратора не нужны.
; Папка ..\publish готовится заранее:
;   dotnet publish -c Release -r win-x64 --self-contained true -o ..\publish
; и в неё копируются model\*.bin, milli.ico и config.json.

#define AppName "Милли"
#define AppVersion "2.0.0"
#define AppExe "Milli.exe"
#define SrcDir AddBackslash(SourcePath) + "..\publish"
#define OutDir SourcePath

[Setup]
; AppId менять нельзя: по нему Windows понимает, что это та же программа
AppId={{7E1E5B62-6C4A-4C3E-9F4B-2D5A2F0C1A77}
AppName={#AppName}
AppVersion={#AppVersion}
AppVerName={#AppName} {#AppVersion}
AppPublisher={#AppName}
VersionInfoVersion={#AppVersion}
DefaultDirName={localappdata}\Programs\Milli
DefaultGroupName={#AppName}
DisableProgramGroupPage=yes
; только текущий пользователь — без запроса администратора
PrivilegesRequired=lowest
OutputDir={#OutDir}
OutputBaseFilename=MilliSetup
SetupIconFile={#OutDir}\milli.ico
UninstallDisplayIcon={app}\{#AppExe}
UninstallDisplayName={#AppName}
Compression=lzma2/ultra64
SolidCompression=yes
WizardStyle=modern
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0
; программу закрывает скрипт ниже, средствами Windows этого делать не нужно
CloseApplications=no
RestartApplications=no

[Languages]
Name: "russian"; MessagesFile: "compiler:Languages\Russian.isl"
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "autostart"; Description: "Запускать {#AppName} при входе в систему"; GroupDescription: "Автозапуск:"; Flags: checkedonce; Languages: russian
Name: "autostart"; Description: "Start {#AppName} when I sign in to Windows"; GroupDescription: "Autostart:"; Flags: checkedonce; Languages: english
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[InstallDelete]
; старый общий README.txt (до версии с двумя языками) больше не нужен
Type: files; Name: "{app}\README.txt"

[Files]
; всё, кроме config.json: его не перезаписываем при обновлении
Source: "{#SrcDir}\*"; Excludes: "config.json"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs
; настройки по умолчанию — только если файла ещё нет; язык программы берём из
; языка установки (русский конфиг или английский)
Source: "{#SrcDir}\config.json"; DestDir: "{app}"; Flags: onlyifdoesntexist; Languages: russian
Source: "{#OutDir}\config.default.en.json"; DestDir: "{app}"; DestName: "config.json"; Flags: onlyifdoesntexist; Languages: english
; инструкция: русская и английская, ссылку в меню «Пуск» выбирает язык установки
Source: "{#OutDir}\README.ru.txt"; DestDir: "{app}"; Flags: ignoreversion; Languages: russian
Source: "{#OutDir}\README.en.txt"; DestDir: "{app}"; Flags: ignoreversion; Languages: english

[Icons]
Name: "{group}\{#AppName}"; Filename: "{app}\{#AppExe}"
Name: "{group}\Как пользоваться"; Filename: "{app}\README.ru.txt"; Languages: russian
Name: "{group}\How to use"; Filename: "{app}\README.en.txt"; Languages: english
Name: "{group}\Удалить {#AppName}"; Filename: "{uninstallexe}"
Name: "{userdesktop}\{#AppName}"; Filename: "{app}\{#AppExe}"; Tasks: desktopicon
Name: "{userstartup}\{#AppName}"; Filename: "{app}\{#AppExe}"; Tasks: autostart

[Run]
Filename: "{app}\{#AppExe}"; Description: "Запустить {#AppName}"; Flags: nowait postinstall skipifsilent; Languages: russian
Filename: "{app}\{#AppExe}"; Description: "Start {#AppName}"; Flags: nowait postinstall skipifsilent; Languages: english

[Code]
// перед установкой закрыть уже запущенную Милли, иначе файлы заняты// перед установкой закрыть уже запущенную Милли, иначе файлы заняты
function PrepareToInstall(var NeedsRestart: Boolean): String;
var
  Code: Integer;
begin
  Exec('taskkill.exe', '/IM {#AppExe} /F', '', SW_HIDE, ewWaitUntilTerminated, Code);
  Sleep(600);
  Result := '';
end;

// и перед удалением тоже, иначе часть файлов останется
procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
var
  Code: Integer;
begin
  if CurUninstallStep = usUninstall then
  begin
    Exec('taskkill.exe', '/IM {#AppExe} /F', '', SW_HIDE, ewWaitUntilTerminated, Code);
    Sleep(600);
  end;
end;
