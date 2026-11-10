param(
    [string]$GameDir,
    [ValidateSet('Unsigned', 'Compile')][string]$ControllerBuild
)

$ErrorActionPreference = 'Stop'
Import-Module Microsoft.PowerShell.Utility -ErrorAction Stop
$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$payload = Join-Path $root 'Payload'
$controllerSource = Join-Path $payload 'DirectCombatController.cs'
$controllerBinary = Join-Path $payload 'DirectCombatController.exe'
$luaSource = Join-Path $payload 'testtools.lua'
$playSource = Join-Path $payload 'Play.cmd'
$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$interactiveInstall = [string]::IsNullOrWhiteSpace($GameDir)
$locationFile = Join-Path $root 'location.txt'

function Normalize-GamePath([string]$path) {
    $clean = $path.Trim()
    if ($clean.Length -ge 2 -and $clean.StartsWith('"') -and $clean.EndsWith('"')) {
        $clean = $clean.Substring(1, $clean.Length - 2)
    }
    return $clean.Trim()
}

function Find-GameDirectory {
    if ($GameDir) { return (Normalize-GamePath $GameDir) }
    Write-Host 'Where is The Witcher: Enhanced Edition installed?'
    do {
        $choice = (Read-Host 'Enter P to paste its folder path, or S to search for the game [P]').Trim()
        if (-not $choice) { $choice = 'P' }
    } until ($choice -match '^[SsPp]$')
    if ($choice -match '^[Pp]$') {
        return (Normalize-GamePath (Read-Host 'Paste the full path to The Witcher Enhanced Edition folder (quotes are optional)'))
    }
    Write-Host 'Searching common Steam library locations...'
    $candidates = @()
    if (${env:ProgramFiles(x86)}) { $candidates += (Join-Path ${env:ProgramFiles(x86)} 'Steam\steamapps\common\The Witcher Enhanced Edition') }
    if ($env:ProgramFiles) { $candidates += (Join-Path $env:ProgramFiles 'Steam\steamapps\common\The Witcher Enhanced Edition') }
    foreach ($drive in (Get-PSDrive -PSProvider FileSystem)) {
        $candidates += (Join-Path $drive.Root 'SteamLibrary\steamapps\common\The Witcher Enhanced Edition')
        $candidates += (Join-Path $drive.Root 'games\Steam\steamapps\common\The Witcher Enhanced Edition')
    }
    foreach ($candidate in $candidates) {
        if (Test-Path -LiteralPath (Join-Path $candidate 'System\witcher.exe')) {
            Write-Host "Found: $candidate"
            return $candidate
        }
    }
    Write-Host 'The game was not found in the searched locations.'
    return (Normalize-GamePath (Read-Host 'Paste the full path to The Witcher Enhanced Edition folder (quotes are optional)'))
}

try {
    foreach ($file in @($controllerSource, $luaSource, $playSource)) {
        if (-not (Test-Path -LiteralPath $file)) { throw "Missing package file: $file" }
    }
    $chosenPath = Find-GameDirectory
    if (-not (Test-Path -LiteralPath $chosenPath)) { throw "Game folder not found: `"$chosenPath`"" }
    $game = (Resolve-Path -LiteralPath $chosenPath).Path
    if (-not (Test-Path -LiteralPath (Join-Path $game 'System\witcher.exe'))) {
        throw "That folder does not contain System\witcher.exe: `"$game`""
    }
    Write-Host "Verified Witcher game folder: `"$game`""
    if ($interactiveInstall) {
        $confirm = (Read-Host 'Install to this folder? [Y/n]').Trim()
        if ($confirm -and $confirm -notmatch '^[Yy]$') { throw 'Installation cancelled before any game files were changed.' }
    }
    if (-not $ControllerBuild) {
        if ($interactiveInstall) {
            do {
                $choice = (Read-Host 'Controller: U to use the bundled unsigned executable, or C to compile from source [U]').Trim()
                if (-not $choice) { $choice = 'U' }
            } until ($choice -match '^[UuCc]$')
            $ControllerBuild = if ($choice -match '^[Cc]$') { 'Compile' } else { 'Unsigned' }
        } else {
            $ControllerBuild = 'Unsigned'
        }
    }
    if ($ControllerBuild -eq 'Compile' -and -not (Test-Path -LiteralPath $compiler)) {
        throw 'Windows .NET Framework C# compiler is missing; choose the bundled unsigned executable instead.'
    }
    if ($ControllerBuild -eq 'Unsigned' -and -not (Test-Path -LiteralPath $controllerBinary)) {
        throw 'The bundled controller executable is missing; choose Compile or download the complete release package.'
    }
    if (Get-Process -Name witcher -ErrorAction SilentlyContinue) { throw 'Close Witcher completely before installing.' }
    $installedController = Join-Path $game 'DirectCombat\DirectCombatController.exe'
    $runningBridge = @(Get-CimInstance Win32_Process -Filter "name = 'DirectCombatController.exe'" |
        Where-Object { $_.ExecutablePath -eq $installedController })
    if ($runningBridge.Count) { throw 'Close this installation of DirectCombatController before installing.' }
    $luaTarget = Join-Path $game 'System\Scripts\testtools.lua'
    if (Test-Path -LiteralPath $luaTarget) {
        $existing = (Get-FileHash -LiteralPath $luaTarget -Algorithm SHA256).Hash
        $ours = (Get-FileHash -LiteralPath $luaSource -Algorithm SHA256).Hash
        if ($existing -ne $ours) { throw 'Another testtools.lua is installed. This installer will not overwrite it.' }
    }
    $compiled = $null
    try {
        if ($ControllerBuild -eq 'Compile') {
            $compiled = Join-Path $env:TEMP ('DirectCombatController-' + [guid]::NewGuid().ToString('N') + '.exe')
            & $compiler /nologo /target:exe /platform:x64 "/out:$compiled" $controllerSource
            if ($LASTEXITCODE -ne 0 -or -not (Test-Path -LiteralPath $compiled)) { throw 'Controller source compilation failed.' }
            $controllerToInstall = $compiled
        } else {
            $controllerToInstall = $controllerBinary
        }
        $installDir = Join-Path $game 'DirectCombat'
        New-Item -ItemType Directory -Path $installDir -Force | Out-Null
        $exeTarget = Join-Path $installDir 'DirectCombatController.exe'
        Copy-Item -LiteralPath $controllerToInstall -Destination $exeTarget -Force
    } finally {
        if ($compiled -and (Test-Path -LiteralPath $compiled)) { Remove-Item -LiteralPath $compiled }
    }
    Copy-Item -LiteralPath $playSource -Destination (Join-Path $installDir 'Play.cmd') -Force
    Copy-Item -LiteralPath $luaSource -Destination $luaTarget -Force
    $manifest = [pscustomobject]@{
        ControllerHash = (Get-FileHash -LiteralPath $exeTarget -Algorithm SHA256).Hash
        PlayHash = (Get-FileHash -LiteralPath (Join-Path $installDir 'Play.cmd') -Algorithm SHA256).Hash
        LuaHash = (Get-FileHash -LiteralPath $luaTarget -Algorithm SHA256).Hash
    }
    $manifest | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $installDir 'installed.json') -Encoding UTF8
    Set-Content -LiteralPath $locationFile -Value $game -Encoding UTF8 -NoNewline
    Write-Host "Installed Witcher Direct Combat to $game"
    Write-Host "Controller installation method: $ControllerBuild"
    Write-Host "Saved game location for uninstall to $locationFile"
    Write-Host "Double-click $installDir\Play.cmd to start the controller bridge and game."
} catch {
    Write-Error $_
    exit 1
}
