function Assert-SafeModVersion {
    param([Parameter(Mandatory)][string]$Version)
    if ($Version.Split('+')[0] -ieq '0.3.0-preview.17') {
        throw 'BLOCKED: preview.17 caused invalid save loading and reported progress loss. Packaging and deployment are prohibited.'
    }
}
