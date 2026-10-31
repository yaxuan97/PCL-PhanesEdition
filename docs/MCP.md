# AI 接入（MCP）

PCL - Phanes Edition 内置一个可选的 **MCP（Model Context Protocol）服务器**，让 AI 客户端可以查询启动器状态，并在授权范围内控制启动、实例、Mod、设置与页面。

> MCP 服务器默认关闭，只监听 `127.0.0.1`，并且需要访问令牌。请只在了解风险的情况下启用“完全控制”权限。

## 1. 能力概览

- **查询**：启动器状态、Minecraft 文件夹、实例、Mod、设置、登录玩家、任务、日志、社区资源搜索。
- **控制**：选择实例、启动/停止游戏、修改非加密设置、启用/禁用 Mod、切换页面、取消任务、安装原版 Minecraft。
- **完全控制**：删除 Mod、关闭启动器。

MCP 不会暴露加密设置（登录密码、令牌、访问令牌等），也不能修改 MCP 自身的启用状态、端口、权限或访问令牌。

## 2. 启用

1. 打开 **设置 → 其他 → AI 接入（MCP）**。
2. 勾选 **启用 MCP 服务器**。
3. 按需选择 **访问权限**：
   - **只读**：只能查询。
   - **标准控制**：可以启动/停止游戏、管理 Mod、修改大部分设置。
   - **完全控制**：额外允许删除 Mod、关闭启动器。
4. 服务器默认监听 `127.0.0.1:37821`。可修改端口；修改后会立即重启服务器。
5. 启用时会自动生成访问令牌。可用 **复制** 获取令牌，用 **重新生成** 使旧客户端失效。

启用成功后，设置页会显示：

```text
运行中：http://127.0.0.1:37821/mcp（权限：标准控制）
```

同时启动器会在可执行文件旁写入桥接客户端配置：

```text
<PCL.exe 所在目录>\PCL-Phanes\McpClient.json
```

禁用 MCP 或退出启动器时，该文件会被删除。

## 3. 客户端配置

### 3.1 Claude Desktop 等 stdio 客户端（推荐）

`PCL.exe --mcp` 会以 stdio 桥接模式启动，把 MCP 消息转发给已经运行的启动器。启动器需要先运行并启用 MCP。

```json
{
  "mcpServers": {
    "pcl-phanes": {
      "command": "C:\\Path\\To\\Plain Craft Launcher - Phanes Edition.exe",
      "args": ["--mcp"]
    }
  }
}
```

也可以在设置页点击 **复制客户端配置**，直接得到这段 JSON。

> `--mcp` 模式只负责桥接，不会打开新的启动器窗口。若启动器尚未运行，客户端会提示无法连接。

### 3.2 Cursor / VS Code 等 Streamable HTTP 客户端

```json
{
  "mcpServers": {
    "pcl-phanes": {
      "url": "http://127.0.0.1:37821/mcp",
      "headers": {
        "Authorization": "Bearer <在设置页复制的访问令牌>"
      }
    }
  }
}
```

不同客户端的配置字段可能略有差异，请以客户端文档为准。服务器实现了 MCP 的 Streamable HTTP 传输（`POST /mcp`），不提供旧版 HTTP+SSE；旧客户端请使用 `--mcp` 桥接。

### 3.3 通用 HTTP 探测

```powershell
# 健康检查（不需要令牌）
Invoke-RestMethod http://127.0.0.1:37821/health

# 读取桥接配置中的地址与令牌
$cfg = Get-Content ".\PCL-Phanes\McpClient.json" -Raw | ConvertFrom-Json
$headers = @{
    Authorization = "Bearer $($cfg.token)"
    "Content-Type" = "application/json"
    "Accept" = "application/json, text/event-stream"
}
$body = '{"jsonrpc":"2.0","id":1,"method":"initialize","params":{"protocolVersion":"2025-06-18"}}'
Invoke-RestMethod -Uri $cfg.url -Method Post -Headers $headers -Body $body
```

## 4. 协议

- JSON-RPC 2.0，换行分隔的 stdio 桥接。
- 支持协议版本：`2024-11-05`、`2025-03-26`、`2025-06-18`。
- 支持方法：`initialize`、`notifications/initialized`、`notifications/cancelled`、`ping`、`tools/list`、`tools/call`。
- 支持批量请求。
- 服务器能力只声明 `tools`；不支持 `resources`、`prompts`、`completion`。
- HTTP 端点：
  - `POST /mcp`：JSON-RPC。
  - `GET /health`：健康检查。
  - `DELETE /mcp`：销毁会话。
  - `GET /mcp`：返回 405（不支持服务器主动 SSE）。
