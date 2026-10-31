Imports System.Security.Cryptography
Imports System.Text

''' <summary>
''' MCP 服务器的生命周期、设置与客户端配置管理。
''' </summary>
Public Module ModMcp

    Public Const DefaultPort As Integer = 37821
    Public Const ConfigFileName As String = "McpClient.json"

    Private Const EnabledSetting As String = "SystemMcpServerEnabled"
    Private Const PortSetting As String = "SystemMcpServerPort"
    Private Const TokenSetting As String = "SystemMcpServerToken"
    Private Const PermissionSetting As String = "SystemMcpPermission"

    Private ReadOnly Sync As New Object()
    Private _Server As McpHttpServer = Nothing
    Private _Token As String = ""
    Private _Permission As Integer = 1
    Private _LastError As String = ""

    ''' <summary>
    ''' 服务器是否正在监听。
    ''' </summary>
    Public ReadOnly Property IsRunning As Boolean
        Get
            SyncLock Sync
                Return _Server IsNot Nothing AndAlso _Server.IsRunning
            End SyncLock
        End Get
    End Property

    ''' <summary>
    ''' 当前 MCP 端点地址。服务器未运行时返回空字符串。
    ''' </summary>
    Public ReadOnly Property Endpoint As String
        Get
            SyncLock Sync
                If _Server Is Nothing OrElse Not _Server.IsRunning Then Return ""
                Return _Server.Endpoint
            End SyncLock
        End Get
    End Property

    ''' <summary>
    ''' 最近一次启动/运行错误。
    ''' </summary>
    Public ReadOnly Property LastError As String
        Get
            SyncLock Sync
                Return _LastError
            End SyncLock
        End Get
    End Property

    ''' <summary>
    ''' 当前 MCP 权限等级：0 只读、1 标准控制、2 完全控制。
    ''' </summary>
    Public ReadOnly Property Permission As Integer
        Get
            SyncLock Sync
                Return _Permission
            End SyncLock
        End Get
    End Property

    ''' <summary>
    ''' 当前活动会话数量。
    ''' </summary>
    Public ReadOnly Property SessionCount As Integer
        Get
            SyncLock Sync
                Return If(_Server Is Nothing, 0, _Server.SessionCount)
            End SyncLock
        End Get
    End Property

    ''' <summary>
    ''' 获取当前访问令牌。仅用于本机鉴权与设置页显示。
    ''' </summary>
    Public Function GetToken() As String
        SyncLock Sync
            Return _Token
        End SyncLock
    End Function

    ''' <summary>
    ''' MCP 客户端配置文件路径。
    ''' </summary>
    Public Function GetConfigPath() As String
        Return Paths.Base & DataFolder & "\" & ConfigFileName
    End Function

    ''' <summary>
    ''' 若设置中已启用 MCP，则启动服务器。应在主窗口加载完成后调用。
    ''' </summary>
    Public Sub StartIfEnabled()
        Try
            ApplySettingsInternal()
        Catch ex As Exception
            Logger.Error(ex, "启动 MCP 服务器失败", LogBehavior.None)
        End Try
    End Sub

    ''' <summary>
    ''' 设置变化回调，可从任意线程触发。
    ''' </summary>
    Public Sub OnSettingChanged(Value As Object)
        Try
            RunInThread(AddressOf ApplySettingsInternal)
        Catch ex As Exception
            Logger.Warn(ex, "应用 MCP 设置失败")
        End Try
    End Sub

    ''' <summary>
    ''' 按当前设置启动、重启或停止服务器。
    ''' </summary>
    Friend Sub ApplySettingsInternal()
        SyncLock Sync
            Try
                Dim Enabled As Boolean = Settings.Get(Of Boolean)(EnabledSetting)
                Dim Port As Integer = Settings.Get(Of Integer)(PortSetting)
                Dim NewPermission As Integer = Settings.Get(Of Integer)(PermissionSetting)
                _Permission = Math.Max(0, Math.Min(2, NewPermission))
                Dim ExistingToken As String = Settings.Get(Of String)(TokenSetting)
                If Not String.IsNullOrEmpty(ExistingToken) Then _Token = ExistingToken

                If Not Enabled Then
                    StopServerLocked()
                    _LastError = ""
                    Return
                End If

                Dim NewToken As String = ExistingToken
                If String.IsNullOrWhiteSpace(NewToken) Then
                    NewToken = GenerateToken()
                    Settings.Set(TokenSetting, NewToken)
                End If
                _Token = NewToken

                If Port < 1024 OrElse Port > 65535 Then
                    StopServerLocked()
                    _LastError = "监听端口必须位于 1024 到 65535 之间。"
                    Logger.Warn("MCP 服务器端口无效：" & Port)
                    Return
                End If

                If _Server IsNot Nothing AndAlso _Server.IsRunning AndAlso _Server.Port = Port Then
                    _LastError = ""
                    WriteClientConfigLocked()
                    Return
                End If

                StopServerLocked()
                Dim NewServer As New McpHttpServer(Port)
                If NewServer.Start() Then
                    _Server = NewServer
                    _LastError = ""
                    Logger.Info("MCP 服务器已启动：" & NewServer.Endpoint)
                    WriteClientConfigLocked()
                Else
                    _Server = Nothing
                    _LastError = If(String.IsNullOrEmpty(NewServer.LastError), "MCP 服务器启动失败。", NewServer.LastError)
                    Logger.Warn("MCP 服务器启动失败：" & _LastError)
                End If
            Catch ex As Exception
                _LastError = ex.Message
                Logger.Error(ex, "初始化 MCP 服务器失败", LogBehavior.None)
            End Try
        End SyncLock
    End Sub

    ''' <summary>
    ''' 停止服务器并删除客户端配置。
    ''' </summary>
    Public Sub [Stop]()
        SyncLock Sync
            StopServerLocked()
        End SyncLock
    End Sub

    ''' <summary>
    ''' 尽力关闭服务器，供程序退出时调用。
    ''' </summary>
    Public Sub Shutdown()
        Try
            [Stop]()
        Catch ex As Exception
            Logger.Warn(ex, "关闭 MCP 服务器失败")
        End Try
    End Sub

    ''' <summary>
    ''' 重新生成访问令牌并立即应用。
    ''' </summary>
    Public Function RegenerateToken() As String
        Dim NewToken As String = GenerateToken()
        Settings.Set(TokenSetting, NewToken)
        ApplySettingsInternal()
        Return NewToken
    End Function

    Private Function GenerateToken() As String
        Dim Bytes(31) As Byte
        Using Rng As New RNGCryptoServiceProvider()
            Rng.GetBytes(Bytes)
        End Using
        Return BitConverter.ToString(Bytes).Replace("-", "").ToLowerInvariant()
    End Function

    Private Sub StopServerLocked()
        DeleteClientConfig()
        If _Server IsNot Nothing Then
            Try
                _Server.Stop()
            Catch ex As Exception
                Logger.Warn(ex, "停止 MCP 服务器失败")
            End Try
            _Server = Nothing
        End If
    End Sub

    Private Sub WriteClientConfigLocked()
        If _Server Is Nothing OrElse Not _Server.IsRunning Then Return
        Try
            Dim Config As New JObject From {
                {"schema", 1},
                {"name", "PCL - Phanes Edition"},
                {"url", _Server.Endpoint},
                {"token", _Token},
                {"permission", _Permission},
                {"pid", Process.GetCurrentProcess().Id},
                {"updatedAt", Date.Now.ToString("o")}
            }
            Dim ConfigPath As String = GetConfigPath()
            DirectoryUtils.Create(Paths.Base & DataFolder)
            Dim TempPath As String = ConfigPath & ".tmp"
            File.WriteAllText(TempPath, Config.ToString(Newtonsoft.Json.Formatting.Indented), New UTF8Encoding(False))
            If File.Exists(ConfigPath) Then File.Delete(ConfigPath)
            File.Move(TempPath, ConfigPath)
        Catch ex As Exception
            Logger.Warn(ex, "写入 MCP 客户端配置失败")
        End Try
    End Sub

    Private Sub DeleteClientConfig()
        Try
            Dim ConfigPath As String = GetConfigPath()
            If File.Exists(ConfigPath) Then File.Delete(ConfigPath)
            If File.Exists(ConfigPath & ".tmp") Then File.Delete(ConfigPath & ".tmp")
        Catch ex As Exception
            Logger.Warn(ex, "删除 MCP 客户端配置失败")
        End Try
    End Sub

End Module
