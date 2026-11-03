param(
    [string]$GameDir = 'C:\Program Files (x86)\Steam\steamapps\common\Slay the Spire 2',
    [string]$Godot = 'C:\Program Files\Godot\4.6.1\_4.5.1\Godot_v4.5.1-stable_mono_win64_console.exe'
)
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$output = Join-Path $root 'artifacts/reload-gate'
New-Item -ItemType Directory -Path $output -Force | Out-Null
Push-Location $root
try {
    . (Join-Path $PSScriptRoot 'runtime-package.ps1')
    dotnet build tools/ReloadGatePayload/ReloadGatePayload.csproj -c Release -p:NuGetAudit=false --ignore-failed-sources
    if ($LASTEXITCODE) { throw 'Reload gate payload build failed' }
    $presetPath = Join-Path $root 'tools/HotReloadGate/export_presets.cfg'
    $preset = [IO.File]::ReadAllText($presetPath)
    $engine = (Join-Path $GameDir 'SlayTheSpire2.exe').Replace('\','/')
    $customized = [regex]::Replace($preset, 'custom_template/release="[^"]*"', [Text.RegularExpressions.MatchEvaluator]{ param($match) 'custom_template/release="' + $engine + '"' })
    try {
        [IO.File]::WriteAllText($presetPath, $customized)
        & $Godot --headless --path tools/HotReloadGate --log-file (Join-Path $output 'export.log') --export-release NativeGate
        if ($LASTEXITCODE) { throw 'Native reload fixture export failed' }
    } finally { [IO.File]::WriteAllText($presetPath, $preset) }
    $hostDir = Join-Path $output 'data_HotReloadGate_windows_x86_64'
    dotnet publish tools/HotReloadGate/HotReloadGate.csproj -c ExportRelease -r win-x64 --self-contained true -p:NuGetAudit=false -p:DevelopmentMode=true --ignore-failed-sources -o $hostDir
    if ($LASTEXITCODE) { throw 'Reload gate host publish failed' }
    dotnet build runtime/Based.Spires2DamageMeter.Runtime.csproj -c Release "-p:HostPath=$hostDir/Based.Spires2DamageMeter.dll" -p:NuGetAudit=false --ignore-failed-sources
    if ($LASTEXITCODE) { throw 'Real runtime build failed' }
    $gameData = Join-Path $GameDir 'data_sts2_windows_x86_64'
    foreach ($file in Get-ChildItem -LiteralPath $gameData -Filter '*.dll') {
        if ($file.Name -match '^(System\.|Microsoft\.|mscorlib\.|netstandard\.|WindowsBase\.|GodotSharp\.)') { continue }
        try { $null = [Reflection.AssemblyName]::GetAssemblyName($file.FullName) } catch { continue }
        Copy-Item -LiteralPath $file.FullName -Destination $hostDir -Force
    }
    $variant = Join-Path $output 'variant'
    New-Item -ItemType Directory -Path $variant -Force | Out-Null
    $calc = (Get-Content -LiteralPath 'runtime/logic/EngineDamageLogic.cs' -Encoding UTF8 -Raw).Replace('Math.Max(0, unblockedDamage)', 'Math.Max(0, unblockedDamage) + 1')
    $ui = (Get-Content -LiteralPath 'runtime/logic/MeterPanelLogic.cs' -Encoding UTF8 -Raw).Replace('"\u2630"', '"DEV 2"')
    [IO.File]::WriteAllText((Join-Path $variant 'EngineDamageLogic.cs'), $calc)
    [IO.File]::WriteAllText((Join-Path $variant 'MeterPanelLogic.cs'), $ui)
    $xmlRoot = [Security.SecurityElement]::Escape($root.Replace('\','/'))
    $xmlHost = [Security.SecurityElement]::Escape($hostDir.Replace('\','/'))
    $xmlGame = [Security.SecurityElement]::Escape($gameData.Replace('\','/'))
    $project = @"
<Project Sdk="Microsoft.NET.Sdk">
<PropertyGroup><TargetFramework>net9.0</TargetFramework><AssemblyName>Based.Spires2DamageMeter.Runtime</AssemblyName><Version>0.3.0-preview.36</Version><EnableDefaultCompileItems>false</EnableDefaultCompileItems><Nullable>enable</Nullable><ImplicitUsings>enable</ImplicitUsings></PropertyGroup>
<ItemGroup>
<Reference Include="Based.Spires2DamageMeter"><HintPath>$xmlHost/Based.Spires2DamageMeter.dll</HintPath><Private>false</Private></Reference>
<Reference Include="Based.Spires2DamageMeter.Contracts"><HintPath>$xmlHost/Based.Spires2DamageMeter.Contracts.dll</HintPath><Private>false</Private></Reference>
<Reference Include="GodotSharp"><HintPath>$xmlHost/GodotSharp.dll</HintPath><Private>false</Private></Reference>
<Reference Include="sts2"><HintPath>$xmlGame/sts2.dll</HintPath><Private>false</Private></Reference>
<Reference Include="0Harmony"><HintPath>$xmlGame/0Harmony.dll</HintPath><Private>false</Private></Reference>
<Compile Include="$xmlRoot/runtime/*.cs;$xmlRoot/runtime/logic/*.cs" Exclude="$xmlRoot/runtime/logic/EngineDamageLogic.cs;$xmlRoot/runtime/logic/MeterPanelLogic.cs" />
<Compile Include="EngineDamageLogic.cs;MeterPanelLogic.cs;$xmlRoot/runtime/capture/*.cs" />
</ItemGroup></Project>
"@
    [IO.File]::WriteAllText((Join-Path $variant 'Variant.csproj'), $project)
    dotnet build (Join-Path $variant 'Variant.csproj') -c Release -p:NuGetAudit=false --ignore-failed-sources
    if ($LASTEXITCODE) { throw 'Changed runtime fixture build failed' }
    $manifests = @()
    foreach ($directory in @((Join-Path $root 'runtime/bin/Release/net9.0'), (Join-Path $variant 'bin/Release/net9.0'))) {
        $manifest = New-RuntimeManifest $hostDir $directory '0.3.0-preview.36'
        $build = Join-Path $hostDir ('runtime/builds/' + $manifest.BuildId)
        New-Item -ItemType Directory -Path $build -Force | Out-Null
        foreach ($name in $manifest.Files.Keys) { Copy-Item -LiteralPath (Join-Path $directory $name) -Destination $build -Force }
        $manifestPath = Join-Path $output ($manifest.BuildId + '.json')
        Write-RuntimeManifest $manifest $manifestPath
        $manifests += $manifestPath
    }
    Copy-Item -LiteralPath $manifests[0] -Destination (Join-Path $hostDir 'runtime/current.json') -Force
    $rejected = Join-Path $output 'rejected'
    New-Item -ItemType Directory -Path $rejected -Force | Out-Null
    function Save-RejectedFixture([string]$Directory, [string]$Name) {
        $manifest = New-RuntimeManifest $hostDir $Directory '0.3.0-preview.36'
        $build = Join-Path $hostDir ('runtime/builds/' + $manifest.BuildId)
        New-Item -ItemType Directory -Path $build -Force | Out-Null
        foreach ($file in $manifest.Files.Keys) { Copy-Item -LiteralPath (Join-Path $Directory $file) -Destination $build -Force }
        Write-RuntimeManifest $manifest (Join-Path $rejected ($Name + '.json'))
    }
    $moduleProject = $project.Replace('Exclude="' + $xmlRoot + '/runtime/logic/EngineDamageLogic.cs;' + $xmlRoot + '/runtime/logic/MeterPanelLogic.cs"',
        'Exclude="' + $xmlRoot + '/runtime/logic/EngineDamageLogic.cs;' + $xmlRoot + '/runtime/logic/MeterPanelLogic.cs;' + $xmlRoot + '/runtime/RuntimeModule.cs"')
    $moduleProject = $moduleProject.Replace('<Compile Include="EngineDamageLogic.cs;', '<Compile Include="RuntimeModule.cs;EngineDamageLogic.cs;')
    [IO.File]::WriteAllText((Join-Path $variant 'Variant.csproj'), $moduleProject)
    $originalModule = Get-Content -LiteralPath 'runtime/RuntimeModule.cs' -Encoding UTF8 -Raw
    foreach ($failure in @('bad-api','ui-failure','ui-null','ui-parent')) {
        $changed = switch ($failure) {
            'bad-api' { $originalModule.Replace('public int ApiVersion => RuntimeContract.ApiVersion;', 'public int ApiVersion => 2;') }
            'ui-failure' { $originalModule.Replace('return panel;', 'throw new InvalidOperationException("fixture UI activation failure");') }
            'ui-null' { $originalModule.Replace('return panel;', 'return null;') }
            'ui-parent' { $originalModule.Replace('return panel;', '((Godot.SceneTree)Godot.Engine.GetMainLoop()).Root.AddChild(panel); return panel;') }
        }
        [IO.File]::WriteAllText((Join-Path $variant 'RuntimeModule.cs'), $changed)
        $failureDir = Join-Path $output ('fixtures/' + $failure)
        dotnet build (Join-Path $variant 'Variant.csproj') -c Release -p:NuGetAudit=false --no-restore -o $failureDir
        if ($LASTEXITCODE) { throw 'Rejected runtime fixture build failed' }
        Save-RejectedFixture $failureDir $failure
    }
    $broken = Join-Path $output 'fixtures/broken'
    New-Item -ItemType Directory -Path $broken -Force | Out-Null
    [IO.File]::WriteAllText((Join-Path $broken 'Based.Spires2DamageMeter.Runtime.dll'), 'not an assembly')
    Save-RejectedFixture $broken 'broken-dll'
    $badState = Get-Content -LiteralPath $manifests[1] -Encoding UTF8 -Raw | ConvertFrom-Json
    $badState.StateVersion = 999
    Write-RuntimeManifest $badState (Join-Path $rejected 'bad-state.json')
    $badHost = Get-Content -LiteralPath $manifests[1] -Encoding UTF8 -Raw | ConvertFrom-Json
    $badHost.HostHash = '0' * 64
    Write-RuntimeManifest $badHost (Join-Path $rejected 'bad-host.json')
    $badHash = Get-Content -LiteralPath $manifests[1] -Encoding UTF8 -Raw | ConvertFrom-Json
    $badHash.Files.'Based.Spires2DamageMeter.Runtime.dll' = '0' * 64
    Write-RuntimeManifest $badHash (Join-Path $rejected 'bad-hash.json')
    [IO.File]::WriteAllText((Join-Path $rejected 'malformed.json'), '{')
    $report = Join-Path $output 'result.txt'
    if (Test-Path -LiteralPath $report) { Remove-Item -LiteralPath $report }
    $info = New-Object Diagnostics.ProcessStartInfo
    $info.FileName = Join-Path $output 'HotReloadGate.exe'
    $payload = Join-Path $root 'tools/ReloadGatePayload/bin/Release/net9.0/ReloadGatePayload.dll'
    $info.Arguments = '--headless --log-file "' + (Join-Path $output 'native.log') + '" -- "' + $payload + '" "' + $report + '" "' + $hostDir + '" "' + $manifests[0] + '" "' + $manifests[1] + '"'
    $info.UseShellExecute = $false
    $info.CreateNoWindow = $true
    $process = [Diagnostics.Process]::Start($info)
    if (-not $process.WaitForExit(30000)) { $process.Kill(); throw 'Reload gate exceeded 30 seconds' }
    if ($process.ExitCode -ne 0 -or -not (Test-Path -LiteralPath $report) -or (Get-Content -LiteralPath $report -Encoding UTF8 -Raw) -notmatch 'PASS 50 real-runtime') {
        throw "Native reload gate failed. See $output"
    }
    Get-Content -LiteralPath $report
} finally { Pop-Location }
