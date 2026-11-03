# PCL - Phanes Edition 项目结构分析

> 分析对象：`C:\Users\yaxuan97\PCL-PhanesEdition`
> 当前版本：**1.0.0**（`VersionCode` 1，单一版本，无分支区分）
> 版本基线：PCL（Plain Craft Launcher）**2.14.0.0** 源码快照
> 分析时间：2026-10-27
> 说明：本文只分析仓库结构与代码组织，不涉及修改任何源码。

## 1. 结论速览

- **主体是一个 VB.NET + WPF 的桌面单体应用**，目标框架为 **.NET Framework 4.8**，解决方案内另有 C# 项目为其提供基础能力。
- **已作为独立项目改造**：移除了启动器更新、公告、匿名统计、在线反馈、联机、赞助/土豆码等原项目体系；品牌、数据目录与版本号均已独立。
- **单版本发布**：不再存在“开源版 / 正式版 / 赞助版”分支标记，所有构建共用一套功能。
- **UI 与业务总体分离**：`Pages/` 放页面（XAML + code-behind），`Modules/` 放网络、下载、启动、实例管理等核心逻辑，`Controls/` 放自绘 WPF 控件。
- **PCLCS 是业务辅助库**：Java 探测、Minecraft 版本解析/比较、启动工具、资源平台映射等从 VB 迁到 C#，编译为 `net48`。
- **MeloongCore 的源码内置在仓库中**：提供配置、日志、进度、Worker、文件/路径/注册表工具与 WPF 适配层；不是 Git 子模块。
- **主程序没有自动化测试**；只有 MeloongCore 自带 TUnit 测试项目。

## 2. 技术栈与规模

| 项目 | 内容 |
| --- | --- |
| 应用类型 | Minecraft 第三方启动器（Windows 桌面） |
| UI 框架 | WPF，自绘控件与动画，无第三方 UI 框架 |
| 主语言 | VB.NET（主程序） |
| 辅助语言 | C# 13（PCLCS、MeloongCore，使用 PolySharp 兼容旧目标框架） |
| 运行时 | .NET Framework 4.8（主程序、PCLCS、MeloongCore.Wpf、Tests） |
| 通用库 | .NET Standard 2.0（MeloongCore.Shared） |
| 构建配置 | `Debug` / `Release` / `Snapshot` |
| 版本 | 1.0.0（`VersionCode`：1） |
| 解决方案 | `Plain Craft Launcher 2.sln`，4 个项目 |
| 程序集名 | `Plain Craft Launcher - Phanes Edition` |
| 代码规模 | 约 5.2 万行（VB / XAML / C#） |

代码量统计（仅 `*.vb` / `*.xaml` / `*.cs`，不含 `bin/obj`、图片和二进制依赖）：

| 项目 | 文件数 | 行数 | 说明 |
| --- | ---: | ---: | --- |
| `Plain Craft Launcher 2`（VB） | 111 | 41,098 | 主程序逻辑 |
| `Plain Craft Launcher 2`（XAML） | 61 | 4,835 | 主程序界面 |
| `PCLCS` | 9 | 约 900 | C# 业务辅助库 |
| `MeloongCore/Shared` | 26 | 约 4,400 | 通用基础库 |
| `MeloongCore/Wpf` | 6 | 约 670 | WPF 适配层 |
| `MeloongCore/Tests` | 9 | 约 730 | 单元测试 |
| **合计** | **约 222** | **约 52,600** | — |

## 3. 顶层目录结构