- 认证：`Authorization: Bearer <token>`。
- 请求体上限 1 MB；最多 4 个并发请求。
- 会话：`initialize` 返回 `Mcp-Session-Id`，后续请求建议携带；未知会话返回 404，客户端可重新初始化。

## 5. 工具参考

“权限”列表示调用所需的最低 MCP 权限。

### 只读（权限 0）

| 工具 | 参数 | 说明 |
| --- | --- | --- |
| `pcl_get_status` | 无 | 版本、当前实例、登录玩家、运行中的游戏、启动进度、任务摘要、MCP 状态 |
| `pcl_list_folders` | 无 | Minecraft 文件夹列表与当前选中项 |
| `pcl_list_instances` | `refresh` bool | 实例列表、状态、版本与选中状态 |
| `pcl_get_instance` | `name` string | 单个实例的详细路径、版本、Mod 数量 |
| `pcl_list_mods` | `instance` string、`include_disabled` bool | 实例 Mod 列表 |
| `pcl_get_settings` | `scope`、`instance`、`keys` | 非加密设置值、类型、默认值与来源 |
| `pcl_get_login` | 无 | 登录方式与玩家名 |
| `pcl_list_tasks` | 无 | 任务加载器名称、状态、进度、错误 |
| `pcl_read_log` | `file` 1–5、`lines` 1–500 | 启动器日志尾部 |
| `pcl_search_resources` | `query`、`type`、`source`、`game_version`、`loader`、`limit` | CurseForge / Modrinth 搜索 |

### 标准控制（权限 1）

| 工具 | 参数 | 说明 |
| --- | --- | --- |
| `pcl_select_instance` | `name` | 切换当前选中实例 |
| `pcl_launch_game` | `instance`、`account_mode`、`offline_username`、`server_ip`、`extra_args`、`wait`、`timeout_seconds` | 启动游戏；可按正版账号或离线身份运行 |
| `pcl_stop_game` | 无 | 结束由启动器跟踪的 Minecraft 进程 |
| `pcl_set_setting` | `key`、`value`、`scope`、`instance` | 修改非加密、非保护设置 |
| `pcl_reset_setting` | `key`、`scope`、`instance` | 恢复设置默认值 |
| `pcl_set_mod_enabled` | `instance`、`file`、`enabled` | 启用/禁用 Mod |
| `pcl_navigate` | `page`、`subpage`、`show` | 切换启动器页面 |
| `pcl_show_launcher` | 无 | 恢复并置顶启动器窗口 |
| `pcl_cancel_task` | `id`、`name`、`all` | 取消任务 |
| `pcl_install_vanilla` | `mc_version`、`name` | 安装原版 Minecraft |

### 完全控制（权限 2）

| 工具 | 参数 | 说明 |
| --- | --- | --- |
| `pcl_delete_mod` | `instance`、`file`、`confirm` | 删除 Mod 到回收站；`confirm` 必须为 `true` |
| `pcl_shutdown_launcher` | `delay_ms` 0–5000 | 延迟关闭启动器 |

### 受保护设置

以下设置不能通过 `pcl_set_setting` / `pcl_reset_setting` 修改：

- 所有 `SystemMcp*`（防止 AI 自我提权）；
- `SystemSystemCache`、`SystemLastVersionReg`、`SystemHelpVersion`、`SystemEulaVersion`、`SystemLaunchCount`、`SystemHighest*`；
- 所有 `Cache*`、`Login*`；
- `LaunchFolderSelect`、`LaunchFolders`；
- 可能执行命令或加载外部内容的 `LaunchAdvanceRun`、`LaunchAdvanceJvm`、`LaunchArgumentJavaSelect`、`LaunchArgumentJavaAll`、`UiCustomNet`；
- 会影响下载安全校验的 `ToolDownloadCert`。

加密设置永远不通过 MCP 读取或写入。

## 6. 典型工作流

### 查询并按实例启动

```text
pcl_get_status {}
pcl_list_instances {"refresh": true}
pcl_select_instance {"name": "1.20.1"}
pcl_launch_game {"instance": "1.20.1", "wait": true, "timeout_seconds": 30}
```

`pcl_launch_game` 支持三种登录模式：

| `account_mode` | 行为 |
| --- | --- |
| `current`（默认） | 沿用启动器当前登录设置 |
| `offline` | 强制以离线身份运行，需要提供 `offline_username` |
| `microsoft` / `official` | 以已登录的微软正版账号身份运行 |

离线模式示例：

```text
pcl_launch_game {"instance": "1.20.1", "account_mode": "offline", "offline_username": "Steve", "wait": true}
```

离线模式只覆盖本次启动，不会把玩家名写入离线用户名历史，也不会修改启动器的全局 `LoginType`。

正版模式示例：

```text
pcl_launch_game {"instance": "1.20.1", "account_mode": "microsoft", "wait": true}
```

