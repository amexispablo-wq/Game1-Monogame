#Requires -Version 5.1
<#
.SYNOPSIS
  Color Blocks — Windows x64 Steam production publish pipeline.

.DESCRIPTION
  Builds a self-contained win-x64 Release publish into Publish/,
  strips developer/user data, validates Content + Steam deps,
  stages SteamBuild/content/ for SteamPipe upload.

  Does not modify gameplay code or change game behavior.
#>

[CmdletBinding()]
param()

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

$ScriptRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
Set-Location -LiteralPath $ScriptRoot

$ProjectFile     = Join-Path $ScriptRoot "Color Blocks.csproj"
$PublishDir      = Join-Path $ScriptRoot "Publish"
$SteamBuildRoot  = Join-Path $ScriptRoot "SteamBuild"
$SteamContent    = Join-Path $SteamBuildRoot "content"
$SteamOutput     = Join-Path $SteamBuildRoot "output"
$SteamScripts    = Join-Path $SteamBuildRoot "scripts"
$SourceContent   = Join-Path $ScriptRoot "Content"
$SteamNativeDll  = Join-Path $ScriptRoot "Steam\Native\Windows-x64\steam_api64.dll"
$ExeName         = "Color Blocks.exe"
$ReportPath      = Join-Path $PublishDir "BuildReport.txt"
$VersionPath     = Join-Path $PublishDir "version.json"

$Warnings = [System.Collections.Generic.List[string]]::new()
$MissingAssets = [System.Collections.Generic.List[string]]::new()
$BuildStart = Get-Date

function Write-Step([string]$Message) {
    Write-Host ""
    Write-Host "==> $Message" -ForegroundColor Cyan
}

function Fail([string]$Reason) {
    Write-Host ""
    Write-Host "PUBLISH FAILED" -ForegroundColor Red
    Write-Host $Reason -ForegroundColor Red
    exit 1
}

function Get-GameVersion {
    $constantsPath = Join-Path $ScriptRoot "Steam\SteamConstants.cs"
    if (Test-Path -LiteralPath $constantsPath) {
        $text = Get-Content -LiteralPath $constantsPath -Raw
        if ($text -match 'GameVersion\s*=\s*"([^"]+)"') {
            return $Matches[1]
        }
    }
    $Warnings.Add("Could not read GameVersion from SteamConstants.cs; defaulting to 0.0.0")
    return "0.0.0"
}

function Get-GitCommit {
    try {
        $commit = & git -C $ScriptRoot rev-parse --short HEAD 2>$null
        if ($LASTEXITCODE -eq 0 -and -not [string]::IsNullOrWhiteSpace($commit)) {
            return $commit.Trim()
        }
    } catch {
        # ignore
    }
    return "unknown"
}

function Get-DirectorySizeBytes([string]$Path) {
    if (-not (Test-Path -LiteralPath $Path)) { return 0 }
    return (Get-ChildItem -LiteralPath $Path -Recurse -File -Force -ErrorAction SilentlyContinue |
        Measure-Object -Property Length -Sum).Sum
}

function Format-Size([long]$Bytes) {
    if ($Bytes -ge 1GB) { return "{0:N2} GB" -f ($Bytes / 1GB) }
    if ($Bytes -ge 1MB) { return "{0:N2} MB" -f ($Bytes / 1MB) }
    if ($Bytes -ge 1KB) { return "{0:N2} KB" -f ($Bytes / 1KB) }
    return "$Bytes B"
}

function Remove-PublishTree {
    if (Test-Path -LiteralPath $PublishDir) {
        Write-Step "Removing previous Publish folder"
        Remove-Item -LiteralPath $PublishDir -Recurse -Force
    }
}

function Ensure-SteamBuildFolders {
    foreach ($dir in @($SteamBuildRoot, $SteamScripts, $SteamOutput, $SteamContent)) {
        if (-not (Test-Path -LiteralPath $dir)) {
            New-Item -ItemType Directory -Path $dir -Force | Out-Null
        }
    }
}

