$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$fixture = Join-Path $root ('artifacts/cleanup-check-' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path (Join-Path $fixture 'scripts') -Force | Out-Null
foreach ($name in @('package.ps1','deploy.ps1','assert-safe-mod-version.ps1','repair-preview17-saves.ps1')) {
    Copy-Item -LiteralPath (Join-Path $root "scripts/$name") -Destination (Join-Path $fixture 'scripts')
}
function Invoke-Blocked([string]$Name,[string]$Expected) {
    $info = New-Object Diagnostics.ProcessStartInfo
    $info.FileName = 'powershell.exe'
    $info.Arguments = '-NoProfile -ExecutionPolicy Bypass -File "' + (Join-Path $fixture "scripts/$Name") + '"'
    $info.UseShellExecute=$false; $info.CreateNoWindow=$true
    $info.RedirectStandardError=$true; $info.RedirectStandardOutput=$true
    $process=[Diagnostics.Process]::Start($info)
    $output=$process.StandardOutput.ReadToEnd()+$process.StandardError.ReadToEnd()
    $process.WaitForExit()
    if ($process.ExitCode -eq 0 -or $output -notmatch $Expected) { throw "Expected safe rejection from $Name : $output" }
}
Set-Content -LiteralPath (Join-Path $fixture 'Based.Spires2DamageMeter.json') -Value '{"version":"0.3.0-preview.17"}'
Set-Content -LiteralPath (Join-Path $fixture 'Based.Spires2DamageMeter.csproj') -Value '<Project><PropertyGroup><Version>0.3.0-preview.18</Version></PropertyGroup></Project>'
Invoke-Blocked 'package.ps1' 'BLOCKED'
Invoke-Blocked 'deploy.ps1' 'BLOCKED'
Set-Content -LiteralPath (Join-Path $fixture 'Based.Spires2DamageMeter.json') -Value '{"version":"0.3.0-preview.18"}'
Set-Content -LiteralPath (Join-Path $fixture 'Based.Spires2DamageMeter.csproj') -Value '<Project><PropertyGroup><Version>0.3.0-preview.17</Version></PropertyGroup></Project>'
Invoke-Blocked 'package.ps1' 'BLOCKED'
Invoke-Blocked 'deploy.ps1' 'BLOCKED'
Invoke-Blocked 'repair-preview17-saves.ps1' 'retired'
if (Test-Path -LiteralPath (Join-Path $fixture 'artifacts')) { throw 'Blocked scripts created build/package output' }
Write-Output 'PASS cleanup guards reject unsafe manifest/project versions and retired recovery'

$resolved = (Resolve-Path -LiteralPath $fixture).Path
$allowed = [IO.Path]::GetFullPath((Join-Path $root 'artifacts')) + [IO.Path]::DirectorySeparatorChar
if (-not $resolved.StartsWith($allowed,[StringComparison]::OrdinalIgnoreCase)) { throw 'Fixture cleanup escaped workspace artifacts' }
Remove-Item -LiteralPath $resolved -Recurse -Force