```text
PCL-PhanesEdition/
├─ Plain Craft Launcher 2.sln        # VS 解决方案（4 个项目）
├─ 一键编译.bat                      # 一键编译入口（双击运行）
├─ 一键编译发布版.bat                # 公开发布版入口（内置 Client ID）
├─ tools/build/                      # 一键编译的实际构建脚本
├─ Plain Craft Launcher 2/           # ★ 主程序（VB.NET / WPF / WinExe）
│  ├─ Application.xaml(.vb)          # 应用入口、全局资源、启动初始化
│  ├─ FormMain.xaml(.vb)             # 主窗口、无边框标题栏、页面导航与切换动画
│  ├─ Controls/                      # 自绘控件与附加行为
│  ├─ Pages/                         # 页面层（XAML + code-behind）
│  │  ├─ PageLaunch/                 # 启动、登录、皮肤
│  │  ├─ PageDownload/               # 下载：游戏安装、Mod、整合包、资源包、光影、数据包
│  │  ├─ PageInstance/               # 版本（实例）总览、设置、Mod 管理、导出
│  │  ├─ PageSetup/                  # 设置：启动、个性化、其他
│  │  ├─ PageOther/                  # 帮助、关于与鸣谢、百宝箱
│  │  ├─ PageSelectLeft/Right.xaml   # 版本选择
│  │  └─ PageSpeedLeft/Right.xaml    # 任务管理（文件名保留 Speed 历史命名）
│  ├─ Modules/                       # 业务逻辑与全局功能
│  │  ├─ Base/                       # 网络、动画、加载器、日志、校验、图片
│  │  ├─ Minecraft/                  # 启动、下载、实例、Java、崩溃分析、进程监控
│  │  ├─ Resource/                   # 社区资源模型、搜索、本地资源扫描
│  │  ├─ Mcp/                        # MCP 服务器、JSON-RPC、工具注册表与 stdio 桥接
│  │  ├─ ThirdParty/                 # 第三方辅助（拖拽等）
│  │  └─ Mod*.vb                     # 全局功能模块（提示、事件、音乐、密钥、开发钩子）
│  ├─ Images/                        # 图标、主题、皮肤、头像等图片资源
│  ├─ Resources/                     # 内置 DLL/JAR、字体、帮助压缩包、默认主页模板
│  ├─ My Project/                    # 程序集信息、自动生成的 Resources/Settings
│  └─ bin/、obj/                     # 构建产物（已被 .gitignore 忽略）
├─ PCLCS/                            # C# 业务辅助库（net48）
├─ MeloongCore/                      # 通用核心库源码（内置，非子模块）
├─ docs/                             # 仓库配图等文档资源
├─ .editorconfig / .gitattributes / .gitignore
├─ PROJECT_STRUCTURE.md              # 本文
├─ README.md                         # 项目简介
└─ LICENCE                           # PCL 分发有限许可（非 OSI 开源协议）
```

> 仓库中已不存在 `.github/`（原 CI 与 Issue 模板已移除），也没有 `patches/` 更新补丁目录。

## 4. 解决方案与项目依赖

### 4.1 项目清单

| 项目 | 类型 | 目标框架 | 输出 | 职责 |
| --- | --- | --- | --- | --- |
| `Plain Craft Launcher 2` | VB.NET / WPF / WinExe | .NET Framework 4.8 | `Plain Craft Launcher - Phanes Edition.exe` | 启动器主程序：UI、页面、业务编排 |
| `PCLCS` | C# 类库 | net48 | `PCLCS.dll` | Java、MC 版本、启动、资源相关业务辅助 |
| `MeloongCore` | C# 类库 | netstandard2.0 | `MeloongCore.dll` | 配置、日志、进度、Worker、文件/网络工具 |
| `MeloongCore.Wpf` | C# 类库 | net48 | `MeloongCore.Wpf.dll` | WPF 对话框、转换器、窗口/系统工具 |
| `MeloongCore.Tests` | C# 可执行测试 | net48 | 测试宿主 | MeloongCore 的 TUnit 测试（不在 `.sln` 中） |

### 4.2 依赖关系

```text
Plain Craft Launcher 2 (VB, net48)
├─> PCLCS (C#, net48)
│    ├─> MeloongCore (netstandard2.0)
│    └─> MeloongCore.Wpf (net48)
├─> MeloongCore
└─> MeloongCore.Wpf
     └─> MeloongCore
```

主程序通过 VB 的全局 `Imports` 直接使用 `MeloongCore`、`MeloongCore.Wpf`、`PCLCS` 命名空间（见 `Plain Craft Launcher 2.vbproj` 的 `<Import Include="...">`）。

### 4.3 构建配置

