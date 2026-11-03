param([string]$Sts2DataDir = 'C:/Program Files (x86)/Steam/steamapps/common/Slay the Spire 2/data_sts2_windows_x86_64')
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$output = Join-Path $root 'artifacts/host-stability'
New-Item -ItemType Directory -Path $output -Force | Out-Null
Push-Location $root
try {
    $before = $null
    foreach ($revision in @('aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa', 'bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb')) {
        dotnet build Based.Spires2DamageMeter.csproj -c Release -p:DevelopmentMode=true "-p:Sts2DataDir=$Sts2DataDir" "-p:SourceRevisionId=$revision" "-p:RepositoryCommit=$revision" -p:NuGetAudit=false --ignore-failed-sources > (Join-Path $output ($revision[0] + '.txt'))
        if ($LASTEXITCODE) { throw 'Host stability build failed' }
        $hashes = foreach ($name in @('Based.Spires2DamageMeter.dll','Based.Spires2DamageMeter.Contracts.dll')) {
            (Get-FileHash -LiteralPath (Join-Path $root ".godot/mono/temp/bin/Release/$name")).Hash
        }
        $current = $hashes -join ':'
        if ($before -and $current -ne $before) { throw 'Git revision metadata changed permanent host or contracts' }
        $before = $current
    }
    Write-Output 'PASS permanent host and contract hashes are independent of Git commit metadata'
} finally { Pop-Location }
