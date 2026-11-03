[CmdletBinding(SupportsShouldProcess)]
param(
    [string]$GameDir = 'C:\Program Files (x86)\Steam\steamapps\common\Slay the Spire 2',
    [string]$LogDirectory = (Join-Path $env:APPDATA 'SlayTheSpire2\Based.Spires2DamageMeter\logs')
)
$ErrorActionPreference = 'Stop'
$root = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../../../..'))
. (Join-Path $PSScriptRoot 'observe.ps1')
if (-not (Get-Process -Name SlayTheSpire2 -ErrorAction SilentlyContinue)) {
    Write-Host 'Skipped: game is not running. No installation or launch attempted.'
    return
}
if (-not $PSCmdlet.ShouldProcess($GameDir, 'Build/check and publish current checkout to running development host')) { return }
$positions = Get-LogPositions $LogDirectory
& (Join-Path $root 'scripts/deploy.ps1') -Development -LiveOnly -GameDir $GameDir
$manifest = Get-Content -LiteralPath (Join-Path $root 'artifacts/development/mods/Based.Spires2DamageMeter/runtime/current.json') -Raw | ConvertFrom-Json
$timer = [Diagnostics.Stopwatch]::StartNew()
do {
    $outcome = Read-ReloadOutcome $LogDirectory $positions $manifest.BuildId
    if ($outcome) { break }
    Start-Sleep -Milliseconds 500
} while ($timer.Elapsed.TotalSeconds -lt 10)
if (-not $outcome) { $outcome = 'published; activation unconfirmed' }
Write-Host "Current working tree build $($manifest.BuildId): $outcome"
