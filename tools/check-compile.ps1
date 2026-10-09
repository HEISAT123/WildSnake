# Batch-компиляция Unity. Путь к Unity.exe: переменная окружения UNITY_EXE
# (например C:\Program Files\Unity\Hub\Editor\6000.0.x\Editor\Unity.exe).
$proj = Split-Path -Parent $PSScriptRoot
if (-not $env:UNITY_EXE -or -not (Test-Path $env:UNITY_EXE)) { Write-Output "UNITY_EXE не задан или путь неверный - проверка компиляции пропущена"; exit 0 }
if (-not (Test-Path "$proj\ProjectSettings\ProjectVersion.txt")) { Write-Output "Unity-проект ещё не создан - пропуск"; exit 0 }
if (Test-Path "$proj\Temp\UnityLockfile") { Write-Output "Редактор открыт (проект заблокирован) - используй консоль через MCP"; exit 0 }
$log = "$env:TEMP\unity-compile.log"
& $env:UNITY_EXE -batchmode -nographics -quit -projectPath $proj -logFile $log
$code = $LASTEXITCODE
Select-String -Path $log -Pattern 'error CS\d+' | Select-Object -ExpandProperty Line -Unique | Select-Object -First 30
if ($code -ne 0) { Write-Output "Unity exit code $code"; exit 2 }
Write-Output "Компиляция OK"
