function Get-LogPositions([string]$Directory) {
    $positions = @{}
    foreach ($file in Get-ChildItem -LiteralPath $Directory -Filter '*.jsonl' -File -ErrorAction SilentlyContinue) {
        $positions[$file.FullName] = $file.Length
    }
    return $positions
}
function Read-ReloadOutcome([string]$Directory, [hashtable]$Positions, [string]$BuildId) {
    $activated = $false
    foreach ($file in Get-ChildItem -LiteralPath $Directory -Filter '*.jsonl' -File -ErrorAction SilentlyContinue) {
        $stream = $null
        try {
            $stream = [IO.File]::Open($file.FullName, 'Open', 'Read', 'ReadWrite')
            $offset = if ($Positions.ContainsKey($file.FullName)) { [long]$Positions[$file.FullName] } else { 0L }
            if ($stream.Length -lt $offset) { $offset = 0L }
            $null = $stream.Seek($offset, 'Begin')
            $reader = [IO.StreamReader]::new($stream, [Text.Encoding]::UTF8)
            $text = $reader.ReadToEnd()
            $end = $text.LastIndexOf("`n")
            if ($end -lt 0) { continue }
            $complete = $text.Substring(0, $end + 1)
            $Positions[$file.FullName] = $offset + [Text.Encoding]::UTF8.GetByteCount($complete)
            foreach ($line in $complete.Split("`n")) {
                try { $event = $line | ConvertFrom-Json -ErrorAction Stop } catch { continue }
                if ($event.kind -eq 'runtime_reload_error') { return 'reload error; inspect game logs (activation not confirmed)' }
                if ($event.kind -eq 'runtime_reloaded' -and $event.data.build -eq $BuildId) { $activated = $true }
            }
        } catch { continue } finally { if ($stream) { $stream.Dispose() } }
    }
    if ($activated) { return 'activated' }
    return $null
}
