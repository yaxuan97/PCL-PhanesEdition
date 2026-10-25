# PCL（Plain Craft Launcher）项目结构分析

> 分析对象：`C:\Users\yaxuan97\PCL-yaxuan97fork`（PCL2 源码仓库，主线版本 **2.14.0.0**）
> 分析时间：2026-10-20
> 说明：本文只分析仓库结构与代码组织，不涉及修改任何源码。

## 1. 结论速览

- **主体是一个 VB.NET + WPF 的桌面单体应用**，目标框架为 **.NET Framework 4.8**，解决方案内另有 3 个 C# 项目为其提供基础能力。
- **UI 与业务总体分离**：`Pages/` 放页面（XAML + code-behind），`Modules/` 放网络、下载、启动、实例管理等核心逻辑，`Controls/` 放自绘 WPF 控件。
- **PCLCS 是“业务辅助库”**：把 Java 探测、Minecraft 版本解析/比较、启动工具、资源平台映射等从 VB 迁到 C#，仍编译为 `net48`，供主程序直接 `Imports PCLCS`。
- **MeloongCore 的源码已内置在本仓库**：提供跨项目的通用基础设施（配置、日志、进度、Worker、文件/路径/注册表工具、WPF 适配层）。本仓库已由上游的 Git submodule 改为普通目录直接跟踪，克隆后无需初始化子模块。
- **`patches/` 不是 Git 补丁**：是 PCL 更新器使用的 **MSDelta 二进制增量更新包**（文件头 `PA30`），不能用 `git apply`。
- **主程序没有自动化测试**；只有 MeloongCore 自带 TUnit 测试项目。

## 2. 技术栈与规模

| 项目 | 内容 |
| --- | --- |
| 应用类型 | Minecraft 第三方启动器（Windows 桌面） |
| UI 框架 | WPF，大量自绘控件与自定义动画，无明显第三方 UI 框架 |
| 主语言 | VB.NET（主程序） |
| 辅助语言 | C# 13（PCLCS、MeloongCore，使用 PolySharp 兼容旧目标框架） |
| 运行时 | .NET Framework 4.8（主程序、PCLCS、MeloongCore.Wpf、Tests） |
| 通用库 | .NET Standard 2.0（MeloongCore.Shared） |
| 构建配置 | `Debug` / `Release` / `Snapshot` |
| 版本 | 2.14.0.0（`VersionCode`：正式版 411，快照版 412） |
| 解决方案 | `Plain Craft Launcher 2.sln`，4 个项目（MeloongCore 的工程已随源码内置） |
| 代码规模 | 约 **5.2 万行**（不含 `bin/obj`、图片和二进制依赖） |

代码量统计（仅 `*.vb` / `*.xaml` / `*.cs`）：

| 项目 | 文件数 | 行数 | 说明 |
| --- | ---: | ---: | --- |
| `Plain Craft Launcher 2` | 176（113 VB + 63 XAML） | 46,384 | 主程序 |
| `PCLCS` | 9（C#） | 854 | C# 业务辅助库 |
| `MeloongCore/Shared` | 26（C#） | 4,023 | 通用基础库 |
| `MeloongCore/Wpf` | 6（C#） | 551 | WPF 适配层 |
| `MeloongCore/Tests` | 9（C#） | 623 | 单元测试 |
| **合计** | **226** | **约 52,435** | — |

## 3. 顶层目录结构

