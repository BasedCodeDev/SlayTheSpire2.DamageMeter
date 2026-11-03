param(
    [string]$Sts2DataDir = 'C:/Program Files (x86)/Steam/steamapps/common/Slay the Spire 2/data_sts2_windows_x86_64',
    [switch]$Development
)
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'assert-safe-mod-version.ps1')
$projectRoot = Split-Path $PSScriptRoot -Parent
Push-Location $projectRoot
try {
    $version = (Get-Content -LiteralPath 'Based.Spires2DamageMeter.json' -Raw | ConvertFrom-Json).version
    Assert-SafeModVersion $version
    [xml]$project = Get-Content -LiteralPath 'Based.Spires2DamageMeter.csproj' -Raw
    Assert-SafeModVersion ([string]$project.Project.PropertyGroup.Version | Where-Object { $_ })
    . (Join-Path $PSScriptRoot 'runtime-package.ps1')
    & (Join-Path $projectRoot 'tests/CleanupChecks.ps1')
    if ($version -eq '0.3.0') {
        dotnet run --project tools/TraceReport -- --release validation/release-evidence.json "$projectRoot" "$version"
        if ($LASTEXITCODE) { throw 'Stable release requires reviewed, version-matched scenario results with hashed evidence references' }
    }
    $developmentMode = $Development.IsPresent.ToString().ToLowerInvariant()
    dotnet build Based.Spires2DamageMeter.csproj -c Release "-p:Sts2DataDir=$Sts2DataDir" "-p:DevelopmentMode=$developmentMode" --ignore-failed-sources
    if ($LASTEXITCODE) { throw 'Build failed' }
    dotnet build runtime/Based.Spires2DamageMeter.Runtime.csproj -c Release "-p:Sts2DataDir=$Sts2DataDir" --ignore-failed-sources
    if ($LASTEXITCODE) { throw 'Runtime build failed' }
    $payloadDir = Join-Path $projectRoot 'runtime/bin/Release/net9.0'
    $runtimeVersion = ([Diagnostics.FileVersionInfo]::GetVersionInfo((Join-Path $payloadDir 'Based.Spires2DamageMeter.Runtime.dll'))).ProductVersion.Split('+')[0]
    Assert-SafeModVersion $runtimeVersion
    if ($runtimeVersion -ne $version) { throw "Runtime/manifest version mismatch: $runtimeVersion / $version" }
    dotnet run --project tests/ProbeChecks.csproj -c Release "-p:Sts2DataDir=$Sts2DataDir" "-p:DevelopmentMode=$developmentMode" -- "$Sts2DataDir"
    if ($LASTEXITCODE) { throw 'Checks or installed-game compatibility validation failed' }
    $packageRoot = if ($Development) { 'artifacts/development' } else { 'artifacts' }
    $packageDir = Join-Path $projectRoot "$packageRoot/mods/Based.Spires2DamageMeter"
    if (Test-Path -LiteralPath $packageDir) {
        $resolved = (Resolve-Path -LiteralPath $packageDir).Path
        $allowed = [IO.Path]::GetFullPath((Join-Path $projectRoot 'artifacts')) + [IO.Path]::DirectorySeparatorChar
        if (-not $resolved.StartsWith($allowed,[StringComparison]::OrdinalIgnoreCase)) { throw 'Package cleanup escaped workspace artifacts' }
        Remove-Item -LiteralPath $resolved -Recurse -Force
    }
    New-Item -ItemType Directory -Force $packageDir | Out-Null
    $hostDir = Join-Path $projectRoot '.godot/mono/temp/bin/Release'
    Copy-Item -LiteralPath (Join-Path $hostDir 'Based.Spires2DamageMeter.dll'),(Join-Path $hostDir 'Based.Spires2DamageMeter.Contracts.dll'),'Based.Spires2DamageMeter.json','THIRD_PARTY_NOTICES.md','README.md','CHANGELOG.md','DESIGN.md' -Destination $packageDir
    $manifest = New-RuntimeManifest $hostDir $payloadDir $version
    $buildDir = Join-Path $packageDir ('runtime/builds/' + $manifest.BuildId)
    New-Item -ItemType Directory -Path $buildDir -Force | Out-Null
    foreach ($name in $manifest.Files.Keys) { Copy-Item -LiteralPath (Join-Path $payloadDir $name) -Destination $buildDir }
    Write-RuntimeManifest $manifest (Join-Path $packageDir 'runtime/current.json')
    if ($Development) { Write-RuntimeManifest @{ Mode = 'development'; HostHash = $manifest.HostHash; ContractsHash = $manifest.ContractsHash } (Join-Path $packageDir 'development-host.json') }
    $licenseDir = Join-Path $packageDir 'licenses'
    New-Item -ItemType Directory -Force $licenseDir | Out-Null
    Copy-Item -LiteralPath 'licenses/DamageTracker-MIT.txt' -Destination $licenseDir -Force
    $assetDir = Join-Path $packageDir 'assets'
    New-Item -ItemType Directory -Force $assetDir | Out-Null
    Copy-Item -LiteralPath 'assets/logo.png','assets/logo-generation.md' -Destination $assetDir -Force
    $docsDir = Join-Path $packageDir 'docs'
    New-Item -ItemType Directory -Force $docsDir | Out-Null
    Copy-Item -LiteralPath 'docs/hot-reload.md' -Destination $docsDir -Force
    $validationDir = Join-Path $packageDir 'validation'
    New-Item -ItemType Directory -Force $validationDir | Out-Null
    Copy-Item -LiteralPath 'validation/storage-incident-2026-11-03.md','validation/README.md','validation/hot-reload-checks-2026-11-03.md' -Destination $validationDir -Force
    $suffix = if ($Development) { '-development' } else { '' }
    Compress-Archive -Path "$packageRoot/mods" -DestinationPath "artifacts/Based.Spires2DamageMeter-$version$suffix.zip" -Force
} finally { Pop-Location }
