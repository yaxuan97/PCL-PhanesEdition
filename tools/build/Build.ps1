#requires -Version 5.1
<#
.SYNOPSIS
    PCL - Phanes Edition 一键编译脚本。
.DESCRIPTION
    自动定位 MSBuild、还原 NuGet 依赖、编译主程序，并整理可直接运行的发行目录与压缩包。
    一般由仓库根目录的“一键编译.bat”调用，也可以直接在 PowerShell 中运行。
.EXAMPLE
    powershell -NoProfile -ExecutionPolicy Bypass -File .\tools\build\Build.ps1
.EXAMPLE
    powershell -NoProfile -ExecutionPolicy Bypass -File .\tools\build\Build.ps1 Release -Clean -NoOpen
#>
[CmdletBinding()]
param(
    [Parameter(Position = 0)]
    [ValidateSet("Debug", "Release", "Snapshot")]
    [string]$Configuration = "Release",

    [switch]$PublicRelease,
    [switch]$Clean,
    [switch]$SkipRestore,
    [switch]$NoPackage,
    [switch]$NoOpen,
    [switch]$Run,
    [switch]$Help
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

trap {
    Write-Host ""
    Write-Host "[错误] $($_.Exception.Message)" -ForegroundColor Red
    Write-Host "       位置：$($_.InvocationInfo.ScriptName):$($_.InvocationInfo.ScriptLineNumber)" -ForegroundColor DarkGray
    exit 1
}

# ---------------------------------------------------------------------------
# 输出辅助
# ---------------------------------------------------------------------------

function Write-Step {
    param([string]$Message)
    Write-Host ""
    Write-Host "==> $Message" -ForegroundColor Cyan
}

function Write-Ok {
    param([string]$Message)
    Write-Host "  [OK] $Message" -ForegroundColor Green
}

function Write-Warn {
    param([string]$Message)
    Write-Host "  [!] $Message" -ForegroundColor Yellow
}

function Write-Info {
    param([string]$Message)
    Write-Host "      $Message" -ForegroundColor DarkGray
}

function Stop-WithError {
    param(
        [string]$Message,
        [string]$Hint = ""
    )
    Write-Host ""
    Write-Host "[错误] $Message" -ForegroundColor Red
    if (-not [string]::IsNullOrWhiteSpace($Hint)) {
        Write-Host "       $Hint" -ForegroundColor Yellow
    }
    exit 1
}

function Show-Help {
    Write-Host "PCL - Phanes Edition 一键编译" -ForegroundColor Cyan
    Write-Host ""
    Write-Host "用法："
    Write-Host "  一键编译.bat [Debug|Release|Snapshot] [-Clean] [-SkipRestore] [-NoPackage] [-NoOpen] [-Run]"
    Write-Host ""
    Write-Host "参数："
    Write-Host "  Debug|Release|Snapshot  编译配置，默认 Release"
    Write-Host "  -Clean                  强制完整清理后编译（默认在配置变化时自动清理）"
    Write-Host "  -SkipRestore            跳过 NuGet 还原"
    Write-Host "  -NoPackage              只编译，不整理发行目录和压缩包"
    Write-Host "  -NoOpen                 编译完成后不自动打开输出目录"
    Write-Host "  -Run                    编译完成后启动产物"
    Write-Host "  -Help                   显示本帮助"
    Write-Host ""
    Write-Host "公开发布版："
    Write-Host "  一键编译发布版.bat"
    Write-Host "  读取环境变量 PCL_MS_CLIENT_ID（必填）和 PCL_CURSEFORGE_API_KEY（可选），编译进 exe。"
    Write-Host ""
    Write-Host "示例："
    Write-Host "  一键编译.bat"
    Write-Host "  一键编译.bat Release -Clean"
    Write-Host "  一键编译.bat Debug -NoOpen"
    Write-Host "  一键编译发布版.bat"
}

# ---------------------------------------------------------------------------
# 路径与环境
# ---------------------------------------------------------------------------

if ($Help) {
    Show-Help
    exit 0
}

$scriptRoot = $PSScriptRoot
if ([string]::IsNullOrEmpty($scriptRoot)) {
    $scriptRoot = Split-Path -Parent $PSCommandPath
}
$repoRoot = Split-Path -Parent (Split-Path -Parent $scriptRoot)

$vbprojPath = Join-Path $repoRoot "Plain Craft Launcher 2\Plain Craft Launcher 2.vbproj"
$vbprojDir = Split-Path -Parent $vbprojPath
$generatedDir = Join-Path $vbprojDir "obj\PublicRelease"
$generatedSecretsPath = Join-Path $generatedDir "PublicReleaseSecrets.g.vb"
$pclcsPath = Join-Path $repoRoot "PCLCS\PCLCS.csproj"
$sharedPath = Join-Path $repoRoot "MeloongCore\Shared\MeloongCore.csproj"
$wpfPath = Join-Path $repoRoot "MeloongCore\Wpf\MeloongCore.Wpf.csproj"
$binDir = Join-Path $repoRoot "Plain Craft Launcher 2\bin"
$exeLeaf = "Plain Craft Launcher - Phanes Edition.exe"
$exePath = Join-Path $binDir $exeLeaf
$artifactRoot = Join-Path $repoRoot "artifacts"
$logDir = Join-Path $artifactRoot "logs"
$outputRoot = Join-Path $artifactRoot "output"
$packageRoot = Join-Path $artifactRoot "packages"
$statePath = Join-Path $artifactRoot ".build-state.json"
$licencePath = Join-Path $repoRoot "LICENCE"

switch ($Configuration.ToLowerInvariant()) {
    "debug" { $Configuration = "Debug" }
    "release" { $Configuration = "Release" }
    "snapshot" { $Configuration = "Snapshot" }
}

if ($env:OS -ne "Windows_NT") {
    Stop-WithError "本脚本仅支持 Windows。" "请在 Windows 10/11 上运行。"
}

if (-not (Test-Path -LiteralPath $vbprojPath -PathType Leaf)) {
    Stop-WithError "未找到主工程：$vbprojPath" "请确认脚本位于仓库的 tools\build 目录下，且仓库结构完整。"
}
if (-not (Test-Path -LiteralPath $pclcsPath -PathType Leaf)) {
    Stop-WithError "未找到 PCLCS 工程：$pclcsPath" "请确认仓库结构完整。"
}

function Find-MSBuild {
    $candidates = New-Object System.Collections.Generic.List[string]

    $programFiles = ${env:ProgramFiles}
    $programFilesX86 = ${env:ProgramFiles(x86)}
    if ([string]::IsNullOrEmpty($programFilesX86)) {
        $programFilesX86 = $programFiles
    }

    $vswhere = Join-Path $programFilesX86 "Microsoft Visual Studio\Installer\vswhere.exe"
    if (Test-Path -LiteralPath $vswhere -PathType Leaf) {
        try {
            $found = & $vswhere -latest -products "*" -requires Microsoft.Component.MSBuild -find "MSBuild\**\Bin\MSBuild.exe" 2>$null
            foreach ($item in @($found)) {
                if (-not [string]::IsNullOrWhiteSpace([string]$item)) {
                    $candidates.Add(([string]$item).Trim())
                }
            }
        } catch {
        }
    }

    $command = Get-Command "msbuild.exe" -ErrorAction SilentlyContinue
    if ($null -ne $command -and -not [string]::IsNullOrWhiteSpace($command.Source)) {
        $candidates.Add($command.Source)
    }

    foreach ($base in @($programFiles, $programFilesX86)) {
        if ([string]::IsNullOrWhiteSpace($base)) {
            continue
        }
        foreach ($edition in @("Enterprise", "Professional", "Community", "BuildTools")) {
            $candidates.Add((Join-Path $base "Microsoft Visual Studio\2022\$edition\MSBuild\Current\Bin\MSBuild.exe"))
        }
    }

    foreach ($candidate in $candidates) {
        if (Test-Path -LiteralPath $candidate -PathType Leaf) {
            return (Resolve-Path -LiteralPath $candidate).Path
        }
    }
    return $null
}

function Show-BuildFailure {
    param(
        [int]$ExitCode,
        [string]$LogPath
    )

    Write-Host ""
    Write-Host "[错误] 编译失败（退出代码 $ExitCode）。" -ForegroundColor Red

    $hint = "请查看日志中的第一条错误；也可以在 Visual Studio 中打开解决方案获取更详细的定位信息。"
    if (Test-Path -LiteralPath $LogPath -PathType Leaf) {
        try {
            if (Select-String -LiteralPath $LogPath -Pattern "MSB3644" -Quiet) {
                $hint = "缺少 .NET Framework 4.8 目标包。请通过 Visual Studio Installer 安装「.NET Framework 4.8 开发工具」后重试。"
            } elseif (Select-String -LiteralPath $LogPath -Pattern "MSB3021|MSB3026|MSB3027|being used by another process|文件正由另一进程使用" -Quiet) {
                $hint = "输出文件被占用。请关闭正在运行的启动器或资源管理器预览窗口后重试。"
            } elseif (Select-String -LiteralPath $LogPath -Pattern "NU1101|NU1102|NU1301" -Quiet) {
                $hint = "NuGet 包还原失败。请检查网络、NuGet 源或代理，或确认缓存后加 -SkipRestore 重试。"
            }
        } catch {
        }

        Write-Host ""
        Write-Host "最近日志：" -ForegroundColor DarkGray
        try {
            Get-Content -LiteralPath $LogPath -Tail 40 -Encoding UTF8 | ForEach-Object {
                Write-Host "  $_"
            }
        } catch {
        }
        Write-Host ""
        Write-Host "完整日志：$LogPath" -ForegroundColor DarkGray
    } else {
        Write-Host "未找到构建日志：$LogPath" -ForegroundColor Yellow
    }

    Write-Host "建议：$hint" -ForegroundColor Yellow
    exit 1
}

function Get-MaskedValue {
    param([string]$Value)
    if ([string]::IsNullOrEmpty($Value)) {
        return "(未设置)"
    }
    if ($Value.Length -le 12) {
        return "***"
    }
    return $Value.Substring(0, 8) + "..." + $Value.Substring($Value.Length - 4)
}

function ConvertTo-VbStringLiteral {
    param([string]$Value)
    if ($null -eq $Value) {
        $Value = ""
    }
    if ($Value.Contains("`r") -or $Value.Contains("`n")) {
        Stop-WithError "环境变量值不能包含换行。" "请检查 PCL_MS_CLIENT_ID / PCL_CURSEFORGE_API_KEY。"
    }
    if ($Value -match "[^\x20-\x7E]") {
        Stop-WithError "环境变量值包含非 ASCII 字符。" "请检查 PCL_MS_CLIENT_ID / PCL_CURSEFORGE_API_KEY 是否被错误设置。"
    }
    return ('"' + $Value.Replace('"', '""') + '"')
}

function Resolve-PublicReleaseSettings {
    if ($Configuration -ne "Release") {
        Stop-WithError "公开发布版仅支持 Release 配置。" "请直接双击 一键编译发布版.bat，或运行 Build.ps1 -PublicRelease。"
    }

    $clientIdRaw = $env:PCL_MS_CLIENT_ID
    if ([string]::IsNullOrWhiteSpace($clientIdRaw)) {
        Stop-WithError "未找到环境变量 PCL_MS_CLIENT_ID。" "请先设置 Microsoft Entra 应用（客户端）ID，例如：setx PCL_MS_CLIENT_ID `"xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx`"。如果是在当前终端临时设置的，请从同一个终端运行；双击入口需要 setx 后重新打开资源管理器/终端。"
    }
    $clientIdRaw = $clientIdRaw.Trim().Trim('"')
    $parsedGuid = [Guid]::Empty
    if (-not [Guid]::TryParse($clientIdRaw, [ref]$parsedGuid)) {
        Stop-WithError "PCL_MS_CLIENT_ID 不是有效的 GUID（当前值：$(Get-MaskedValue $clientIdRaw)）。" "请使用 Microsoft Entra 应用的「应用程序（客户端）ID」，而不是目录（租户）ID。"
    }
    $clientId = $parsedGuid.ToString()

    $curseForgeKey = [string]$env:PCL_CURSEFORGE_API_KEY
    $curseForgeKey = $curseForgeKey.Trim()
    if ($curseForgeKey.Length -gt 0 -and $curseForgeKey -match "[^\x20-\x7E]") {
        Stop-WithError "PCL_CURSEFORGE_API_KEY 包含非 ASCII 字符。" "请检查环境变量是否被错误设置。"
    }

    return [pscustomobject]@{
        ClientId = $clientId
        CurseForgeApiKey = $curseForgeKey
    }
}

function Write-PublicReleaseSecretsSource {
    param(
        [string]$ClientId,
        [string]$CurseForgeApiKey
    )

    try {
        if (-not (Test-Path -LiteralPath $generatedDir -PathType Container)) {
            New-Item -ItemType Directory -Path $generatedDir -Force | Out-Null
        }
    } catch {
        Stop-WithError "无法创建公开发布版生成目录：$generatedDir" "请检查仓库目录的写入权限。"
    }

    $clientLiteral = ConvertTo-VbStringLiteral $ClientId
    $keyLiteral = ConvertTo-VbStringLiteral $CurseForgeApiKey
    $lines = @(
        "' <auto-generated>",
        "' Generated by tools\build\Build.ps1 -PublicRelease. Do not commit.",
        "Friend NotInheritable Class PublicReleaseSecrets",
        "    Friend Const OAuthClientId As String = $clientLiteral",
        "    Friend Const CurseForgeAPIKey As String = $keyLiteral",
        "End Class"
    )
    $content = ($lines -join "`r`n") + "`r`n"

    try {
        $existing = ""
        if (Test-Path -LiteralPath $generatedSecretsPath -PathType Leaf) {
            $existing = [IO.File]::ReadAllText($generatedSecretsPath)
        }
        if ($existing -ne $content) {
            [IO.File]::WriteAllText($generatedSecretsPath, $content, [Text.Encoding]::ASCII)
        }
    } catch {
        Stop-WithError "无法写入公开发布版密钥文件：$generatedSecretsPath" "请检查仓库目录的写入权限；错误：$($_.Exception.Message)"
    }
}

function Test-EmbeddedPublicReleaseValue {
    param(
        [string]$ExePath,
        [string]$Value
    )
    if ([string]::IsNullOrEmpty($Value)) {
        return $true
    }
    $bytes = [IO.File]::ReadAllBytes($ExePath)
    $evenLength = $bytes.Length - ($bytes.Length % 2)
    if ($evenLength -le 0) {
        return $false
    }
    $text = [Text.Encoding]::Unicode.GetString($bytes, 0, $evenLength)
    return $text.Contains($Value)
}

$publicReleaseSecrets = $null
if ($PublicRelease) {
    $publicReleaseSecrets = Resolve-PublicReleaseSettings
}

Write-Host ""
if ($PublicRelease) {
    Write-Host "PCL - Phanes Edition 公开发布版编译" -ForegroundColor Cyan
} else {
    Write-Host "PCL - Phanes Edition 一键编译" -ForegroundColor Cyan
}
Write-Host "  配置：$Configuration"
if ($PublicRelease) {
    Write-Host "  模式：公开发布版（Client ID：$(Get-MaskedValue $publicReleaseSecrets.ClientId)）"
} else {
    Write-Host "  模式：普通构建"
}
Write-Host "  仓库：$repoRoot"

Write-Step "检查编译环境"

$msbuild = Find-MSBuild
if ([string]::IsNullOrEmpty($msbuild)) {
    Stop-WithError "未找到 MSBuild。" "请安装 Visual Studio 2022（勾选「.NET 桌面开发」和「.NET Framework 4.8 开发工具」），或把 MSBuild.exe 加入 PATH。"
}
Write-Ok "MSBuild：$msbuild"

try {
    $msbuildVersionText = [string](& $msbuild -version -nologo 2>$null | Select-Object -Last 1)
} catch {
    $msbuildVersionText = ""
}
$msbuildVersionText = $msbuildVersionText.Trim()
if (-not [string]::IsNullOrWhiteSpace($msbuildVersionText)) {
    $numericVersion = ($msbuildVersionText -replace "[^0-9\.].*$", "")
    try {
        $msbuildVersion = [version]$numericVersion
        if ($msbuildVersion.Major -lt 16) {
            Stop-WithError "MSBuild 版本过低：$msbuildVersionText" "请使用 Visual Studio 2022 自带的 MSBuild。"
        }
    } catch {
    }
    Write-Ok "MSBuild 版本：$msbuildVersionText"
}

$programFilesX86 = ${env:ProgramFiles(x86)}
if ([string]::IsNullOrEmpty($programFilesX86)) {
    $programFilesX86 = ${env:ProgramFiles}
}
$targetingPack = Join-Path $programFilesX86 "Reference Assemblies\Microsoft\Framework\.NETFramework\v4.8"
if (Test-Path -LiteralPath $targetingPack -PathType Container) {
    Write-Ok ".NET Framework 4.8 引用程序集"
} else {
    Write-Warn "未检测到 .NET Framework 4.8 引用程序集：$targetingPack"
    Write-Warn "如果编译报 MSB3644，请通过 Visual Studio Installer 安装「.NET Framework 4.8 开发工具」。"
}

$running = Get-Process -Name "Plain Craft Launcher - Phanes Edition" -ErrorAction SilentlyContinue
if ($null -ne $running) {
    Stop-WithError "启动器正在运行。" "请先关闭所有 PCL - Phanes Edition 窗口，再重新运行本脚本。"
}

$dotnetCommand = Get-Command "dotnet.exe" -ErrorAction SilentlyContinue
if ($null -ne $dotnetCommand) {
    $dotnetPath = $dotnetCommand.Source
} else {
    $dotnetPath = $null
    Write-Warn "未找到 dotnet，将改用 msbuild -t:Restore 还原依赖。"
}

try {
    foreach ($dir in @($logDir, $outputRoot, $packageRoot)) {
        if (-not (Test-Path -LiteralPath $dir -PathType Container)) {
            New-Item -ItemType Directory -Path $dir -Force | Out-Null
        }
    }
} catch {
    Stop-WithError "无法创建输出目录：$artifactRoot" "请检查仓库目录的写入权限。"
}

# ---------------------------------------------------------------------------
# 依赖还原
# ---------------------------------------------------------------------------

if ($SkipRestore) {
    Write-Step "跳过 NuGet 还原（-SkipRestore）"
} else {
    Write-Step "还原 NuGet 依赖"
    $restoreExit = 0
    if ($null -ne $dotnetPath) {
        Write-Info "dotnet restore `"$pclcsPath`""
        & $dotnetPath restore $pclcsPath --nologo --verbosity minimal
        $restoreExit = $LASTEXITCODE
    } else {
        Write-Info "msbuild `"$pclcsPath`" /t:Restore"
        & $msbuild $pclcsPath /nologo /v:minimal /t:Restore "/p:Configuration=$Configuration"
        $restoreExit = $LASTEXITCODE
    }
    if ($restoreExit -ne 0) {
        Stop-WithError "NuGet 还原失败（退出代码 $restoreExit）。" "请检查网络、NuGet 源或代理；确认已有缓存时可加 -SkipRestore 重试。"
    }
    Write-Ok "依赖还原完成"
}

# ---------------------------------------------------------------------------
# 清理判断
# ---------------------------------------------------------------------------

$needClean = [bool]$Clean
$cleanReason = ""
if ($Clean) {
    $cleanReason = "指定了 -Clean"
} elseif (-not (Test-Path -LiteralPath $exePath -PathType Leaf)) {
    $needClean = $true
    $cleanReason = "尚未生成输出文件"
} elseif (-not (Test-Path -LiteralPath $statePath -PathType Leaf)) {
    $needClean = $true
    $cleanReason = "没有可用的构建状态记录"
} else {
    try {
        $previousState = Get-Content -LiteralPath $statePath -Raw -Encoding UTF8 | ConvertFrom-Json
        $currentTicks = (Get-Item -LiteralPath $exePath).LastWriteTimeUtc.Ticks
        $previousTicks = [long]$previousState.ExeLastWriteTimeUtc
        $previousPublic = $false
        $publicProperty = $previousState.PSObject.Properties["PublicRelease"]
        if ($null -ne $publicProperty) {
            $previousPublic = [bool]$publicProperty.Value
        }
        if ([string]$previousState.Configuration -ne $Configuration) {
            $needClean = $true
            $cleanReason = "配置从 $($previousState.Configuration) 切换为 $Configuration"
        } elseif ($previousPublic -ne [bool]$PublicRelease) {
            $needClean = $true
            if ($PublicRelease) {
                $cleanReason = "构建模式切换为公开发布版"
            } else {
                $cleanReason = "构建模式切换为普通构建"
            }
        } elseif ($previousTicks -ne $currentTicks) {
            $needClean = $true
            $cleanReason = "输出文件被其他构建更新"
        }
    } catch {
        $needClean = $true
        $cleanReason = "构建状态记录无法读取"
    }
}

if ($needClean) {
    Write-Step "清理编译产物（$cleanReason）"
    $cleanTargets = @(
        @{ Path = $sharedPath; Name = "MeloongCore" },
        @{ Path = $wpfPath; Name = "MeloongCore.Wpf" },
        @{ Path = $pclcsPath; Name = "PCLCS" },
        @{ Path = $vbprojPath; Name = "主工程" }
    )
    foreach ($target in $cleanTargets) {
        & $msbuild $target.Path /nologo /v:minimal /t:Clean "/p:Configuration=$Configuration"
        if ($LASTEXITCODE -ne 0) {
            Stop-WithError "清理 $($target.Name) 失败（退出代码 $LASTEXITCODE）。" "请查看上方输出，或稍后重试。"
        }
    }
    Write-Ok "清理完成"
} else {
    Write-Step "增量编译（未检测到配置或输出变化；可用 -Clean 强制完整编译）"
}

if ($PublicRelease) {
    Write-Step "生成公开发布版密钥"
    Write-PublicReleaseSecretsSource -ClientId $publicReleaseSecrets.ClientId -CurseForgeApiKey $publicReleaseSecrets.CurseForgeApiKey
    Write-Ok "生成文件：$generatedSecretsPath"
    Write-Ok "Microsoft Client ID：$(Get-MaskedValue $publicReleaseSecrets.ClientId)"
    if ([string]::IsNullOrEmpty($publicReleaseSecrets.CurseForgeApiKey)) {
        Write-Warn "未找到 PCL_CURSEFORGE_API_KEY，公开发布版将不内置 CurseForge Key。"
    } else {
        Write-Ok "CurseForge API Key：$(Get-MaskedValue $publicReleaseSecrets.CurseForgeApiKey)"
    }
}

# ---------------------------------------------------------------------------
# 编译
# ---------------------------------------------------------------------------

$timestamp = Get-Date -Format "yyyyMMdd-HHmmss"
$logPath = Join-Path $logDir "build-$timestamp.log"
$stopwatch = [Diagnostics.Stopwatch]::StartNew()

Write-Step "编译主程序（$Configuration）"
Write-Info "日志：$logPath"
$buildArgs = @(
    $vbprojPath,
    "/nologo",
    "/m",
    "/t:Build",
    "/p:Configuration=$Configuration"
)
if ($PublicRelease) {
    $buildArgs += "/p:PublicRelease=true"
}
$buildArgs += @(
    "/v:minimal",
    "/fl",
    "/flp:LogFile=$logPath;Verbosity=minimal;Encoding=UTF-8"
)
& $msbuild @buildArgs
$buildExit = $LASTEXITCODE
$stopwatch.Stop()
$elapsed = $stopwatch.Elapsed.ToString("mm\:ss")

if ($buildExit -ne 0) {
    Show-BuildFailure -ExitCode $buildExit -LogPath $logPath
}

if (-not (Test-Path -LiteralPath $exePath -PathType Leaf)) {
    Stop-WithError "编译报告成功，但没有找到输出文件：$exePath" "请加 -Clean 重试，或用 Visual Studio 打开解决方案检查输出路径。"
}

$exeItem = Get-Item -LiteralPath $exePath
if ($exeItem.Length -le 0) {
    Stop-WithError "输出文件大小为 0：$exePath" "请加 -Clean 重试。"
}

$version = "1.0.0"
try {
    $fileVersion = [Diagnostics.FileVersionInfo]::GetVersionInfo($exePath).FileVersion
    if (-not [string]::IsNullOrWhiteSpace($fileVersion)) {
        $cleanVersion = ($fileVersion -replace "[^0-9\.].*$", "").Trim(".")
        if (-not [string]::IsNullOrWhiteSpace($cleanVersion)) {
            $version = $cleanVersion
        }
    }
} catch {
}

Write-Ok "编译成功，用时 $elapsed"
Write-Ok "输出文件：$exePath（$([math]::Round($exeItem.Length / 1MB, 1)) MB，版本 $version）"
Write-Info "最后写入：$($exeItem.LastWriteTime.ToString("yyyy-MM-dd HH:mm:ss"))"

if ($PublicRelease) {
    Write-Step "校验公开发布版密钥"
    if (-not (Test-EmbeddedPublicReleaseValue -ExePath $exePath -Value $publicReleaseSecrets.ClientId)) {
        Stop-WithError "公开发布版校验失败：产物中未找到内置的 Microsoft Client ID。" "请检查 vbproj 中的 PublicRelease 条件、PUBLIC_RELEASE 编译常量与生成文件：$generatedSecretsPath"
    }
    Write-Ok "Microsoft Client ID 已确认内置"
    if (-not [string]::IsNullOrEmpty($publicReleaseSecrets.CurseForgeApiKey)) {
        if (-not (Test-EmbeddedPublicReleaseValue -ExePath $exePath -Value $publicReleaseSecrets.CurseForgeApiKey)) {
            Stop-WithError "公开发布版校验失败：产物中未找到内置的 CurseForge API Key。" "请检查生成文件：$generatedSecretsPath"
        }
        Write-Ok "CurseForge API Key 已确认内置"
    }
}

$buildState = [ordered]@{
    Configuration = $Configuration
    PublicRelease = [bool]$PublicRelease
    ExeLastWriteTimeUtc = $exeItem.LastWriteTimeUtc.Ticks
    BuiltAt = (Get-Date).ToString("o")
}
try {
    $buildState | ConvertTo-Json | Set-Content -LiteralPath $statePath -Encoding UTF8
} catch {
    Write-Warn "无法写入构建状态记录：$statePath"
}

# ---------------------------------------------------------------------------
# 整理发行目录与压缩包
# ---------------------------------------------------------------------------

$stageDirFull = $null
$zipPath = $null
$packageLabel = $Configuration
if ($PublicRelease) {
    $packageLabel = "PublicRelease"
}

if ($NoPackage) {
    Write-Step "跳过发行目录与压缩包（-NoPackage）"
} else {
    Write-Step "整理发行目录"

    $outputRootFull = [IO.Path]::GetFullPath($outputRoot)
    $stageDirFull = [IO.Path]::GetFullPath((Join-Path $outputRootFull ("PCL-Phanes-{0}-{1}" -f $version, $packageLabel)))
    $outputPrefix = $outputRootFull.TrimEnd([IO.Path]::DirectorySeparatorChar) + [IO.Path]::DirectorySeparatorChar

    if (-not $stageDirFull.StartsWith($outputPrefix, [StringComparison]::OrdinalIgnoreCase)) {
        Stop-WithError "输出目录不在预期位置，已中止：$stageDirFull" "请检查 artifacts\output 目录。"
    }

    try {
        if (Test-Path -LiteralPath $stageDirFull) {
            Remove-Item -LiteralPath $stageDirFull -Recurse -Force
        }
        New-Item -ItemType Directory -Path $stageDirFull -Force | Out-Null
    } catch {
        Stop-WithError "无法重建发行目录：$stageDirFull" "请关闭占用该目录的程序后重试。"
    }

    try {
        $runtimeFiles = @(Get-ChildItem -LiteralPath $binDir -File | Where-Object { $_.Extension -in @(".exe", ".dll", ".config") })
        if ($runtimeFiles.Count -eq 0) {
            Stop-WithError "bin 目录下没有可复制的运行时文件：$binDir" "请先完成一次编译。"
        }
        foreach ($file in $runtimeFiles) {
            Copy-Item -LiteralPath $file.FullName -Destination $stageDirFull -Force
        }
        if (Test-Path -LiteralPath $licencePath -PathType Leaf) {
            Copy-Item -LiteralPath $licencePath -Destination $stageDirFull -Force
        } else {
            Write-Warn "未找到 LICENCE，压缩包中将不包含许可证文件。"
        }
    } catch {
        Stop-WithError "复制运行时文件失败。" "请关闭占用发行目录或 bin 目录的程序后重试。错误：$($_.Exception.Message)"
    }

    $stagedFiles = @(Get-ChildItem -LiteralPath $stageDirFull -File)
    Write-Ok "发行目录：$stageDirFull（$($stagedFiles.Count) 个文件）"

    Write-Step "生成压缩包"
    $zipPath = Join-Path $packageRoot ("PCL-Phanes-{0}-{1}-{2}.zip" -f $version, $packageLabel, $timestamp)
    try {
        if (Test-Path -LiteralPath $zipPath) {
            Remove-Item -LiteralPath $zipPath -Force
        }
        Compress-Archive -Path (Join-Path $stageDirFull "*") -DestinationPath $zipPath -CompressionLevel Optimal -Force
        Write-Ok "压缩包：$zipPath"
    } catch {
        Write-Warn "压缩包生成失败：$($_.Exception.Message)"
        Write-Warn "发行目录已生成，可手动压缩：$stageDirFull"
        $zipPath = $null
    }
}

# ---------------------------------------------------------------------------
# 汇总
# ---------------------------------------------------------------------------

Write-Host ""
$summaryTitle = "编译成功"
if ($PublicRelease) {
    $summaryTitle = "公开发布版编译成功"
}
Write-Host "==================== $summaryTitle ====================" -ForegroundColor Green
Write-Host "  配置：$Configuration"
if ($PublicRelease) {
    Write-Host "  模式：公开发布版"
    Write-Host "  Client ID：$(Get-MaskedValue $publicReleaseSecrets.ClientId)"
    if ([string]::IsNullOrEmpty($publicReleaseSecrets.CurseForgeApiKey)) {
        Write-Host "  CurseForge Key：未内置"
    } else {
        Write-Host "  CurseForge Key：$(Get-MaskedValue $publicReleaseSecrets.CurseForgeApiKey)"
    }
}
Write-Host "  用时：$elapsed"
Write-Host "  程序：$exePath"
if ($null -ne $stageDirFull) {
    Write-Host "  发行目录：$stageDirFull"
}
if ($null -ne $zipPath) {
    Write-Host "  压缩包：$zipPath"
}
Write-Host "  日志：$logPath"
Write-Host "=================================================="

if (-not $NoOpen) {
    $openTarget = $binDir
    if ($null -ne $stageDirFull) {
        $openTarget = $stageDirFull
    }
    try {
        Start-Process -FilePath "explorer.exe" -ArgumentList ('"{0}"' -f $openTarget) | Out-Null
    } catch {
        Write-Warn "打开输出目录失败：$($_.Exception.Message)"
    }
}

if ($Run) {
    Write-Step "启动程序"
    $runTarget = $exePath
    if ($null -ne $stageDirFull) {
        $runTarget = Join-Path $stageDirFull $exeLeaf
    }
    try {
        Start-Process -FilePath $runTarget -WorkingDirectory (Split-Path -Parent $runTarget) | Out-Null
        Write-Ok "已启动：$runTarget"
    } catch {
        Stop-WithError "启动失败：$($_.Exception.Message)" "可以手动双击发行目录中的 exe。"
    }
}

exit 0
