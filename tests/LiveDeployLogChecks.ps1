$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
. (Join-Path $root '.agents/skills/spires-live-deploy/scripts/observe.ps1')
$dir = Join-Path $root ('artifacts/log-check-' + [guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory $dir -Force | Out-Null
$log = Join-Path $dir 'session.jsonl'
function Check($Condition, $Name) { if (-not $Condition) { throw $Name }; Write-Output "PASS $Name" }
$event = '{"kind":"runtime_reloaded","data":{"build":"expected"}}'
[IO.File]::WriteAllText($log,$event+"`n")
$positions = Get-LogPositions $dir
Check ($null -eq (Read-ReloadOutcome $dir $positions 'expected')) 'old activation excluded'
[IO.File]::AppendAllText($log,"{`"kind`":`"runtime_reloaded`",`"data`":{`"build`":`"other`"}}`n")
Check ($null -eq (Read-ReloadOutcome $dir $positions 'expected')) 'other build excluded'
[IO.File]::AppendAllText($log,$event)
Check ($null -eq (Read-ReloadOutcome $dir $positions 'expected')) 'partial line waits'
[IO.File]::AppendAllText($log,"`n")
Check ((Read-ReloadOutcome $dir $positions 'expected') -eq 'activated') 'matching new activation confirmed'
[IO.File]::AppendAllText($log,"{`"kind`":`"runtime_reload_error`",`"data`":{}}`n")
Check ((Read-ReloadOutcome $dir $positions 'expected') -like 'reload error*') 'reload error separate from activation'
Check ($null -eq (Read-ReloadOutcome (Join-Path $dir 'missing') @{} 'expected')) 'missing logs unconfirmed'
