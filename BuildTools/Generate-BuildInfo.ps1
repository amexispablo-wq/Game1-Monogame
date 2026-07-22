#Requires -Version 5.1
<#
.SYNOPSIS
  Generates Content\version.json (BuildInfo) for Color Blocks.
  Invoked automatically by the GenerateBuildInfo MSBuild target on every build/publish.
#>

[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string]$ProjectRoot,
    [string]$Configuration = "Debug"
)

$ErrorActionPreference = "Stop"

function Get-GameVersion {
    $constantsPath = Join-Path $ProjectRoot "Steam\SteamConstants.cs"
    if (Test-Path -LiteralPath $constantsPath) {
        $text = Get-Content -LiteralPath $constantsPath -Raw
        if ($text -match 'GameVersion\s*=\s*"([^"]+)"') {
            return $Matches[1]
        }
    }
    return "0.0.0"
}

function Get-GitValue([string[]]$GitArgs) {
    try {
        $value = & git -C $ProjectRoot @GitArgs 2>$null
        if ($LASTEXITCODE -eq 0 -and -not [string]::IsNullOrWhiteSpace($value)) {
            return ([string]$value).Trim()
        }
    } catch {
        # git unavailable — fall through
    }
    return "unknown"
}

$payload = [ordered]@{
    GameVersion       = Get-GameVersion
    BuildTimestampUtc = (Get-Date).ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ssZ")
    GitCommit         = Get-GitValue @("rev-parse", "--short", "HEAD")
    GitBranch         = Get-GitValue @("rev-parse", "--abbrev-ref", "HEAD")
    Configuration     = $Configuration
    BuildGuid         = [guid]::NewGuid().ToString("N").ToUpperInvariant()
}

$contentDir = Join-Path $ProjectRoot "Content"
if (-not (Test-Path -LiteralPath $contentDir)) {
    New-Item -ItemType Directory -Path $contentDir -Force | Out-Null
}

$versionPath = Join-Path $contentDir "version.json"
$json = $payload | ConvertTo-Json -Depth 3
Set-Content -LiteralPath $versionPath -Value $json -Encoding UTF8
Write-Host "BuildInfo written: $versionPath (Build $($payload.BuildGuid.Substring(0,6)) Commit $($payload.GitCommit))"
exit 0
