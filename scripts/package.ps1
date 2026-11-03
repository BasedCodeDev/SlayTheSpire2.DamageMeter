param([string]$Sts2DataDir = 'C:/Program Files (x86)/Steam/steamapps/common/Slay the Spire 2/data_sts2_windows_x86_64')
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'assert-safe-mod-version.ps1')
$projectRoot = Split-Path $PSScriptRoot -Parent
Push-Location $projectRoot
try {
    $version = (Get-Content -LiteralPath 'Based.Spires2DamageMeter.json' -Raw | ConvertFrom-Json).version
    Assert-SafeModVersion $version
    [xml]$project = Get-Content -LiteralPath 'Based.Spires2DamageMeter.csproj' -Raw
    Assert-SafeModVersion ([string]$project.Project.PropertyGroup.Version | Where-Object { $_ })
    & (Join-Path $projectRoot 'tests/CleanupChecks.ps1')
    if ($version -eq '0.3.0') {
        dotnet run --project tools/TraceReport -- --release validation/release-evidence.json "$projectRoot" "$version"
        if ($LASTEXITCODE) { throw 'Stable release requires reviewed, version-matched scenario results with hashed evidence references' }
    }
    dotnet build Based.Spires2DamageMeter.csproj -c Release "-p:Sts2DataDir=$Sts2DataDir" --ignore-failed-sources
    if ($LASTEXITCODE) { throw 'Build failed' }
    Assert-SafeModVersion ([Diagnostics.FileVersionInfo]::GetVersionInfo((Join-Path $projectRoot '.godot/mono/temp/bin/Release/Based.Spires2DamageMeter.dll')).ProductVersion)
    dotnet run --project tests/ProbeChecks.csproj "-p:Sts2DataDir=$Sts2DataDir" -- "$Sts2DataDir"
    if ($LASTEXITCODE) { throw 'Checks or installed-game compatibility validation failed' }
    $packageDir = Join-Path $projectRoot 'artifacts/mods/Based.Spires2DamageMeter'
    New-Item -ItemType Directory -Force $packageDir | Out-Null
    Copy-Item -LiteralPath '.godot/mono/temp/bin/Release/Based.Spires2DamageMeter.dll','Based.Spires2DamageMeter.json','THIRD_PARTY_NOTICES.md','README.md','CHANGELOG.md','DESIGN.md' -Destination $packageDir
    $licenseDir = Join-Path $packageDir 'licenses'
    New-Item -ItemType Directory -Force $licenseDir | Out-Null
    Copy-Item -LiteralPath 'licenses/DamageTracker-MIT.txt' -Destination $licenseDir -Force
    $assetDir = Join-Path $packageDir 'assets'
    New-Item -ItemType Directory -Force $assetDir | Out-Null
    Copy-Item -LiteralPath 'assets/logo.png','assets/logo-generation.md' -Destination $assetDir -Force
    $validationDir = Join-Path $packageDir 'validation'
    New-Item -ItemType Directory -Force $validationDir | Out-Null
    Copy-Item -LiteralPath 'validation/storage-incident-2026-11-03.md','validation/README.md' -Destination $validationDir -Force
    Compress-Archive -Path 'artifacts/mods' -DestinationPath "artifacts/Based.Spires2DamageMeter-$version.zip" -Force
} finally { Pop-Location }