```text
PCL-yaxuan97fork/
├─ Plain Craft Launcher 2.sln        # VS 解决方案（4 个项目）
├─ Plain Craft Launcher 2/           # ★ 主程序（VB.NET / WPF / WinExe）
│  ├─ Application.xaml(.vb)          # 应用入口、全局资源、启动初始化
│  ├─ FormMain.xaml(.vb)             # 主窗口、无边框标题栏、页面导航与切换动画
│  ├─ Controls/                      # 34 个自绘控件与附加行为
│  ├─ Pages/                         # 页面层（XAML + code-behind）
│  │  ├─ PageLaunch/                 # 启动、登录、皮肤
│  │  ├─ PageDownload/               # 下载：游戏安装、Mod、整合包、资源包、光影、数据包
│  │  ├─ PageInstance/               # 版本（实例）总览、设置、Mod 管理、导出
│  │  ├─ PageSetup/                  # 设置：启动、联机、界面、系统
│  │  ├─ PageOther/                  # 帮助、关于、测试
│  │  ├─ PageLink/                   # 联机
│  │  ├─ PageSelectLeft/Right.xaml   # 版本选择
│  │  └─ PageSpeedLeft/Right.xaml    # 任务管理（文件名保留 Speed 历史命名）
│  ├─ Modules/                       # 业务逻辑与全局功能（含少量 UI 辅助）
│  │  ├─ Base/                       # 网络、动画、加载器、日志、校验、图片
│  │  ├─ Minecraft/                  # 启动、下载、实例、Java、崩溃分析、进程监控
│  │  ├─ Resource/                   # 社区资源模型、搜索、本地资源扫描
│  │  ├─ ThirdParty/                 # 第三方辅助（拖拽等）
│  │  └─ Mod*.vb                     # 全局功能模块（提示、事件、音乐、密钥、开发钩子）
│  ├─ Images/                        # 图标、主题、皮肤、头像等图片资源
│  ├─ Resources/                     # 内置 DLL/JAR、字体、帮助压缩包
│  ├─ My Project/                    # 程序集信息、自动生成的 Resources/Settings
│  └─ bin/、obj/                     # 构建产物（已被 .gitignore 忽略）
├─ PCLCS/                            # C# 业务辅助库（net48）
│  ├─ Java.cs                        # Java 实例探测与版本
│  ├─ Minecraft/                     # 版本解析/比较、安装清单、OptiFine 哈希
│  ├─ Launch/                        # 启动辅助与内嵌 JAR
│  ├─ Resource/                      # 资源平台/类型映射、Wiki 条目
│  └─ Configs.cs、Constants.cs       # 配置项与常量/枚举
├─ MeloongCore/                      # 通用核心库源码（已内置，非子模块）
│  ├─ Shared/                        # netstandard2.0 通用库
│  ├─ Wpf/                           # net48 WPF 适配层
│  ├─ Tests/                         # TUnit 测试项目
│  └─ AGENTS.md、.agents/            # 该库自身的开发约定与架构文档
├─ .github/                          # GitHub Actions 构建与 Issue 模板
├─ .editorconfig                     # 代码风格
├─ .gitattributes / .gitignore
├─ PROJECT_STRUCTURE.md              # 本文（项目结构分析）
├─ README.md                         # 项目简介
└─ LICENCE                           # PCL 分发有限许可（非 OSI 开源协议）
```

## 4. 解决方案与项目依赖

### 4.1 项目清单

| 项目 | 类型 | 目标框架 | 输出 | 职责 |
| --- | --- | --- | --- | --- |
| `Plain Craft Launcher 2` | VB.NET / WPF / WinExe | .NET Framework 4.8 | `Plain Craft Launcher 2.exe` | 启动器主程序：UI、页面、业务编排 |
| `PCLCS` | C# 类库 | net48 | `PCLCS.dll` | Java、MC 版本、启动、资源相关业务辅助 |
| `MeloongCore` | C# 类库 | netstandard2.0 | `MeloongCore.dll` | 配置、日志、进度、Worker、文件/网络工具 |
| `MeloongCore.Wpf` | C# 类库 | net48 | `MeloongCore.Wpf.dll` | WPF 对话框、转换器、窗口/系统工具 |
| `MeloongCore.Tests` | C# 可执行测试 | net48 | 测试宿主 | MeloongCore 的 TUnit 测试（在仓库内，不包含在 `.sln` 中） |

### 4.2 依赖关系

```text
Plain Craft Launcher 2 (VB, net48)
├─> PCLCS (C#, net48)
│    ├─> MeloongCore (netstandard2.0)
│    └─> MeloongCore.Wpf (net48)
├─> MeloongCore
└─> MeloongCore.Wpf
     └─> MeloongCore

MeloongCore.Tests (net48) ──> MeloongCore + MeloongCore.Wpf
```

主程序通过 VB 的全局 `Imports` 直接使用 `MeloongCore`、`MeloongCore.Wpf`、`PCLCS` 命名空间（见 `Plain Craft Launcher 2.vbproj` 的 `<Import Include="...">`）。

### 4.3 构建配置

| 配置 | 关键定义 | 用途 |
| --- | --- | --- |
| `Debug` | 编译器自动定义 `DEBUG`，项目另定义 `SNAPSHOT` | 开发调试，执行 `ModDevelop.Start()` |
| `Release` | 定义 `RELEASE` | 正式发布 |
| `Snapshot` | 不定义 `DEBUG` / `RELEASE` | 快照版，优化开启、无调试符号 |