function Invoke-DotNetPublish {
    Write-Step "dotnet publish (Release / win-x64 / self-contained)"

    if (-not (Test-Path -LiteralPath $ProjectFile)) {
        Fail "Project file not found: $ProjectFile"
    }

    # Trim disabled: MonoGame DesktopGL is not trim-safe.
    $publishArgs = @(
        "publish",
        $ProjectFile,
        "-c", "Release",
        "-r", "win-x64",
        "--self-contained", "true",
        "-p:PublishSingleFile=false",
        "-p:PublishReadyToRun=true",
        "-p:PublishTrimmed=false",
        "-p:DebugType=None",
        "-p:DebugSymbols=false",
        "-o", $PublishDir
    )

    & dotnet @publishArgs
    if ($LASTEXITCODE -ne 0) {
        Fail "dotnet publish exited with code $LASTEXITCODE"
    }
}

function Copy-RequiredSteamAssets {
    Write-Step "Ensuring Steam redistributables"

    $destDll = Join-Path $PublishDir "steam_api64.dll"
    if (-not (Test-Path -LiteralPath $destDll)) {
        if (-not (Test-Path -LiteralPath $SteamNativeDll)) {
            Fail "steam_api64.dll missing from source: $SteamNativeDll"
        }
        Copy-Item -LiteralPath $SteamNativeDll -Destination $destDll -Force
        Write-Host "Copied steam_api64.dll"
    }

    $vdfSources = @(
        @{ Src = "Steam\steam_input_manifest.vdf"; Dest = "Steam\steam_input_manifest.vdf" },
        @{ Src = "Steam\controller_gamepad.vdf"; Dest = "Steam\controller_gamepad.vdf" }
    )
    foreach ($v in $vdfSources) {
        $src = Join-Path $ScriptRoot $v.Src
        $dst = Join-Path $PublishDir $v.Dest
        if (Test-Path -LiteralPath $src) {
            $dstDir = Split-Path -Parent $dst
            if (-not (Test-Path -LiteralPath $dstDir)) {
                New-Item -ItemType Directory -Path $dstDir -Force | Out-Null
            }
            if (-not (Test-Path -LiteralPath $dst)) {
                Copy-Item -LiteralPath $src -Destination $dst -Force
                Write-Host "Copied $($v.Dest)"
            }
        } else {
            $Warnings.Add("Steam VDF source missing: $($v.Src)")
        }
    }
}

function Sync-ShippedContent {
    Write-Step "Syncing shipped Content assets"

    $contentDest = Join-Path $PublishDir "Content"
    if (-not (Test-Path -LiteralPath $contentDest)) {
        New-Item -ItemType Directory -Path $contentDest -Force | Out-Null
    }

    # Ship only runtime level data — not store art, MGCB, or obj intermediates.
    $shipGlobs = @(
        @{ Rel = "level.json"; IsDir = $false },
        @{ Rel = "OfficialLevels"; IsDir = $true }
    )

    foreach ($item in $shipGlobs) {
        $src = Join-Path $SourceContent $item.Rel
        $dst = Join-Path $contentDest $item.Rel
        if (-not (Test-Path -LiteralPath $src)) {
            $MissingAssets.Add("Source Content missing: Content\$($item.Rel)")
            continue
        }
        if ($item.IsDir) {
            if (Test-Path -LiteralPath $dst) {
                Remove-Item -LiteralPath $dst -Recurse -Force
            }
            Copy-Item -LiteralPath $src -Destination $dst -Recurse -Force
        } else {
            $dstParent = Split-Path -Parent $dst
            if (-not (Test-Path -LiteralPath $dstParent)) {
                New-Item -ItemType Directory -Path $dstParent -Force | Out-Null
            }
            Copy-Item -LiteralPath $src -Destination $dst -Force
        }
    }
}

