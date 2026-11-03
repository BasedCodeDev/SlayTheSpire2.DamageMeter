param([string]$Package = (Join-Path (Split-Path $PSScriptRoot -Parent) 'artifacts/development/mods/Based.Spires2DamageMeter'))
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$fixture = Join-Path $root ('artifacts/deploy-check-' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path (Join-Path $fixture 'scripts') -Force | Out-Null
foreach ($name in @('deploy.ps1','runtime-package.ps1','assert-safe-mod-version.ps1')) {
    Copy-Item -LiteralPath (Join-Path $root "scripts/$name") -Destination (Join-Path $fixture 'scripts')
}
@'
param([string]$Sts2DataDir,[switch]$Development)
Set-Content -LiteralPath (Join-Path $PSScriptRoot '../package-called.txt') -Value 'called'
'@ | Set-Content -LiteralPath (Join-Path $fixture 'scripts/package.ps1')
Copy-Item -LiteralPath (Join-Path $root 'Based.Spires2DamageMeter.csproj'),(Join-Path $root 'Based.Spires2DamageMeter.json') -Destination $fixture
foreach ($directory in @('artifacts/development/mods/Based.Spires2DamageMeter','artifacts/mods/Based.Spires2DamageMeter')) {
    $copy = Join-Path $fixture $directory
    New-Item -ItemType Directory -Path $copy -Force | Out-Null
    Get-ChildItem -LiteralPath $Package | Copy-Item -Destination $copy -Recurse -Force
}
$game = Join-Path $fixture 'game'
New-Item -ItemType Directory -Path $game -Force | Out-Null
Set-Content -LiteralPath (Join-Path $game 'SlayTheSpire2.exe') -Value 'fixture only'
$destination = Join-Path $game 'mods/Based.Spires2DamageMeter'
$deploy = Join-Path $fixture 'scripts/deploy.ps1'
. (Join-Path $fixture 'scripts/runtime-package.ps1')
$global:DamageMeterFixtureRunning = $true
$global:DamageMeterFixtureLaunches = 0
function Get-Process { param([string]$Name) if ($Name -eq 'SlayTheSpire2' -and $global:DamageMeterFixtureRunning) { [pscustomobject]@{ Id = -1 } } }
function Start-Process { param([string]$FilePath) $global:DamageMeterFixtureLaunches++ }
function Check([bool]$Condition, [string]$Name) { if (-not $Condition) { throw $Name }; Write-Output "PASS $Name" }
function Reject([scriptblock]$Action, [string]$Match, [string]$Name) {
    $rejected = $false
    try { & $Action } catch { if ($_.Exception.Message -match $Match) { $rejected = $true } else { throw } }
    Check $rejected $Name
}
try {
    & $deploy -Development -GameDir $game -WhatIf
    Check (-not (Test-Path -LiteralPath (Join-Path $fixture 'package-called.txt'))) 'WhatIf does not build or publish'
    Reject { & $deploy -GameDir $game } 'Close Slay' 'normal deployment refuses a running game'
    Reject { & $deploy -Development -GameDir $game } 'No development host' 'first development installation requires closing the game'
    Check (-not (Test-Path -LiteralPath $destination)) 'rejected installation does not create installed mod files'
    New-Item -ItemType Directory -Path $destination -Force | Out-Null
    Get-ChildItem -LiteralPath $Package | Copy-Item -Destination $destination -Recurse -Force
    $fixtureHostDll = Join-Path $destination 'Based.Spires2DamageMeter.dll'
    $hostHash = (Get-FileHash -LiteralPath $fixtureHostDll -Algorithm SHA256).Hash
    & $deploy -Development -GameDir $game
    Check ($global:DamageMeterFixtureLaunches -eq 0) 'running development deployment does not launch or close the game'
    Check ((Get-FileHash -LiteralPath $fixtureHostDll -Algorithm SHA256).Hash -eq $hostHash) 'running development deployment leaves host DLL unchanged'
    $pointer = Join-Path $destination 'runtime/current.json'
    $before = Get-Content -LiteralPath $pointer -Raw
    & $deploy -Development -GameDir $game
    Check ((Get-Content -LiteralPath $pointer -Raw) -eq $before) 'duplicate build publication preserves content'
    Add-Content -LiteralPath $fixtureHostDll -Value 'changed host'
    Reject { & $deploy -Development -GameDir $game } 'Host or shared state changed' 'running deployment rejects an incompatible host'
    Copy-Item -LiteralPath (Join-Path $Package 'Based.Spires2DamageMeter.dll') -Destination $fixtureHostDll -Force
    $corruptPackage = Join-Path $fixture 'corrupt-package'
    New-Item -ItemType Directory -Path $corruptPackage -Force | Out-Null
    Copy-Item -LiteralPath (Join-Path $Package 'Based.Spires2DamageMeter.dll'),(Join-Path $Package 'Based.Spires2DamageMeter.Contracts.dll') -Destination $corruptPackage
    $payload = Join-Path $fixture 'new-payload'
    New-Item -ItemType Directory -Path $payload -Force | Out-Null
    $sourceManifest = Get-Content -LiteralPath (Join-Path $Package 'runtime/current.json') -Raw | ConvertFrom-Json
    Copy-Item -LiteralPath (Join-Path $Package ('runtime/builds/' + $sourceManifest.BuildId + '/Based.Spires2DamageMeter.Runtime.dll')) -Destination $payload
    Add-Content -LiteralPath (Join-Path $payload 'Based.Spires2DamageMeter.Runtime.dll') -Value 'new fixture revision'
    $manifest = New-RuntimeManifest $corruptPackage $payload $sourceManifest.Version
    $build = Join-Path $corruptPackage ('runtime/builds/' + $manifest.BuildId)
    New-Item -ItemType Directory -Path $build -Force | Out-Null
    Copy-Item -LiteralPath (Join-Path $payload 'Based.Spires2DamageMeter.Runtime.dll') -Destination $build
    Write-RuntimeManifest $manifest (Join-Path $corruptPackage 'runtime/current.json')
    Add-Content -LiteralPath (Join-Path $build 'Based.Spires2DamageMeter.Runtime.dll') -Value 'partial copy'
    Reject { Publish-RuntimeBuild $corruptPackage $destination } 'hash mismatch' 'incomplete runtime publication is rejected'
    Check ((Get-Content -LiteralPath $pointer -Raw) -eq $before) 'failed publication leaves the previous pointer untouched'
    $global:DamageMeterFixtureRunning = $false
    & $deploy -Development -GameDir $game
    Check ($global:DamageMeterFixtureLaunches -eq 1) 'closed-game development installation requests one launch'
    & $deploy -GameDir $game
    Check ($global:DamageMeterFixtureLaunches -eq 2 -and -not (Test-Path -LiteralPath (Join-Path $destination 'development-host.json'))) 'closed-game normal deployment removes development marker and launches once'
} finally {
    Remove-Variable -Name DamageMeterFixtureRunning,DamageMeterFixtureLaunches -Scope Global -ErrorAction SilentlyContinue
}