`ModBase.vb` 通过 `#If DEBUG / #ElseIf RELEASE / #Else` 推导 `BuildType`，并决定版本号与提示文案。

### 4.4 依赖形式

- **NuGet**：`PolySharp`、`Newtonsoft.Json`、`Microsoft.Win32.Registry`、`ThrottleDebounce`、`Ookii.Dialogs.Wpf`、`System.IO.Compression`、`TUnit`（仅测试）。
- **本地 DLL**：主项目 `Resources/` 下直接引用 `CacheCow.*`、`NAudio`、`Imazen.WebP`、`Newtonsoft.Json`、`Ookii.Dialogs.Wpf`、`ThrottleDebounce`、`System.Net.Http.Formatting` 等。
- **内嵌资源**：`PCLCS` 内嵌 `JavaWrapper.jar`、`LwjglUnsafeAgent.jar`、`WikiEntries.txt`、`OptiFineHashes.json`；主项目内嵌 `forge-installer.jar`、`transformer.jar`、`java-wrapper.jar`、`lwjgl-unsafe-agent.jar`、`Help.zip`、字体等。

## 5. 主项目 `Plain Craft Launcher 2/`

### 5.1 入口与主窗口

**`Application.xaml` / `Application.xaml.vb`**

- `StartupUri="FormMain.xaml"`，启动模式为 `OnExplicitShutdown`。
- 启动阶段依次完成：
  1. `MeloongCore.Main.Init("PCL")`、`MeloongCore.Wpf.Main.Init()`；
  2. 单实例互斥量、日志系统初始化（命令行更新/显卡/内存模式除外）；
  3. 提升主线程优先级；
  4. Debug 构建执行 `ModDevelop.Start()` 开发测试；
  5. 检查 .NET Framework 4.8；
  6. 修复 WPF 字体加载异常；
  7. 处理 `--update` / `--gpu` / `--memory` 命令行模式；
  8. 创建 `PCL\Pictures`、`PCL\Musics`，检查目录写权限与缓存目录；
  9. 进入主窗口。
- `Application.xaml` 同时定义全局颜色、样式、转换器和资源字典。

**`FormMain.xaml` / `FormMain.xaml.vb`**

- 无边框、透明、可缩放的 WPF 主窗口，自绘标题栏与缩放边框。
- 维护页面栈 `PageStack`、当前页面 `PageCurrent`、当前子页 `PageCurrentSub`。
- `PageType` 定义顶级/次级页面：`Launch`、`Download`、`Link`、`Setup`、`Other`、`InstanceSelect`、`TaskManager`、`InstanceSetup`、`ResourceDetail`、`HelpDetail`。
- 页面实例按需创建并缓存在 `FrmLaunchLeft`、`FrmDownloadLeft` 等字段中，避免重复构造。
- `PageChange` / `PageChangeActual` / `PageBack` 负责页面切换、子页入栈/出栈、标题栏与左右页面切换动画。

### 5.2 页面层 `Pages/`

页面普遍以 `MyPageRight`（右半区内容页）或 `MyPageLeft`（左半区导航页）为根，配套同名 `.xaml.vb`。

| 页面组 | 主要文件 | 职责 |
| --- | --- | --- |
| `PageLaunch/` | `PageLaunchLeft`、`PageLaunchRight`、`PageLoginMs/Nide/Auth/Legacy`、`MySkin`、`MyMsgLogin` | 启动主页、版本选择入口、微软/Nide/离线/Authlib 登录、皮肤设置 |
| `PageDownload/` | `PageDownloadInstall`、`PageDownloadLeft`、`PageDownloadModify`、`ModDownloadLib`、`Resource/*` | 游戏安装、Mod/整合包/资源包/光影/数据包搜索与下载、资源详情 |
| `PageInstance/` | `PageInstanceOverall`、`PageInstanceSetup`、`PageInstanceMod`、`PageInstanceModDisabled`、`PageInstanceExport`、`PageInstanceLeft` | 单个版本的概览、设置、Mod 管理、禁用 Mod、整合包导出 |
| `PageSetup/` | `PageSetupLaunch`、`PageSetupLink`、`PageSetupUI`、`PageSetupSystem`、`PageSetupLeft`、`Settings.vb`、`SettingService.vb` | 设置界面、设置项注册与持久化 |
| `PageOther/` | `PageOtherAbout`、`PageOtherHelp`、`PageOtherHelpDetail`、`PageOtherTest`、`PageOtherLeft` | 关于、帮助文档、开发者测试 |
| `PageLink/` | `PageLinkMain` | 联机功能入口 |
| 根级页面 | `PageSelectLeft/Right`、`PageSpeedLeft/Right` | 版本选择、任务管理（下载任务/速度/线程监控） |

