#requires -Version 5.1
<#
.SYNOPSIS
    PCL - Phanes Edition MCP 服务器冒烟测试。

.DESCRIPTION
    需要先启动启动器并在 设置 → 其他 → AI 接入（MCP） 中启用 MCP。
    默认从构建输出目录读取 McpClient.json。

.EXAMPLE
    powershell -ExecutionPolicy Bypass -File .\docs\mcp-smoke-test.ps1
#>
[CmdletBinding()]
param(
    [string]$ConfigPath = "",
    [switch]$SkipSearch
)

$ErrorActionPreference = "Stop"

if ([string]::IsNullOrEmpty($ConfigPath)) {
    $scriptRoot = $PSScriptRoot
    if ([string]::IsNullOrEmpty($scriptRoot)) { $scriptRoot = Split-Path -Parent $PSCommandPath }
    $ConfigPath = Join-Path $scriptRoot "..\Plain Craft Launcher 2\bin\PCL-Phanes\McpClient.json"
}

function Assert-True {
    param([bool]$Condition, [string]$Message)
    if (-not $Condition) { throw "断言失败：$Message" }
    Write-Host "  [OK] $Message" -ForegroundColor Green
}

function Get-StatusCode {
    param($ErrorRecord)
    try { return [int]$ErrorRecord.Exception.Response.StatusCode } catch { return 0 }
}

function Invoke-Mcp {
    param(
        [string]$Url,
        [string]$Token,
        [string]$SessionId,
        [int]$Id,
        [string]$Method,
        $Params = @{}
    )
    $headers = @{
        Authorization = "Bearer $Token"
        "Content-Type" = "application/json"
        "Accept" = "application/json, text/event-stream"
    }
    if (-not [string]::IsNullOrEmpty($SessionId)) { $headers["Mcp-Session-Id"] = $SessionId }
    $body = @{
        jsonrpc = "2.0"
        id = $Id
        method = $Method
        params = $Params
    } | ConvertTo-Json -Compress -Depth 20
    $response = Invoke-WebRequest -Uri $Url -Method Post -Headers $headers -Body $body -TimeoutSec 60 -UseBasicParsing
    return ($response.Content | ConvertFrom-Json)
}

function Invoke-Tool {
    param(
        [string]$Url,
        [string]$Token,
        [string]$SessionId,
        [int]$Id,
        [string]$ToolName,
        $Arguments = @{}
    )
    $response = Invoke-Mcp -Url $Url -Token $Token -SessionId $SessionId -Id $Id -Method "tools/call" -Params @{
        name = $ToolName
        arguments = $Arguments
    }
    $text = $response.result.content[0].text
    return ($text | ConvertFrom-Json)
}

Write-Host "PCL MCP 冒烟测试" -ForegroundColor Cyan
Assert-True (Test-Path $ConfigPath) "找到客户端配置：$ConfigPath"
$config = Get-Content $ConfigPath -Raw | ConvertFrom-Json
Assert-True (-not [string]::IsNullOrEmpty($config.url)) "配置包含 url"
Assert-True (-not [string]::IsNullOrEmpty($config.token)) "配置包含 token"

$url = [string]$config.url
$token = [string]$config.token
$baseHeaders = @{
    Authorization = "Bearer $token"
    "Content-Type" = "application/json"
    "Accept" = "application/json, text/event-stream"
}

Write-Host "1. 健康检查" -ForegroundColor Cyan
$health = Invoke-RestMethod -Uri ($url -replace "/mcp$", "/health") -TimeoutSec 10
Assert-True ($health.status -eq "ok") "健康检查返回 ok"

Write-Host "2. initialize" -ForegroundColor Cyan
$initBody = @{
    jsonrpc = "2.0"
    id = 1
    method = "initialize"
    params = @{
        protocolVersion = "2025-06-18"
        capabilities = @{}
        clientInfo = @{ name = "pcl-smoke-test"; version = "1.0" }
    }
} | ConvertTo-Json -Compress -Depth 10
$initResponse = Invoke-WebRequest -Uri $url -Method Post -Headers $baseHeaders -Body $initBody -TimeoutSec 15 -UseBasicParsing
Assert-True ($initResponse.StatusCode -eq 200) "initialize 返回 200"
$sessionId = [string]($initResponse.Headers["Mcp-Session-Id"] | Select-Object -First 1)
Assert-True (-not [string]::IsNullOrEmpty($sessionId)) "返回 Mcp-Session-Id"
$init = $initResponse.Content | ConvertFrom-Json
Assert-True ($init.result.protocolVersion -eq "2025-06-18") "协议版本协商正确"
Assert-True ($init.result.serverInfo.name -eq "pcl-phanes") "serverInfo 正确"
Assert-True ($null -ne $init.result.capabilities.tools) "声明 tools 能力"

