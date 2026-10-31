Imports System.IO
Imports System.Net
Imports System.Text
Imports System.Threading

''' <summary>
''' 一个 MCP 工具的定义。
''' </summary>
Public Class McpToolDefinition
    Public Name As String
    Public Title As String
    Public Description As String
    Public InputSchema As JObject
    Public MinPermission As Integer
    Public ReadOnlyHint As Boolean
    Public DestructiveHint As Boolean
    Public IdempotentHint As Boolean
    Public OpenWorldHint As Boolean
    Public Handler As Func(Of JObject, JObject)
End Class

''' <summary>
''' 工具参数/业务错误，会被转换为 MCP 的 isError 结果。
''' </summary>
Public Class McpToolException
    Inherits Exception

    Public ReadOnly Code As String

    Public Sub New(Code As String, Message As String)
        MyBase.New(Message)
        Me.Code = Code
    End Sub

End Class

''' <summary>
''' MCP 工具注册表与实现。
''' </summary>
Public Module McpTools

    Private ReadOnly DefinitionsList As New List(Of McpToolDefinition)
    Private ReadOnly DefinitionsLock As New Object()
    Private _Initialized As Boolean = False

    Private ReadOnly ProtectedExact As New HashSet(Of String)(StringComparer.OrdinalIgnoreCase) From {
        "SystemSystemCache",
        "SystemLastVersionReg",
        "SystemHelpVersion",
        "SystemEulaVersion",
        "SystemLaunchCount",
        "LaunchFolderSelect",
        "LaunchFolders",
        "LaunchAdvanceRun",
        "LaunchAdvanceJvm",
        "LaunchArgumentJavaSelect",
        "LaunchArgumentJavaAll",
        "UiCustomNet",
        "ToolDownloadCert"
    }
    Private ReadOnly ProtectedPrefixes As String() = {"SystemMcp", "SystemHighest", "Cache", "Login"}

    Public ReadOnly Property Definitions As List(Of McpToolDefinition)
        Get
            EnsureDefinitions()
            Return DefinitionsList
        End Get
    End Property

    ''' <summary>
    ''' 返回 tools/list 的 result 对象。
    ''' </summary>
    Public Function GetToolListResult() As JObject
        EnsureDefinitions()
        Dim Tools As New JArray()
        For Each Definition As McpToolDefinition In DefinitionsList
            Tools.Add(New JObject From {
                {"name", Definition.Name},
                {"title", Definition.Title},
                {"description", Definition.Description},
                {"inputSchema", Definition.InputSchema},
                {"annotations", New JObject From {
                    {"readOnlyHint", Definition.ReadOnlyHint},
                    {"destructiveHint", Definition.DestructiveHint},
                    {"idempotentHint", Definition.IdempotentHint},
                    {"openWorldHint", Definition.OpenWorldHint}
                }}
            })
        Next
        Return New JObject From {{"tools", Tools}}
    End Function

    ''' <summary>
    ''' 执行工具。返回值已经是 MCP 的 CallToolResult 结构。
    ''' </summary>
    Public Function Invoke(Name As String, Args As JObject) As JObject
        EnsureDefinitions()
        Dim Definition As McpToolDefinition = DefinitionsList.FirstOrDefault(Function(d) d.Name = Name)
        If Definition Is Nothing Then Return McpResult.Failure("unknown_tool", "未知工具：" & Name)
        Dim StartedAt As Long = GetTimeMs()
        Try
            If ModMcp.Permission < Definition.MinPermission Then
                Return McpResult.Failure("permission_denied",
                    $"当前 MCP 权限为「{PermissionName(ModMcp.Permission)}」，执行 {Name} 需要「{PermissionName(Definition.MinPermission)}」。")
            End If
            If Args Is Nothing Then Args = New JObject()
            Dim Result As JObject = Definition.Handler(Args)
            Logger.Trace($"MCP 工具调用：{Name}（{GetTimeMs() - StartedAt} ms）")
            Return Result
        Catch ex As McpToolException
            Logger.Warn($"MCP 工具调用失败：{Name}：{ex.Message}")
            Return McpResult.Failure(ex.Code, ex.Message)
        Catch ex As Exception
            Logger.Error(ex, $"MCP 工具调用失败：{Name}", LogBehavior.None)
            Return McpResult.Failure("internal_error", ex.Message)
        End Try
    End Function

    Private Sub EnsureDefinitions()
        SyncLock DefinitionsLock
            If _Initialized Then Return
            Try
                RegisterReadTools()
                RegisterControlTools()
                RegisterFullTools()
                _Initialized = True
            Catch
                DefinitionsList.Clear()
                Throw
            End Try
        End SyncLock
    End Sub

#Region "工具注册"

    Private Sub RegisterReadTools()
        Register("pcl_get_status", "获取启动器状态", "返回版本、当前实例、登录玩家、运行中的游戏、启动进度、任务摘要与 MCP 状态。", EmptySchema(), 0,
            AddressOf ToolGetStatus, ReadOnlyHint:=True)

        Register("pcl_list_folders", "列出 Minecraft 文件夹", "返回启动器已知的 Minecraft 文件夹、类型与当前选中的文件夹。", EmptySchema(), 0,
            AddressOf ToolListFolders, ReadOnlyHint:=True)

        Register("pcl_list_instances", "列出 Minecraft 实例", "返回当前 Minecraft 文件夹中的实例、状态、版本与选中状态。",
            Schema(New JObject From {
                {"refresh", BooleanProperty("是否强制重新扫描实例列表", False)}
            }), 0, AddressOf ToolListInstances, ReadOnlyHint:=True)

        Register("pcl_get_instance", "获取实例详情", "返回指定实例的路径、版本信息、Mod 数量与 JSON 路径。名称为空时使用当前选中实例。",
            Schema(New JObject From {
                {"name", StringProperty("实例名；为空时使用当前选中实例")}
            }), 0, AddressOf ToolGetInstance, ReadOnlyHint:=True)

        Register("pcl_list_mods", "列出实例 Mod", "扫描实例的 mods 文件夹，返回文件名、启用状态、大小与修改时间。",
            Schema(New JObject From {
                {"instance", StringProperty("实例名；为空时使用当前选中实例")},
                {"include_disabled", BooleanProperty("是否包含 .disabled / .old 文件", True)}
            }), 0, AddressOf ToolListMods, ReadOnlyHint:=True)

        Register("pcl_get_settings", "读取启动器设置", "返回非加密设置的当前值、类型、默认值与来源。加密设置不会返回。",
            Schema(New JObject From {
                {"scope", StringProperty("设置范围", {"launcher", "instance"}, "launcher")},
                {"instance", StringProperty("scope 为 instance 时必须提供实例名")},
                {"keys", ArrayOfStringsProperty("只读取指定设置项；省略时返回全部非加密项")}
            }), 0, AddressOf ToolGetSettings, ReadOnlyHint:=True)

        Register("pcl_get_login", "获取登录信息", "返回当前登录方式与玩家名，不返回密码、令牌或 UUID 凭据。", EmptySchema(), 0,
            AddressOf ToolGetLogin, ReadOnlyHint:=True)

        Register("pcl_list_tasks", "列出任务", "返回任务管理器中加载器的名称、状态、进度与错误摘要。", EmptySchema(), 0,
            AddressOf ToolListTasks, ReadOnlyHint:=True)

        Register("pcl_read_log", "读取启动器日志", "读取 PCL-Phanes\\LogN.txt 的末尾若干行。",
            Schema(New JObject From {
                {"file", IntegerProperty("日志编号，1 到 5", 1, 5, 1)},
                {"lines", IntegerProperty("读取的行数", 1, 500, 100)}
            }), 0, AddressOf ToolReadLog, ReadOnlyHint:=True)

        Register("pcl_search_resources", "搜索社区资源", "通过启动器现有的 CurseForge / Modrinth 搜索链路查找 Mod、整合包、资源包、光影或数据包。",
            Schema(New JObject From {
                {"query", StringProperty("搜索关键词；可以为空以获取热门结果", Nothing, "")},
                {"type", StringProperty("资源类型", {"mod", "modpack", "resourcepack", "shader", "datapack"}, "mod")},
                {"source", StringProperty("来源平台", {"any", "curseforge", "modrinth"}, "any")},
                {"game_version", StringProperty("Minecraft 版本，例如 1.20.1")},
                {"loader", StringProperty("Mod 加载器", {"any", "forge", "fabric", "neoforge", "liteloader"}, "any")},
                {"limit", IntegerProperty("最多返回条数", 1, 40, 10)}
            }), 0, AddressOf ToolSearchResources, ReadOnlyHint:=True, OpenWorldHint:=True)
    End Sub

    Private Sub RegisterControlTools()
        Register("pcl_select_instance", "选择实例", "将指定实例设为启动器当前选中的 Minecraft 版本。",
            Schema(New JObject From {
                {"name", StringProperty("实例名", Nothing, Nothing)}
            }, {"name"}), 1, AddressOf ToolSelectInstance, IdempotentHint:=True)

        Register("pcl_launch_game", "启动游戏", "启动指定实例。可选择沿用当前登录设置、以微软正版账号身份运行，或强制使用离线身份运行。",
            Schema(New JObject From {
                {"instance", StringProperty("要启动的实例名；省略时使用当前选中实例")},
                {"account_mode", StringProperty("登录模式：current 沿用当前设置，microsoft/official 正版账号，offline 离线身份", {"current", "microsoft", "official", "offline"}, "current")},
                {"offline_username", StringProperty("离线模式玩家名；account_mode 为 offline 时必填")},
                {"server_ip", StringProperty("启动后进入的服务器地址")},
                {"extra_args", ArrayOfStringsProperty("额外的游戏参数")},
                {"wait", BooleanProperty("是否等待游戏进程启动", False)},
                {"timeout_seconds", IntegerProperty("wait 为 true 时的等待秒数", 1, 120, 30)}
            }), 1, AddressOf ToolLaunchGame, DestructiveHint:=True)

        Register("pcl_stop_game", "停止游戏", "结束所有由启动器跟踪的 Minecraft 进程，并取消正在进行的启动。", EmptySchema(), 1,
            AddressOf ToolStopGame, DestructiveHint:=True)

        Register("pcl_set_setting", "修改启动器设置", "修改非加密、非保护设置项。加密设置、MCP 自身设置、登录与缓存设置不可通过 MCP 修改。",
            Schema(New JObject From {
                {"key", StringProperty("设置项键名", Nothing, Nothing)},
                {"value", AnyProperty("新的值；类型必须与设置项类型匹配")},
                {"scope", StringProperty("设置范围", {"launcher", "instance"}, "launcher")},
                {"instance", StringProperty("scope 为 instance 时必须提供实例名")}
            }, {"key", "value"}), 1, AddressOf ToolSetSetting, IdempotentHint:=True)

        Register("pcl_reset_setting", "重置启动器设置", "将非加密、非保护设置项恢复为默认值。",
            Schema(New JObject From {
                {"key", StringProperty("设置项键名", Nothing, Nothing)},
                {"scope", StringProperty("设置范围", {"launcher", "instance"}, "launcher")},
                {"instance", StringProperty("scope 为 instance 时必须提供实例名")}
            }, {"key"}), 1, AddressOf ToolResetSetting, IdempotentHint:=True)

        Register("pcl_set_mod_enabled", "启用或禁用 Mod", "按启动器现有规则重命名 Mod 文件，在 .jar/.zip/.litemod 与 .disabled/.old 之间切换。",
            Schema(New JObject From {
                {"instance", StringProperty("实例名；为空时使用当前选中实例")},
                {"file", StringProperty("Mod 文件名或启用状态下的文件名", Nothing, Nothing)},
                {"enabled", BooleanProperty("true 为启用，false 为禁用", Nothing)}
            }, {"file", "enabled"}), 1, AddressOf ToolSetModEnabled, IdempotentHint:=True)

        Register("pcl_navigate", "切换启动器页面", "切换主页面，可选子页面；show 为 true 时将窗口置顶。",
            Schema(New JObject From {
                {"page", StringProperty("目标页面", {"launch", "download", "setup", "other", "instance_select", "task_manager"}, "launch")},
                {"subpage", IntegerProperty("子页面编号；省略时使用 0", 0, 20, 0)},
                {"show", BooleanProperty("是否将启动器置顶", False)}
            }, {"page"}), 1, AddressOf ToolNavigate, IdempotentHint:=True)

        Register("pcl_show_launcher", "显示启动器", "恢复并置顶启动器主窗口。", EmptySchema(), 1,
            AddressOf ToolShowLauncher, IdempotentHint:=True)

        Register("pcl_cancel_task", "取消任务", "按编号、名称或全部取消任务管理器中的加载器。",
            Schema(New JObject From {
                {"id", IntegerProperty("任务加载器 Uuid", 1, Integer.MaxValue, Nothing)},
                {"name", StringProperty("任务名称，支持包含匹配")},
                {"all", BooleanProperty("是否取消全部任务", False)}
            }), 1, AddressOf ToolCancelTask, DestructiveHint:=True)

        Register("pcl_install_vanilla", "安装原版 Minecraft", "通过启动器现有下载链路安装一个原版 Minecraft 版本。",
            Schema(New JObject From {
                {"mc_version", StringProperty("Minecraft 版本号，例如 1.20.1", Nothing, Nothing)},
                {"name", StringProperty("实例名；省略时与版本号相同")}
            }, {"mc_version"}), 1, AddressOf ToolInstallVanilla, DestructiveHint:=True)
    End Sub

    Private Sub RegisterFullTools()
        Register("pcl_delete_mod", "删除 Mod", "将指定 Mod 文件移入回收站。必须显式传入 confirm=true。",
            Schema(New JObject From {
                {"instance", StringProperty("实例名；为空时使用当前选中实例")},
                {"file", StringProperty("Mod 文件名或启用状态下的文件名", Nothing, Nothing)},
                {"confirm", BooleanProperty("必须为 true 才会执行删除", False)}
            }, {"file", "confirm"}), 2, AddressOf ToolDeleteMod, DestructiveHint:=True)

        Register("pcl_shutdown_launcher", "关闭启动器", "延迟关闭启动器。工具会先返回结果，再执行关闭。",
            Schema(New JObject From {
                {"delay_ms", IntegerProperty("延迟毫秒数", 0, 5000, 500)}
            }), 2, AddressOf ToolShutdownLauncher, DestructiveHint:=True)
    End Sub

    Private Sub Register(Name As String, Title As String, Description As String, InputSchema As JObject, MinPermission As Integer,
                         Handler As Func(Of JObject, JObject), Optional ReadOnlyHint As Boolean = False,
                         Optional DestructiveHint As Boolean = False, Optional IdempotentHint As Boolean = False,
                         Optional OpenWorldHint As Boolean = False)
        DefinitionsList.Add(New McpToolDefinition With {
            .Name = Name,
            .Title = Title,
            .Description = Description,
            .InputSchema = InputSchema,
            .MinPermission = MinPermission,
            .Handler = Handler,
            .ReadOnlyHint = ReadOnlyHint,
            .DestructiveHint = DestructiveHint,
            .IdempotentHint = IdempotentHint,
            .OpenWorldHint = OpenWorldHint
        })
    End Sub

#End Region

#Region "只读工具"

    Private Function ToolGetStatus(Args As JObject) As JObject
        Dim Data As JObject = InvokeUi(Function()
            Dim Result As New JObject From {
                {"name", "PCL - Phanes Edition"},
                {"version", VersionBaseName},
                {"versionCode", VersionCode},
                {"buildType", BuildTypeDisplay},
                {"pid", Process.GetCurrentProcess().Id},
                {"uptimeSeconds", Math.Round((GetTimeMs() - ApplicationStartTick) / 1000.0, 1)},
                {"minecraftFolder", McFolderSelected},
                {"selectedInstance", If(McInstanceSelected Is Nothing, "", McInstanceSelected.Name)},
                {"folderListState", McFolderListLoader.State.ToString()},
                {"instanceListState", McInstanceListLoader.State.ToString()},
                {"login", New JObject From {
                    {"type", Settings.Get(Of McLoginType)("LoginType").ToString()},
                    {"playerName", If(McLoginName(), "")}
                }},
                {"launch", New JObject From {
                    {"state", McLaunchLoader.State.ToString()},
                    {"progress", McLaunchLoader.Progress},
                    {"processId", If(McLaunchProcess IsNot Nothing AndAlso Not McLaunchProcess.HasExited, McLaunchProcess.Id, 0)}
                }},
                {"mcp", New JObject From {
                    {"enabled", Settings.Get(Of Boolean)("SystemMcpServerEnabled")},
                    {"running", ModMcp.IsRunning},
                    {"endpoint", ModMcp.Endpoint},
                    {"permission", ModMcp.Permission},
                    {"permissionName", PermissionName(ModMcp.Permission)},
                    {"sessions", ModMcp.SessionCount},
                    {"lastError", ModMcp.LastError}
                }}
            }

            Dim Games As New JArray()
            For Each GameWatcher As Watcher In McWatcherList.ToList()
                If GameWatcher.GameProcess Is Nothing Then Continue For
                Games.Add(New JObject From {
                    {"pid", GameWatcher.GameProcess.Id},
                    {"instance", If(GameWatcher.Instance Is Nothing, "", GameWatcher.Instance.Name)},
                    {"state", GameWatcher.State.ToString()},
                    {"exited", GameWatcher.GameProcess.HasExited}
                })
            Next
            Result("runningGames") = Games

            Dim Tasks As New JArray()
            For Each TaskLoader As LoaderBase In LoaderTaskbar.ToList()
                Tasks.Add(New JObject From {
                    {"id", TaskLoader.Uuid},
                    {"name", TaskLoader.Name},
                    {"state", TaskLoader.State.ToString()},
                    {"progress", Math.Round(TaskLoader.Progress, 3)}
                })
            Next
            Result("tasks") = Tasks

            Result("folderCount") = McFolderList.Count
            Result("instanceCount") = GetAllInstances().Count
            Return Result
        End Function)
        Return McpResult.Success(Data)
    End Function

    Private Function ToolListFolders(Args As JObject) As JObject
        EnsureFolderList()
        Dim Data As JObject = InvokeUi(Function()
            Dim Folders As New JArray()
            For Each Folder As McFolder In McFolderList.ToList()
                Folders.Add(New JObject From {
                    {"name", Folder.Name},
                    {"path", Folder.Location},
                    {"type", Folder.Type.ToString()},
                    {"selected", PathUtils.IsSame(Folder.Location, McFolderSelected)}
                })
            Next
            Return New JObject From {
                {"folders", Folders},
                {"selected", McFolderSelected}
            }
        End Function)
        Return McpResult.Success(Data)
    End Function

    Private Function ToolListInstances(Args As JObject) As JObject
        Dim Refresh As Boolean = GetBoolValue(Args, "refresh", False)
        EnsureInstanceList(Refresh)
        Dim Snapshot As List(Of McInstance) = InvokeUi(Function() GetAllInstances())
        Dim Folder As String = InvokeUi(Function() McFolderSelected)
        Dim SelectedName As String = InvokeUi(Function() If(McInstanceSelected Is Nothing, "", McInstanceSelected.Name))
        Dim Instances As New JArray()
        For Each Instance As McInstance In Snapshot
            Instances.Add(GetInstanceJson(Instance, False))
        Next
        Return McpResult.Success(New JObject From {
            {"folder", Folder},
            {"selected", SelectedName},
            {"count", Instances.Count},
            {"instances", Instances}
        })
    End Function

    Private Function ToolGetInstance(Args As JObject) As JObject
        EnsureInstanceList(False)
        Dim Name As String = GetStringValue(Args, "name", False, "")
        Dim Instance As McInstance = InvokeUi(Function()
            If String.IsNullOrWhiteSpace(Name) Then
                If McInstanceSelected Is Nothing Then Throw New McpToolException("instance_not_found", "当前没有选中的 Minecraft 实例。")
                Return McInstanceSelected
            End If
            Dim Found As McInstance = FindInstance(Name)
            If Found Is Nothing Then Throw New McpToolException("instance_not_found", "未找到实例：" & Name)
            Return Found
        End Function)
        Return McpResult.Success(GetInstanceJson(Instance, True))
    End Function

    Private Function ToolListMods(Args As JObject) As JObject
        Dim IncludeDisabled As Boolean = GetBoolValue(Args, "include_disabled", True)
        Dim Instance As McInstance = ResolveInstance(Args)
        Dim Mods As List(Of LocalResourceFile) = GetModFiles(Instance)
        Dim Result As New JArray()
        For Each ModFile As LocalResourceFile In Mods
            If Not IncludeDisabled AndAlso Not ModFile.IsEnabled Then Continue For
            Result.Add(GetModJson(ModFile))
        Next
        Return McpResult.Success(New JObject From {
            {"instance", Instance.Name},
            {"modsPath", Instance.PathIndie & "mods\"},
            {"count", Result.Count},
            {"mods", Result}
        })
    End Function

    Private Function ToolGetSettings(Args As JObject) As JObject
        Dim Scope As String = GetScope(Args)
        Dim Instance As McInstance = Nothing
        If Scope = "instance" Then Instance = ResolveInstance(Args)

        Dim RequestedKeys As HashSet(Of String) = Nothing
        Dim KeysToken = Args("keys")
        If KeysToken IsNot Nothing AndAlso KeysToken.Type <> JTokenType.Null Then
            If TypeOf KeysToken IsNot JArray Then Throw New McpToolException("invalid_params", "keys 必须是字符串数组。")
            RequestedKeys = New HashSet(Of String)(DirectCast(KeysToken, JArray).Select(Function(t) t.ToString()), StringComparer.OrdinalIgnoreCase)
            For Each Key As String In RequestedKeys
                If Not Settings.Entries.ContainsKey(Key) Then Throw New McpToolException("invalid_params", "未知设置项：" & Key)
            Next
        End If

        Dim SettingsResult As New JArray()
        For Each Pair As KeyValuePair(Of String, Settings.Setting) In Settings.Entries
            Dim Key As String = Pair.Key
            Dim Entry As Settings.Setting = Pair.Value
            If Entry.Encrypted Then Continue For
            If Scope = "launcher" AndAlso Entry.Source = Settings.Sources.Instance Then Continue For
            If Scope = "instance" AndAlso Entry.Source <> Settings.Sources.Instance Then Continue For
            If RequestedKeys IsNot Nothing AndAlso Not RequestedKeys.Contains(Key) Then Continue For
            Try
                Dim CurrentValue As Object = Settings.Get(Key, Instance)
                SettingsResult.Add(New JObject From {
                    {"key", Key},
                    {"value", ToJToken(CurrentValue)},
                    {"type", Entry.Type.Name},
                    {"default", ToJToken(Entry.DefaultValue)},
                    {"source", Entry.Source.ToString()},
                    {"protected", IsProtectedSetting(Key)}
                })
            Catch ex As Exception
                SettingsResult.Add(New JObject From {
                    {"key", Key},
                    {"error", ex.Message},
                    {"type", Entry.Type.Name},
                    {"source", Entry.Source.ToString()},
                    {"protected", IsProtectedSetting(Key)}
                })
            End Try
        Next
        Return McpResult.Success(New JObject From {
            {"scope", Scope},
            {"instance", If(Instance Is Nothing, "", Instance.Name)},
            {"settings", SettingsResult}
        })
    End Function

    Private Function ToolGetLogin(Args As JObject) As JObject
        Dim Data As JObject = InvokeUi(Function()
            Return New JObject From {
                {"loginType", Settings.Get(Of McLoginType)("LoginType").ToString()},
                {"playerName", If(McLoginName(), "")}
            }
        End Function)
        Return McpResult.Success(Data)
    End Function

    Private Function ToolListTasks(Args As JObject) As JObject
        Dim Tasks As New JArray()
        For Each TaskLoader As LoaderBase In LoaderTaskbar.ToList()
            Tasks.Add(New JObject From {
                {"id", TaskLoader.Uuid},
                {"name", TaskLoader.Name},
                {"state", TaskLoader.State.ToString()},
                {"progress", Math.Round(TaskLoader.Progress, 3)},
                {"show", TaskLoader.Show},
                {"error", If(TaskLoader.Error Is Nothing, "", TaskLoader.Error.Message)}
            })
        Next
        Return McpResult.Success(New JObject From {{"tasks", Tasks}})
    End Function

    Private Function ToolReadLog(Args As JObject) As JObject
        Dim FileIndex As Integer = GetIntValue(Args, "file", 1, 1, 5)
        Dim LineCount As Integer = GetIntValue(Args, "lines", 100, 1, 500)
        Dim LogPath As String = Paths.Base & DataFolder & "\Log" & FileIndex & ".txt"
        If Not File.Exists(LogPath) Then Throw New McpToolException("log_not_found", "日志文件不存在：" & LogPath)
        Dim Lines As New Queue(Of String)()
        Using Stream As New FileStream(LogPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite)
            Using Reader As New StreamReader(Stream, New UTF8Encoding(False), True)
                Do Until Reader.EndOfStream
                    Lines.Enqueue(Reader.ReadLine())
                    If Lines.Count > LineCount Then Lines.Dequeue()
                Loop
            End Using
        End Using
        Dim Result As New JArray()
        For Each Line As String In Lines
            Result.Add(Line)
        Next
        Return McpResult.Success(New JObject From {
            {"file", "Log" & FileIndex & ".txt"},
            {"path", LogPath},
            {"lines", Result}
        })
    End Function

    Private Function ToolSearchResources(Args As JObject) As JObject
        Dim Query As String = GetStringValue(Args, "query", False, "")
        Dim ResourceType As ResourceTypes = ParseResourceType(GetStringValue(Args, "type", False, "mod"))
        Dim Platform As ResourcePlatforms = ParseResourcePlatforms(GetStringValue(Args, "source", False, "any"))
        Dim Loader As ModLoaders = ParseModLoaders(GetStringValue(Args, "loader", False, "any"))
        Dim GameVersion As String = GetStringValue(Args, "game_version", False, "")
        Dim Limit As Integer = GetIntValue(Args, "limit", 10, 1, 40)

        Dim Storage As New ResourceSearcher.SearchResult()
        Dim Request As New ResourceSearcher.SearchRequest(ResourceType, Storage, Limit) With {
            .SearchText = Query,
            .Sources = Platform,
            .ModLoaders = Loader,
            .GameVersion = If(String.IsNullOrWhiteSpace(GameVersion), Nothing, GameVersion)
        }
        Dim SearchLoader As LoaderTask(Of ResourceSearcher.SearchRequest, Integer) = ResourceSearcher.GetLoader(Function() Request)
        Try
            SearchLoader.WaitForExitTime(30000, Request)
        Catch ex As TimeoutException
            SearchLoader.Cancel()
            Throw New McpToolException("timeout", "社区资源搜索超时。")
        End Try

        Dim Results As New JArray()
        For Each Project As ResourceProject In Storage.Results.Take(Limit)
            Dim Drops As New JArray()
            For Each Drop As Integer In Project.Drops
                Drops.Add(Drop)
            Next
            Dim Tags As New JArray()
            For Each Tag As String In Project.Tags
                Tags.Add(Tag)
            Next
            Results.Add(New JObject From {
                {"name", Project.RawName},
                {"translatedName", Project.TranslatedName},
                {"slug", Project.Slug},
                {"id", Project.Id},
                {"platform", Project.Platform.ToString()},
                {"types", Project.Types.ToString()},
                {"modLoaders", Project.ModLoaders.ToString()},
                {"description", Project.Description},
                {"website", Project.Website},
                {"downloadCount", Project.DownloadCount},
                {"drops", Drops},
                {"tags", Tags}
            })
        Next
        Return McpResult.Success(New JObject From {
            {"query", Query},
            {"type", ResourceType.ToString()},
            {"source", Platform.ToString()},
            {"count", Results.Count},
            {"error", If(Storage.ErrorMessage, "")},
            {"results", Results}
        })
    End Function

#End Region

#Region "控制工具"

    Private Function ToolSelectInstance(Args As JObject) As JObject
        Dim Name As String = GetStringValue(Args, "name", True)
        EnsureInstanceList(False)
        Dim Instance As McInstance = InvokeUi(Function()
            Dim Found As McInstance = FindInstance(Name)
            If Found Is Nothing Then Throw New McpToolException("instance_not_found", "未找到实例：" & Name)
            McInstanceSelected = Found
            If FrmLaunchLeft IsNot Nothing Then FrmLaunchLeft.RefreshButtonsUI()
            Return Found
        End Function)
        Return McpResult.Success(GetInstanceJson(Instance, False))
    End Function

    Private Function ToolLaunchGame(Args As JObject) As JObject
        Dim Name As String = GetStringValue(Args, "instance", False, "")
        Dim ServerIp As String = GetStringValue(Args, "server_ip", False, "")
        Dim ExtraArgs As List(Of String) = GetStringArray(Args, "extra_args")
        Dim AccountMode As String = GetStringValue(Args, "account_mode", False, "current").ToLowerInvariant()
        Dim OfflineUserName As String = GetStringValue(Args, "offline_username", False, "")
        Dim WaitForStart As Boolean = GetBoolValue(Args, "wait", False)
        Dim TimeoutSeconds As Integer = GetIntValue(Args, "timeout_seconds", 30, 1, 120)
        EnsureInstanceList(False)

        Dim Instance As McInstance = InvokeUi(Function()
            Dim Found As McInstance = If(String.IsNullOrWhiteSpace(Name), McInstanceSelected, FindInstance(Name))
            If Found Is Nothing Then
                If String.IsNullOrWhiteSpace(Name) Then Throw New McpToolException("instance_not_found", "当前没有选中的 Minecraft 实例。")
                Throw New McpToolException("instance_not_found", "未找到实例：" & Name)
            End If
            Return Found
        End Function)

        Dim LoginOverride As McLoginData = ResolveLaunchLogin(AccountMode, OfflineUserName, Instance)
        Dim PlayerName As String = GetLoginPlayerName(LoginOverride)
        Dim LoginTypeName As String = If(LoginOverride Is Nothing, Settings.Get(Of McLoginType)("LoginType").ToString(), LoginOverride.Type.ToString())
        Dim Accepted As Boolean = False
        InvokeUi(Sub()
            Dim Options As New McLaunchOptions With {.Instance = Instance, .LoginOverride = LoginOverride, .SuppressPrompts = True}
            If Not String.IsNullOrWhiteSpace(ServerIp) Then Options.ServerIp = ServerIp
            If ExtraArgs IsNot Nothing AndAlso ExtraArgs.Any() Then Options.ExtraGameArgs.AddRange(ExtraArgs)
            Accepted = McLaunchStart(Options)
        End Sub)

        If Not Accepted Then Return McpResult.Failure("launch_rejected", "启动器拒绝了启动请求，请查看启动器中的提示。")

        Dim TimedOut As Boolean = False
        Dim ProcessId As Integer = 0
        If WaitForStart Then
            Dim Deadline As Long = GetTimeMs() + TimeoutSeconds * 1000L
            Do While GetTimeMs() < Deadline
                Dim GameProcess As Process = McLaunchProcess
                If GameProcess IsNot Nothing AndAlso Not GameProcess.HasExited Then
                    ProcessId = GameProcess.Id
                    Exit Do
                End If
                If McLaunchLoader.State = LoadState.Failed Then Throw New McpToolException("launch_failed", "启动失败：" & LoaderMessage(McLaunchLoader))
                If McLaunchLoader.State = LoadState.Canceled Then Throw New McpToolException("launch_canceled", "启动已取消。")
                Thread.Sleep(100)
            Loop
            TimedOut = ProcessId = 0
        End If

        Return McpResult.Success(New JObject From {
            {"accepted", True},
            {"instance", Instance.Name},
            {"accountMode", AccountMode},
            {"playerName", PlayerName},
            {"loginType", LoginTypeName},
            {"state", McLaunchLoader.State.ToString()},
            {"processId", ProcessId},
            {"timedOut", TimedOut}
        })
    End Function

    Private Function ToolStopGame(Args As JObject) As JObject
        Dim StoppedPids As New JArray()
        Dim Stopped As Integer = 0
        InvokeUi(Sub()
            Dim Seen As New HashSet(Of Integer)()
            For Each GameWatcher As Watcher In McWatcherList.ToList()
                If GameWatcher.GameProcess Is Nothing Then Continue For
                If GameWatcher.State = Watcher.MinecraftState.Ended OrElse
                   GameWatcher.State = Watcher.MinecraftState.Canceled Then Continue For
                Try
                    If Not GameWatcher.GameProcess.HasExited AndAlso Seen.Add(GameWatcher.GameProcess.Id) Then
                        StoppedPids.Add(GameWatcher.GameProcess.Id)
                        GameWatcher.Kill()
                        Stopped += 1
                    End If
                Catch ex As Exception
                    Logger.Warn(ex, "MCP 停止游戏进程失败")
                End Try
            Next
            If McLaunchLoader.State = LoadState.Loading Then McLaunchLoader.Cancel()
            If McLaunchLoaderReal IsNot Nothing AndAlso McLaunchLoaderReal.State = LoadState.Loading Then McLaunchLoaderReal.Cancel()
        End Sub)
        Return McpResult.Success(New JObject From {
            {"stopped", Stopped},
            {"processIds", StoppedPids}
        })
    End Function

    Private Function ToolSetSetting(Args As JObject) As JObject
        Dim Key As String = GetStringValue(Args, "key", True)
        Dim ValueToken As JToken = Args("value")
        If ValueToken Is Nothing Then Throw New McpToolException("invalid_params", "缺少参数：value")
        Dim Scope As String = GetScope(Args)
        Dim Instance As McInstance = Nothing
        If Scope = "instance" Then Instance = ResolveInstance(Args)
        Dim Entry As Settings.Setting = GetSettingEntry(Key)
        If Entry.Encrypted Then Throw New McpToolException("encrypted_setting", "不允许通过 MCP 修改加密设置项：" & Key)
        If IsProtectedSetting(Key) Then Throw New McpToolException("protected_setting", "该设置项被保护，不能通过 MCP 修改：" & Key)
        If Scope = "launcher" AndAlso Entry.Source = Settings.Sources.Instance Then
            Throw New McpToolException("invalid_params", "该设置项属于实例范围，请使用 scope=instance 并提供实例名。")
        End If
        If Scope = "instance" AndAlso Entry.Source <> Settings.Sources.Instance Then
            Throw New McpToolException("invalid_params", "该设置项属于启动器范围，请使用 scope=launcher。")
        End If

        Dim NewValue As Object = ConvertSettingValue(Entry, ValueToken)
        Dim OldValue As Object = Settings.Get(Key, Instance)
        InvokeUi(Sub() Settings.SetSafe(Key, NewValue, Instance))
        Dim AppliedValue As Object = Settings.Get(Key, Instance)
        Return McpResult.Success(New JObject From {
            {"key", Key},
            {"scope", Scope},
            {"instance", If(Instance Is Nothing, "", Instance.Name)},
            {"oldValue", ToJToken(OldValue)},
            {"newValue", ToJToken(AppliedValue)}
        })
    End Function

    Private Function ToolResetSetting(Args As JObject) As JObject
        Dim Key As String = GetStringValue(Args, "key", True)
        Dim Scope As String = GetScope(Args)
        Dim Instance As McInstance = Nothing
        If Scope = "instance" Then Instance = ResolveInstance(Args)
        Dim Entry As Settings.Setting = GetSettingEntry(Key)
        If Entry.Encrypted Then Throw New McpToolException("encrypted_setting", "不允许通过 MCP 重置加密设置项：" & Key)
        If IsProtectedSetting(Key) Then Throw New McpToolException("protected_setting", "该设置项被保护，不能通过 MCP 重置：" & Key)
        If Scope = "launcher" AndAlso Entry.Source = Settings.Sources.Instance Then
            Throw New McpToolException("invalid_params", "该设置项属于实例范围，请使用 scope=instance 并提供实例名。")
        End If
        If Scope = "instance" AndAlso Entry.Source <> Settings.Sources.Instance Then
            Throw New McpToolException("invalid_params", "该设置项属于启动器范围，请使用 scope=launcher。")
        End If
        InvokeUi(Sub() Settings.Reset(Key, Instance))
        Return McpResult.Success(New JObject From {
            {"key", Key},
            {"scope", Scope},
            {"instance", If(Instance Is Nothing, "", Instance.Name)},
            {"defaultValue", ToJToken(Entry.DefaultValue)},
            {"currentValue", ToJToken(Settings.Get(Key, Instance))}
        })
    End Function

    Private Function ToolSetModEnabled(Args As JObject) As JObject
        Dim Instance As McInstance = ResolveInstance(Args)
        Dim FileName As String = GetStringValue(Args, "file", True)
        If Args("enabled") Is Nothing Then Throw New McpToolException("invalid_params", "缺少参数：enabled")
        Dim Enabled As Boolean = GetBoolValue(Args, "enabled", False)
        Dim NewPath As String = SetModEnabled(Instance, FileName, Enabled)
        Return McpResult.Success(New JObject From {
            {"instance", Instance.Name},
            {"file", FileName},
            {"enabled", Enabled},
            {"path", NewPath}
        })
    End Function

    Private Function ToolNavigate(Args As JObject) As JObject
        Dim PageText As String = GetStringValue(Args, "page", True).ToLowerInvariant()
        Dim Page As FormMain.PageType
        Select Case PageText
            Case "launch"
                Page = FormMain.PageType.Launch
            Case "download"
                Page = FormMain.PageType.Download
            Case "setup"
                Page = FormMain.PageType.Setup
            Case "other"
                Page = FormMain.PageType.Other
            Case "instance_select"
                Page = FormMain.PageType.InstanceSelect
            Case "task_manager"
                Page = FormMain.PageType.TaskManager
            Case Else
                Throw New McpToolException("invalid_params", "未知页面：" & PageText)
        End Select
        Dim SubType As FormMain.PageSubType = FormMain.PageSubType.Default
        Dim SubPageToken = Args("subpage")
        If SubPageToken IsNot Nothing AndAlso SubPageToken.Type <> JTokenType.Null Then
            SubType = CType(SubPageToken.ToObject(Of Integer)(), FormMain.PageSubType)
        End If
        Dim ShowWindow As Boolean = GetBoolValue(Args, "show", False)
        InvokeUi(Sub()
            FrmMain.PageChange(New FormMain.PageStackData With {.Page = Page}, SubType)
            If ShowWindow Then FrmMain.ShowWindowToTop()
        End Sub)
        Return McpResult.Success(New JObject From {
            {"page", PageText},
            {"subpage", CInt(SubType)},
            {"shown", ShowWindow}
        })
    End Function

    Private Function ToolShowLauncher(Args As JObject) As JObject
        InvokeUi(Sub() FrmMain.ShowWindowToTop())
        Return McpResult.Success(New JObject From {{"shown", True}})
    End Function

    Private Function ToolCancelTask(Args As JObject) As JObject
        Dim AllTasks As Boolean = GetBoolValue(Args, "all", False)
        Dim Name As String = GetStringValue(Args, "name", False, "")
        Dim Id As Integer = GetIntValue(Args, "id", 0, 0, Integer.MaxValue)
        If Not AllTasks AndAlso String.IsNullOrEmpty(Name) AndAlso Id <= 0 Then
            Throw New McpToolException("invalid_params", "请提供 id、name 或 all。")
        End If
        Dim Tasks As List(Of LoaderBase) = LoaderTaskbar.ToList()
        Dim Targets As New List(Of LoaderBase)
        For Each TaskLoader As LoaderBase In Tasks
            If AllTasks OrElse (Id > 0 AndAlso TaskLoader.Uuid = Id) OrElse
               (Not String.IsNullOrEmpty(Name) AndAlso TaskLoader.Name.IndexOf(Name, StringComparison.OrdinalIgnoreCase) >= 0) Then
                Targets.Add(TaskLoader)
            End If
        Next
        If Not Targets.Any() Then Return McpResult.Failure("task_not_found", "没有找到匹配的任务。")
        Dim Names As New JArray()
        For Each TaskLoader As LoaderBase In Targets
            Names.Add(TaskLoader.Name)
            Try
                TaskLoader.Cancel()
            Catch ex As Exception
                Logger.Warn(ex, "MCP 取消任务失败：" & TaskLoader.Name)
            End Try
        Next
        Return McpResult.Success(New JObject From {
            {"cancelled", Names.Count},
            {"tasks", Names}
        })
    End Function

    Private Function ToolInstallVanilla(Args As JObject) As JObject
        Dim McVersionName As String = GetStringValue(Args, "mc_version", True)
        Dim InstanceName As String = GetStringValue(Args, "name", False, McVersionName)
        If Not McVersionName.RegexCheck("^[0-9A-Za-z][0-9A-Za-z._-]*$") Then
            Throw New McpToolException("invalid_params", "Minecraft 版本号格式无效：" & McVersionName)
        End If
        ValidateInstanceName(InstanceName)
        EnsureFolderList()
        Dim VersionFolder As String = $"{McFolderSelected}versions\{InstanceName}\"
        If DirectoryUtils.Exists(VersionFolder) Then
            Throw New McpToolException("instance_exists", "实例已存在：" & InstanceName)
        End If
        Dim JsonUrl As String = DlClientListGet(McVersionName)
        If String.IsNullOrEmpty(JsonUrl) Then Throw New McpToolException("download_failed", "无法获取 Minecraft " & McVersionName & " 的下载信息。")
        Dim Components As New McComponents(McVersionName, Nothing, Nothing, Nothing, Nothing, Nothing, Nothing, Nothing)
        Dim Request As New McInstallRequest With {
            .NewInstanceName = InstanceName,
            .InstanceFolder = VersionFolder,
            .Components = Components,
            .MinecraftJson = JsonUrl
        }
        Dim Started As Boolean = InvokeUi(Function() McInstallStart(Request))
        If Not Started Then Throw New McpToolException("install_failed", "启动器未能开始安装 Minecraft " & McVersionName & "。")
        Return McpResult.Success(New JObject From {
            {"accepted", True},
            {"instance", InstanceName},
            {"mcVersion", McVersionName},
            {"taskName", InstanceName & " 安装"}
        })
    End Function

#End Region

#Region "完全控制工具"

    Private Function ToolDeleteMod(Args As JObject) As JObject
        Dim Confirm As Boolean = GetBoolValue(Args, "confirm", False)
        If Not Confirm Then Throw New McpToolException("confirm_required", "删除 Mod 需要显式传入 confirm=true。")
        Dim Instance As McInstance = ResolveInstance(Args)
        Dim FileName As String = GetStringValue(Args, "file", True)
        Dim ModFile As LocalResourceFile = ResolveModFile(Instance, FileName)
        Dim FilePath As String = ModFile.File.FullName
        Dim DeleteResult As Boolean? = FileUtils.Delete(FilePath, True)
        If DeleteResult Is Nothing Then Throw New McpToolException("mod_not_found", "Mod 文件已不存在：" & FileName)
        RefreshModsUi(Instance)
        Return McpResult.Success(New JObject From {
            {"deleted", True},
            {"instance", Instance.Name},
            {"file", ModFile.File.Name},
            {"path", FilePath},
            {"recycleBin", DeleteResult.Value}
        })
    End Function

    Private Function ToolShutdownLauncher(Args As JObject) As JObject
        Dim DelayMs As Integer = GetIntValue(Args, "delay_ms", 500, 0, 5000)
        RunInNewThread(
        Sub()
            Thread.Sleep(DelayMs)
            RunInUi(
            Sub()
                FrmMain.EndProgram(False)
            End Sub)
        End Sub, "MCP Shutdown")
        Return McpResult.Success(New JObject From {
            {"accepted", True},
            {"delayMs", DelayMs}
        })
    End Function

#End Region

#Region "实例、Mod 与设置辅助"

    Private Function EmptySchema() As JObject
        Return Schema(New JObject())
    End Function

    Private Function Schema(Properties As JObject, Optional Required As String() = Nothing) As JObject
        Dim Result As New JObject From {
            {"type", "object"},
            {"properties", Properties},
            {"additionalProperties", False}
        }
        If Required IsNot Nothing AndAlso Required.Length > 0 Then
            Dim RequiredArray As New JArray()
            For Each Item As String In Required
                RequiredArray.Add(Item)
            Next
            Result("required") = RequiredArray
        End If
        Return Result
    End Function

    Private Function StringProperty(Description As String, Optional EnumValues As String() = Nothing, Optional DefaultValue As String = Nothing) As JObject
        Dim Result As New JObject From {
            {"type", "string"},
            {"description", Description}
        }
        If EnumValues IsNot Nothing Then
            Dim EnumArray As New JArray()
            For Each Item As String In EnumValues
                EnumArray.Add(Item)
            Next
            Result("enum") = EnumArray
        End If
        If DefaultValue IsNot Nothing Then Result("default") = JToken.FromObject(DefaultValue)
        Return Result
    End Function

    Private Function BooleanProperty(Description As String, DefaultValue As Object) As JObject
        Dim Result As New JObject From {
            {"type", "boolean"},
            {"description", Description}
        }
        If DefaultValue IsNot Nothing Then Result("default") = JToken.FromObject(DefaultValue)
        Return Result
    End Function

    Private Function IntegerProperty(Description As String, Min As Integer, Max As Integer, DefaultValue As Object) As JObject
        Dim Result As New JObject From {
            {"type", "integer"},
            {"description", Description},
            {"minimum", Min},
            {"maximum", Max}
        }
        If DefaultValue IsNot Nothing Then Result("default") = JToken.FromObject(DefaultValue)
        Return Result
    End Function

    Private Function ArrayOfStringsProperty(Description As String) As JObject
        Return New JObject From {
            {"type", "array"},
            {"description", Description},
            {"items", New JObject From {{"type", "string"}}}
        }
    End Function

    Private Function AnyProperty(Description As String) As JObject
        Return New JObject From {
            {"description", Description}
        }
    End Function

    Private Function GetStringValue(Args As JObject, Name As String, Optional Required As Boolean = False, Optional DefaultValue As String = Nothing) As String
        Dim Token = Args(Name)
        If Token Is Nothing OrElse Token.Type = JTokenType.Null Then
            If Required Then Throw New McpToolException("invalid_params", "缺少参数：" & Name)
            Return DefaultValue
        End If
        If Token.Type = JTokenType.Object OrElse Token.Type = JTokenType.Array Then
            Throw New McpToolException("invalid_params", $"参数 {Name} 必须是字符串。")
        End If
        Return Token.ToString()
    End Function

    Private Function GetBoolValue(Args As JObject, Name As String, DefaultValue As Boolean) As Boolean
        Dim Token = Args(Name)
        If Token Is Nothing OrElse Token.Type = JTokenType.Null Then Return DefaultValue
        If Token.Type = JTokenType.Boolean Then Return Token.Value(Of Boolean)()
        Dim Text As String = Token.ToString()
        Dim Parsed As Boolean
        If Boolean.TryParse(Text, Parsed) Then Return Parsed
        If Text = "1" Then Return True
        If Text = "0" Then Return False
        Throw New McpToolException("invalid_params", $"参数 {Name} 必须是布尔值。")
    End Function

    Private Function GetIntValue(Args As JObject, Name As String, DefaultValue As Integer, Min As Integer, Max As Integer) As Integer
        Dim Token = Args(Name)
        If Token Is Nothing OrElse Token.Type = JTokenType.Null Then Return DefaultValue
        Dim Parsed As Integer
        If Token.Type = JTokenType.Integer Then
            Parsed = Token.Value(Of Integer)()
        ElseIf Not Integer.TryParse(Token.ToString(), Parsed) Then
            Throw New McpToolException("invalid_params", $"参数 {Name} 必须是整数。")
        End If
        If Parsed < Min OrElse Parsed > Max Then
            Throw New McpToolException("invalid_params", $"参数 {Name} 必须在 {Min} 到 {Max} 之间。")
        End If
        Return Parsed
    End Function

    Private Function GetStringArray(Args As JObject, Name As String) As List(Of String)
        Dim Token = Args(Name)
        If Token Is Nothing OrElse Token.Type = JTokenType.Null Then Return New List(Of String)
        If TypeOf Token IsNot JArray Then Throw New McpToolException("invalid_params", $"参数 {Name} 必须是字符串数组。")
        Return DirectCast(Token, JArray).Select(Function(t) t.ToString()).ToList()
    End Function

    Private Function GetScope(Args As JObject) As String
        Dim Scope As String = GetStringValue(Args, "scope", False, "launcher").ToLowerInvariant()
        If Scope <> "launcher" AndAlso Scope <> "instance" Then
            Throw New McpToolException("invalid_params", "scope 只能是 launcher 或 instance。")
        End If
        Return Scope
    End Function

    Private Function GetSettingEntry(Key As String) As Settings.Setting
        Dim Entry As Settings.Setting = Nothing
        If Not Settings.Entries.TryGetValue(Key, Entry) Then Throw New McpToolException("invalid_params", "未知设置项：" & Key)
        Return Entry
    End Function

    Private Function ConvertSettingValue(Entry As Settings.Setting, Token As JToken) As Object
        If Token Is Nothing OrElse Token.Type = JTokenType.Null Then Throw New McpToolException("invalid_params", "设置值不能为 null。")
        Dim TargetType As Type = Entry.Type
        If TargetType Is GetType(String) Then Return Token.ToString()
        If TargetType Is GetType(Boolean) Then
            If Token.Type = JTokenType.Boolean Then Return Token.Value(Of Boolean)()
            Dim Text As String = Token.ToString()
            Dim Parsed As Boolean
            If Boolean.TryParse(Text, Parsed) Then Return Parsed
            If Text = "1" Then Return True
            If Text = "0" Then Return False
            Throw New McpToolException("invalid_params", "该设置项需要布尔值。")
        End If
        If TargetType Is GetType(Integer) Then
            If Token.Type = JTokenType.Integer Then Return Token.Value(Of Integer)()
            Dim Parsed As Integer
            If Integer.TryParse(Token.ToString(), Parsed) Then Return Parsed
            Throw New McpToolException("invalid_params", "该设置项需要整数。")
        End If
        If TargetType Is GetType(Long) Then
            If Token.Type = JTokenType.Integer Then Return Token.Value(Of Long)()
            Dim Parsed As Long
            If Long.TryParse(Token.ToString(), Parsed) Then Return Parsed
            Throw New McpToolException("invalid_params", "该设置项需要 64 位整数。")
        End If
        If TargetType.IsEnum Then Return [Enum].Parse(TargetType, Token.ToString(), True)
        Return CTypeDynamic(Token.ToString(), TargetType)
    End Function

    Private Function IsProtectedSetting(Key As String) As Boolean
        If ProtectedExact.Contains(Key) Then Return True
        For Each Prefix As String In ProtectedPrefixes
            If Key.StartsWith(Prefix, StringComparison.OrdinalIgnoreCase) Then Return True
        Next
        Return False
    End Function

    Private Function ToJToken(Value As Object) As JToken
        If Value Is Nothing Then Return JValue.CreateNull()
        If Value.GetType().IsEnum Then Return New JValue(Value.ToString())
        If TypeOf Value Is Date Then Return New JValue(DirectCast(Value, Date).ToString("o"))
        Return JToken.FromObject(Value)
    End Function

    Private Function PermissionName(Value As Integer) As String
        Select Case Value
            Case 0
                Return "只读"
            Case 1
                Return "标准控制"
            Case Else
                Return "完全控制"
        End Select
    End Function

    Private Function ResolveInstance(Args As JObject) As McInstance
        EnsureInstanceList(False)
        Dim Name As String = GetStringValue(Args, "instance", False, "")
        Dim Instance As McInstance = InvokeUi(Function()
            If String.IsNullOrWhiteSpace(Name) Then Return McInstanceSelected
            Return FindInstance(Name)
        End Function)
        If Instance Is Nothing Then
            If String.IsNullOrWhiteSpace(Name) Then Throw New McpToolException("instance_not_found", "当前没有选中的 Minecraft 实例。")
            Throw New McpToolException("instance_not_found", "未找到实例：" & Name)
        End If
        Return Instance
    End Function

    ''' <summary>
    ''' 为本次启动构造登录覆盖。current 返回 Nothing，沿用启动器当前设置。
    ''' </summary>
    Private Function ResolveLaunchLogin(Mode As String, OfflineUserName As String, Instance As McInstance) As McLoginData
        Select Case Mode
            Case "current", ""
                Return Nothing
            Case "offline"
                Dim UserName As String = OfflineUserName.Trim()
                If UserName = "" Then
                    Throw New McpToolException("offline_username_required", "离线模式需要提供 offline_username。")
                End If
                ValidateOfflineUserName(UserName, Instance)
                Return New McLoginLegacy With {
                    .UserName = UserName,
                    .SkinType = Settings.Get(Of Integer)("LaunchSkinType"),
                    .SkinName = Settings.Get(Of String)("LaunchSkinID"),
                    .SkipHistorySave = True
                }
            Case "microsoft", "official"
                If String.IsNullOrWhiteSpace(OAuthClientId) Then
                    Throw New McpToolException("microsoft_login_unavailable",
                        "当前启动器未配置微软登录 Client ID（PCL_MS_CLIENT_ID），无法使用正版账号身份运行。请配置后重启启动器，或使用离线模式。")
                End If
                Dim RefreshToken As String = Settings.Get(Of String)("CacheMsV2OAuthRefresh")
                If String.IsNullOrWhiteSpace(RefreshToken) Then
                    Throw New McpToolException("microsoft_account_required",
                        "当前没有已登录的微软账号，无法使用正版账号身份运行。请先在启动器中登录，或使用离线模式。")
                End If
                Return New McLoginMs With {
                    .OAuthRefreshToken = RefreshToken,
                    .UserName = Settings.Get(Of String)("CacheMsV2Name"),
                    .AccessToken = Settings.Get(Of String)("CacheMsV2Access"),
                    .Uuid = Settings.Get(Of String)("CacheMsV2Uuid"),
                    .ProfileJson = Settings.Get(Of String)("CacheMsV2ProfileJson")
                }
            Case Else
                Throw New McpToolException("invalid_params", "account_mode 只能是 current、microsoft 或 offline。")
        End Select
    End Function

    Private Sub ValidateOfflineUserName(UserName As String, Instance As McInstance)
        If String.IsNullOrWhiteSpace(UserName) Then Throw New McpToolException("invalid_params", "离线玩家名不能为空。")
        If UserName.Contains("""") Then Throw New McpToolException("invalid_params", "离线玩家名不能包含英文引号。")
        If UserName.Length > 16 Then
            Try
                If Instance.Version.Valid AndAlso Instance.Version.Vanilla >= New Version(20, 0, 3) Then
                    Throw New McpToolException("invalid_params", "自 1.20.3 起，离线玩家名至多只能包含 16 个字符。")
                End If
            Catch ex As McpToolException
                Throw
            Catch
                '版本信息不可用时交给启动器的预检测处理
            End Try
        End If
    End Sub

    Private Function GetLoginPlayerName(Login As McLoginData) As String
        If Login Is Nothing Then Return If(McLoginName(), "")
        Dim LegacyLogin = TryCast(Login, McLoginLegacy)
        If LegacyLogin IsNot Nothing Then Return LegacyLogin.UserName
        Dim MsLogin = TryCast(Login, McLoginMs)
        If MsLogin IsNot Nothing Then Return MsLogin.UserName
        Return ""
    End Function

    Private Sub EnsureFolderList()
        If McFolderListLoader.State = LoadState.Finished Then Return
        Try
            McFolderListLoader.Start(0)
            If Not WaitForLoader(McFolderListLoader, 20000) Then
                Throw New McpToolException("loader_failed", "Minecraft 文件夹列表加载失败：" & LoaderMessage(McFolderListLoader))
            End If
        Catch ex As McpToolException
            Throw
        Catch ex As Exception
            Throw New McpToolException("loader_failed", "Minecraft 文件夹列表加载失败：" & ex.Message)
        End Try
    End Sub

    Private Sub EnsureInstanceList(Refresh As Boolean)
        EnsureFolderList()
        If Not Refresh AndAlso McInstanceListLoader.State = LoadState.Finished Then Return
        Try
            LoaderFolderRun(McInstanceListLoader, McFolderSelected, LoaderFolderRunType.ForceRun, MaxDepth:=1, ExtraPath:="versions\", WaitForExit:=True)
        Catch ex As Exception
            Throw New McpToolException("loader_failed", "Minecraft 实例列表加载失败：" & ex.Message)
        End Try
        If McInstanceListLoader.State <> LoadState.Finished Then
            Throw New McpToolException("loader_failed", "Minecraft 实例列表加载失败：" & LoaderMessage(McInstanceListLoader))
        End If
    End Sub

    Private Function WaitForLoader(Loader As LoaderBase, TimeoutMs As Integer) As Boolean
        Dim Deadline As Long = GetTimeMs() + TimeoutMs
        While Loader.State = LoadState.Loading AndAlso GetTimeMs() < Deadline
            Thread.Sleep(50)
        End While
        Return Loader.State = LoadState.Finished
    End Function

    Private Function LoaderMessage(Loader As LoaderBase) As String
        If Loader Is Nothing Then Return "未知加载器错误"
        If Loader.Error IsNot Nothing Then Return Loader.Error.Message
        Return Loader.State.ToString()
    End Function

    Private Function GetAllInstances() As List(Of McInstance)
        Dim Result As New List(Of McInstance)
        For Each Pair As KeyValuePair(Of McInstanceCardType, List(Of McInstance)) In McInstanceList
            If Pair.Value IsNot Nothing Then Result.AddRange(Pair.Value)
        Next
        Return Result
    End Function

    Private Function FindInstance(Name As String) As McInstance
        If String.IsNullOrWhiteSpace(Name) Then Return Nothing
        For Each Instance As McInstance In GetAllInstances()
            If String.Equals(Instance.Name, Name, StringComparison.OrdinalIgnoreCase) Then Return Instance
        Next
        Return Nothing
    End Function

    Private Function GetInstanceJson(Instance As McInstance, Detail As Boolean) As JObject
        Dim Result As New JObject From {
            {"name", Instance.Name},
            {"info", Instance.Info},
            {"state", Instance.State.ToString()},
            {"path", Instance.PathVersion},
            {"selected", Instance Is McInstanceSelected},
            {"star", Instance.IsStar},
            {"displayType", Instance.DisplayType.ToString()}
        }
        Try
            Dim Version As McVersion = Instance.Version
            Dim Loaders As New JObject()
            For Each Pair As KeyValuePair(Of ModLoaders, String) In Version.ModLoaderVersions
                Loaders(Pair.Key.ToString()) = Pair.Value
            Next
            Result("version") = New JObject From {
                {"vanilla", Version.VanillaName},
                {"modLoaders", Loaders},
                {"optiFine", If(Version.HasOptiFine, Version.OptiFine, "")},
                {"releaseTime", Version.ReleaseTime.ToString("o")}
            }
            Result("versionDisplay") = Instance.VersionDisplayName()
        Catch ex As Exception
            Result("versionError") = ex.Message
        End Try
        If Detail Then
            Try
                Result("pathIndie") = Instance.PathIndie
                Result("jsonPath") = Instance.GetJsonPath()
                Result("modable") = Instance.Modable
                Result("modsCount") = GetModFiles(Instance).Count
            Catch ex As Exception
                Result("detailError") = ex.Message
            End Try
        End If
        Return Result
    End Function

    Private Function GetModFiles(Instance As McInstance) As List(Of LocalResourceFile)
        Dim Result As New List(Of LocalResourceFile)
        Dim Folder As String = Instance.PathIndie & "mods\"
        If Not DirectoryUtils.Exists(Folder) Then Return Result
        Try
            For Each FilePath As String In DirectoryUtils.EnumerateFiles(Folder, True)
                If Not PathUtils.IsSame(PathUtils.RemoveLastPart(FilePath), Folder) Then Continue For
                Dim Extension As String = PathUtils.GetExtension(FilePath).ToLowerInvariant()
                Dim BasePath As String = FilePath
                If Extension = "disabled" OrElse Extension = "old" Then BasePath = PathUtils.GetFileNameWithoutExtension(FilePath)
                Dim BaseExtension As String = PathUtils.GetExtension(BasePath).ToLowerInvariant()
                If Not {"jar", "zip", "litemod"}.Contains(BaseExtension) Then Continue For
                Result.Add(New LocalResourceFile(FilePath))
            Next
        Catch ex As Exception
            Throw New McpToolException("mods_unavailable", "读取 Mod 文件夹失败：" & ex.Message)
        End Try
        Result.Sort(Function(A, B) String.Compare(A.File.Name, B.File.Name, StringComparison.OrdinalIgnoreCase))
        Return Result
    End Function

    Private Function GetModJson(ModFile As LocalResourceFile) As JObject
        Return New JObject From {
            {"name", ModFile.File.Name},
            {"enabledName", ModFile.EnabledName},
            {"enabled", ModFile.IsEnabled},
            {"size", ModFile.File.Length},
            {"modifiedUtc", ModFile.File.LastWriteTimeUtc.ToString("o")},
            {"path", ModFile.File.FullName}
        }
    End Function

    Private Function ResolveModFile(Instance As McInstance, FileName As String) As LocalResourceFile
        If String.IsNullOrWhiteSpace(FileName) Then Throw New McpToolException("invalid_params", "缺少 Mod 文件名。")
        If FileName.Contains("\") OrElse FileName.Contains("/") OrElse Path.IsPathRooted(FileName) OrElse FileName = ".." Then
            Throw New McpToolException("invalid_path", "只能指定 mods 文件夹中的文件名。")
        End If
        Dim Mods As List(Of LocalResourceFile) = GetModFiles(Instance)
        Dim Exact As List(Of LocalResourceFile) = Mods.Where(Function(m) String.Equals(m.File.Name, FileName, StringComparison.OrdinalIgnoreCase)).ToList()
        If Exact.Count = 1 Then Return Exact(0)
        If Exact.Count > 1 Then Throw New McpToolException("mod_conflict", "存在多个同名 Mod 文件：" & FileName)
        Dim EnabledMatches As List(Of LocalResourceFile) = Mods.Where(Function(m) String.Equals(m.EnabledName, FileName, StringComparison.OrdinalIgnoreCase)).ToList()
        If EnabledMatches.Count = 1 Then Return EnabledMatches(0)
        If EnabledMatches.Count > 1 Then Throw New McpToolException("mod_conflict", "存在多个同名的启用/禁用 Mod 文件：" & FileName)
        Throw New McpToolException("mod_not_found", "未找到 Mod：" & FileName)
    End Function

    Private Function SetModEnabled(Instance As McInstance, FileName As String, Enabled As Boolean) As String
        Dim ModFile As LocalResourceFile = ResolveModFile(Instance, FileName)
        If ModFile.IsEnabled = Enabled Then Return ModFile.File.FullName
        Dim CurrentPath As String = ModFile.File.FullName
        Dim NewPath As String = If(Enabled, ModFile.EnabledFullName, CurrentPath & If(FileUtils.Exists(CurrentPath & ".old"), ".old", ".disabled"))
        If FileUtils.Exists(NewPath) Then
            Throw New McpToolException("mod_conflict", "目标文件已存在，无法切换 Mod 状态：" & NewPath)
        End If
        FileUtils.Move(CurrentPath, NewPath)
        RefreshModsUi(Instance)
        Return NewPath
    End Function

    Private Sub RefreshModsUi(Instance As McInstance)
        Try
            InvokeUi(Sub()
                If FrmInstanceMod IsNot Nothing AndAlso PageInstanceLeft.Instance IsNot Nothing AndAlso
                   PathUtils.IsSame(PageInstanceLeft.Instance.PathVersion, Instance.PathVersion) Then
                    FrmInstanceMod.ReloadModList(True)
                End If
            End Sub)
        Catch ex As Exception
            Logger.Warn(ex, "刷新 MCP Mod 列表 UI 失败")
        End Try
    End Sub

    Private Sub ValidateInstanceName(Name As String)
        If String.IsNullOrWhiteSpace(Name) Then Throw New McpToolException("invalid_params", "实例名不能为空。")
        If Name.Length > 100 Then Throw New McpToolException("invalid_params", "实例名过长。")
        If Name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 OrElse Name.Contains("!") OrElse Name.Contains(";") Then
            Throw New McpToolException("invalid_params", "实例名包含非法字符：" & Name)
        End If
    End Sub

    Private Function ParseResourceType(Text As String) As ResourceTypes
        Select Case Text.ToLowerInvariant()
            Case "mod"
                Return ResourceTypes.Mod
            Case "modpack"
                Return ResourceTypes.ModPack
            Case "resourcepack"
                Return ResourceTypes.ResourcePack
            Case "shader"
                Return ResourceTypes.Shader
            Case "datapack"
                Return ResourceTypes.DataPack
            Case Else
                Throw New McpToolException("invalid_params", "未知资源类型：" & Text)
        End Select
    End Function

    Private Function ParseResourcePlatforms(Text As String) As ResourcePlatforms
        Select Case Text.ToLowerInvariant()
            Case "any"
                Return ResourcePlatforms.Any
            Case "curseforge"
                Return ResourcePlatforms.CurseForge
            Case "modrinth"
                Return ResourcePlatforms.Modrinth
            Case Else
                Throw New McpToolException("invalid_params", "未知资源来源：" & Text)
        End Select
    End Function

    Private Function ParseModLoaders(Text As String) As ModLoaders
        Select Case Text.ToLowerInvariant()
            Case "any"
                Return ModLoaders.None
            Case "forge"
                Return ModLoaders.Forge
            Case "fabric"
                Return ModLoaders.Fabric
            Case "neoforge"
                Return ModLoaders.NeoForge
            Case "liteloader"
                Return ModLoaders.LiteLoader
            Case Else
                Throw New McpToolException("invalid_params", "未知 Mod 加载器：" & Text)
        End Select
    End Function

    Private Function InvokeUi(Of T)(Action As Func(Of T), Optional TimeoutMs As Integer = 10000) As T
        Dim CurrentApplication As Application = Application.Current
        If CurrentApplication Is Nothing Then Throw New McpToolException("launcher_not_ready", "启动器尚未初始化。")
        Dim Dispatcher As Windows.Threading.Dispatcher = CurrentApplication.Dispatcher
        If Dispatcher.HasShutdownStarted OrElse Dispatcher.HasShutdownFinished Then
            Throw New McpToolException("launcher_shutting_down", "启动器正在退出。")
        End If
        If Dispatcher.CheckAccess() Then Return Action()
        Try
            Return Dispatcher.Invoke(Action, Windows.Threading.DispatcherPriority.Normal, CancellationToken.None,
                                     TimeSpan.FromMilliseconds(TimeoutMs))
        Catch ex As TimeoutException
            Throw New McpToolException("ui_timeout", "等待启动器界面响应超时。")
        End Try
    End Function

    Private Sub InvokeUi(Action As Action, Optional TimeoutMs As Integer = 10000)
        InvokeUi(Of Object)(
        Function()
            Action()
            Return Nothing
        End Function, TimeoutMs)
    End Sub

#End Region

End Module

''' <summary>
''' 统一的 MCP 工具结果包装。
''' </summary>
Public Module McpResult

    Public Function Success(Data As JToken) As JObject
        Dim Wrapper As New JObject From {
            {"ok", True},
            {"result", If(Data Is Nothing, New JObject(), Data)}
        }
        Return New JObject From {
            {"content", New JArray From {
                New JObject From {{"type", "text"}, {"text", Wrapper.ToString(Newtonsoft.Json.Formatting.None)}}
            }},
            {"structuredContent", Wrapper}
        }
    End Function

    Public Function Failure(Code As String, Message As String) As JObject
        Dim Wrapper As New JObject From {
            {"ok", False},
            {"error", New JObject From {{"code", Code}, {"message", Message}}}
        }
        Return New JObject From {
            {"content", New JArray From {
                New JObject From {{"type", "text"}, {"text", Wrapper.ToString(Newtonsoft.Json.Formatting.None)}}
            }},
            {"isError", True},
            {"structuredContent", Wrapper}
        }
    End Function

End Module