页面切换的典型模式：

```text
用户点击导航/按钮
  → FormMain.PageChange(PageType, PageSubType)
  → PageChangeActual 处理标题栏、页面栈、动画
  → 懒加载对应的 PageXxx 控件
  → 页面 Loaded 事件中通过 PageLoaderInit 启动后台加载
```

### 5.3 业务逻辑层 `Modules/`

`Modules/` 承担主程序的核心业务逻辑，页面主要负责交互与呈现（部分复杂页面仍有较多业务代码）。

#### Base：基础设施

| 文件 | 职责 |
| --- | --- |
| `ModBase.vb` | 全局状态：版本号、构建类型、路径（程序目录/缓存/图片）、语言、启动计时、程序退出标志等 |
| `ModLoader.vb` | 通用异步加载框架：`LoaderBase`、`LoaderTask(Of TIn, TOut)`、`LoaderWorker`、`LoaderCombo`；注意它不是 Forge/Fabric 的 Mod 加载器 |
| `ModNet.vb` | HTTP 请求封装、慢速/快速重试、备用 URL、下载限速、浏览器请求头模拟 |
| `ModAnimation.vb` | 自研动画引擎（`Ani*` / `Aa*` API），支持缓动、并行动画、控件动画 |
| `MyBitmap.vb` | 图片解码、格式转换、缩放等图像处理 |
| `PclLogger.vb` | 将主程序日志接入 MeloongCore 的 `Logger` |
| `ModValidate.vb` | 输入校验规则（`Validate*`） |

#### Minecraft：启动器核心

| 文件 | 行数（约） | 职责 |
| --- | ---: | --- |
| `ModLaunch.vb` | 2,108 | 启动参数构建、JVM/游戏参数、登录凭据、启动脚本、进程启动 |
| `ModMinecraft.vb` | 2,024 | Minecraft 目录、实例/版本元数据、继承版本、文件结构 |
| `ModDownload.vb` | 1,311 | 原版客户端、依赖库、资源文件等下载任务构建，镜像源排序 |
| `ModModpack.vb` | 834 | 整合包安装、导出、格式解析（zip/rar/mrpack 等） |
| `ModJava.vb` | 540 | Java 扫描、下载、版本识别、迁移、选择与排序 |
| `ModCrash.vb` | 1,068 | 崩溃日志收集、规则分析、报告生成 |
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
| `ModMain.vb` | 全局 UI 辅助：左下角提示（`Hint`）、提示队列与动画 |
| `ModEvent.vb` | 自定义事件与附加属性，属于公开 API |
| `ModSecret.vb` | OAuth Client ID、CurseForge API Key、更新器文件替换（`UpdateReplace`） |
| `ModMusic.vb` | 基于 NAudio 的背景音乐播放列表 |
| `ModDevelop.vb` | 开发版启动钩子（当前为空实现） |
| `ThirdParty/DragHelper.vb` | 窗口/控件拖拽辅助 |

### 5.4 控件层 `Controls/`

34 个自绘/封装控件，统一 `My` 前缀，构成 PCL 的视觉风格：

- 页面容器：`MyPageLeft`、`MyPageRight`（页面进入/退出动画、异步加载状态机）。
- 交互控件：`MyButton`、`MyTextButton`、`MyIconButton`、`MyIconTextButton`、`MyExtraButton`、`MyExtraTextButton`、`MyCheckBox`、`MyRadioButton`、`MyRadioBox`、`MyComboBox`、`MyComboBoxItem`、`MySlider`、`MySliderDot`、`MySearchBox`、`MyTextBox`。
- 展示控件：`MyCard`、`MyListItem`、`MyLoading`、`MyHint`、`MyImage`、`MyDropShadow`、`MyScrollBar`、`MyScrollViewer`、`MyVirtualizingElement`。
- 弹窗/菜单：`MyMsg/MyMsgText`、`MyMsgInput`、`MyMsgSelect`、`MyMenuItem`。
- 窗口辅助：`MyResizer`；行为：`Behaviors/ClipboardInterceptor`、`LazyLoadBehavior`、`Interfaces`。

### 5.5 页面加载机制