function Remove-ForbiddenFiles {
    Write-Step "Stripping developer / user / temp files"

    $rootExact = @(
        "developer_settings.json",
        "steam_appid.txt",
        "best_times.json"
    )
    foreach ($name in $rootExact) {
        $path = Join-Path $PublishDir $name
        if (Test-Path -LiteralPath $path) {
            Remove-Item -LiteralPath $path -Force
            Write-Host "Removed $name"
        }
    }

    $contentExact = @(
        "settings.json",
        "skin_library.json",
        ".level_migration_v1",
        "Content.mgcb"
    )
    foreach ($name in $contentExact) {
        $path = Join-Path $PublishDir (Join-Path "Content" $name)
        if (Test-Path -LiteralPath $path) {
            Remove-Item -LiteralPath $path -Force
            Write-Host "Removed Content\$name"
        }
    }

    $contentDirs = @(
        "Levels",
        "UserLevels",
        "WorkshopLevels",
        "Workshop",
        "Replays",
        "Ghost",
        "GhostRecordings",
        "BestTimes",
        "LevelPreviews",
        "Steam",
        "obj"
    )
    foreach ($dirName in $contentDirs) {
        $path = Join-Path $PublishDir (Join-Path "Content" $dirName)
        if (Test-Path -LiteralPath $path) {
            Remove-Item -LiteralPath $path -Recurse -Force
            Write-Host "Removed Content\$dirName"
        }
    }

    $rootDirs = @(
        "Settings",
        "Saves",
        "UserLevels",
        "Workshop",
        "WorkshopLevels",
        "BestTimes",
        "Ghosts",
        "Replays",
        "Highlights",
        "Screenshots",
        "Benchmarks",
        "Logs",
        "Skins",
        "Cache",
        "Temporary",
        "Developer",
        "GameplayBenchmark",
        "FuzzFailures",
        "Benchmark",
        "BenchmarkCache",
        "Recordings"
    )
    foreach ($dirName in $rootDirs) {
        $path = Join-Path $PublishDir $dirName
        if (Test-Path -LiteralPath $path) {
            Remove-Item -LiteralPath $path -Recurse -Force
            Write-Host "Removed $dirName"
        }
    }

    # Fast extension sweep — avoid Get-ChildItem -Include (can hang).
    $badExt = @(".user", ".tmp", ".pdb", ".log")
    Get-ChildItem -LiteralPath $PublishDir -Recurse -File -Force -ErrorAction SilentlyContinue |
        Where-Object { $badExt -contains $_.Extension.ToLowerInvariant() } |
        ForEach-Object {
            $rel = $_.FullName.Substring($PublishDir.Length).TrimStart("\", "/")
            Remove-Item -LiteralPath $_.FullName -Force
            Write-Host "Removed $rel"
        }
}

function Test-ContentAssets {
    Write-Step "Validating Content assets"

    $requiredRelPaths = [System.Collections.Generic.List[string]]::new()

    $levelJson = Join-Path $SourceContent "level.json"
    if (Test-Path -LiteralPath $levelJson) {
        $requiredRelPaths.Add("Content\level.json")
    }

    foreach ($sub in @("OfficialLevels")) {
        $srcDir = Join-Path $SourceContent $sub
        if (Test-Path -LiteralPath $srcDir) {
            Get-ChildItem -LiteralPath $srcDir -Recurse -File -Force |
                Where-Object { $_.Extension -notin @(".user", ".tmp", ".mgstats") } |
                ForEach-Object {
                    $rel = $_.FullName.Substring($SourceContent.Length).TrimStart("\", "/")
                    $requiredRelPaths.Add(("Content\" + $rel))
                }
        }
    }

    foreach ($rel in $requiredRelPaths) {
        $path = Join-Path $PublishDir $rel
        if (-not (Test-Path -LiteralPath $path)) {
            $MissingAssets.Add($rel)
        }
    }

    if ($MissingAssets.Count -gt 0) {
        foreach ($m in $MissingAssets) {
            Write-Host "MISSING: $m" -ForegroundColor Yellow
        }
    } else {
        Write-Host "All required Content assets present."
    }
}

function Test-PublishOutput {
    Write-Step "Validating publish output"

    $exe = Join-Path $PublishDir $ExeName
    if (-not (Test-Path -LiteralPath $exe)) {
        Fail "Executable missing: $ExeName"
    }
    Write-Host "OK executable: $ExeName"

    $content = Join-Path $PublishDir "Content"
    if (-not (Test-Path -LiteralPath $content)) {
        Fail "Content folder missing"
    }
    Write-Host "OK Content folder"

    $steamDll = Join-Path $PublishDir "steam_api64.dll"
    if (-not (Test-Path -LiteralPath $steamDll)) {
        Fail "steam_api64.dll missing from Publish"
    }
    Write-Host "OK steam_api64.dll"

    $steamworks = Join-Path $PublishDir "Steamworks.NET.dll"
    if (-not (Test-Path -LiteralPath $steamworks)) {
        Fail "Steamworks.NET.dll missing from Publish"
    }
    Write-Host "OK Steamworks.NET.dll"

    $sdl = Join-Path $PublishDir "SDL2.dll"
    if (-not (Test-Path -LiteralPath $sdl)) {
        Fail "SDL2.dll missing (MonoGame native dependency)"
    }
    Write-Host "OK SDL2.dll"

    $openal = Join-Path $PublishDir "openal.dll"
    if (-not (Test-Path -LiteralPath $openal)) {
        $openal = Join-Path $PublishDir "soft_oal.dll"
    }
    if (-not (Test-Path -LiteralPath $openal)) {
        $Warnings.Add("OpenAL native DLL not found (soft_oal.dll / openal.dll)")
        Write-Host "WARN OpenAL native DLL not found" -ForegroundColor Yellow
    } else {
        Write-Host "OK $(Split-Path -Leaf $openal)"
    }

    foreach ($banned in @("developer_settings.json", "steam_appid.txt", "best_times.json")) {
        $path = Join-Path $PublishDir $banned
        if (Test-Path -LiteralPath $path) {
            Fail "Forbidden file still present: $banned"
        }
    }
    Write-Host "OK no developer_settings.json / steam_appid.txt"

    $bannedDirs = @(
        (Join-Path $PublishDir "Developer"),
        (Join-Path $PublishDir "FuzzFailures"),
        (Join-Path $PublishDir "Settings"),
        (Join-Path $PublishDir "Saves"),
        (Join-Path $PublishDir "UserLevels"),
        (Join-Path $PublishDir "Workshop"),
        (Join-Path $PublishDir "BestTimes"),
        (Join-Path $PublishDir "Ghosts"),
        (Join-Path $PublishDir "Replays"),
        (Join-Path $PublishDir "Highlights"),
        (Join-Path $PublishDir "Screenshots"),
        (Join-Path $PublishDir "Benchmarks"),
        (Join-Path $PublishDir "Logs"),
        (Join-Path $PublishDir "Skins"),
        (Join-Path $PublishDir "Cache"),
        (Join-Path $PublishDir "Temporary"),
        (Join-Path $PublishDir "Content\UserLevels"),
        (Join-Path $PublishDir "Content\Levels"),
        (Join-Path $PublishDir "Content\WorkshopLevels"),
        (Join-Path $PublishDir "Content\Replays"),
        (Join-Path $PublishDir "Content\BestTimes"),
        (Join-Path $PublishDir "Content\settings.json")
    )
    foreach ($path in $bannedDirs) {
        if (Test-Path -LiteralPath $path) {
            Fail "Forbidden path still present: $($path.Substring($PublishDir.Length).TrimStart('\','/'))"
        }
    }
    Write-Host "OK no user-data / developer folders"

    if ($MissingAssets.Count -gt 0) {
        Fail ("Missing required Content assets:`n  - " + ($MissingAssets -join "`n  - "))
    }
}

function Write-VersionFile {
    Write-Step "Writing version.json"

    $payload = [ordered]@{
        GameVersion = Get-GameVersion
        BuildDate   = (Get-Date).ToUniversalTime().ToString("o")
        GitCommit   = Get-GitCommit
        BuildType   = "Release"
    }

    $json = $payload | ConvertTo-Json -Depth 3
    Set-Content -LiteralPath $VersionPath -Value $json -Encoding UTF8
    Write-Host "Wrote version.json"
}

function Sync-SteamBuildContent {
    Write-Step "Staging SteamBuild/content"

    Ensure-SteamBuildFolders

    $contentGitignore = Join-Path $SteamContent ".gitignore"
    $gitignoreBackup = $null
    if (Test-Path -LiteralPath $contentGitignore) {
        $gitignoreBackup = Get-Content -LiteralPath $contentGitignore -Raw
    }

    if (Test-Path -LiteralPath $SteamContent) {
        Get-ChildItem -LiteralPath $SteamContent -Force | Remove-Item -Recurse -Force
    } else {
        New-Item -ItemType Directory -Path $SteamContent -Force | Out-Null
    }

    # -Path (not -LiteralPath) so the * wildcard expands.
    Copy-Item -Path (Join-Path $PublishDir "*") -Destination $SteamContent -Recurse -Force

    # BuildReport is audit-only — keep out of SteamPipe depot content.
    $stagedReport = Join-Path $SteamContent "BuildReport.txt"
    if (Test-Path -LiteralPath $stagedReport) {
        Remove-Item -LiteralPath $stagedReport -Force
    }

    if ($null -ne $gitignoreBackup) {
        Set-Content -LiteralPath $contentGitignore -Value $gitignoreBackup -Encoding UTF8 -NoNewline
    } elseif (-not (Test-Path -LiteralPath $contentGitignore)) {
        Set-Content -LiteralPath $contentGitignore -Value ("# SteamBuild staging folders (filled by publish.ps1)`n*`n!.gitignore`n") -Encoding UTF8
    }

    Write-Host "Copied Publish -> SteamBuild/content"
}

function Write-BuildReport {
    param(
        [TimeSpan]$Elapsed,
        [long]$SizeBytes,
        [int]$FileCount
    )

    Write-Step "Writing BuildReport.txt"

    $sizeLabel = Format-Size $SizeBytes
    $elapsedLabel = $Elapsed.ToString('hh\:mm\:ss\.fff')
    $finishedUtc = (Get-Date).ToUniversalTime().ToString('o')
    $gameVer = Get-GameVersion
    $gitCommit = Get-GitCommit

    $lines = @(
        "Color Blocks - Steam Production Build Report"
        "============================================"
        ""
        "Build time          : $elapsedLabel"
        "Build finished (UTC): $finishedUtc"
        "Output size         : $sizeLabel ($SizeBytes bytes)"
        "Number of files     : $FileCount"
        ""
        "Build configuration"
        "-------------------"
        "Configuration       : Release"
        "RuntimeIdentifier   : win-x64"
        "SelfContained       : true"
        "PublishSingleFile   : false"
        "PublishReadyToRun   : true"
        "PublishTrimmed      : false (MonoGame incompatible)"
        "Output              : Publish/"
        "Steam stage         : SteamBuild/content/"
        "Executable          : Publish/$ExeName"
        "GameVersion         : $gameVer"
        "GitCommit           : $gitCommit"
        ""
        "Warnings ($($Warnings.Count))"
        "-------------------"
    )

    if ($Warnings.Count -eq 0) {
        $lines += "(none)"
    } else {
        foreach ($w in $Warnings) { $lines += "- $w" }
    }

    $lines += ""
    $lines += "Missing assets ($($MissingAssets.Count))"
    $lines += "-------------------"
    if ($MissingAssets.Count -eq 0) {
        $lines += "(none)"
    } else {
        foreach ($m in $MissingAssets) { $lines += "- $m" }
    }

    $lines += ""
    $lines += "Result: SUCCESS"

    $text = ($lines -join [Environment]::NewLine) + [Environment]::NewLine
    Set-Content -LiteralPath $ReportPath -Value $text -Encoding UTF8

    $steamReport = Join-Path $SteamBuildRoot "BuildReport.txt"
    Set-Content -LiteralPath $steamReport -Value $text -Encoding UTF8
}

# ---------------------------------------------------------------------------
# Main
# ---------------------------------------------------------------------------

try {
    Write-Host "Color Blocks Steam publish pipeline" -ForegroundColor Green
    Write-Host "Root: $ScriptRoot"

    Remove-PublishTree
    Ensure-SteamBuildFolders
    Invoke-DotNetPublish
    Copy-RequiredSteamAssets
    Sync-ShippedContent
    Remove-ForbiddenFiles
    Write-VersionFile
    Test-ContentAssets
    Test-PublishOutput
    Sync-SteamBuildContent

    $elapsed = (Get-Date) - $BuildStart
    $sizeBytes = [long](Get-DirectorySizeBytes $PublishDir)
    $fileCount = @(Get-ChildItem -LiteralPath $PublishDir -Recurse -File -Force).Count

    Write-BuildReport -Elapsed $elapsed -SizeBytes $sizeBytes -FileCount $fileCount

    $exePath = Join-Path $PublishDir $ExeName

    Write-Host ""
    Write-Host "PUBLISH SUCCESS" -ForegroundColor Green
    Write-Host "Build size     : $(Format-Size $sizeBytes)"
    Write-Host "File count     : $fileCount"
    Write-Host "Executable     : $exePath"
    Write-Host "Steam content  : $SteamContent"
    Write-Host "Build report   : $ReportPath"
    Write-Host "Elapsed        : $($elapsed.ToString('hh\:mm\:ss\.fff'))"
    exit 0
}
catch {
    Fail $_.Exception.Message
}
