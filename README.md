<div align="center">
  <img src="docs/assets/banner.png" alt="PCL - Phanes Edition" width="100%">

  <p>
    一个由个人维护的 <b>Plain Craft Launcher</b> 衍生版本。<br>
    <sub>保留熟悉的启动体验，把功能与性能打磨得更合自己的心意。</sub>
  </p>

  <p>
    <img alt=".NET Framework 4.8" src="https://img.shields.io/badge/.NET%20Framework-4.8-512BD4?style=flat-square&logo=dotnet&logoColor=white">&nbsp;
    <img alt="Windows" src="https://img.shields.io/badge/Platform-Windows-0078D4?style=flat-square&logo=windows11&logoColor=white">&nbsp;
    <img alt="Based on PCL 2.14.0.0" src="https://img.shields.io/badge/Based%20on-PCL%202.14.0.0-2563EB?style=flat-square">&nbsp;
    <img alt="License" src="https://img.shields.io/badge/License-PCL%20Distribution%20License-0284C7?style=flat-square">&nbsp;
    <img alt="Status" src="https://img.shields.io/badge/Status-Active%20Development-16A34A?style=flat-square">
  </p>
</div>

> [!IMPORTANT]
> **PCL - Phanes Edition 是第三方基于 PCL 独立进行二次创作的产物**，并非 PCL 官方版本，与龙腾猫跃及 PCL 官方没有隶属关系。完整说明见 [许可证与合规](#许可证与合规)。

## 关于 Phanes Edition

**Plain Craft Launcher - Phanes Edition**（简称 **PCL - Phanes Edition**）从 **PCL 2.14.0.0 的源码快照**出发独立维护。它保留了 PCL 成熟的界面、下载与启动链路，也保留了那套自绘控件与动画体系；在此基础上，项目会按自己的节奏继续演进。

这个版本的出发点很简单：**把 PCL 已经很好用的部分保留下来，把还不够顺的地方一点点改好。**

- **体验优先**：缩短高频操作的路径，补齐状态、提示与失败反馈，让等待变得可感知。
- **性能可度量**：关注启动耗时、页面切换、资源扫描与下载调度的开销，用数据判断优化是否值得。
- **改动可维护**：把构建、日志、测试这些地基慢慢补齐，让后续改动不必只靠手工验证兜底。

当前是起步阶段：代码基线已经就绪，功能与性能上的改动会逐步落地。这里没有“重新发明一个启动器”的打算，先把已有的东西做得更顺手、更可靠，再谈新的可能。

## 继承自 PCL 的能力

以下能力来自 PCL 2.14.0.0 基线，Phanes Edition 会保留并继续完善：

- **🚀 启动实例**：多版本安装与隔离、实例级设置、整合包导出；Java 自动扫描、识别与选择。
- **📥 下载安装**：原版游戏与依赖库、Forge / Fabric / NeoForge / OptiFine、Mod、整合包、资源包、光影、数据包；多镜像源排序与失败重试。
- **🔐 账号登录**：微软正版登录、Nide、Authlib-Injector 与离线模式。
- **🧩 内容管理**：Mod 启用 / 禁用 / 删除、本地资源扫描、CurseForge 与 Modrinth 搜索。
- **🩺 运行保障**：崩溃日志规则分析、游戏进程监控、任务管理器。
- **🎨 界面体验**：自绘 WPF 控件与动画引擎、主题与背景音乐、内置帮助文档。
- **🤖 AI 接入（MCP）**：内置本地 MCP 服务器，让 Claude Desktop、Cursor、VS Code 等 AI 客户端查询并控制启动器；支持按当前设置、微软正版账号或离线身份启动游戏；默认关闭，支持分级权限、访问令牌与 stdio 桥接。

## 打磨方向

| 方向 | 想做的事 | 状态 |
| :-- | :-- | :-- |
| 交互体验 | 精简高频操作，完善状态反馈、错误提示与空状态 | 进行中 |
| 性能 | 启动、页面切换、资源扫描与下载调度的开销优化 | 观察与迭代 |
| 功能 | 在保留原有能力的前提下，补齐实用细节 | 规划中 |
| 工程质量 | 完善构建流程、日志与测试覆盖 | 长期 |

> 上表是方向清单，不是已完成清单。

## 技术栈

| 项目 | 内容 |
| :-- | :-- |
| 运行平台 | Windows 10 / 11 |
| 主程序 | VB.NET + WPF，.NET Framework 4.8 |
| 辅助库 | `PCLCS`（C# 13，net48）、`MeloongCore`（C# 13，netstandard 2.0 / net48） |
| 构建配置 | `Debug` / `Release` / `Snapshot`，MSBuild 或 Visual Studio 2022 |
| 代码规模 | 约 5.2 万行（VB / XAML / C#） |
| 版本基线 | PCL 2.14.0.0 |
| 许可 | 《PCL 分发有限许可》（自定义许可，非 OSI 开源协议） |

## 快速开始

### 环境要求

- Windows 10 / 11；
- Visual Studio 2022，勾选 **.NET 桌面开发** 与 **.NET Framework 4.8 开发工具**；
- .NET SDK 9 或更高版本（C# 13），或 VS 2022 17.12+；
- Git。

### 构建

```powershell
# 1. 还原 C# 项目依赖（MeloongCore 已内置在仓库中，无需初始化子模块）
dotnet restore PCLCS\PCLCS.csproj

# 2. 编译 VB.NET 主程序（Debug / Release / Snapshot）
msbuild "Plain Craft Launcher 2\Plain Craft Launcher 2.vbproj" -p:Configuration=Release
```

也可以直接用 Visual Studio 打开 `Plain Craft Launcher 2.sln` 后构建。

> 主程序是旧式非 SDK 工程，请使用 MSBuild 或 Visual Studio 构建，不要直接对它执行 `dotnet build`。

构建产物位于 `Plain Craft Launcher 2\bin\Plain Craft Launcher - Phanes Edition.exe`。

### 一键构建（推荐）

不想记命令时，直接双击仓库根目录的 `一键编译.bat`；它也可以在命令行中运行：

```powershell
.\一键编译.bat                  # Release（自动判断是否需要清理）
.\一键编译.bat Debug            # Debug 配置
.\一键编译.bat Release -Clean   # 强制清理后完整编译
```

脚本会自动定位 VS 2022 的 MSBuild、还原 NuGet 依赖、编译并校验产物，然后把可直接运行的发行文件整理到 `artifacts/`：

- `artifacts/output/PCL-Phanes-<版本>-<配置>/`：exe 与运行时依赖，可直接双击运行；
- `artifacts/packages/PCL-Phanes-<版本>-<配置>-<时间>.zip`：发行压缩包；
- `artifacts/logs/build-<时间>.log`：完整编译日志。

其他参数：`-SkipRestore`、`-NoPackage`、`-NoOpen`、`-Run`、`-Help`。脚本会在首次运行、切换配置或检测到输出文件被其他构建更新时自动清理后重新编译；`-Clean` 可强制清理。CI 或脚本调用时建议加 `-NoOpen`，也可以直接调用 `tools\build\Build.ps1`。`artifacts/` 已被 `.gitignore` 忽略。

### 公开发布版（内置 Microsoft Client ID）

如果希望终端用户**不需要**注册 Microsoft Entra 应用、提交申请或配置环境变量就能使用微软登录，请构建公开发布版：

```powershell
# 当前终端（临时生效）
$env:PCL_MS_CLIENT_ID = "xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx"
$env:PCL_CURSEFORGE_API_KEY = "..."   # 可选
.\一键编译发布版.bat

# 双击入口（使用永久环境变量；设置后重开资源管理器/终端）
setx PCL_MS_CLIENT_ID "xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx"
setx PCL_CURSEFORGE_API_KEY "..."   # 可选
```

公开发布版会把 `PCL_MS_CLIENT_ID`（必填）和可选的 `PCL_CURSEFORGE_API_KEY` 编译进 exe，产物位于：

- `artifacts/output/PCL-Phanes-<版本>-PublicRelease/`
- `artifacts/packages/PCL-Phanes-<版本>-PublicRelease-<时间>.zip`

说明：

- `PCL_MS_CLIENT_ID` 必须是 Microsoft Entra 应用的「应用程序（客户端）ID」，不是目录（租户）ID；对应应用需允许公共客户端设备代码流，并具备 `XboxLive.signin` / `offline_access` 权限。
- 客户端 ID 是公开标识，不是密钥；它会出现在发布产物中，这是 OAuth 公共客户端的正常特性。
- `PCL_CURSEFORGE_API_KEY` 属于敏感凭据，内置到公开发布产物可能违反 CurseForge 的使用条款且可被提取；如果不希望内置，请在构建前取消该环境变量。未设置时构建仍会成功，只是 CurseForge 相关功能不可用。
- 构建脚本不会把完整 ID/Key 打印到控制台或日志；生成文件位于已忽略的 `obj\PublicRelease`，发行包只包含运行时文件与 `LICENCE`。
- 公开发布版与普通构建共用 `bin`，脚本会检测构建模式并自动清理。

### 运行

```powershell
& ".\Plain Craft Launcher 2\bin\Plain Craft Launcher - Phanes Edition.exe"
```

## 项目结构

```text
PCL-PhanesEdition/
├─ Plain Craft Launcher 2/        # 主程序（VB.NET / WPF）
│  ├─ Controls/                   # 自绘控件与附加行为
│  ├─ Pages/                      # 启动、下载、实例、设置等页面
│  ├─ Modules/                    # 下载、启动、实例、资源等业务逻辑
│  ├─ Images/、Resources/         # 图片与内置资源
│  └─ Plain Craft Launcher 2.vbproj
├─ PCLCS/                         # C# 业务辅助库（Java、版本解析、启动工具）
├─ MeloongCore/                   # 通用核心库（配置、日志、进度、工具；已内置）
├─ 一键编译.bat                   # 一键编译入口（双击运行）
├─ 一键编译发布版.bat             # 公开发布版入口（内置 Client ID）
├─ tools/build/                   # 一键编译的实际构建脚本
├─ PROJECT_STRUCTURE.md           # 完整项目结构分析
├─ Plain Craft Launcher 2.sln
├─ LICENCE                        # PCL 分发有限许可
└─ README.md
```

完整的分层、依赖关系与调用链路见 **[PROJECT_STRUCTURE.md](PROJECT_STRUCTURE.md)**。

### 想改哪里

| 我想…… | 从哪里开始 |
| :-- | :-- |
| 改界面 / 页面 | `Plain Craft Launcher 2/Pages/`、`Plain Craft Launcher 2/Controls/` |
| 改下载与安装 | `Plain Craft Launcher 2/Modules/Minecraft/ModDownload.vb`、`ModModpack.vb` |
| 改启动流程 | `Plain Craft Launcher 2/Modules/Minecraft/ModLaunch.vb`、`ModJava.vb` |
| 改设置项 | `Plain Craft Launcher 2/Pages/PageSetup/Settings.vb`、`SettingService.vb` |
| Java / 版本解析 | `PCLCS/` |
| 通用工具 / 配置 / 日志 | `MeloongCore/` |

## 文档

- **[PROJECT_STRUCTURE.md](PROJECT_STRUCTURE.md)** —— 项目结构、模块职责、依赖关系与主要运行链路。
- **[docs/MCP.md](docs/MCP.md)** —— MCP 服务器启用、客户端配置、工具参考与安全说明。
- **[MeloongCore/AGENTS.md](MeloongCore/AGENTS.md)** —— 通用核心库自身的开发约定。
- **[LICENCE](LICENCE)** —— 《PCL 分发有限许可》与《PCL 存储库合理使用指南》。

## 参与

欢迎一起把它做得更好。为了让改动更容易被理解：

1. 先说明动机与范围（Issue 或讨论），再提交实现；
2. 一次 Pull Request 只做一件事，避免混入无关重构；
3. UI 改动请附截图，行为改动请写清复现步骤；
4. 遵循仓库中的 `.editorconfig`。

项目目前由个人维护，反馈可能不会非常及时，请多包涵。

## 致谢

PCL - Phanes Edition 站在 PCL 与社区的肩膀上：

- **[龙腾猫跃 (LTCat)](https://meloong.com/afd/a/LTCat)** —— Plain Craft Launcher 的作者，本项目全部基础的来源。
- **[Meloong-Git](https://github.com/Meloong-Git/MeloongCore)** —— MeloongCore 通用核心库。
- **PCL 社区** —— 镜像源、帮助文档、反馈与测试，以及一直以来的支持。

非常感谢社区的大力支持！

## 许可证与合规

本项目以 PCL 2.14.0.0 源码为基础独立开发，沿用 PCL 的 **《分发有限许可》**，而不是常见的 OSI 开源协议。完整文本见 [LICENCE](LICENCE)。

- **名称**：全称为 **Plain Craft Launcher - Phanes Edition**，以 `Plain Craft Launcher (PCL)` 开头，并以 `Phanes Edition` 标明这是第三方修改版本。
- **身份**：第三方衍生项目，与龙腾猫跃及 PCL 官方无隶属关系，不代表 PCL 官方立场。
- **署名**：沿用 PCL 关于页首位的原作者署名与赞助入口。
- **再分发**：分发本项目或基于本项目二次创作时，请同样遵守 `LICENCE` 中的条款。

## English

**PCL - Phanes Edition** is a third-party, independently maintained fork of [Plain Craft Launcher](https://github.com/Meloong-Git/PCL), based on the PCL 2.14.0.0 source snapshot. Built with VB.NET and WPF on .NET Framework 4.8, it keeps PCL's launcher experience while focusing on usability, performance, and long-term maintainability. It is not affiliated with or endorsed by the PCL author.

Licensed under the PCL Distribution License — see [LICENCE](LICENCE).

---

<p align="center">
  <sub>PCL - Phanes Edition · 按自己的节奏，把熟悉的东西做得更好。</sub>
</p>