| 配置 | 关键定义 | 用途 |
| --- | --- | --- |
| `Debug` | 编译器自动定义 `DEBUG`，项目另定义 `SNAPSHOT` | 开发调试，执行 `ModDevelop.Start()` |
| `Release` | 定义 `RELEASE` | 正式发布 |
| `Snapshot` | 不定义 `DEBUG` / `RELEASE` | 快照构建，优化开启、无调试符号 |

`ModBase.vb` 通过 `#If DEBUG / #ElseIf RELEASE / #Else` 推导 `BuildType`，仅影响显示名称与调试行为，不再影响功能可用性。

## 5. 主项目 `Plain Craft Launcher 2/`

### 5.1 入口与主窗口

**`Application.xaml` / `Application.xaml.vb`**

- `StartupUri="FormMain.xaml"`，启动模式为 `OnExplicitShutdown`。
- 启动阶段依次完成：
  1. `MeloongCore.Main.Init(DataFolder)`、`MeloongCore.Wpf.Main.Init()`；
  2. 单实例互斥量 `PCL-Phanes_SingletonMutex`（`--gpu` / `--memory` 模式跳过）；
  3. 日志初始化（日志目录为程序旁的 `PCL-Phanes\`）；
  4. 提升主线程优先级；
  5. Debug 构建执行 `ModDevelop.Start()`；
  6. 检查 .NET Framework 4.8；
  7. 修复 WPF 字体加载异常；
  8. 处理 `--gpu` / `--memory` 命令行模式（原 `--update` 已移除）；
  9. 创建 `PCL-Phanes\Pictures`、`PCL-Phanes\Musics`，检查目录写权限与缓存目录；
  10. 进入主窗口。

**`FormMain.xaml` / `FormMain.xaml.vb`**

- 无边框、透明、可缩放的 WPF 主窗口，自绘标题栏与缩放边框；标题为 `Plain Craft Launcher - Phanes Edition`。
- 维护页面栈 `PageStack`、当前页面 `PageCurrent`、当前子页 `PageCurrentSub`。
- `PageType`（与标题栏按钮下标一致）：`Launch = 0`、`Download = 1`、`Setup = 2`、`Other = 3`；副页面：`InstanceSelect = 5`、`TaskManager = 6`、`InstanceSetup = 7`、`ResourceDetail = 8`、`HelpDetail = 9`。
- `PageSubType`：下载 0–6；设置 `SetupLaunch = 0`、`SetupUI = 1`、`SetupSystem = 2`；更多 `OtherHelp = 0`、`OtherAbout = 1`、`OtherTest = 2`；版本 `InstanceOverall = 0` … `InstanceExport = 5`。
- 页面实例按需创建并缓存在 `FrmLaunchLeft`、`FrmDownloadLeft` 等字段中，避免重复构造。

### 5.2 页面层 `Pages/`

页面普遍以 `MyPageRight`（右半区内容页）或 `MyPageLeft`（左半区导航页）为根，配套同名 `.xaml.vb`。

| 页面组 | 主要文件 | 职责 |
| --- | --- | --- |
| `PageLaunch/` | `PageLaunchLeft`、`PageLaunchRight`、`PageLoginMs/Nide/Auth/Legacy`、`MySkin`、`MyMsgLogin` | 启动主页、版本选择入口、微软/Nide/离线/Authlib 登录、皮肤设置 |
| `PageDownload/` | `PageDownloadInstall`、`PageDownloadLeft`、`PageDownloadModify`、`ModDownloadLib`、`Resource/*` | 游戏安装、Mod/整合包/资源包/光影/数据包搜索与下载、资源详情 |
| `PageInstance/` | `PageInstanceOverall`、`PageInstanceSetup`、`PageInstanceMod`、`PageInstanceModDisabled`、`PageInstanceExport`、`PageInstanceLeft` | 单个版本的概览、设置、Mod 管理、禁用 Mod、整合包导出 |
| `PageSetup/` | `PageSetupLaunch`、`PageSetupUI`、`PageSetupSystem`、`PageSetupLeft`、`Settings.vb`、`SettingService.vb` | 设置界面（启动 / 个性化 / 其他）、设置项注册与持久化 |
| `PageOther/` | `PageOtherAbout`、`PageOtherHelp`、`PageOtherHelpDetail`、`PageOtherTest`、`PageOtherLeft` | 关于与鸣谢、帮助文档、百宝箱（当前为占位页） |
| 根级页面 | `PageSelectLeft/Right`、`PageSpeedLeft/Right` | 版本选择、任务管理（下载任务/速度/线程监控） |

> 原 `PageLink/PageLinkMain`（联机）与 `PageSetup/PageSetupLink`（联机设置）已删除。

### 5.3 业务逻辑层 `Modules/`

#### Base：基础设施

| 文件 | 职责 |
| --- | --- |
| `ModBase.vb` | 全局状态：版本号（1.0.0 / VersionCode 1）、构建类型、路径（程序目录/缓存/图片）、数据文件夹名 `PCL-Phanes`、语言、启动计时、程序退出标志等 |
| `ModLoader.vb` | 通用异步加载框架：`LoaderBase`、`LoaderTask(Of TIn, TOut)`、`LoaderWorker`、`LoaderCombo` |
| `ModNet.vb` | HTTP 请求封装、慢速/快速重试、备用 URL、下载限速、浏览器请求头模拟 |
| `ModAnimation.vb` | 自研动画引擎（`Ani*` / `Aa*` API） |
| `MyBitmap.vb` | 图片解码、格式转换、缩放等图像处理 |
| `PclLogger.vb` | 将主程序日志接入 MeloongCore 的 `Logger`；错误时提供“打开日志文件夹”入口 |
| `ModValidate.vb` | 输入校验规则（`Validate*`） |

#### Minecraft：启动器核心

| 文件 | 行数（约） | 职责 |
| --- | ---: | --- |
| `ModLaunch.vb` | 2,100 | 启动参数构建、JVM/游戏参数、登录凭据、启动脚本、进程启动；`McLaunchOptions.LoginOverride` 支持单次启动的登录方式覆盖（MCP 的离线/正版模式） |
| `ModMinecraft.vb` | 2,100 | Minecraft 目录、实例/版本元数据、继承版本、文件结构 |
| `ModDownload.vb` | 1,300 | 原版客户端、依赖库、资源文件等下载任务构建，镜像源排序 |
| `ModModpack.vb` | 860 | 整合包安装、导出、格式解析（zip/rar/mrpack 等） |
| `ModJava.vb` | 540 | Java 扫描、下载、版本识别、迁移、选择与排序 |
| `ModCrash.vb` | 1,100 | 崩溃日志收集、规则分析、报告生成 |
| `ModWatcher.vb` | — | 游戏进程监控：启动/运行/崩溃状态、启动器退出策略 |

#### Resource：资源与实例内容

| 文件 | 职责 |
| --- | --- |
| `ResourceProject.vb` | 社区资源项目模型（名称、描述、平台、类型等） |
| `ResourceVersion.vb` | 资源版本/文件模型 |
| `ResourceSearcher.vb` | CurseForge / Modrinth 等平台搜索 |
| `LocalResourceFile.vb` | 本地 Mod/资源包文件模型 |
| `LocalResourceLoaders.vb` | 本地资源扫描、解析与加载 |

#### 其他全局模块

| 文件 | 职责 |
| --- | --- |
| `ModMain.vb` | 全局 UI 辅助：左下角提示（`Hint`）、提示队列与动画；本地日志反馈入口 |
| `ModEvent.vb` | 自定义事件与附加属性，属于公开 API |
| `ModSecret.vb` | OAuth Client ID、CurseForge API Key、注册表位置 `PCL-Phanes`、网络 UA、主题色（开发构建运行时读环境变量，公开发布版由构建脚本编译注入） |
| `ModMusic.vb` | 基于 NAudio 的背景音乐播放列表 |
| `ModDevelop.vb` | 开发版启动钩子（当前为空实现） |
| `ThirdParty/DragHelper.vb` | 窗口/控件拖拽辅助 |

#### Mcp：AI 接入

| 文件 | 职责 |
| --- | --- |
| `Mcp/ModMcp.vb` | MCP 服务器生命周期、设置读取、访问令牌生成、`McpClient.json` 写入/删除 |
| `Mcp/McpHttpServer.vb` | 基于 `HttpListener` 的回环 HTTP 传输、令牌/Origin 校验、会话与请求限流 |
| `Mcp/McpProtocol.vb` | JSON-RPC 2.0、MCP `initialize` / `tools/list` / `tools/call` / `ping` |
| `Mcp/McpTools.vb` | 工具注册表与 22 个工具实现（状态、实例、Mod、设置、启动、导航、资源搜索等） |
| `Mcp/McpStdioBridge.vb` | `--mcp` 模式：把 stdin/stdout 的 MCP 消息转发给已运行的启动器 |

MCP 默认关闭，只监听 `127.0.0.1`；详细配置与工具说明见 `docs/MCP.md`。

### 5.4 控件层 `Controls/`

自绘/封装控件统一以 `My` 为前缀，构成界面视觉风格：

- 页面容器：`MyPageLeft`、`MyPageRight`（页面进入/退出动画、异步加载状态机）。
- 交互控件：`MyButton`、`MyTextButton`、`MyIconButton`、`MyIconTextButton`、`MyExtraButton`、`MyExtraTextButton`、`MyCheckBox`、`MyRadioButton`、`MyRadioBox`、`MyComboBox`、`MyComboBoxItem`、`MySlider`、`MySliderDot`、`MySearchBox`、`MyTextBox`。
- 展示控件：`MyCard`、`MyListItem`、`MyLoading`、`MyHint`、`MyImage`、`MyDropShadow`、`MyScrollBar`、`MyScrollViewer`、`MyVirtualizingElement`。
- 弹窗/菜单：`MyMsg/MyMsgText`、`MyMsgInput`、`MyMsgSelect`、`MyMenuItem`。
- 窗口辅助：`MyResizer`；行为：`Behaviors/ClipboardInterceptor`、`LazyLoadBehavior`、`Interfaces`。

### 5.5 页面加载机制

`MyPageRight` 内置“页面级异步加载”机制：

```text
PageLoaderInit(loader, 加载UI, 内容面板, 常驻面板, 输入委托, 完成回调)
  → 页面 Loaded 时自动 Start
  → 根据 Loader 状态切换：加载中 / 成功 / 失败（可点击重试）
  → PageLoaderRestart 支持强制刷新
```

`ModLoader.vb` 中的 Loader 家族负责实际后台工作，页面只订阅状态并渲染结果。

### 5.6 设置系统

`Pages/PageSetup/Settings.vb` + `SettingService.vb`：

- `Settings.Entries` 集中注册所有设置项：键名、默认值、来源、是否加密、变更回调。
- 设置来源分三类：`Normal`（INI）、`Registry`（`HKEY_CURRENT_USER\Software\PCL-Phanes`）、`Instance`（版本目录下的 `PCL\Setup.ini`）。
- 敏感项使用 DES 加密，密钥固定为 `PCL-Phanes`。
- `SettingService` 通过 WPF 附加属性把 XAML 控件的值绑定到设置键，减少页面重复代码。
- MCP 设置项：`SystemMcpServerEnabled`、`SystemMcpServerPort`、`SystemMcpServerToken`（加密）、`SystemMcpPermission`；修改会触发生命周期回调。
- 注意：主程序同时存在 **旧 `Settings` 系统（INI/注册表）** 与 **MeloongCore `ConfigEntry` JSON 配置**，PCLCS 使用后者。

### 5.7 数据与路径

| 内容 | 位置 |
| --- | --- |
| 程序旁数据目录 | `<exe 所在目录>\PCL-Phanes\`（Pictures、Musics、Custom.xaml、Logo.png、Help、Log1~5.txt、MCP 启用时的 McpClient.json） |
| 用户数据目录 | `%APPDATA%\PCL-Phanes\`（自定义皮肤等） |
| 临时缓存目录 | `%TEMP%\PCL-Phanes\`（可在设置中修改） |
| 注册表 | `HKEY_CURRENT_USER\Software\PCL-Phanes` |
| Minecraft 文件夹缓存 | `<MC 文件夹>\PCL-Phanes.ini`（实例列表与卡片缓存） |
| 实例配置 | `<版本目录>\PCL\Setup.ini`（沿用 PCL 整合包格式，便于导入导出） |
| 单实例互斥量 | `PCL-Phanes_SingletonMutex` |

> 实例级的 `PCL\` 目录是整合包格式的一部分，未随启动器数据目录改名。

## 6. `PCLCS`：C# 业务辅助库

`PCLCS` 是主程序旁边独立编译的 C# 类库（`net48`），承载更适合 C# 实现的业务逻辑，并被主程序直接引用。

| 文件 | 职责 |
| --- | --- |
| `Java.cs` | 单个 Java 实例：路径、版本、编码、启动参数、比较/序列化 |
| `Minecraft/McVersion.cs` | Minecraft 版本号与组件信息、版本格式转换 |
| `Minecraft/McUtils.cs` | 版本名比较（支持 `1.7.10-pre4`、`1.14 Pre-Release 2`、`26.1-snapshot-1` 等） |
| `Minecraft/McInstallManifest.cs` | Minecraft 安装清单模型 |
| `Minecraft/OptifineHashes.json` | OptiFine 安装包哈希数据库（内嵌资源） |
| `Launch/LaunchUtils.cs` | 启动辅助、内嵌 JAR/补丁文件释放 |
| `Launch/JavaWrapper.jar` | Java 包装器（内嵌，运行时释放） |
| `Launch/LwjglUnsafeAgent.jar` | LWJGL 兼容 Agent（内嵌） |
| `Resource/Resource.cs` | 资源类型/平台/Mod 加载器枚举与 CurseForge 映射 |
| `Resource/WikiEntry.cs` + `WikiEntries.txt` | 内置帮助条目与解析 |
| `Configs.cs` | 基于 MeloongCore `ConfigEntry` 的强类型配置（Java 列表、安装清单等） |
| `Constants.cs` | 资源类型、平台、Mod 加载器、缓存版本号等常量 |

## 7. `MeloongCore`：通用核心库（源码已内置）

`MeloongCore` 原为 Git 子模块，本仓库将其改为**普通源码目录**，随主仓库统一跟踪。它不依赖 PCL 的业务代码，可被其他项目复用。

### 7.1 `Shared`（.NET Standard 2.0）

| 目录/文件 | 能力 |
| --- | --- |
| `Main.cs` | 初始化应用名、默认正则超时 |
| `General/Config.cs` | 强类型配置项、JSON 配置 provider、防抖保存 |
| `General/Logger.cs` | 日志抽象与文件日志 |
| `General/Progress.cs` | 分阶段进度、进度观察者 |
| `General/Worker/` | 节流、防抖、可重跑后台任务 |
| `General/MsDelta.cs` | 调用 Windows `msdelta.dll` 生成/应用增量补丁 |
| `Utils/`、`Extensions/` | 文件、目录、路径、注册表、加密、任务、HTTP 等工具与扩展 |

### 7.2 `Wpf`（net48）

提供 WPF 初始化、现代 ToolTip、文件/文件夹/消息对话框、绑定转换器、窗口与系统操作。

### 7.3 `Tests`

基于 **TUnit**（`net48` 可执行测试项目），覆盖配置、进度、Worker、加密、目录、文件、路径、任务/进程等 Shared 能力。不能代表 WPF 或主程序行为已被覆盖。

## 8. 主要运行链路

### 8.1 启动器启动

```text
Application_Startup
  → MeloongCore 初始化 / 单实例 / 日志
  → 参数与环境检查（--gpu / --memory）
  → FormMain
  → PageChange(Launch)
  → PageLaunchLeft / PageLaunchRight
```

### 8.2 下载与安装

```text
PageDownload*
  → MyPageRight.PageLoaderInit
  → ModLoader.LoaderTask / LoaderCombo
  → ModDownload（构建 NetFile 列表、镜像源排序）
  → ModNet（HTTP、重试、限速）
  → 文件落盘 / 安装
```

### 8.3 启动 Minecraft

```text
PageLaunchLeft / PageLaunchRight
  → ModLaunch（构建 McLaunchOptions、JVM/游戏参数、登录信息）
  → ModJava + PCLCS.Java（选择 Java）
  → ModDownload（补齐缺失文件）
  → 启动进程
  → ModWatcher 监控状态
  → 崩溃时 ModCrash 分析日志
```

### 8.4 资源/Mod 管理

```text
PageDownload/Resource/* 或 PageInstance/PageInstanceMod
  → ResourceSearcher（在线搜索）/ LocalResourceLoaders（本地扫描）
  → ResourceProject / ResourceVersion / LocalResourceFile
  → 下载或启用/禁用/删除
```

## 9. 构建

前置条件：

- Visual Studio 2022，安装 **.NET 桌面开发** 与 **.NET Framework 4.8 开发工具**；
- .NET SDK 9+（C# 13；或 VS 2022 17.12+）；
- Git。

```powershell
# 1. 还原 C# 项目依赖（MeloongCore 源码已随仓库提供，无需初始化子模块）
dotnet restore PCLCS\PCLCS.csproj

# 2. 构建主程序（Debug / Release / Snapshot）
msbuild "Plain Craft Launcher 2\Plain Craft Launcher 2.vbproj" -p:Configuration=Release
```

也可以直接用 VS 打开 `Plain Craft Launcher 2.sln` 构建。主项目是旧式非 SDK 工程，建议使用 MSBuild/VS，而不是直接 `dotnet build`。

输出路径：`Plain Craft Launcher 2\bin\Plain Craft Launcher - Phanes Edition.exe`。

### 一键编译

双击仓库根目录的 `一键编译.bat`（或运行 `.\一键编译.bat [Debug|Release|Snapshot]`）会自动定位 MSBuild、还原依赖、编译并校验产物，并把可直接运行的发行文件整理到：

- `artifacts/output/PCL-Phanes-<版本>-<配置>/`
- `artifacts/packages/PCL-Phanes-<版本>-<配置>-<时间>.zip`
- `artifacts/logs/build-<时间>.log`

构建逻辑位于 `tools/build/Build.ps1`，支持 `-Clean`、`-SkipRestore`、`-NoPackage`、`-NoOpen`、`-Run`、`-Help`。脚本会在首次运行、切换配置或检测到输出文件被其他构建更新时自动清理后重新编译；`-Clean` 可强制清理。`artifacts/` 已被 `.gitignore` 忽略。

### 公开发布版

`一键编译发布版.bat` 会读取环境变量 `PCL_MS_CLIENT_ID`（必填）与 `PCL_CURSEFORGE_API_KEY`（可选），通过 `-p:PublicRelease=true` 和 `PUBLIC_RELEASE` 编译常量把值注入 exe：

- 生成文件：`Plain Craft Launcher 2\obj\PublicRelease\PublicReleaseSecrets.g.vb`（位于已忽略的 `obj` 下，不提交）
- 产物：`artifacts/output/PCL-Phanes-<版本>-PublicRelease/` 与对应 zip
- 构建脚本会静态校验 exe 中确实包含客户端 ID（以及可选 Key），并在普通/公开构建模式切换时自动清理

普通构建仍然只在应用运行时读取 `PCL_MS_CLIENT_ID` / `PCL_CURSEFORGE_API_KEY`，不内置任何值。

MCP 冒烟测试（需先运行启动器并启用 MCP）：

```powershell
powershell -ExecutionPolicy Bypass -File .\docs\mcp-smoke-test.ps1
```

> 仓库不再包含 CI 工作流；`Resources/Help.zip` 已随仓库提供，不再由流水线生成。

## 10. 相对 PCL 2.14.0.0 的改造

| 项目 | 处理结果 |
| --- | --- |
| 启动器更新 / 公告 | 已移除：检查更新、`--update`、`Latest.exe` 下载、公告设置与 `Notice.cfg` 读取 |
| 在线反馈 / 新功能投票 | 已移除：侧栏入口、PCL Issues/Discussions 链接；错误提示改为“打开日志文件夹” |
| 匿名数据收集 | 已移除：`Telemetry()` 与全部调用点、设置开关、隐私文案 |
| 联机 | 已移除：`PageLinkMain`、`PageSetupLink`、EasyTier 模块、服务端配置读取 |
| 赞助 / 土豆码 / 识别码 | 已移除：赞助者名单、解锁主题、土豆码、识别码；保留“赞助 PCL 原作者”链接 |
| 隐藏主题 | 已移除隐藏主题与自定义主题滑杆，仅保留 5 个基础主题 |
| 分支标记 | 已移除 `VersionBranchMain`；不再有开源版/正式版功能差异 |
| 品牌 | 标题、程序集 Title/Product/Copyright、导出文件名、UA、互斥量、`launcher_profiles` 等已改为 Phanes Edition |
| 数据目录 | 启动器数据隔离到 `PCL-Phanes`；注册表改为 `PCL-Phanes`；实例级 `PCL\Setup.ini` 保留 |
| 版本号 | 重置为 1.0.0 / `VersionCode` 1，PCL 历史版本迁移逻辑仍保留 |

## 11. 已知占位与注意事项

- **百宝箱页面**（`PageOtherTest`）目前是占位实现：`MemoryOptimize`、`GetRandomHint`、`GetRandomCave` 等返回“百宝箱功能暂未开放。”，因此 `--memory` 命令行模式实际不会执行内存优化。
- **主题颜色**：`ThemeRefresh` 使用固定色相/饱和度，5 个主题选项目前不会产生不同配色（历史实现已在基线中被裁剪）。
- **`Resources/Help.zip`** 仍是原帮助内容，内部含 PCL wiki / Discussions 链接；后续可整体替换。
- **`Resources/Custom.xaml`** 默认主页模板已更新命名空间，但仍含指向 PCL Discussions 的“联网主页”说明链接。
- **`PCL2 Skin.zip`**、`PCL2 启动器日志.txt` 等为兼容旧实例/日志保留的名称。
- 物理工程仍名为 `Plain Craft Launcher 2`（解决方案、`.vbproj`、文件夹），仅程序集名与界面品牌已改；如需连工程/文件一起改名，需同步全部 `pack://` 资源 URI。
- 主程序无单元测试，改动启动/下载链路主要依赖手工验证。

## 12. 推荐阅读顺序

1. `Modules/Base/ModBase.vb`：版本、路径、全局状态。
2. `Application.xaml.vb`：启动流程与初始化。
3. `FormMain.xaml.vb`：主窗口、`PageType`、页面栈与切换。
4. `Controls/MyPageRight.vb` + `Modules/Base/ModLoader.vb`：页面异步加载机制。
5. `Pages/PageLaunch/PageLaunchLeft.xaml.vb`、`PageLaunchRight.xaml.vb`：最典型的页面。
6. `Modules/Minecraft/ModLaunch.vb`：启动链路核心。
7. `PCLCS/`：C# 辅助能力。
8. `MeloongCore/Shared/`：通用基础设施。

常见修改入口：

| 需求 | 入口 |
| --- | --- |
| 改界面/布局 | `Pages/**/*.xaml` + 对应 `.xaml.vb`、`Controls/` |
| 新增页面 | `FormMain.PageType/PageSubType`、`PageChangeActual`、`Pages/` 下新建 `MyPageRight` 子类、左导航 |
| 改下载/安装逻辑 | `Modules/Minecraft/ModDownload.vb`、`ModModpack.vb`、`Pages/PageDownload/` |
| 改启动逻辑 | `Modules/Minecraft/ModLaunch.vb`、`ModJava.vb`、`ModMinecraft.vb` |
| 新增设置项 | `Pages/PageSetup/Settings.vb` 注册 + `SettingService` 绑定 |
| 通用工具/配置/日志 | `MeloongCore/`（源码已内置） |
| Java/版本解析 | `PCLCS` |

---

*本文由仓库静态结构分析生成；行数为近似统计，不包含 `bin/obj`、图片与二进制依赖。*
