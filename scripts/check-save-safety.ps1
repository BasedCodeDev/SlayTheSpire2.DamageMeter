param([Parameter(Mandatory)][string]$HostDll,[Parameter(Mandatory)][string]$ContractDll,[Parameter(Mandatory)][string]$PayloadDll,[Parameter(Mandatory)][string]$GameAssembly,[Parameter(Mandatory)][string]$EngineExecutable)
$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
$evidence=Get-Content -LiteralPath (Join-Path $root 'validation/save-safety-evidence.json') -Raw | ConvertFrom-Json
if($evidence.schemaVersion -ne 1 -or $evidence.status -ne 'passed' -or $null -eq $evidence.candidate){throw 'Save safety NOT certified. Keep the README incident warning.'}
foreach($key in @('HostDll','ContractDll','PayloadDll','GameAssembly','EngineExecutable')){
 $path=Get-Variable -Name $key -ValueOnly
 if((Get-FileHash -LiteralPath $path).Hash -ne $evidence.candidate.$key){throw "Certification does not match $key"}
}
$required=@('startup-profile-prefs-progress','generic-loaders-unpatched','unrelated-io-excluded','solo-two-restarts','host-two-restarts','guest-two-restarts','failed-cancelled-save','primary-backup-fallback','mod-checkpoint-errors','nested-loads','overlapping-stale-saves','profile-run-switch','replayed-encounter','reload-during-save-load','failed-reload-retains-runtime','repeated-reload-no-hooks-leaks')
foreach($name in $required){
 $cases=@($evidence.cases | Where-Object scenario -EQ $name)
 if($cases.Count -ne 1){throw "Missing or duplicate case: $name"}
 $case=$cases[0]
 if($case.status -ne 'passed' -or -not $case.expected -or -not $case.observed -or -not $case.reviewedBy -or -not $case.reviewedAt){throw "Unverified case: $name"}
 [DateTimeOffset]::Parse($case.reviewedAt) | Out-Null
 if(@($case.references).Count -eq 0){throw "Evidence missing: $name"}
 foreach($reference in $case.references){
  $path=[IO.Path]::GetFullPath((Join-Path $root $reference.path))
  if(-not $path.StartsWith($root+[IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase)){throw 'Evidence path escaped workspace'}
  if(-not $reference.locator -or (Get-FileHash -LiteralPath $path).Hash -ne $reference.sha256){throw "Evidence mismatch: $name"}
 }
}
Write-Output 'PASS save-safety evidence matches candidate; historical incident record must remain'
