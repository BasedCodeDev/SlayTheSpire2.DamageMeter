[CmdletBinding(SupportsShouldProcess)]
param(
    [string]$GameDir = 'C:\Program Files (x86)\Steam\steamapps\common\Slay the Spire 2',
    [switch]$Development
)

$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'assert-safe-mod-version.ps1')
$modName = 'Based.Spires2DamageMeter'
$projectRoot = Split-Path $PSScriptRoot -Parent
$packageRoot = if ($Development) { 'artifacts\development' } else { 'artifacts' }
$source = Join-Path $projectRoot "$packageRoot\mods\$modName"
$destination = Join-Path $GameDir "mods\$modName"

$sourceVersion = (Get-Content -LiteralPath (Join-Path $projectRoot "$modName.json") -Raw | ConvertFrom-Json).version
Assert-SafeModVersion $sourceVersion
[xml]$sourceProject = Get-Content -LiteralPath (Join-Path $projectRoot "$modName.csproj") -Raw
Assert-SafeModVersion ([string]$sourceProject.Project.PropertyGroup.Version | Where-Object { $_ })

if (-not $Development -and (Get-Process -Name SlayTheSpire2 -ErrorAction SilentlyContinue)) {
    throw 'Close Slay the Spire 2 before deploying the mod.'
}
if (-not (Test-Path -LiteralPath (Join-Path $GameDir 'SlayTheSpire2.exe') -PathType Leaf)) {
    throw "Game executable not found in: $GameDir"
}
if ($PSCmdlet.ShouldProcess($destination, 'Build, check, package, deploy and launch the current damage meter')) {
    & (Join-Path $PSScriptRoot 'package.ps1') -Sts2DataDir (Join-Path $GameDir 'data_sts2_windows_x86_64') -Development:$Development
    . (Join-Path $PSScriptRoot 'runtime-package.ps1')
    $version = (Get-Content -LiteralPath (Join-Path $projectRoot "$modName.json") -Raw | ConvertFrom-Json).version
    $packagedVersion = (Get-Content -LiteralPath (Join-Path $source "$modName.json") -Raw | ConvertFrom-Json).version
    $runtime = Get-Content -LiteralPath (Join-Path $source 'runtime/current.json') -Raw | ConvertFrom-Json
    $dllVersion = ([Diagnostics.FileVersionInfo]::GetVersionInfo((Join-Path $source ('runtime/builds/' + $runtime.BuildId + '/Based.Spires2DamageMeter.Runtime.dll')))).ProductVersion.Split('+')[0]
    foreach ($checkedVersion in @($version,$packagedVersion,$dllVersion)) { Assert-SafeModVersion $checkedVersion }
    if ($version -ne $packagedVersion -or $version -ne $dllVersion) {
        throw "Version mismatch: source=$version, package=$packagedVersion, DLL=$dllVersion. Deployment stopped."
    }
    if (Get-Process -Name SlayTheSpire2 -ErrorAction SilentlyContinue) {
        if (-not $Development) { throw 'The game was opened during the build. Close it and rerun deployment.' }
        Assert-InstalledDevelopmentHost $source $destination
        $build = Publish-RuntimeBuild $source $destination
        Write-Host "Published development runtime $version ($build). The running host will detect it immediately."
        Write-Host 'Check the meter diagnostics/game log for activation or rejection. No game launch was requested.'
        return
    }
    New-Item -ItemType Directory -Path $destination -Force | Out-Null
    Get-ChildItem -LiteralPath $source | Copy-Item -Destination $destination -Recurse -Force
    if (-not $Development -and (Test-Path -LiteralPath (Join-Path $destination 'development-host.json'))) { Remove-Item -LiteralPath (Join-Path $destination 'development-host.json') }
    foreach ($file in @("$modName.dll", "$modName.Contracts.dll", "$modName.json")) {
        if ((Get-FileHash -LiteralPath (Join-Path $source $file)).Hash -ne (Get-FileHash -LiteralPath (Join-Path $destination $file)).Hash) {
            throw "Installed $file does not match the package. Game launch stopped."
        }
    }
    Write-Host "Deployed version $version to: $destination"
    Start-Process -FilePath 'steam://rungameid/2868840'
    Write-Host 'Game launch requested through Steam. Enable the mod in its mod controls if needed.'
}
