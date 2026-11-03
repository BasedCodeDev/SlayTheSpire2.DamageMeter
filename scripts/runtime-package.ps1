function Get-RuntimeContentId($Files) {
    [string[]]$names = @($Files.Keys)
    [Array]::Sort($names, [StringComparer]::Ordinal)
    $lines = foreach ($name in $names) { $name + ':' + $Files[$name].ToUpperInvariant() }
    $sha = [Security.Cryptography.SHA256]::Create()
    try { return ([BitConverter]::ToString($sha.ComputeHash([Text.Encoding]::UTF8.GetBytes(($lines -join "`n"))))).Replace('-','') }
    finally { $sha.Dispose() }
}

function New-RuntimeManifest([string]$HostDirectory, [string]$PayloadDirectory, [string]$Version) {
    $shared = @('Based.Spires2DamageMeter.dll','Based.Spires2DamageMeter.Contracts.dll','GodotSharp.dll','sts2.dll','0Harmony.dll')
    $files = @{}
    foreach ($file in Get-ChildItem -LiteralPath $PayloadDirectory -File) {
        if ($file.Extension -eq '.dll' -and $file.Name -notin $shared) {
            $files[$file.Name] = (Get-FileHash -LiteralPath $file.FullName -Algorithm SHA256).Hash
            $pdb = [IO.Path]::ChangeExtension($file.FullName, '.pdb')
            if (Test-Path -LiteralPath $pdb) { $files[[IO.Path]::GetFileName($pdb)] = (Get-FileHash -LiteralPath $pdb -Algorithm SHA256).Hash }
        }
    }
    $deps = Join-Path $PayloadDirectory 'Based.Spires2DamageMeter.Runtime.deps.json'
    if (Test-Path -LiteralPath $deps) { $files[[IO.Path]::GetFileName($deps)] = (Get-FileHash -LiteralPath $deps -Algorithm SHA256).Hash }
    if (-not $files.ContainsKey('Based.Spires2DamageMeter.Runtime.dll')) { throw 'Runtime DLL is missing' }
    return [ordered]@{
        BuildId = Get-RuntimeContentId $files
        Version = $Version
        HostHash = (Get-FileHash -LiteralPath (Join-Path $HostDirectory 'Based.Spires2DamageMeter.dll') -Algorithm SHA256).Hash
        ContractsHash = (Get-FileHash -LiteralPath (Join-Path $HostDirectory 'Based.Spires2DamageMeter.Contracts.dll') -Algorithm SHA256).Hash
        ApiVersion = 1
        StateVersion = 1
        Files = $files
    }
}

function Write-RuntimeManifest($Manifest, [string]$Path) {
    [IO.File]::WriteAllText($Path, ($Manifest | ConvertTo-Json -Depth 5), [Text.UTF8Encoding]::new($false))
}

function Assert-InstalledDevelopmentHost([string]$Package, [string]$Destination) {
    if (-not (Test-Path -LiteralPath (Join-Path $Destination 'development-host.json'))) {
        throw 'No development host is installed. Close the game and run update.bat -Development once.'
    }
    foreach ($name in @('Based.Spires2DamageMeter.dll','Based.Spires2DamageMeter.Contracts.dll')) {
        $installed = Join-Path $Destination $name
        if (-not (Test-Path -LiteralPath $installed) -or
            (Get-FileHash -LiteralPath $installed -Algorithm SHA256).Hash -ne (Get-FileHash -LiteralPath (Join-Path $Package $name) -Algorithm SHA256).Hash) {
            throw "Host or shared state changed ($name). Close the game and run update.bat -Development to reinstall."
        }
    }
}

function Publish-RuntimeBuild([string]$Package, [string]$Destination) {
    $manifest = Get-Content -LiteralPath (Join-Path $Package 'runtime/current.json') -Raw | ConvertFrom-Json
    if ($manifest.BuildId -notmatch '^[A-Fa-f0-9]{64}$') { throw 'Invalid runtime build identity' }
    $files = @{}
    foreach ($entry in $manifest.Files.PSObject.Properties) {
        if ([IO.Path]::GetFileName($entry.Name) -ne $entry.Name -or $entry.Name -match '[/\\:]') { throw 'Invalid runtime file name' }
        $files[$entry.Name] = $entry.Value
    }
    if ((Get-RuntimeContentId $files) -ne $manifest.BuildId) { throw 'Runtime build identity mismatch' }
    $source = Join-Path $Package ('runtime/builds/' + $manifest.BuildId)
    $builds = Join-Path $Destination 'runtime/builds'
    New-Item -ItemType Directory -Path $builds -Force | Out-Null
    $published = Join-Path $builds $manifest.BuildId
    if (-not (Test-Path -LiteralPath $published)) {
        $staging = Join-Path $builds ('.pending-' + [Guid]::NewGuid().ToString('N'))
        New-Item -ItemType Directory -Path $staging | Out-Null
        foreach ($name in $files.Keys) { Copy-Item -LiteralPath (Join-Path $source $name) -Destination $staging }
        foreach ($name in $files.Keys) {
            if ((Get-FileHash -LiteralPath (Join-Path $staging $name) -Algorithm SHA256).Hash -ne $files[$name]) { throw "Staged runtime hash mismatch: $name" }
        }
        $allowed = [IO.Path]::GetFullPath($builds) + [IO.Path]::DirectorySeparatorChar
        foreach ($target in @($staging, $published)) {
            if (-not [IO.Path]::GetFullPath($target).StartsWith($allowed, [StringComparison]::OrdinalIgnoreCase)) { throw 'Runtime staging escaped the installation build directory' }
        }
        Move-Item -LiteralPath $staging -Destination $published
    }
    foreach ($name in $files.Keys) {
        if ((Get-FileHash -LiteralPath (Join-Path $published $name) -Algorithm SHA256).Hash -ne $files[$name]) { throw "Published runtime is corrupt: $name" }
    }
    $pointer = Join-Path $Destination 'runtime/current.json'
    $temporary = $pointer + '.' + [Guid]::NewGuid().ToString('N') + '.tmp'
    Write-RuntimeManifest $manifest $temporary
    if (Test-Path -LiteralPath $pointer) { [IO.File]::Replace($temporary, $pointer, $pointer + '.previous') }
    else { [IO.File]::Move($temporary, $pointer) }
    return $manifest.BuildId
}