`MyPageRight` 内置了一套“页面级异步加载”机制：

```text
PageLoaderInit(loader, 加载UI, 内容面板, 常驻面板, 输入委托, 完成回调)
  → 页面 Loaded 时自动 Start
  → 根据 Loader 状态切换：加载中 / 成功 / 失败（可点击重试）
  → PageLoaderRestart 支持强制刷新
```

`ModLoader.vb` 中的 Loader 家族负责实际后台工作，页面只订阅状态并渲染结果。这套机制是理解 PCL 页面代码的关键入口。

### 5.6 设置系统

`Pages/PageSetup/Settings.vb` + `SettingService.vb`：

- `Settings.Entries` 集中注册所有设置项：键名、默认值、来源、是否加密、变更回调。
- 设置来源分三类：
  - `Normal`：普通 INI 配置；
  - `Registry`：`HKEY_CURRENT_USER\Software\...`；
  - `Instance`：版本目录下的 `PCL\Setup.ini`。
- 敏感项（登录令牌、统计信息等）使用 DES 加密后存储。
- `SettingService` 通过 WPF 附加属性把 XAML 控件的值绑定到设置键，减少页面重复代码。
- 注意：主程序同时存在 **旧 `Settings` 系统（INI/注册表）** 与 **MeloongCore `ConfigEntry` JSON 配置**，PCLCS 使用后者。

## 6. `PCLCS`：C# 业务辅助库

`PCLCS` 是主程序旁边独立编译的 C# 类库（`net48`），用于承载更适合 C# 实现的业务逻辑，并被主程序直接引用。

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
| `Constants.cs` | 资源类型、平台、Mod 加载器、赞助等级、缓存版本号等常量 |

## 7. `MeloongCore`：通用核心库（源码已内置）

`MeloongCore` 原为 Git 子模块（上游独立仓库，提交 `c774ae4`），本仓库已将其改为 **普通源码目录**，随主仓库统一跟踪。它不依赖 PCL 的业务代码，可被其他项目复用。

### 7.1 `Shared`（.NET Standard 2.0）

| 目录/文件 | 能力 |
| --- | --- |
| `Main.cs` | 初始化应用名、默认正则超时 |
| `General/Config.cs` | 强类型配置项、JSON 配置 provider、防抖保存 |
| `General/Logger.cs` | 日志抽象与文件日志 |
| `General/Progress.cs` | 分阶段进度、进度观察者 |
| `General/Worker/` | 节流、防抖、可重跑后台任务 |
| `General/MsDelta.cs` | 调用 Windows `msdelta.dll` 生成/应用增量补丁 |
| `General/ConcurrentList.cs`、`OneOf.cs`、`ValueRange.cs` | 并发列表、联合类型、范围 |
| `Utils/FileUtils.cs`、`DirectoryUtils.cs`、`PathUtils.cs` | 文件、目录、路径、压缩包、编码处理 |
| `Utils/RegistryUtils.cs`、`CryptographyUtils.cs` | 注册表、加密 |
| `Utils/TaskUtils.cs`、`WebUtils.cs` | 异步/并行、进程执行、HTTP |
| `Extensions/` | 集合、字符串、异常、Task、HTTP 扩展方法 |

> 平台边界：`Shared` 目标为 netstandard2.0，但注册表与 MsDelta 属于 Windows 专属能力；文档约定通用代码尽量兼顾 Windows/macOS。

### 7.2 `Wpf`（net48）

| 文件 | 能力 |
| --- | --- |
| `Main.cs` | WPF 初始化入口 |
| `Controls/ModernTooltipService.cs` | 现代 ToolTip 附加行为 |
| `General/Dialogs.cs` | 文件/文件夹/消息对话框 |
| `General/ValueConverters.cs` | 常用绑定转换器 |
| `Extensions/WpfExtensions.cs` | WPF 控件/对象扩展 |
| `Utils/WindowsUtils.cs` | Windows 权限、窗口与系统操作 |

### 7.3 `Tests`

- 基于 **TUnit**（`net48`，可执行测试项目），`global.json` 指定 `Microsoft.Testing.Platform`。
- 覆盖配置、进度、Worker、加密、目录、文件、路径、任务/进程等 Shared 能力。
- `TestFiles/FileUtils/` 包含损坏压缩包、GB/UTF-8 编码、Zip Slip 等测试样本。
- 测试主要覆盖 `Shared`，不能代表 WPF 或主程序行为已被覆盖。

