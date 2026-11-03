param(
    [string]$GameDir = 'C:\Program Files (x86)\Steam\steamapps\common\Slay the Spire 2',
    [string]$Godot = 'C:\Program Files\Godot\4.6.1\_4.5.1\Godot_v4.5.1-stable_mono_win64_console.exe'
)
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$output = Join-Path $root 'artifacts/save-safety-gate'
New-Item -ItemType Directory -Path $output -Force | Out-Null
Push-Location $root
try {
    & $Godot --headless --path tools/SaveSafetyGate --log-file (Join-Path $output 'export.log') --export-release NativeGate
    if ($LASTEXITCODE) { throw "Native fixture export failed" }
    dotnet publish tools/SaveSafetyGate/SaveSafetyGate.csproj -c ExportRelease -r win-x64 --self-contained true -p:NuGetAudit=false --ignore-failed-sources -o (Join-Path $output 'data_SaveSafetyGate_windows_x86_64')
    if ($LASTEXITCODE) { throw 'Reload gate host publish failed' }
    $report = Join-Path $output 'result.txt'
    if (Test-Path -LiteralPath $report) { Remove-Item -LiteralPath $report }
    $info = New-Object Diagnostics.ProcessStartInfo
    $info.FileName = Join-Path $output 'SaveSafetyGate.exe'
    $info.Arguments = '--headless --log-file "' + (Join-Path $output 'native.log') + '" -- "' + $report + '"'
    $info.UseShellExecute = $false
    $info.CreateNoWindow = $true
    $process = [Diagnostics.Process]::Start($info)
    if (-not $process.WaitForExit(30000)) { $process.Kill(); throw 'Reload gate exceeded 30 seconds' }
    if ($process.ExitCode -ne 0 -or -not (Test-Path -LiteralPath $report) -or (Get-Content -LiteralPath $report -Raw) -notmatch 'PASS native SerializableProgress') {
        throw "Native save fixture gate failed (exit $($process.ExitCode)). See $output"
    }
    Get-Content -LiteralPath $report
} finally { Pop-Location }