> 正版模式需要启动器配置了 `PCL_MS_CLIENT_ID`，并且已经登录过微软账号。当前构建如果未配置 Client ID，会返回 `microsoft_login_unavailable`；未登录账号会返回 `microsoft_account_required`。离线模式不依赖微软登录，可直接使用。
>
> 若当前登录方式本身不是离线，或者尚未配置登录，`current` 可能触发启动器的登录界面；AI 无法代替用户完成微软登录或输入密码，此时建议显式使用 `offline`。

### 搜索与安装

```text
pcl_search_resources {"query": "sodium", "type": "mod", "source": "modrinth", "limit": 5}
pcl_install_vanilla {"mc_version": "1.20.1", "name": "1.20.1-vanilla"}
pcl_list_tasks {}
```

### 排查问题

```text
pcl_get_status {}
pcl_read_log {"file": 1, "lines": 200}
pcl_list_tasks {}
```

### 管理 Mod

```text
pcl_list_mods {"instance": "1.20.1"}
pcl_set_mod_enabled {"instance": "1.20.1", "file": "example.jar", "enabled": false}
```

删除 Mod 需要完全控制：

```text
pcl_delete_mod {"instance": "1.20.1", "file": "example.jar", "confirm": true}
```

## 7. 安全说明

- 服务器只绑定 `127.0.0.1`，不会暴露到局域网。
- 访问令牌随机生成，使用启动器现有的加密设置保存；`McpClient.json` 中的令牌用于本机 stdio 桥接，请勿上传或分享。
- 默认权限为“标准控制”；删除 Mod、关闭启动器需要手动切换到“完全控制”。
- AI 无法通过 MCP 修改 MCP 自身的设置，无法读取加密设置，也无法越过 `mods` 目录操作任意文件。
- 工具调用会记录在启动器日志中（工具名、耗时、结果），不会记录令牌或密码。
- 建议只在需要时启用 MCP；不使用时在设置页关闭。

## 8. 故障排查

| 现象 | 处理 |
| --- | --- |
| 连接被拒绝 | 确认启动器正在运行、MCP 已启用、端口未被其他程序占用 |
| `401 Unauthorized` | 检查 `Authorization: Bearer <token>`；令牌可能在设置页重新生成过 |
| `404 Session not found` | 重新发送 `initialize`；桥接器会自动清除旧会话并重试一次 |
| stdio 客户端提示无法启动 | 确认 `McpClient.json` 存在；先启动启动器并启用 MCP |
| 工具返回 `permission_denied` | 在设置页提高 MCP 权限 |
| 工具返回 `ui_timeout` | 启动器界面被模态窗口或长操作阻塞，关闭弹窗后重试 |
| 端口被占用 | 修改监听端口，或关闭占用该端口的程序 |
| 修改端口后客户端断开 | 同步修改客户端的 URL 或重新复制配置 |
| 日志中出现 401/404 | 属于客户端重试流程；若持续出现，检查令牌与客户端配置 |

## 9. 文件位置

| 内容 | 位置 |
| --- | --- |
| 客户端桥接配置 | `<PCL.exe 所在目录>\PCL-Phanes\McpClient.json` |
| 启动器日志 | `<PCL.exe 所在目录>\PCL-Phanes\Log1.txt` … `Log5.txt` |
| 设置存储 | `HKEY_CURRENT_USER\Software\PCL-Phanes`（`SystemMcp*`） |

## 10. 扩展工具

实现位于：

- `Plain Craft Launcher 2/Modules/Mcp/ModMcp.vb`：生命周期、设置、令牌、客户端配置。
- `McpHttpServer.vb`：HTTP 传输、认证、会话。
- `McpProtocol.vb`：JSON-RPC 与 MCP 方法。
- `McpTools.vb`：工具注册表与实现。
- `McpStdioBridge.vb`：stdio 桥接。

新增工具时：

1. 在 `McpTools.RegisterReadTools/RegisterControlTools/RegisterFullTools` 中注册；
2. 声明 `InputSchema`、最低权限与 `annotations`；
3. 处理函数返回 `McpResult.Success(data)` 或抛出 `McpToolException`；
4. 需要 UI 的调用使用 `InvokeUi`，需要等待加载器的调用使用现有 `LoaderTask.WaitForExit`；
5. 不要在工具输出或日志中写入令牌、密码等敏感信息。

## 11. 当前不包含

- 不内置 AI 对话或模型调用，只提供 MCP 服务器。
- 不支持局域网/公网访问、OAuth 或多用户鉴权。
- 不实现旧版 HTTP+SSE 传输（使用 `--mcp` 桥接）。
- 不提供整合包安装、社区资源下载安装、账号登录、实例重命名/删除/导出等交互式或高风险流程；这些可以在后续版本中通过新增工具扩展。