## 8. 主要运行链路

### 8.1 启动器启动

```text
Application_Startup
  → MeloongCore 初始化 / 单实例 / 日志
  → 参数与环境检查
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
PageLaunchRight / PageLaunchLeft
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

## 9. 构建与 CI

### 9.1 本地构建

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

输出路径：`Plain Craft Launcher 2\bin\Plain Craft Launcher 2.exe`。

### 9.2 CI（`.github/workflows/build.yml`）

- 触发条件：主项目或 workflow 文件变更、PR、手动触发；矩阵为 `Debug` / `Release`。
- 步骤：
  1. checkout 源码（MeloongCore 已内置，无需拉取子模块）；
  2. 从 `LTCatt/PCL2Help` 拉取帮助文档并打包为 `Resources/Help.zip`；
  3. 用仓库 Secrets 替换 `ModSecret.vb` 中的 OAuth Client ID、CurseForge API Key；
  4. 把提交哈希写入 `ModBase.vb` 的 `CommitHash`；
  5. `dotnet restore PCLCS` 后由 MSBuild 构建 VB 主项目；
  6. 上传构建产物。

> 注意：workflow 的 `paths` 只包含 `Plain Craft Launcher 2/**` 与 workflow 自身；单独修改 `PCLCS/` 或 `MeloongCore/` 不会触发该 CI。

## 10. 仓库中的非源码内容与当前状态

### 10.1 `patches/` 与 `更新正式版.zip`（已移除）

- 上游仓库曾跟踪 13 个 `patches/Release x.y.z <hash>.patch`（覆盖 2.12.6.1–2.13.1.1）以及完整更新包 `更新正式版.zip`。
- 它们文件头为 `PA30`，是 **MSDelta 增量补丁**（对应 `MeloongCore/Shared/General/MsDelta.cs`），不是文本 diff，**不要执行 `git apply`**；属于更新器使用的发布产物，不是源码。
- 本仓库重新初始化时这些文件已从工作区删除，因此不再被跟踪；如需使用，请从上游 PCL 仓库获取。

### 10.2 分支与提交

- 本仓库已按当前工作区状态重新初始化：只有一个初始提交，当前分支 `main`。
- 上游 PCL 的历史提交与标签未保留，也未配置 remote。
- 版本号仍沿用上游的 2.14.0.0（`VersionCode` 411 / 412），后续版本自行维护。

### 10.3 MeloongCore 源码目录（原 Git 子模块）

- 本仓库已取消 Git 子模块：删除了 `.gitmodules`，`MeloongCore/` 作为普通目录随主仓库一起跟踪，克隆后可直接构建。
- 上游对应仓库为 `https://github.com/Meloong-Git/MeloongCore.git`（提交 `c774ae4`），当前源码即取自该提交。
- 修改 `MeloongCore/` 直接在主仓库提交，不再需要同步“子模块指针”；若要跟进上游更新，需手动对比合并。

### 10.4 生成目录

`bin/`、`obj/`、`.vs/` 均为生成/IDE 目录，已被 `.gitignore` 忽略；分析或提交时不应把它们当作源码。

## 11. 推荐阅读顺序

如果是第一次接触该项目，建议按以下顺序阅读：

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

## 12. 结构评价与注意事项

**优点**

- 页面、控件、业务模块边界清晰，`Pages`/`Modules`/`Controls` 三层容易定位。
- 自研控件与动画体系统一，视觉一致性强。
- 通用能力持续下沉到 `MeloongCore`，C# 侧有独立测试。
- 旧式 VB 主工程 + 新式 SDK C# 工程的组合，迁移路径明确。

**需要注意**

- 主程序体量大（单文件最大 2,108 行），部分页面 code-behind 仍承载较多业务逻辑。
- VB 主工程为旧式项目格式，构建依赖 MSBuild/VS，不能简单 `dotnet build`。
- 同时存在两套配置体系（旧 `Settings` 与 MeloongCore `ConfigEntry`），修改配置时需确认读写的是哪一套。
- 主程序无单元测试，改动启动/下载链路主要依赖手工验证。
- `MeloongCore/` 已由子模块改为内置源码，跟进上游更新需要手动对比合并。
- 许可证为 PCL 自定义的《分发有限许可》，不是常规开源协议，分发/复用前需阅读 `LICENCE`。

---

*本文由仓库静态结构分析生成；行数统计不包含 `bin/obj`、图片与二进制依赖。*
