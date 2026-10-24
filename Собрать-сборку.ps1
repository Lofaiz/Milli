# Сборка C#-версии Милли: публикация приложения, упаковка моделей распознавания
# и сборка установщика MilliSetup.exe (Inno Setup).
#
# Запуск:
#   powershell -ExecutionPolicy Bypass -File "Собрать-сборку.ps1"
#   powershell -ExecutionPolicy Bypass -File "Собрать-сборку.ps1" -NoInstaller
#
# Что нужно на машине сборки:
#   * .NET SDK 10 (dotnet);
#   * Inno Setup 6 (iscc.exe) — только если собирается установщик;
#   * модели распознавания: скрипт скачает недостающие сам.

[CmdletBinding()]
param(
    [switch]$NoInstaller,          # только собрать приложение, без установщика
    [switch]$KeepRunning           # не закрывать уже запущенную Милли
)

$ErrorActionPreference = 'Stop'

$Root      = Split-Path -Parent $MyInvocation.MyCommand.Path
$Proj      = Join-Path $Root 'MilliCs\MilliCs.csproj'
$Pub       = Join-Path $Root 'publish'
$ModelDir  = Join-Path $Root 'model'
$InstDir   = Join-Path $Root 'installer'
$Iss       = Join-Path $InstDir 'milli.iss'
$OutSetup  = Join-Path $InstDir 'MilliSetup.exe'
$Desktop   = [Environment]::GetFolderPath('Desktop')

# версию установщика держим тут же, в milli.iss — здесь только напоминание
$BaseModel  = 'ggml-base-q5_1.bin'
$SmallModel = 'ggml-small-q5_1.bin'
$HF = 'https://huggingface.co/ggerganov/whisper.cpp/resolve/main'

function Head($text) { Write-Host ''; Write-Host ('=== ' + $text + ' ===') -ForegroundColor Cyan }
function Info($text) { Write-Host ('    ' + $text) }
function Good($text) { Write-Host ('    ' + $text) -ForegroundColor Green }
function Bad($text)  { Write-Host ('    ' + $text) -ForegroundColor Red }

# ------------------------------------------------------------- проверки
Head 'Проверки'

$dotnet = (Get-Command dotnet -ErrorAction SilentlyContinue)
if (-not $dotnet) { Bad 'не найден dotnet. Поставьте .NET SDK 10 и повторите.'; exit 1 }
Good ('dotnet: ' + $dotnet.Source)

if (-not (Test-Path $Proj)) { Bad ('не найден проект: ' + $Proj); exit 1 }

$iscc = $null
if (-not $NoInstaller) {
    $cands = @(
        (Join-Path $env:LOCALAPPDATA 'Programs\Inno Setup 6\ISCC.exe'),
        'C:\Program Files (x86)\Inno Setup 6\ISCC.exe',
        'C:\Program Files\Inno Setup 6\ISCC.exe'
    )
    foreach ($c in $cands) { if (Test-Path $c) { $iscc = $c; break } }
    if (-not $iscc) { Bad 'не найден ISCC.exe (Inno Setup 6). Установите или запустите с -NoInstaller.'; exit 1 }
    Good ('Inno Setup: ' + $iscc)
}

# ------------------------------------------------------- модели речи
Head 'Модели распознавания'
if (-not (Test-Path $ModelDir)) { New-Item -ItemType Directory -Force -Path $ModelDir | Out-Null }
$pick = (Get-Command dotnet)
foreach ($m in @($BaseModel, $SmallModel)) {
    $path = Join-Path $ModelDir $m
    if (Test-Path $path) {
        $mb = [Math]::Round((Get-Item $path).Length / 1MB, 1)
        Good ($m + ' — на месте (' + $mb + ' МБ)')
    } else {
        Info ('качаю ' + $m + '...')
        try {
            Invoke-WebRequest ($HF + '/' + $m) -OutFile $path -TimeoutSec 1800
            $mb = [Math]::Round((Get-Item $path).Length / 1MB, 1)
            Good ($m + ' — скачана (' + $mb + ' МБ)')
        } catch {
            Bad ('не удалось скачать ' + $m + ': ' + $_.Exception.Message)
            Bad ('возьмите файл вручную: ' + $HF + '/' + $m)
            exit 1
        }
    }
}

# ------------------------------------------------- закрыть запущенную
Head 'Запущенная Милли'
if ($KeepRunning) {
    Info 'не трогаю (-KeepRunning)'
} else {
    $killed = 0
    Get-Process Milli -ErrorAction SilentlyContinue | Where-Object { $_.Path -like ($Root + '*') } | ForEach-Object {
        Stop-Process -Id $_.Id -Force
        $killed++
    }
    if ($killed -gt 0) { Start-Sleep -Seconds 2; Good ('закрыто процессов: ' + $killed) } else { Info 'не запущена' }
}

# ------------------------------------------------------------ публикация
Head 'Публикация'
if (Test-Path $Pub) { Remove-Item $Pub -Recurse -Force }
& dotnet publish $Proj -c Release -r win-x64 --self-contained true -o $Pub -v q --nologo
if ($LASTEXITCODE -ne 0) { Bad 'сборка не удалась'; exit 1 }

# лишнее: нативные библиотеки других платформ и отладочные символы
Remove-Item (Join-Path $Pub 'runtimes\win-arm64'), (Join-Path $Pub 'runtimes\win-x86') -Recurse -Force -ErrorAction SilentlyContinue
Remove-Item (Join-Path $Pub 'Milli.pdb') -Force -ErrorAction SilentlyContinue

# модели, значок, настройки по умолчанию
$pubModel = Join-Path $Pub 'model'
New-Item -ItemType Directory -Force -Path $pubModel | Out-Null
Copy-Item (Join-Path $ModelDir '*.bin') $pubModel -Force
Copy-Item (Join-Path $InstDir 'milli.ico') $Pub -Force

$defaultCfg = Join-Path $InstDir 'config.default.json'
if (Test-Path $defaultCfg) { Copy-Item $defaultCfg (Join-Path $Pub 'config.json') -Force }
else { Bad 'нет installer\config.default.json — настройки по умолчанию не попали в сборку' }

$size = [Math]::Round((Get-ChildItem $Pub -Recurse -File | Measure-Object -Property Length -Sum).Sum / 1MB, 1)
Good ('приложение собрано: ' + $size + ' МБ')

if ($NoInstaller) {
    Head 'Готово'
    Good ('папка сборки: ' + $Pub)
    exit 0
}

# ------------------------------------------------------------ установщик
Head 'Установщик'
if (Test-Path $OutSetup) { Remove-Item $OutSetup -Force }
& $iscc $Iss
if ($LASTEXITCODE -ne 0 -or -not (Test-Path $OutSetup)) { Bad 'Inno Setup не собрал установщик'; exit 1 }
$setupMb = [Math]::Round((Get-Item $OutSetup).Length / 1MB, 1)
Good ('установщик готов: ' + $OutSetup + ' (' + $setupMb + ' МБ)')

# копия на «Рабочий стол» — оттуда легко запустить или отдать другу
$outCopy = Join-Path $Desktop 'MilliSetup.exe'
Copy-Item $OutSetup $outCopy -Force
$sha = (Get-FileHash $outCopy -Algorithm SHA256).Hash
Good ('копия: ' + $outCopy)
Info ('SHA256: ' + $sha)

Head 'Готово'
Info 'Установка: запустить MilliSetup.exe, подтвердить — права администратора не нужны.'
Info 'Ставится в %LOCALAPPDATA%\Programs\Milli, ярлыки: меню «Пуск», рабочий стол, автозапуск.'