Write-Host "3. notifications/initialized" -ForegroundColor Cyan
$headersWithSession = @{
    Authorization = "Bearer $token"
    "Content-Type" = "application/json"
    "Accept" = "application/json, text/event-stream"
    "Mcp-Session-Id" = $sessionId
}
$notifyBody = '{"jsonrpc":"2.0","method":"notifications/initialized"}'
$notifyResponse = Invoke-WebRequest -Uri $url -Method Post -Headers $headersWithSession -Body $notifyBody -TimeoutSec 15 -UseBasicParsing
Assert-True ($notifyResponse.StatusCode -eq 202) "通知返回 202"

Write-Host "4. tools/list" -ForegroundColor Cyan
$list = Invoke-Mcp -Url $url -Token $token -SessionId $sessionId -Id 2 -Method "tools/list" -Params @{}
$tools = @($list.result.tools)
Assert-True ($tools.Count -eq 22) "工具数量为 22"
foreach ($name in @("pcl_get_status", "pcl_list_instances", "pcl_launch_game", "pcl_set_mod_enabled", "pcl_delete_mod", "pcl_shutdown_launcher")) {
    Assert-True ($tools.name -contains $name) "包含工具 $name"
}
$launchTool = $tools | Where-Object { $_.name -eq "pcl_launch_game" }
Assert-True ($launchTool.inputSchema.properties.account_mode.enum -contains "offline") "启动工具声明 offline 模式"
Assert-True ($launchTool.inputSchema.properties.account_mode.enum -contains "microsoft") "启动工具声明 microsoft 模式"
Assert-True ($launchTool.inputSchema.properties.PSObject.Properties.Name -contains "offline_username") "启动工具声明 offline_username"

Write-Host "5. 调用只读工具" -ForegroundColor Cyan
$status = Invoke-Tool -Url $url -Token $token -SessionId $sessionId -Id 3 -ToolName "pcl_get_status"
Assert-True ($status.ok -eq $true) "pcl_get_status 成功"
$folders = Invoke-Tool -Url $url -Token $token -SessionId $sessionId -Id 4 -ToolName "pcl_list_folders"
Assert-True ($folders.ok -eq $true) "pcl_list_folders 成功"
$settings = Invoke-Tool -Url $url -Token $token -SessionId $sessionId -Id 5 -ToolName "pcl_get_settings" -Arguments @{ keys = @("ToolDownloadThread") }
Assert-True ($settings.ok -eq $true) "pcl_get_settings 成功"
Assert-True (@($settings.result.settings).Count -eq 1) "pcl_get_settings 按键过滤"
$log = Invoke-Tool -Url $url -Token $token -SessionId $sessionId -Id 6 -ToolName "pcl_read_log" -Arguments @{ lines = 3 }
Assert-True ($log.ok -eq $true) "pcl_read_log 成功"

if (-not $SkipSearch) {
    Write-Host "6. 社区资源搜索" -ForegroundColor Cyan
    $search = Invoke-Tool -Url $url -Token $token -SessionId $sessionId -Id 7 -ToolName "pcl_search_resources" -Arguments @{
        query = "sodium"; type = "mod"; source = "modrinth"; limit = 1
    }
    Assert-True ($search.ok -eq $true) "pcl_search_resources 成功"
    Assert-True (@($search.result.results).Count -ge 1) "搜索返回结果"
}

Write-Host "7. 错误处理" -ForegroundColor Cyan
$protected = Invoke-Tool -Url $url -Token $token -SessionId $sessionId -Id 8 -ToolName "pcl_set_setting" -Arguments @{
    key = "SystemMcpServerEnabled"; value = $false
}
Assert-True ($protected.ok -eq $false) "受保护设置拒绝写入"
Assert-True ($protected.error.code -eq "protected_setting") "受保护设置返回 protected_setting"

try {
    $badHeaders = @{
        Authorization = "Bearer invalid-token"
        "Content-Type" = "application/json"
        "Accept" = "application/json"
    }
    Invoke-WebRequest -Uri $url -Method Post -Headers $badHeaders -Body $initBody -TimeoutSec 10 -UseBasicParsing | Out-Null
    throw "错误令牌未被拒绝"
} catch {
    $statusCode = Get-StatusCode $_
    Assert-True ($statusCode -eq 401) "错误令牌返回 401"
}

try {
    $deadHeaders = $headersWithSession.Clone()
    $deadHeaders["Mcp-Session-Id"] = "deadbeef"
    $body = '{"jsonrpc":"2.0","id":9,"method":"tools/list"}'
    Invoke-WebRequest -Uri $url -Method Post -Headers $deadHeaders -Body $body -TimeoutSec 10 -UseBasicParsing | Out-Null
    throw "未知会话未被拒绝"
} catch {
    $statusCode = Get-StatusCode $_
    Assert-True ($statusCode -eq 404) "未知会话返回 404"
}

try {
    $badJson = "{not-json"
    Invoke-WebRequest -Uri $url -Method Post -Headers $headersWithSession -Body $badJson -TimeoutSec 10 -UseBasicParsing | Out-Null
    throw "非法 JSON 未被拒绝"
} catch {
    $statusCode = Get-StatusCode $_
    Assert-True ($statusCode -eq 400) "非法 JSON 返回 400"
}

Write-Host ""
Write-Host "MCP 冒烟测试全部通过。" -ForegroundColor Green
