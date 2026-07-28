#Requires -Version 5.1
<#
.SYNOPSIS
  Generates Content\OfficialLevels\manifest.json with SHA256 hashes of each official level JSON.
  Invoked by the GenerateOfficialManifest MSBuild target.
#>

[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string]$ProjectRoot
)

$ErrorActionPreference = "Stop"

$officialRoot = Join-Path $ProjectRoot "Content\OfficialLevels"
if (-not (Test-Path -LiteralPath $officialRoot)) {
    Write-Warning "OfficialLevels folder missing: $officialRoot"
    return
}

$levels = [ordered]@{}
Get-ChildItem -LiteralPath $officialRoot -Filter "*.json" -File |
    Where-Object { $_.Name -ne "manifest.json" } |
    Sort-Object Name |
    ForEach-Object {
        $hash = (Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash
        $stem = [System.IO.Path]::GetFileNameWithoutExtension($_.Name)
        $levels[$stem] = $hash
        Write-Host "  $stem = $hash"
    }

$payload = [ordered]@{
    version = 1
    levels  = $levels
}

$manifestPath = Join-Path $officialRoot "manifest.json"
$json = ($payload | ConvertTo-Json -Depth 5)
# ConvertTo-Json may lowercase keys inconsistently; write UTF8 without BOM
[System.IO.File]::WriteAllText($manifestPath, $json + "`n")
Write-Host "Wrote official manifest ($($levels.Count) levels) -> $manifestPath"
