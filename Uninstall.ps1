param([string]$GameDir)

$ErrorActionPreference = 'Stop'
Import-Module Microsoft.PowerShell.Utility -ErrorAction Stop
$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$locationFile = Join-Path $root 'location.txt'
if (-not $GameDir) {
    if (-not (Test-Path -LiteralPath $locationFile)) {
        Write-Error 'location.txt is missing. Reinstall the mod or run Uninstall.ps1 with -GameDir to specify the game folder.'
        exit 1
    }
    $GameDir = (Get-Content -LiteralPath $locationFile -Raw).Trim()
    Write-Host "Using saved game location: `"$GameDir`""
}
try {
    $game = (Resolve-Path -LiteralPath $GameDir.Trim('"')).Path
    $installDir = Join-Path $game 'DirectCombat'
    $manifestPath = Join-Path $installDir 'installed.json'
    if (-not (Test-Path -LiteralPath $manifestPath)) { throw 'Install manifest is missing; leaving files untouched.' }
    $manifest = Get-Content -LiteralPath $manifestPath -Raw | ConvertFrom-Json
    $pairs = @(
        @((Join-Path $game 'System\Scripts\testtools.lua'), $manifest.LuaHash),
        @((Join-Path $installDir 'DirectCombatController.exe'), $manifest.ControllerHash),
        @((Join-Path $installDir 'Play.cmd'), $manifest.PlayHash)
    )
    foreach ($pair in $pairs) {
        if (-not $pair[1]) { throw 'Install manifest is incomplete; leaving files untouched.' }
        if ((Test-Path -LiteralPath $pair[0]) -and
            (Get-FileHash -LiteralPath $pair[0] -Algorithm SHA256).Hash -ne $pair[1]) {
            throw "Installed file changed; leaving it untouched: $($pair[0])"
        }
    }
    while (Get-Process -Name witcher -ErrorAction SilentlyContinue) {
        Write-Host 'Witcher is still running. Save and fully exit the game to continue uninstalling.'
        Read-Host 'Press Enter after Witcher closes' | Out-Null
    }
    $controllerPath = Join-Path $installDir 'DirectCombatController.exe'
    $bridges = @(Get-CimInstance Win32_Process -Filter "name = 'DirectCombatController.exe'" |
        Where-Object { $_.ExecutablePath -eq $controllerPath })
    foreach ($bridge in $bridges) { Stop-Process -Id $bridge.ProcessId -ErrorAction Stop }
    if ($bridges.Count) {
        Wait-Process -Id @($bridges | ForEach-Object { $_.ProcessId }) -Timeout 5 -ErrorAction SilentlyContinue
        $stillRunning = @(Get-CimInstance Win32_Process -Filter "name = 'DirectCombatController.exe'" |
            Where-Object { $_.ExecutablePath -eq $controllerPath })
        if ($stillRunning.Count) { throw 'The controller bridge did not stop; leaving files untouched.' }
    }
    foreach ($pair in $pairs) { if (Test-Path -LiteralPath $pair[0]) { Remove-Item -LiteralPath $pair[0] } }
    Remove-Item -LiteralPath $manifestPath
    if ((Test-Path -LiteralPath $installDir) -and -not (Get-ChildItem -LiteralPath $installDir -Force)) { Remove-Item -LiteralPath $installDir }
    if (Test-Path -LiteralPath $locationFile) {
        $recordedGame = (Get-Content -LiteralPath $locationFile -Raw).Trim()
        if ($recordedGame -eq $game) { Remove-Item -LiteralPath $locationFile }
    }
    Write-Host "Removed Witcher Direct Combat from $game"
} catch {
    Write-Error $_
    exit 1
}
